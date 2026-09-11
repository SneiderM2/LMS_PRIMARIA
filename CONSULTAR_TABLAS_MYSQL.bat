@echo off
chcp 65001 > nul
title LMS Primaria - Consola SQL de MySQL
color 0F

echo =====================================================================
echo        🐬 CONSOLA SQL MYSQL (lms_scikids) 📊
echo =====================================================================
echo.
echo Comandos utiles que puedes escribir dentro:
echo   SHOW TABLES;                             -- Listar todas las tablas (roles, usuarios, perfiles, etc.)
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
echo Ingresa tu contrasena de root si te la solicita...
echo.

"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -h 127.0.0.1 -u root -p lms_scikids

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [NOTA] Si dice que la base de datos no existe todavia, primero ejecuta:
    echo        1_INICIAR_BACKEND.bat  para que el sistema cree las tablas automaticamente.
)

pause
