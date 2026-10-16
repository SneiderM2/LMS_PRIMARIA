-- ==============================================================================
-- LMS PRIMARIA - SCRIPT DE MIGRACIÓN 100% COMPATIBLE CON MYSQL WORKBENCH
-- Evita errores de Safe Updates (1175), columnas duplicadas (1060) o tablas faltantes
-- ==============================================================================

USE lms_scikids;

-- Desactivar temporalmente restricciones de Workbench y claves foráneas
SET SQL_SAFE_UPDATES = 0;
SET FOREIGN_KEY_CHECKS = 0;

-- 1. Agregar 'nombre' a 'usuarios' si aún no existe
SET @col_nombre = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'usuarios' AND column_name = 'nombre');
SET @sql = IF(@col_nombre = 0, 
    'ALTER TABLE usuarios ADD COLUMN nombre VARCHAR(100) NOT NULL DEFAULT \'\' AFTER password_hash;', 
    'SELECT "La columna nombre ya existía." AS estado;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 2. Agregar 'apellido' a 'usuarios' si aún no existe
SET @col_apellido = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'usuarios' AND column_name = 'apellido');
SET @sql = IF(@col_apellido = 0, 
    'ALTER TABLE usuarios ADD COLUMN apellido VARCHAR(100) NOT NULL DEFAULT \'\' AFTER nombre;', 
    'SELECT "La columna apellido ya existía." AS estado;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 3. Agregar 'avatar_url' a 'usuarios' si aún no existe
SET @col_avatar = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'usuarios' AND column_name = 'avatar_url');
SET @sql = IF(@col_avatar = 0, 
    'ALTER TABLE usuarios ADD COLUMN avatar_url VARCHAR(500) NULL AFTER apellido;', 
    'SELECT "La columna avatar_url ya existía." AS estado;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 4. Migrar los datos desde 'perfiles' a 'usuarios' (solo si 'perfiles' aún existe)
SET @tabla_perfiles = (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'perfiles');
SET @sql = IF(@tabla_perfiles > 0, 
    'UPDATE usuarios u INNER JOIN perfiles p ON u.id = p.usuario_id SET u.nombre = p.nombre, u.apellido = p.apellido, u.avatar_url = p.avatar_url WHERE u.id > 0;', 
    'SELECT "La tabla perfiles ya no existe o ya fue migrada." AS estado;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 5. Eliminar la tabla 'perfiles'
DROP TABLE IF EXISTS perfiles;

-- 6. Asegurar el rol '.admin' en el catálogo de roles
INSERT INTO roles (nombre, descripcion)
VALUES ('.admin', 'Administrador del sistema con permisos directivos')
ON DUPLICATE KEY UPDATE descripcion = VALUES(descripcion);

-- Restaurar configuraciones de seguridad
SET SQL_SAFE_UPDATES = 1;
SET FOREIGN_KEY_CHECKS = 1;

-- 7. Verificar que las columnas 'nombre', 'apellido', 'avatar_url' estén presentes
DESCRIBE usuarios;
