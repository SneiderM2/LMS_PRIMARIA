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

        // Se han eliminado por completo las cuentas de prueba predeterminadas (Data Seeding de usuarios).
        // Los usuarios se crean dinámicamente mediante el endpoint de registro público o la gestión del docente/admin.
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
