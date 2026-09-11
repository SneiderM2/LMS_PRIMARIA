@echo off
title LMS Primaria - Iniciar Base de Datos y Backend .NET
color 0B

echo =====================================================================
echo           [LMS PRIMARIA] - SERVIDOR BACKEND .NET 10
echo =====================================================================
echo.

:: 1. Verificar .NET SDK
echo [1/3] Comprobando .NET SDK...
if exist "C:\Program Files\dotnet\dotnet.exe" (
    set "PATH=C:\Program Files\dotnet;%PATH%"
)
dotnet --version > nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET 10 SDK no esta instalado o no se encuentra en el PATH.
    pause
    exit /b 1
) else (
    echo [OK] .NET SDK detectado correctamente:
    dotnet --version
)
echo.

:: 2. Comprobar / Iniciar Servicio MySQL
echo [2/3] Comprobando servicio de MySQL (MySQL80)...
net start MySQL80 > nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Servicio MySQL 'MySQL80' iniciado y listo.
) else (
    echo [AVISO] Si el servicio ya esta corriendo o requiere permisos de Admin,
    echo         asegurate de que MySQL este activo en el puerto 3306.
    echo         Puedes iniciarlo desde MySQL Workbench o el Panel de Servicios de Windows.
)
echo.

:: 3. Ejecutar la API
echo [3/3] Compilando y ejecutando LMS.API...
echo.
echo  - API Backend:     http://localhost:5000
echo  - Scalar API Docs: http://localhost:5000/scalar/v1
echo.
cd /d "%~dp0backend\LMS.API"
set "ASPNETCORE_ENVIRONMENT=Development"
dotnet run

pause
