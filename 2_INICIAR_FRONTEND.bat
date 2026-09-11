@echo off
title LMS Primaria - Iniciar Frontend Angular
color 0E

echo =====================================================================
echo          [LMS PRIMARIA] - SERVIDOR FRONTEND ANGULAR
echo =====================================================================
echo.

:: 1. Comprobar Node.js
where node > nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Node.js NO esta instalado en tu equipo.
    echo Para ejecutar la aplicacion completa necesitas instalar Node.js LTS:
    echo   https://nodejs.org/es/download/
    pause
    exit /b 1
)

echo [OK] Node.js detectado correctamente:
node -v
call npm -v
echo.

cd /d "%~dp0frontend"

:: 2. Instalar dependencias si no existen
if not exist "node_modules" (
    echo [1/2] Instalando paquetes de Angular - primera vez...
    call npm install
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Ocurrio un problema al instalar los paquetes de Node.
        pause
        exit /b 1
    )
)

:: 3. Iniciar servidor de desarrollo
echo [2/2] Iniciando servidor de desarrollo Angular...
echo Se abrira en: http://localhost:4200
echo.
call npm start

pause
