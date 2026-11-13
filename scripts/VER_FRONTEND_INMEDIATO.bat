@echo off
chcp 65001 > nul
title LMS Primaria - Vista Previa Inmediata
color 0A

echo =====================================================================
echo         [LMS PRIMARIA] - VISTA PREVIA INMEDIATA (OFFLINE)
echo =====================================================================
echo.
echo Esta vista previa permite interactuar con la interfaz del estudiante
echo y el portal institucional sin necesidad de levantar el servidor Node.
echo.

if exist "%~dp0..\frontend\standalone-preview.html" (
    echo [OK] Abriendo vista previa standalone en tu navegador predeterminado...
    start "" "%~dp0..\frontend\standalone-preview.html"
) else (
    echo [ERROR] No se encontro el archivo frontend\standalone-preview.html.
    pause
    exit /b 1
)

timeout /t 3 > nul
