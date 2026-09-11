using LMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Data;

public class LMSDbContext : DbContext
{
    public LMSDbContext(DbContextOptions<LMSDbContext> options) : base(options)
    {
    }

    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Grado> Grados => Set<Grado>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Alumno> Alumnos => Set<Alumno>();
    public DbSet<Curso> Cursos => Set<Curso>();
    public DbSet<Inscripcion> Inscripciones => Set<Inscripcion>();
    public DbSet<Tarea> Tareas => Set<Tarea>();
    public DbSet<Entrega> Entregas => Set<Entrega>();
    public DbSet<ArchivoEntrega> ArchivosEntrega => Set<ArchivoEntrega>();
    public DbSet<Material> Materiales => Set<Material>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. ROLES
        modelBuilder.Entity<Rol>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(r => r.Nombre).HasColumnName("nombre").HasMaxLength(30).IsRequired();
            entity.Property(r => r.Descripcion).HasColumnName("descripcion").HasMaxLength(150);

            entity.HasIndex(r => r.Nombre)
                  .IsUnique()
                  .HasDatabaseName("uq_roles_nombre");
        });

        // 2. GRADOS
        modelBuilder.Entity<Grado>(entity =>
        {
            entity.ToTable("grados");
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(g => g.Nombre).HasColumnName("nombre").HasMaxLength(30).IsRequired();
            entity.Property(g => g.Descripcion).HasColumnName("descripcion").HasMaxLength(150);

            entity.HasIndex(g => g.Nombre)
                  .IsUnique()
                  .HasDatabaseName("uq_grados_nombre");
        });

        // 3. USUARIOS
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("usuarios");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(u => u.RolId).HasColumnName("rol_id").IsRequired();
            entity.Property(u => u.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
            entity.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            entity.Property(u => u.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            entity.Property(u => u.Apellido).HasColumnName("apellido").HasMaxLength(100).IsRequired();
            entity.Property(u => u.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);
            entity.Ignore(u => u.FullName);
            entity.Property(u => u.Activo).HasColumnName("activo").HasColumnType("tinyint(1)").HasDefaultValue(true);
            entity.Property(u => u.FechaCreacion).HasColumnName("fecha_creacion").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(u => u.Username)
                  .IsUnique()
                  .HasDatabaseName("uq_usuarios_username");

            entity.HasIndex(u => u.RolId)
                  .HasDatabaseName("idx_usuarios_rol");

            entity.HasOne(u => u.Rol)
                  .WithMany(r => r.Usuarios)
                  .HasForeignKey(u => u.RolId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_usuarios_roles");
        });

        // 5. ALUMNOS
        modelBuilder.Entity<Alumno>(entity =>
        {
            entity.ToTable("alumnos");
            entity.HasKey(a => a.UsuarioId);
            entity.Property(a => a.UsuarioId).HasColumnName("usuario_id").ValueGeneratedNever();
            entity.Property(a => a.GradoId).HasColumnName("grado_id").IsRequired();

            entity.HasIndex(a => a.GradoId)
                  .HasDatabaseName("idx_alumnos_grado");

            entity.HasOne(a => a.Usuario)
                  .WithOne(u => u.Alumno)
                  .HasForeignKey<Alumno>(a => a.UsuarioId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_alumnos_usuarios");

            entity.HasOne(a => a.Grado)
                  .WithMany(g => g.Alumnos)
                  .HasForeignKey(a => a.GradoId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_alumnos_grados");
        });

        // 6. CURSOS
        modelBuilder.Entity<Curso>(entity =>
        {
            entity.ToTable("cursos");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(c => c.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            entity.Property(c => c.GradoId).HasColumnName("grado_id").IsRequired();
            entity.Property(c => c.Grupo).HasColumnName("grupo").HasMaxLength(10).IsRequired();
            entity.Property(c => c.DocenteId).HasColumnName("docente_id").IsRequired();
            entity.Property(c => c.Descripcion).HasColumnName("descripcion").HasColumnType("text");
            entity.Property(c => c.FechaCreacion).HasColumnName("fecha_creacion").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(c => c.Activo).HasColumnName("activo").HasColumnType("tinyint(1)").HasDefaultValue(true);

            entity.HasIndex(c => new { c.Nombre, c.GradoId, c.Grupo })
                  .IsUnique()
                  .HasDatabaseName("uq_cursos_nombre_grado_grupo");

            entity.HasIndex(c => c.GradoId)
                  .HasDatabaseName("idx_cursos_grado");

            entity.HasIndex(c => c.DocenteId)
                  .HasDatabaseName("idx_cursos_docente");

            entity.HasOne(c => c.Grado)
                  .WithMany(g => g.Cursos)
                  .HasForeignKey(c => c.GradoId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_cursos_grados");

            entity.HasOne(c => c.Docente)
                  .WithMany(u => u.CursosDocente)
                  .HasForeignKey(c => c.DocenteId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_cursos_docentes");
        });

        // 7. INSCRIPCIONES
        modelBuilder.Entity<Inscripcion>(entity =>
        {
            entity.ToTable("inscripciones");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(i => i.CursoId).HasColumnName("curso_id").IsRequired();
            entity.Property(i => i.AlumnoId).HasColumnName("alumno_id").IsRequired();
            entity.Property(i => i.FechaRegistro).HasColumnName("fecha_registro").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(i => i.Estado).HasColumnName("estado").HasMaxLength(20).HasDefaultValue("ACTIVA");

            entity.HasIndex(i => new { i.CursoId, i.AlumnoId })
                  .IsUnique()
                  .HasDatabaseName("uq_inscripcion_curso_alumno");

            entity.HasIndex(i => i.CursoId)
                  .HasDatabaseName("idx_inscripciones_curso");

            entity.HasIndex(i => i.AlumnoId)
                  .HasDatabaseName("idx_inscripciones_alumno");

            entity.HasOne(i => i.Curso)
                  .WithMany(c => c.Inscripciones)
                  .HasForeignKey(i => i.CursoId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_inscripciones_cursos");

            entity.HasOne(i => i.Alumno)
                  .WithMany(a => a.Inscripciones)
                  .HasForeignKey(i => i.AlumnoId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_inscripciones_alumnos");
        });

        // 8. TAREAS
        modelBuilder.Entity<Tarea>(entity =>
        {
            entity.ToTable("tareas");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(t => t.CursoId).HasColumnName("curso_id").IsRequired();
            entity.Property(t => t.Titulo).HasColumnName("titulo").HasMaxLength(150).IsRequired();
            entity.Property(t => t.Descripcion).HasColumnName("descripcion").HasColumnType("text");
            entity.Property(t => t.FechaPublicacion).HasColumnName("fecha_publicacion").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(t => t.FechaLimite).HasColumnName("fecha_limite").IsRequired();
            entity.Property(t => t.PuntajeMaximo).HasColumnName("puntaje_maximo").HasPrecision(5, 2).HasDefaultValue(100.00m);
            entity.Property(t => t.Activo).HasColumnName("activo").HasColumnType("tinyint(1)").HasDefaultValue(true);

            entity.HasIndex(t => t.CursoId)
                  .HasDatabaseName("idx_tareas_curso");

            entity.HasOne(t => t.Curso)
                  .WithMany(c => c.Tareas)
                  .HasForeignKey(t => t.CursoId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_tareas_cursos");
        });

        // 9. ENTREGAS
        modelBuilder.Entity<Entrega>(entity =>
        {
            entity.ToTable("entregas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.TareaId).HasColumnName("tarea_id").IsRequired();
            entity.Property(e => e.AlumnoId).HasColumnName("alumno_id").IsRequired();
            entity.Property(e => e.ContenidoTexto).HasColumnName("contenido_texto").HasColumnType("text");
            entity.Property(e => e.FechaEntrega).HasColumnName("fecha_entrega").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(20).HasDefaultValue("ENVIADA");
            entity.Property(e => e.Calificacion).HasColumnName("calificacion").HasPrecision(5, 2);
            entity.Property(e => e.Retroalimentacion).HasColumnName("retroalimentacion").HasColumnType("text");
            entity.Property(e => e.FechaCalificacion).HasColumnName("fecha_calificacion");

            entity.HasIndex(e => new { e.TareaId, e.AlumnoId })
                  .IsUnique()
                  .HasDatabaseName("uq_entrega_tarea_alumno");

            entity.HasIndex(e => e.TareaId)
                  .HasDatabaseName("idx_entregas_tarea");

            entity.HasIndex(e => e.AlumnoId)
                  .HasDatabaseName("idx_entregas_alumno");

            entity.HasOne(e => e.Tarea)
                  .WithMany(t => t.Entregas)
                  .HasForeignKey(e => e.TareaId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_entregas_tareas");

            entity.HasOne(e => e.Alumno)
                  .WithMany(a => a.Entregas)
                  .HasForeignKey(e => e.AlumnoId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_entregas_alumnos");
        });

        // 10. ARCHIVOS_ENTREGA
        modelBuilder.Entity<ArchivoEntrega>(entity =>
        {
            entity.ToTable("archivos_entrega");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(a => a.EntregaId).HasColumnName("entrega_id").IsRequired();
            entity.Property(a => a.NombreOriginal).HasColumnName("nombre_original").HasMaxLength(255).IsRequired();
            entity.Property(a => a.NombreArchivo).HasColumnName("nombre_archivo").HasMaxLength(255).IsRequired();
            entity.Property(a => a.RutaArchivo).HasColumnName("ruta_archivo").HasMaxLength(500).IsRequired();
            entity.Property(a => a.TipoMime).HasColumnName("tipo_mime").HasMaxLength(100).IsRequired();
            entity.Property(a => a.TamanoBytes).HasColumnName("tamano_bytes").HasColumnType("bigint unsigned").IsRequired();
            entity.Property(a => a.FechaSubida).HasColumnName("fecha_subida").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(a => a.EntregaId)
                  .HasDatabaseName("idx_archivos_entrega");

            entity.HasOne(a => a.Entrega)
                  .WithMany(e => e.Archivos)
                  .HasForeignKey(a => a.EntregaId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_archivos_entrega");
        });

        // 11. MATERIALES
        modelBuilder.Entity<Material>(entity =>
        {
            entity.ToTable("materiales");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(m => m.CursoId).HasColumnName("curso_id").IsRequired();
            entity.Property(m => m.Titulo).HasColumnName("titulo").HasMaxLength(150).IsRequired();
            entity.Property(m => m.Descripcion).HasColumnName("descripcion").HasColumnType("text");
            entity.Property(m => m.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
            entity.Property(m => m.RecursoUrl).HasColumnName("recurso_url").HasMaxLength(500).IsRequired();
            entity.Property(m => m.FechaPublicacion).HasColumnName("fecha_publicacion").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(m => m.Activo).HasColumnName("activo").HasColumnType("tinyint(1)").HasDefaultValue(true);

            entity.HasIndex(m => m.CursoId)
                  .HasDatabaseName("idx_materiales_curso");

            entity.HasOne(m => m.Curso)
                  .WithMany(c => c.Materiales)
                  .HasForeignKey(m => m.CursoId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_materiales_cursos");
        });
    }
}
