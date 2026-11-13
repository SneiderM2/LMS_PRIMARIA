@echo off
chcp 65001 > nul
title LMS Primaria - Consola SQL de MySQL
color 0F

echo =====================================================================
echo        [LMS PRIMARIA] - CONSOLA SQL MYSQL (lms_scikids)
echo =====================================================================
echo.
echo [NOTA INFORMATIVA]
echo   La plataforma opera principalmente con PostgreSQL 17.6 en Supabase.
echo   Este script se mantiene para desarrollo y pruebas locales con MySQL.
echo.
echo Comandos utiles que puedes escribir dentro:
echo   SHOW TABLES;                             -- Listar todas las tablas
echo   DESCRIBE usuarios;                       -- Ver columnas de la tabla usuarios
echo   SELECT * FROM usuarios;                  -- Ver todos los usuarios registrados
echo   SELECT * FROM cursos;                    -- Ver todos los cursos
echo   SELECT * FROM inscripciones;             -- Ver matriculas
echo   SELECT * FROM tareas;                    -- Ver tareas asignadas
echo.
echo   -- Salir:
echo   EXIT;
echo =====================================================================
echo.

set "MYSQL_EXE="
where mysql.exe > nul 2>&1
if %ERRORLEVEL% EQU 0 (
    set "MYSQL_EXE=mysql.exe"
) else (
    for /d %%D in ("C:\Program Files\MySQL\MySQL Server *") do (
        if exist "%%D\bin\mysql.exe" set "MYSQL_EXE=%%D\bin\mysql.exe"
    )
    if not defined MYSQL_EXE (
        if exist "C:\xampp\mysql\bin\mysql.exe" set "MYSQL_EXE=C:\xampp\mysql\bin\mysql.exe"
    )
)

if defined MYSQL_EXE (
    echo [OK] Cliente MySQL detectado: "%MYSQL_EXE%"
    echo Ingresa tu contrasena de root si te la solicita...
    echo.
    "%MYSQL_EXE%" -h 127.0.0.1 -u root -p lms_scikids
    if %ERRORLEVEL% NEQ 0 (
        echo.
        echo [NOTA] Si la base de datos no existe todavia, ejecuta el script de
        echo        inicializacion en database\bd.txt o database\migration_unify_perfiles_usuarios.sql.
    )
) else (
    echo [AVISO] No se encontro el cliente mysql.exe en el sistema.
    where winget > nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo [INFO] Puedes instalar MySQL Server ejecutando:
        echo        winget install Oracle.MySQL
    )
    echo Descarga oficial: https://dev.mysql.com/downloads/installer/
)

pause
