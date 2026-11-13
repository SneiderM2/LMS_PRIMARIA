@echo off
chcp 65001 > nul
title LMS Primaria - MySQL Workbench
color 0A

echo =====================================================================
echo              [LMS PRIMARIA] - MYSQL WORKBENCH
echo =====================================================================
echo.
echo [NOTA INFORMATIVA]
echo   La plataforma Sci-Kids / LMS Primaria opera principalmente sobre
echo   PostgreSQL 17.6 alojado en Supabase Cloud.
echo   Este script se mantiene para desarrollo y consulta en entornos locales
echo   heredados con base de datos MySQL (lms_scikids).
echo.
echo Buscando instalacion de MySQL Workbench...

set "WB_EXE="
if exist "C:\Program Files\MySQL\MySQL Workbench 8.0 CE\MySQLWorkbench.exe" (
    set "WB_EXE=C:\Program Files\MySQL\MySQL Workbench 8.0 CE\MySQLWorkbench.exe"
) else if exist "C:\Program Files\MySQL\MySQL Workbench 8.0\MySQLWorkbench.exe" (
    set "WB_EXE=C:\Program Files\MySQL\MySQL Workbench 8.0\MySQLWorkbench.exe"
) else (
    for /d %%D in ("C:\Program Files\MySQL\MySQL Workbench*") do (
        if exist "%%D\MySQLWorkbench.exe" set "WB_EXE=%%D\MySQLWorkbench.exe"
    )
)

if not defined WB_EXE (
    where mysqlworkbench.exe > nul 2>&1
    if %ERRORLEVEL% EQU 0 set "WB_EXE=mysqlworkbench.exe"
)

if defined WB_EXE (
    echo [OK] MySQL Workbench detectado. Iniciando aplicacion...
    start "" "%WB_EXE%"
    echo.
    echo INSTRUCCIONES:
    echo  1. Abre la conexion local (usuario: root)
    echo  2. Selecciona la base de datos 'lms_scikids'
    echo  3. Consulta las tablas: usuarios, alumnos, cursos, tareas, etc.
) else (
    echo.
    echo [AVISO] MySQL Workbench no fue encontrado en las rutas habituales.
    where winget > nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo [INFO] Puedes instalarlo automaticamente ejecutando:
        echo        winget install Oracle.MySQLWorkbench
    )
    echo Descarga oficial: https://dev.mysql.com/downloads/workbench/
)

timeout /t 5 > nul
