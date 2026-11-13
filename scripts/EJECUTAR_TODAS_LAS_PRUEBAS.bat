@echo off
chcp 65001 > nul
title LMS Primaria - Suite Completa de Pruebas (Backend + Frontend)
color 0A

echo =====================================================================
echo    [LMS PRIMARIA] - SUITE INTEGRAL DE PRUEBAS AUTOMATIZADAS
echo =====================================================================
echo.

:: 1. Configurar SDK .NET
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet\tools;%PATH%"
) else if exist "C:\Program Files\dotnet\sdk" (
    set "DOTNET_ROOT=C:\Program Files\dotnet"
    set "PATH=C:\Program Files\dotnet;%PATH%"
)

:: 2. Pruebas Backend
echo ---------------------------------------------------------------------
echo [1/2] EJECUTANDO PRUEBAS DEL BACKEND (.NET 8 - 129 CASOS)
echo ---------------------------------------------------------------------
cd /d "%~dp0..\backend"
dotnet test LMS.API.Tests\LMS.API.Tests.csproj -c Release --nologo
if %ERRORLEVEL% NEQ 0 (
    color 0C
    echo.
    echo [ERROR] Algunas pruebas del backend fallaron.
    pause
    exit /b 1
)

echo.
:: 3. Pruebas Frontend
echo ---------------------------------------------------------------------
echo [2/2] EJECUTANDO PRUEBAS DEL FRONTEND (ANGULAR 18 - 24 CASOS)
echo ---------------------------------------------------------------------
cd /d "%~dp0..\frontend"
call npm test
if %ERRORLEVEL% NEQ 0 (
    color 0C
    echo.
    echo [ERROR] Algunas pruebas del frontend fallaron.
    pause
    exit /b 1
)

color 0A
echo.
echo =====================================================================
echo  TODAS LAS PRUEBAS (BACKEND Y FRONTEND) SUPERADAS CON EXITO!
echo =====================================================================
echo.
pause
