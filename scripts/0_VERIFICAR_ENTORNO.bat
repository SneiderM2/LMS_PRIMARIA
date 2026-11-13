@echo off
chcp 65001 > nul
title LMS Primaria - Diagnostico y Preparacion de Entorno
color 0A

echo =====================================================================
echo    [LMS PRIMARIA] - DIAGNOSTICO Y PREPARACION DE DEPENDENCIAS
echo =====================================================================
echo.
echo Este script verifica e instala todas las dependencias necesarias
echo para compilar y ejecutar el proyecto localmente.
echo.

:: -----------------------------------------------------------------------
:: 1. VERIFICAR / INSTALAR .NET 8 SDK
:: -----------------------------------------------------------------------
echo [1/5] Verificando .NET 8 SDK...
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

:: -----------------------------------------------------------------------
:: 2. VERIFICAR / INSTALAR NODE.JS Y NPM
:: -----------------------------------------------------------------------
echo [2/5] Verificando Node.js y npm...
where node > nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    if exist "C:\Program Files\nodejs\node.exe" (
        set "PATH=C:\Program Files\nodejs;%APPDATA%\npm;%PATH%"
    )
)

where node > nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    call :INSTALAR_NODE
)

where node > nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] No se pudo instalar Node.js automaticamente.
    echo Por favor instala Node.js LTS manualmente desde: https://nodejs.org/
    pause
    exit /b 1
)

echo [OK] Node.js detectado:
node -v
call npm -v
echo.

:: -----------------------------------------------------------------------
:: 3. VERIFICAR SECRETOS LOCALES (appsettings.Local.json)
:: -----------------------------------------------------------------------
echo [3/5] Verificando archivo de secretos locales...
if not exist "%~dp0..\backend\LMS.API\appsettings.Local.json" (
    echo [INFO] Creando appsettings.Local.json a partir de appsettings.Example.json...
    copy "%~dp0..\backend\LMS.API\appsettings.Example.json" "%~dp0..\backend\LMS.API\appsettings.Local.json" > nul
    echo [OK] Archivo appsettings.Local.json creado.
) else (
    echo [OK] appsettings.Local.json ya existe.
)
echo.

:: -----------------------------------------------------------------------
:: 4. RESTAURAR DEPENDENCIAS BACKEND (NuGet)
:: -----------------------------------------------------------------------
echo [4/5] Restaurando dependencias NuGet del backend...
dotnet restore "%~dp0..\backend\LMS.API\LMS.API.csproj" --verbosity minimal
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Error restaurando paquetes NuGet.
    pause
    exit /b 1
)
echo [OK] Dependencias NuGet restauradas correctamente.
echo.

:: -----------------------------------------------------------------------
:: 5. RESTAURAR DEPENDENCIAS FRONTEND (npm)
:: -----------------------------------------------------------------------
echo [5/5] Verificando dependencias npm del frontend...
cd /d "%~dp0..\frontend"
if not exist "node_modules" (
    echo [INFO] Instalando paquetes de Angular con npm install...
    call npm install
) else if not exist "node_modules\@angular\core" (
    echo [INFO] Reinstalando paquetes faltantes con npm install...
    call npm install
) else (
    echo [OK] Paquetes de npm ya instalados.
)
echo.

echo =====================================================================
echo      TODO EL ENTORNO ESTA CONFIGURADO Y LISTO PARA EJECUTAR
echo =====================================================================
echo.
echo Para iniciar la plataforma:
echo   1. Ejecuta scripts\1_INICIAR_BACKEND.bat  - API en http://localhost:5000
echo   2. Ejecuta scripts\2_INICIAR_FRONTEND.bat - App en http://localhost:4200
echo.
pause
exit /b 0

:: =======================================================================
:: SUBRUTINAS DE INSTALACION
:: =======================================================================
:INSTALAR_DOTNET
echo [AVISO] .NET 8 SDK no encontrado. Intentando instalacion automatica...
where winget > nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [INFO] Instalando .NET 8 SDK mediante winget...
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
    echo [INFO] Descargando e instalando .NET 8 SDK oficial mediante script de Microsoft...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile '$env:TEMP\dotnet-install.ps1'; & '$env:TEMP\dotnet-install.ps1' -Channel 8.0 -InstallDir '$env:USERPROFILE\.dotnet'"
    if exist "%USERPROFILE%\.dotnet\sdk" (
        set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
        set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet\tools;%PATH%"
    )
)
del "%TEMP%\lms_sdks.tmp" 2>nul
goto :eof

:INSTALAR_NODE
echo [AVISO] Node.js no encontrado. Intentando instalacion automatica...
where winget > nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [INFO] Instalando Node.js LTS mediante winget...
    winget install OpenJS.NodeJS.LTS --accept-source-agreements --accept-package-agreements --silent
    if exist "C:\Program Files\nodejs" (
        set "PATH=C:\Program Files\nodejs;%APPDATA%\npm;%PATH%"
    )
)
goto :eof
