using LMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(LMSDbContext context)
    {
        await EnsureColumnsMigratedAsync(context);

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

        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Nombre == "ADMINISTRADOR" || r.Nombre == ".admin")
            ?? await context.Roles.FirstAsync();
        var teacherRole = await context.Roles.FirstOrDefaultAsync(r => r.Nombre == "DOCENTE") ?? adminRole;
        var studentRole = await context.Roles.FirstOrDefaultAsync(r => r.Nombre == "ALUMNO") ?? adminRole;
        var primerGrado = await context.Grados.FirstOrDefaultAsync() 
            ?? new Grado { Nombre = "1°", Descripcion = "Primer grado de primaria" };

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

        await context.SaveChangesAsync();
    }

    private static async Task EnsureColumnsMigratedAsync(LMSDbContext context)
    {
        try
        {
            var conn = context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

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
                    Console.WriteLine($"[DbInitializer] Secuencia '{table}': {seqEx.Message}");
                }
            }

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
                END LOOP;
            END $$;";
            await boolCmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbInitializer] Migración: {ex.Message}");
        }
    }
}