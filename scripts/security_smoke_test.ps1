#requires -Version 5.1
<#
.SYNOPSIS
    Prueba de humo de seguridad para la API de LMS Primaria.

.DESCRIPTION
    Verifica controles básicos que no deben romperse en un despliegue:
    cabeceras de seguridad, CORS, endpoints retirados, privacidad y protección de archivos.
    No modifica datos ni requiere credenciales.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\security_smoke_test.ps1
    powershell -ExecutionPolicy Bypass -File scripts\security_smoke_test.ps1 -BaseUrl https://lms-primaria.onrender.com
#>
param(
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Continue"
$results = @()

function Invoke-Check {
    param(
        [string]$Url,
        [hashtable]$Headers = @{}
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -Headers $Headers -UseBasicParsing -TimeoutSec 15
        return @{
            Status  = [int]$response.StatusCode
            Headers = $response.Headers
            Content = $response.Content
        }
    }
    catch {
        $response = $_.Exception.Response
        if ($null -ne $response) {
            return @{
                Status  = [int]$response.StatusCode
                Headers = $response.Headers
                Content = $_.ErrorDetails.Message
            }
        }

        return @{
            Status  = 0
            Headers = @{}
            Content = $_.Exception.Message
        }
    }
}

function Add-Result {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail
    )

    $script:results += [pscustomobject]@{
        Prueba = $Name
        Estado = if ($Passed) { "OK" } else { "FALLA" }
        Detalle = $Detail
    }
}

function Get-HeaderValue {
    param($Headers, [string]$Name)

    if ($null -eq $Headers) { return $null }
    $value = $Headers[$Name]
    if ($null -eq $value) { return $null }
    return ($value -join ",")
}

$base = $BaseUrl.TrimEnd("/")

Write-Host "== Prueba de humo de seguridad ==" -ForegroundColor Cyan
Write-Host "Destino: $base" -ForegroundColor DarkGray
Write-Host ""

# 1. Disponibilidad
$ping = Invoke-Check -Url "$base/api/ping"
Add-Result "GET /api/ping responde 200" ($ping.Status -eq 200) "HTTP $($ping.Status)"

# 2. Cabeceras de seguridad
$headersToCheck = @(
    @{ Name = "X-Content-Type-Options"; Expected = "nosniff" },
    @{ Name = "X-Frame-Options"; Expected = "DENY" },
    @{ Name = "Referrer-Policy"; Expected = "no-referrer" },
    @{ Name = "Content-Security-Policy"; Contains = "default-src 'none'" },
    @{ Name = "Permissions-Policy"; Contains = "camera=()" }
)

foreach ($header in $headersToCheck) {
    $value = Get-HeaderValue -Headers $ping.Headers -Name $header.Name
    $detail = if ($null -ne $value) { $value } else { "<ausente>" }

    $hasContains = $header -is [System.Collections.IDictionary] -and $header.Contains("Contains")

    $ok = if ($hasContains) {
        $expectedFragment = [string]$header["Contains"]
        $null -ne $value -and $value.Contains($expectedFragment)
    } else {
        $value -eq [string]$header["Expected"]
    }

    Add-Result "Cabecera $($header.Name)" $ok $detail
}

$serverHeader = Get-HeaderValue -Headers $ping.Headers -Name "Server"
$serverDetail = if ($null -ne $serverHeader) { $serverHeader } else { "<ausente>" }
Add-Result "Cabecera Server no expuesta" ([string]::IsNullOrWhiteSpace($serverHeader)) $serverDetail

# 3. CORS
$evil = Invoke-Check -Url "$base/api/ping" -Headers @{ Origin = "https://evil.example" }
$evilAllowed = Get-HeaderValue -Headers $evil.Headers -Name "Access-Control-Allow-Origin"
$evilDetail = if ($null -ne $evilAllowed) { $evilAllowed } else { "<sin cabecera>" }
Add-Result "CORS rechaza origen no autorizado" ([string]::IsNullOrWhiteSpace($evilAllowed)) $evilDetail

$allowedOrigin = "https://sneiderm2.github.io"
$good = Invoke-Check -Url "$base/api/ping" -Headers @{ Origin = $allowedOrigin }
$goodAllowed = Get-HeaderValue -Headers $good.Headers -Name "Access-Control-Allow-Origin"
$goodDetail = if ($null -ne $goodAllowed) { $goodAllowed } else { "<sin cabecera>" }
Add-Result "CORS permite origen institucional" ($goodAllowed -eq $allowedOrigin) $goodDetail

# 4. Endpoints de riesgo retirados
foreach ($path in @("/api/auth/diagnostic", "/api/auth/seed-users")) {
    $r = Invoke-Check -Url "$base$path"
    Add-Result "Endpoint retirado $path responde 404" ($r.Status -eq 404) "HTTP $($r.Status)"
}

# 5. Privacidad pública
$policy = Invoke-Check -Url "$base/api/privacy/policy"
$policyOk = $policy.Status -eq 200
if ($policyOk) {
    try {
        $json = $policy.Content | ConvertFrom-Json
        $policyOk = -not [string]::IsNullOrWhiteSpace($json.version) -and $json.rights.Count -ge 4
    }
    catch {
        $policyOk = $false
    }
}
Add-Result "Política de privacidad pública disponible" $policyOk "HTTP $($policy.Status)"

# 6. Protección de archivos
$legacy = Invoke-Check -Url "$base/uploads/test.pdf"
Add-Result "Ruta estática /uploads no expuesta" ($legacy.Status -eq 404) "HTTP $($legacy.Status)"

$fileNoAuth = Invoke-Check -Url "$base/api/files/submissions/00000000000000000000000000000000.pdf"
Add-Result "Descarga de archivos exige autenticación" ($fileNoAuth.Status -eq 401) "HTTP $($fileNoAuth.Status)"

# 7. Endpoints administrativos protegidos
foreach ($path in @("/api/admin/compliance", "/api/admin/users", "/api/teacher/students", "/api/student/dashboard")) {
    $r = Invoke-Check -Url "$base$path"
    Add-Result "Acceso anónimo bloqueado en $path" ($r.Status -eq 401) "HTTP $($r.Status)"
}

# 8. Swagger en producción (informativo)
$swagger = Invoke-Check -Url "$base/swagger/index.html"
if ($swagger.Status -eq 404) {
    Add-Result "Swagger no expuesto en producción" $true "HTTP 404"
}
else {
    Write-Host "INFO: Swagger responde HTTP $($swagger.Status). En Development es esperado; verifique que no esté expuesto en producción." -ForegroundColor Yellow
}

# Resultados
$results | Format-Table -AutoSize -Wrap

$failed = @($results | Where-Object { $_.Estado -eq "FALLA" })
if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "RESULTADO: $($failed.Count) prueba(s) fallida(s)." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "RESULTADO: todas las pruebas pasaron." -ForegroundColor Green
exit 0
