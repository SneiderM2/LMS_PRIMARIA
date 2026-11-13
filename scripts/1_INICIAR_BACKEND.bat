@echo off
chcp 65001 > nul
title LMS Primaria - Iniciar Backend .NET 8
color 0B

echo =====================================================================
echo           [LMS PRIMARIA] - SERVIDOR BACKEND .NET 8
echo =====================================================================
echo.

:: 1. Comprobar y configurar rutas del SDK de .NET
echo [1/4] Comprobando .NET 8 SDK...

if exist "%USERPROFILE%\.dotnet\sdk" (
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet\tools;%PATH%"
) else if exist "C:\Program Files\dotnet\sdk" (
    set "DOTNET_ROOT=C:\Program Files\dotnet"
    set "PATH=C:\Program Files\dotnet;%PATH%"
)

set "SDK_FOUND=0"
dotnet --list-sdks > "%TEMP%\lms_sdks.tmp" 2>nul
if %ERRORLEVEL% EQU 0 (
    findstr /R /C:"8\." "%TEMP%\lms_sdks.tmp" > nul 2>&1
    if %ERRORLEVEL% EQU 0 set "SDK_FOUND=1"
)
del "%TEMP%\lms_sdks.tmp" 2>nul

if "%SDK_FOUND%"=="0" (
    call :INSTALAR_DOTNET
)

echo [OK] .NET 8 SDK detectado:
dotnet --version
echo.

:: 2. Verificar configuracion local de secretos
echo [2/4] Comprobando configuracion local de secretos...
if not exist "%~dp0..\backend\LMS.API\appsettings.Local.json" (
    echo [AVISO] No se encontro appsettings.Local.json.
    echo [INFO] Creando appsettings.Local.json a partir de appsettings.Example.json...
    copy "%~dp0..\backend\LMS.API\appsettings.Example.json" "%~dp0..\backend\LMS.API\appsettings.Local.json" > nul
    echo [OK] Archivo appsettings.Local.json generado correctamente.
    echo.
    echo [IMPORTANTE] Recuerda configurar tu conexion a Supabase y claves secretas
    echo             en backend\LMS.API\appsettings.Local.json (ver SETUP_SECRETS.md).
) else (
    echo [OK] appsettings.Local.json encontrado.
)
echo.

:: 3. Verificar y restaurar paquetes NuGet
echo [3/4] Verificando dependencias NuGet de LMS.API...
dotnet restore "%~dp0..\backend\LMS.API\LMS.API.csproj" --verbosity minimal
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Hubo un problema al restaurar los paquetes NuGet.
    pause
    exit /b 1
)
echo [OK] Paquetes NuGet listos y actualizados.
echo.

:: 4. Compilar y ejecutar la API
echo [4/4] Iniciando LMS.API (.NET 8)...
echo.
echo  - API Backend:  http://localhost:5000
echo  - Swagger Docs: http://localhost:5000/swagger  (solo Development)
echo  - Detener:      Presiona Ctrl + C
echo.
cd /d "%~dp0..\backend\LMS.API"
set "ASPNETCORE_ENVIRONMENT=Development"
dotnet run

pause
exit /b 0

:: =======================================================================
:: SUBRUTINAS DE INSTALACION
:: =======================================================================
:INSTALAR_DOTNET
echo.
echo [AVISO] No se detecto el SDK de .NET 8.0 en el sistema.
echo [INFO] Intentando instalar .NET 8 SDK automaticamente...
echo.

where winget > nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [INFO] Instalando via winget (Microsoft.DotNet.SDK.8)...
    winget install Microsoft.DotNet.SDK.8 --accept-source-agreements --accept-package-agreements --silent
    if exist "%USERPROFILE%\.dotnet\sdk" (
        set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
        set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet\tools;%PATH%"
    ) else if exist "C:\Program Files\dotnet\sdk" (
        set "DOTNET_ROOT=C:\Program Files\dotnet"
        set "PATH=C:\Program Files\dotnet;%PATH%"
    )
)

dotnet --list-sdks > "%TEMP%\lms_sdks.tmp" 2>nul
findstr /R /C:"8\." "%TEMP%\lms_sdks.tmp" > nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [INFO] Descargando e instalando .NET 8 SDK oficial mediante dotnet-install...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile '$env:TEMP\dotnet-install.ps1'; & '$env:TEMP\dotnet-install.ps1' -Channel 8.0 -InstallDir '$env:USERPROFILE\.dotnet'"
    if exist "%USERPROFILE%\.dotnet\sdk" (
        set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
        set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet\tools;%PATH%"
    )
)
del "%TEMP%\lms_sdks.tmp" 2>nul

dotnet --list-sdks > "%TEMP%\lms_sdks.tmp" 2>nul
findstr /R /C:"8\." "%TEMP%\lms_sdks.tmp" > nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    del "%TEMP%\lms_sdks.tmp" 2>nul
    echo [ERROR] No se pudo instalar automaticamente el SDK de .NET 8.
    echo Por favor descargalo e instalalo manualmente desde:
    echo   https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)
del "%TEMP%\lms_sdks.tmp" 2>nul
goto :eof
