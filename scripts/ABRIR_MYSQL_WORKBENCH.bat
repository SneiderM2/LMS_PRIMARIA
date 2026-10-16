@echo off
chcp 65001 > nul
title LMS Primaria - Abrir MySQL Workbench
color 0A

echo =====================================================================
echo              🐬 ABRIENDO MYSQL WORKBENCH 8.0 🚀
echo =====================================================================
echo.
echo MySQL Workbench es la herramienta visual para:
echo   - Ver y editar las tablas del LMS (Users, Contents, Submissions)
echo   - Ejecutar consultas SQL
echo   - Modificar datos de los estudiantes y sus semaforos
echo.
echo Iniciando MySQL Workbench...
echo.

if exist "C:\Program Files\MySQL\MySQL Workbench 8.0 CE\MySQLWorkbench.exe" (
    start "" "C:\Program Files\MySQL\MySQL Workbench 8.0 CE\MySQLWorkbench.exe"
    echo [OK] MySQL Workbench iniciado correctamente.
    echo.
    echo INSTRUCCIONES DENTRO DE WORKBENCH:
    echo  1. Haz clic en la conexion 'Local instance MySQL80' (usuario: root)
    echo  2. En el panel izquierdo, haz doble clic en 'lms_scikids'
    echo  3. Expande 'Tables' para ver: usuarios, perfiles, alumnos, cursos, tareas, etc.
    echo  4. Clic derecho en una tabla -^> Select Rows para ver todos los datos
) else (
    echo [ERROR] MySQL Workbench no se encontro en la ruta estandar.
    echo Buscalo en el menu Inicio de Windows como "MySQL Workbench 8.0 CE".
)

timeout /t 5 > nul
