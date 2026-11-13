@echo off
chcp 65001 > nul
title LMS Primaria - Pruebas Unitarias del Backend (.NET 8 xUnit)
color 0B

echo =====================================================================
echo    [LMS PRIMARIA] - EJECUTANDO PRUEBAS DEL BACKEND (.NET 8)
echo =====================================================================
echo.

:: Configurar ruta del SDK instalado en el perfil de usuario o sistema
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet\tools;%PATH%"
) else if exist "C:\Program Files\dotnet\sdk" (
    set "DOTNET_ROOT=C:\Program Files\dotnet"
    set "PATH=C:\Program Files\dotnet;%PATH%"
)

cd /d "%~dp0..\backend"

echo Ubicacion actual: %CD%
echo Ejecutando suite xUnit en modo Release...
echo.

dotnet test LMS.API.Tests\LMS.API.Tests.csproj -c Release --nologo

echo.
echo =====================================================================
echo  Pruebas finalizadas.
echo =====================================================================
pause
