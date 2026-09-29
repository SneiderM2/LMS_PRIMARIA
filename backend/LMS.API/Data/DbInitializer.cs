using LMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(LMSDbContext context)
    {
        // Asegurar que la base de datos y tablas estén creadas
        await context.Database.EnsureCreatedAsync();

        // Asegurar que la tabla usuarios cuente con las nuevas columnas unificadas
        await EnsureColumnsMigratedAsync(context);

        // 1. Inicializar Roles obligatorios según bd.txt si la tabla está vacía
        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Rol>
            {
                new Rol { Nombre = ".admin", Descripcion = "Administrador del sistema con permisos directivos." },
                new Rol { Nombre = "ADMINISTRADOR", Descripcion = "Administra usuarios, cursos y configuración del LMS." },
                new Rol { Nombre = "DOCENTE", Descripcion = "Gestiona cursos, materiales, tareas y calificaciones." },
                new Rol { Nombre = "ALUMNO", Descripcion = "Consulta cursos, materiales y entrega actividades." }
            };
            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }
        else if (!await context.Roles.AnyAsync(r => r.Nombre == ".admin"))
        {
            context.Roles.Add(new Rol { Nombre = ".admin", Descripcion = "Administrador del sistema con permisos directivos." });
            await context.SaveChangesAsync();
        }

        // 2. Inicializar Grados obligatorios de primaria según bd.txt si la tabla está vacía
        if (!await context.Grados.AnyAsync())
        {
            var grados = new List<Grado>
            {
                new Grado { Nombre = "1°", Descripcion = "Primer grado de primaria." },
                new Grado { Nombre = "2°", Descripcion = "Segundo grado de primaria." },
                new Grado { Nombre = "3°", Descripcion = "Tercer grado de primaria." },
                new Grado { Nombre = "4°", Descripcion = "Cuarto grado de primaria." },
                new Grado { Nombre = "5°", Descripcion = "Quinto grado de primaria." }
            };
            await context.Grados.AddRangeAsync(grados);
            await context.SaveChangesAsync();
        }

        // 3. Garantizar cuentas del sistema con credenciales conocidas para acceso garantizado
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Nombre == "ADMINISTRADOR" || r.Nombre == ".admin")
            ?? await context.Roles.FirstAsync();
        var teacherRole = await context.Roles.FirstOrDefaultAsync(r => r.Nombre == "DOCENTE") ?? adminRole;
        var studentRole = await context.Roles.FirstOrDefaultAsync(r => r.Nombre == "ALUMNO") ?? adminRole;
        var primerGrado = await context.Grados.FirstOrDefaultAsync() 
            ?? new Grado { Nombre = "1°", Descripcion = "Primer grado de primaria" };

        // 3.1 Usuario Administrador
        var adminUser = await context.Usuarios.FirstOrDefaultAsync(u => u.Username.ToLower() == "admin");
        if (adminUser == null)
        {
            adminUser = new Usuario
            {
                Username = "admin",
                RolId = adminRole.Id,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Nombre = "Administrador",
                Apellido = "General",
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=admin",
                Activo = true,
                DataPolicyAccepted = true,
                DataPolicyAcceptedAt = DateTime.UtcNow,
                AccessFailedCount = 0,
                LockoutEnd = null
            };
            context.Usuarios.Add(adminUser);
        }
        else
        {
            adminUser.Activo = true;
            adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123");
            adminUser.LockoutEnd = null;
            adminUser.AccessFailedCount = 0;
            adminUser.DataPolicyAccepted = true;
        }

        // 3.2 Usuario Docente
        var docenteUser = await context.Usuarios.FirstOrDefaultAsync(u => u.Username.ToLower() == "docente");
        if (docenteUser == null)
        {
            docenteUser = new Usuario
            {
                Username = "docente",
                RolId = teacherRole.Id,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("docente123"),
                Nombre = "Profesor",
                Apellido = "Primaria",
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=docente",
                Activo = true,
                DataPolicyAccepted = true,
                DataPolicyAcceptedAt = DateTime.UtcNow,
                AccessFailedCount = 0,
                LockoutEnd = null
            };
            context.Usuarios.Add(docenteUser);
        }
        else
        {
            docenteUser.Activo = true;
            docenteUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("docente123");
            docenteUser.LockoutEnd = null;
            docenteUser.AccessFailedCount = 0;
            docenteUser.DataPolicyAccepted = true;
        }

        // 3.3 Usuario Estudiante
        var studentUser = await context.Usuarios.Include(u => u.Alumno).FirstOrDefaultAsync(u => u.Username.ToLower() == "estudiante");
        if (studentUser == null)
        {
            studentUser = new Usuario
            {
                Username = "estudiante",
                RolId = studentRole.Id,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("estudiante123"),
                Nombre = "Estudiante",
                Apellido = "Primaria",
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=estudiante",
                Activo = true,
                DataPolicyAccepted = true,
                DataPolicyAcceptedAt = DateTime.UtcNow,
                AccessFailedCount = 0,
                LockoutEnd = null
            };
            context.Usuarios.Add(studentUser);
            await context.SaveChangesAsync();

            var alumnoRecord = new Alumno
            {
                UsuarioId = studentUser.Id,
                GradoId = primerGrado.Id
            };
            context.Alumnos.Add(alumnoRecord);
        }
        else
        {
            studentUser.Activo = true;
            studentUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("estudiante123");
            studentUser.LockoutEnd = null;
            studentUser.AccessFailedCount = 0;
            studentUser.DataPolicyAccepted = true;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Auto-migración defensiva: comprueba la estructura de PostgreSQL y añade las columnas
    /// a la tabla 'usuarios' si aún no existen.
    /// </summary>
    private static async Task EnsureColumnsMigratedAsync(LMSDbContext context)
    {
        try
        {
            var conn = context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            // Asegurar auto-incremento (secuencias) para claves primarias en PostgreSQL
            var tablesWithAutoIncrement = new[] { "roles", "grados", "usuarios", "cursos", "inscripciones", "tareas", "entregas", "archivos_entrega", "materiales" };
            foreach (var table in tablesWithAutoIncrement)
            {
                try
                {
                    using var seqCmd = conn.CreateCommand();
                    seqCmd.CommandText = $@"
                        DO $$
                        BEGIN
                            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{table}')
                               AND NOT EXISTS (
                                SELECT 1 FROM information_schema.columns 
                                WHERE table_schema = 'public' AND table_name = '{table}' AND column_name = 'id' 
                                AND (column_default LIKE 'nextval%' OR is_identity = 'YES')
                            ) THEN
                                CREATE SEQUENCE IF NOT EXISTS {table}_id_seq;
                                PERFORM setval('{table}_id_seq', COALESCE((SELECT MAX(id) FROM {table}), 0) + 1, false);
                                ALTER TABLE {table} ALTER COLUMN id SET DEFAULT nextval('{table}_id_seq');
                                ALTER SEQUENCE {table}_id_seq OWNED BY {table}.id;
                            END IF;
                        END $$;";
                    await seqCmd.ExecuteNonQueryAsync();
                }
                catch (Exception seqEx)
                {
                    Console.WriteLine($"[DbInitializer] Secuencia para '{table}': {seqEx.Message}");
                }
            }

            // Asegurar que columnas booleanas que vinieron como smallint de MySQL se conviertan a boolean en PostgreSQL
            try
            {
                using var boolCmd = conn.CreateCommand();
                boolCmd.CommandText = @"
                    DO $$
                    DECLARE
                        tbl text;
                    BEGIN
                        FOR tbl IN SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' LOOP
                            IF EXISTS (
                                SELECT 1 FROM information_schema.columns 
                                WHERE table_schema = 'public' AND table_name = tbl AND column_name = 'activo' AND data_type IN ('smallint', 'integer')
                            ) THEN
                                EXECUTE format('ALTER TABLE %I ALTER COLUMN activo DROP DEFAULT;', tbl);
                                EXECUTE format('ALTER TABLE %I ALTER COLUMN activo TYPE boolean USING (activo <> 0);', tbl);
                                EXECUTE format('ALTER TABLE %I ALTER COLUMN activo SET DEFAULT true;', tbl);
                            END IF;

                            IF EXISTS (
                                SELECT 1 FROM information_schema.columns 
                                WHERE table_schema = 'public' AND table_name = tbl AND column_name = 'data_policy_accepted' AND data_type IN ('smallint', 'integer')
                            ) THEN
                                EXECUTE format('ALTER TABLE %I ALTER COLUMN data_policy_accepted DROP DEFAULT;', tbl);
                                EXECUTE format('ALTER TABLE %I ALTER COLUMN data_policy_accepted TYPE boolean USING (data_policy_accepted <> 0);', tbl);
                                EXECUTE format('ALTER TABLE %I ALTER COLUMN data_policy_accepted SET DEFAULT false;', tbl);
                            END IF;
                        END LOOP;
                    END $$;";
                await boolCmd.ExecuteNonQueryAsync();
            }
            catch (Exception boolEx)
            {
                Console.WriteLine($"[DbInitializer] Conversión booleana: {boolEx.Message}");
            }

            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT column_name 
                    FROM information_schema.columns 
                    WHERE table_schema = 'public' AND table_name = 'usuarios';";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existingColumns.Add(reader.GetString(0));
                }
            }

            if (!existingColumns.Contains("nombre"))
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS nombre VARCHAR(100) NOT NULL DEFAULT '';";
                await alterCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("apellido"))
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS apellido VARCHAR(100) NOT NULL DEFAULT '';";
                await alterCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("avatar_url"))
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS avatar_url VARCHAR(500) NULL;";
                await alterCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("data_policy_accepted"))
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS data_policy_accepted BOOLEAN NOT NULL DEFAULT FALSE;";
                await alterCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("data_policy_accepted_at"))
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS data_policy_accepted_at TIMESTAMP WITHOUT TIME ZONE NULL;";
                await alterCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("access_failed_count"))
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS access_failed_count INT NOT NULL DEFAULT 0;";
                await alterCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("lockout_end"))
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS lockout_end TIMESTAMP WITHOUT TIME ZONE NULL;";
                await alterCmd.ExecuteNonQueryAsync();
            }

            // Si la tabla 'perfiles' aún existe físicamente, migrar los datos a 'usuarios'
            using (var tableCmd = conn.CreateCommand())
            {
                tableCmd.CommandText = @"
                    SELECT COUNT(*) 
                    FROM information_schema.tables 
                    WHERE table_schema = 'public' AND table_name = 'perfiles';";

                var count = Convert.ToInt32(await tableCmd.ExecuteScalarAsync());
                if (count > 0)
                {
                    using var migrateCmd = conn.CreateCommand();
                    migrateCmd.CommandText = @"
                        UPDATE usuarios u
                        SET nombre = p.nombre, apellido = p.apellido, avatar_url = p.avatar_url
                        FROM perfiles p
                        WHERE u.id = p.usuario_id AND (u.nombre = '' OR u.nombre IS NULL);";
                    await migrateCmd.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbInitializer] Nota de migración de columnas: {ex.Message}");
        }
    }
}
