@echo off
chcp 65001 > nul
title LMS Primaria - Pruebas Unitarias del Frontend (Karma / Jasmine)
color 0E

echo =====================================================================
echo    [LMS PRIMARIA] - EJECUTANDO PRUEBAS DEL FRONTEND (ANGULAR 18)
echo =====================================================================
echo.

cd /d "%~dp0..\frontend"

echo Ubicacion actual: %CD%
echo Ejecutando 24 pruebas Karma/Jasmine en ChromeHeadless...
echo.

call npm test

echo.
echo =====================================================================
echo  Pruebas del frontend finalizadas.
echo =====================================================================
pause
