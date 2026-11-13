@echo off
chcp 65001 > nul
title LMS Primaria - Iniciar Frontend Angular
color 0E

echo =====================================================================
echo          [LMS PRIMARIA] - SERVIDOR FRONTEND ANGULAR
echo =====================================================================
echo.

:: 1. Comprobar Node.js y npm
echo [1/3] Comprobando entorno Node.js y npm...

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
    echo.
    echo [ERROR] No se pudo instalar Node.js automaticamente.
    echo Por favor descarga e instala Node.js LTS manualmente desde:
    echo   https://nodejs.org/
    start https://nodejs.org/
    echo.
    pause
    exit /b 1
)

echo [OK] Node.js y npm detectados:
node -v
call npm -v
echo.

cd /d "%~dp0..\frontend"

:: 2. Instalar dependencias si no existen o estan incompletas
echo [2/3] Verificando dependencias de Angular...
if not exist "node_modules" (
    echo [INFO] Carpeta node_modules no encontrada.
    echo [INFO] Instalando paquetes de Angular con npm install...
    call npm install
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Ocurrio un problema al instalar las dependencias de npm.
        pause
        exit /b 1
    )
    echo [OK] Dependencias instaladas con exito.
) else if not exist "node_modules\@angular\core" (
    echo [AVISO] Dependencias incompletas en node_modules.
    echo [INFO] Reinstalando dependencias con npm install...
    call npm install
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Ocurrio un problema al completar la instalacion de dependencias.
        pause
        exit /b 1
    )
    echo [OK] Dependencias completadas con exito.
) else (
    echo [OK] Dependencias de Angular verificadas correctamente.
)
echo.

:: 3. Iniciar servidor de desarrollo
echo [3/3] Iniciando servidor de desarrollo Angular...
echo.
echo  - Aplicacion Frontend: http://localhost:4200
echo  - Detener servidor:    Presiona Ctrl + C
echo.
call npm start

pause
exit /b 0

:: =======================================================================
:: SUBRUTINAS DE INSTALACION
:: =======================================================================
:INSTALAR_NODE
echo.
echo [AVISO] Node.js no fue detectado en el sistema.
echo [INFO] Intentando instalar Node.js LTS automaticamente via winget...
echo.

where winget > nul 2>&1
if %ERRORLEVEL% EQU 0 (
    winget install OpenJS.NodeJS.LTS --accept-source-agreements --accept-package-agreements --silent
    if exist "C:\Program Files\nodejs" (
        set "PATH=C:\Program Files\nodejs;%APPDATA%\npm;%PATH%"
    )
)
goto :eof
