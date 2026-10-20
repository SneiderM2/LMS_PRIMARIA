#requires -Version 5.1
<#
.SYNOPSIS
    Prueba funcional de extremo a extremo de la API de LMS Primaria (entorno local).

.DESCRIPTION
    Requiere que el backend esté en ejecución en modo Development con
    Recaptcha:AllowDevBypass=true (el token DEV_TEST_TOKEN se usa solo en desarrollo).

    Credenciales administrativas:
      $env:LMS_ADMIN_USER      (por defecto: admin)
      $env:LMS_ADMIN_PASSWORD  (por defecto: admin123 — cámbiela en producción)

    Código de invitación docente:
      $env:LMS_TEACHER_INVITE  (por defecto: DOCENTE-LMS-2026)
#>
param(
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')

$adminUser = if ($env:LMS_ADMIN_USER) { $env:LMS_ADMIN_USER } else { "admin" }
$adminPass = if ($env:LMS_ADMIN_PASSWORD) { $env:LMS_ADMIN_PASSWORD } else { "admin123" }
$teacherInvite = if ($env:LMS_TEACHER_INVITE) { $env:LMS_TEACHER_INVITE } else { "DOCENTE-LMS-2026" }
$policyVersion = "2026-10-19"

if (-not $env:LMS_ADMIN_PASSWORD) {
    Write-Warning "Usando contraseña administrativa por defecto. Defina LMS_ADMIN_PASSWORD para entornos reales."
}

function Invoke-LmsApi {
    param(
        [string]$Method,
        [string]$Path,
        $Body = $null,
        [string]$Token = $null
    )

    $headers = @{}
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    $params = @{
        Uri         = "$base$Path"
        Method      = $Method
        Headers     = $headers
        ContentType = "application/json; charset=utf-8"
    }

    if ($null -ne $Body) {
        $params["Body"] = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 8 -Compress))
    }

    try {
        return Invoke-RestMethod @params
    }
    catch {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        throw "Error HTTP $status en $Method $Path. $($_.ErrorDetails.Message)"
    }
}

$suffix = (Get-Random -Minimum 1000 -Maximum 9999).ToString()

Write-Host "========================================="
Write-Host "1. Autenticación del ADMINISTRADOR"
Write-Host "========================================="
$adminAuth = Invoke-LmsApi -Method POST -Path "/api/auth/login" -Body @{
    id           = $adminUser
    password     = $adminPass
    captchaToken = "DEV_TEST_TOKEN"
}
Write-Host "  -> Sesión admin iniciada: $($adminAuth.user.fullName) [Rol: $($adminAuth.user.role)]"
$adminToken = $adminAuth.token

$courses = Invoke-LmsApi -Method GET -Path "/api/admin/courses" -Token $adminToken
Write-Host "  -> Cursos iniciales: $($courses.Count)"

$createdCourse = Invoke-LmsApi -Method POST -Path "/api/admin/courses" -Token $adminToken -Body @{
    nombre      = "Robotica y Logica $suffix"
    grupo       = "5-B"
    descripcion = "Taller institucional de algoritmos"
    grado       = "5°"
}
Write-Host "  -> Curso creado: ID=$($createdCourse.id), Nombre=$($createdCourse.nombre)"

$updatedRes = Invoke-LmsApi -Method PUT -Path "/api/admin/courses/$($createdCourse.id)" -Token $adminToken -Body @{
    nombre      = "Robotica Avanzada $suffix"
    grupo       = "5-B"
    descripcion = "Actualizado para pruebas"
    grado       = "5°"
}
Write-Host "  -> Curso actualizado: $($updatedRes.message)"

$toggleRes = Invoke-LmsApi -Method PATCH -Path "/api/admin/courses/$($createdCourse.id)/toggle-status" -Token $adminToken
Write-Host "  -> Estado del curso alternado: $($toggleRes.message)"

Write-Host "`n=========================================="
Write-Host "2. Registro y endpoints del DOCENTE"
Write-Host "=========================================="
$teacherId = "prof_$suffix"
$teacherAuth = Invoke-LmsApi -Method POST -Path "/api/auth/register" -Body @{
    id                   = $teacherId
    fullName             = "Profe Laura Hernandez"
    role                 = "Teacher"
    password             = "DocenteSeguro2026x"
    teacherInviteCode    = $teacherInvite
    acceptDataPolicy     = $true
    dataPolicyVersion    = $policyVersion
    captchaToken         = "DEV_TEST_TOKEN"
}
Write-Host "  -> Docente registrado: $($teacherAuth.user.fullName) [$($teacherAuth.user.role)]"
$teacherToken = $teacherAuth.token
$teacherInternalId = $teacherAuth.user.internalId

$assignRes = Invoke-LmsApi -Method PUT -Path "/api/admin/courses/$($createdCourse.id)" -Token $adminToken -Body @{
    nombre      = "Robotica Avanzada $suffix"
    grupo       = "5-B"
    descripcion = "Con docente asignado"
    grado       = "5°"
    docenteId   = $teacherInternalId
}
Write-Host "  -> Docente asignado al curso: $($assignRes.message)"

$toggleBack = Invoke-LmsApi -Method PATCH -Path "/api/admin/courses/$($createdCourse.id)/toggle-status" -Token $adminToken
Write-Host "  -> Curso reactivado: $($toggleBack.activo)"

$contentRes = Invoke-LmsApi -Method POST -Path "/api/teacher/contents" -Token $teacherToken -Body @{
    title       = "Reto 1: Introduccion a Variables"
    description = "Crear programa de saludo"
    type        = "Assignment"
    subject     = "Robotica Avanzada $suffix"
    gradeLevel  = "5°"
    dueDate     = "2026-10-30T23:59:59"
}
Write-Host "  -> Tarea creada: ID=$($contentRes.id), Título=$($contentRes.title)"

$submissions = Invoke-LmsApi -Method GET -Path "/api/teacher/submissions/$($contentRes.realId)" -Token $teacherToken
Write-Host "  -> Entregas iniciales: $($submissions.Count)"

Write-Host "`n=========================================="
Write-Host "3. Registro y endpoints del ESTUDIANTE"
Write-Host "=========================================="
$studentId = "est_$suffix"
$studentAuth = Invoke-LmsApi -Method POST -Path "/api/auth/register" -Body @{
    id                   = $studentId
    fullName             = "Pepito Perez"
    role                 = "Student"
    grade                = "5°"
    password             = "EstudianteSeguro2026x"
    guardianFullName     = "Maria Perez Acudiente"
    guardianDocument     = "1029384756"
    guardianAuthorization = $true
    acceptDataPolicy     = $true
    dataPolicyVersion    = $policyVersion
    captchaToken         = "DEV_TEST_TOKEN"
}
Write-Host "  -> Estudiante registrado: $($studentAuth.user.fullName) [$($studentAuth.user.role)]"
$studentToken = $studentAuth.token

$enrollRes = Invoke-LmsApi -Method POST -Path "/api/teacher/courses/$($createdCourse.id)/enroll" -Token $teacherToken -Body @{
    studentId = $studentId
}
Write-Host "  -> Estudiante matriculado: $($enrollRes.message)"

$studentDash = Invoke-LmsApi -Method GET -Path "/api/student/dashboard" -Token $studentToken
Write-Host "  -> Elementos en el panel del estudiante: $($studentDash.Count)"
$studentTask = $studentDash | Where-Object { $_.realId -eq $contentRes.realId }
Write-Host "  -> Tarea visible: '$($studentTask.title)', Entregada=$($studentTask.hasSubmitted)"

Write-Host "`n4. Entrega de la tarea (multipart con PDF válido)..."
$boundary = [System.Guid]::NewGuid().ToString()
$filePath = Join-Path $PSScriptRoot "test_upload.pdf"
[System.IO.File]::WriteAllText($filePath, "%PDF-1.4`r`n%Dummy content for testing submission delivery`r`n%%EOF")

$fileBytes = [System.IO.File]::ReadAllBytes($filePath)
$fileContent = [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($fileBytes)

$LF = "`r`n"
$bodyLines = (
    "--$boundary",
    'Content-Disposition: form-data; name="AssignmentId"',
    '',
    "$($contentRes.realId)",
    "--$boundary",
    'Content-Disposition: form-data; name="File"; filename="tarea_pepito.pdf"',
    'Content-Type: application/pdf',
    '',
    $fileContent,
    "--$boundary--"
) -join $LF

$uploadRes = Invoke-RestMethod `
    -Uri "$base/api/student/upload-assignment" `
    -Method POST `
    -ContentType "multipart/form-data; boundary=$boundary" `
    -Headers @{ Authorization = "Bearer $studentToken" } `
    -Body ([System.Text.Encoding]::GetEncoding("iso-8859-1").GetBytes($bodyLines))

Write-Host "  -> Entrega registrada: $($uploadRes.message)"
Write-Host "  -> Archivo protegido: $($uploadRes.submission.fileUrl)"

if (Test-Path $filePath) { Remove-Item $filePath -Force }

$submissionsAfter = Invoke-LmsApi -Method GET -Path "/api/teacher/submissions/$($contentRes.realId)" -Token $teacherToken
Write-Host "  -> Entregas visibles para el docente: $($submissionsAfter.Count)"

$gradeRes = Invoke-LmsApi -Method POST -Path "/api/teacher/submissions/$($submissionsAfter[0].submissionId)/grade" -Token $teacherToken -Body @{
    grade    = 98
    feedback = "Excelente trabajo y desarrollo impecable con diagramas."
}
Write-Host "  -> Calificación registrada: $($gradeRes.message), Estado: $($gradeRes.status)"

$studentDashAfter = Invoke-LmsApi -Method GET -Path "/api/student/dashboard" -Token $studentToken
$gradedTask = $studentDashAfter | Where-Object { $_.realId -eq $contentRes.realId }
Write-Host "  -> Verificación del estudiante: nota=$($gradedTask.mySubmission.grade), estado=$($gradedTask.mySubmission.status)"

Write-Host "`n=========================================================="
Write-Host ">>> FLUJO FUNCIONAL VERIFICADO <<<"
Write-Host "=========================================================="
