@echo off
title LMS Primaria - Iniciar Backend .NET 8
color 0B

echo =====================================================================
echo           [LMS PRIMARIA] - SERVIDOR BACKEND .NET 8
echo =====================================================================
echo.

:: 1. Verificar .NET SDK
echo [1/3] Comprobando .NET SDK...
if exist "C:\Program Files\dotnet\dotnet.exe" (
    set "PATH=C:\Program Files\dotnet;%PATH%"
)
dotnet --version > nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET SDK no esta instalado o no se encuentra en el PATH.
    pause
    exit /b 1
) else (
    echo [OK] .NET SDK detectado correctamente:
    dotnet --version
)
echo.

:: 2. Verificar configuracion local de secretos
echo [2/3] Comprobando configuracion local...
if exist "%~dp0..\backend\LMS.API\appsettings.Local.json" (
    echo [OK] appsettings.Local.json encontrado.
) else (
    echo [AVISO] No existe appsettings.Local.json.
    echo         Copia appsettings.Example.json y completa la cadena de conexion
    echo         y las claves (ver SETUP_SECRETS.md). Tambien puedes usar variables
    echo         de entorno: ConnectionStrings__DefaultConnection y Jwt__SecretKey.
)
echo.

:: 3. Ejecutar la API
echo [3/3] Compilando y ejecutando LMS.API...
echo.
echo  - API Backend:  http://localhost:5000
echo  - Swagger Docs: http://localhost:5000/swagger  (solo Development)
echo.
cd /d "%~dp0..\backend\LMS.API"
set "ASPNETCORE_ENVIRONMENT=Development"
dotnet run

pause
