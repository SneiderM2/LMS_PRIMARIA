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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ConsentimientoHabeasData> ConsentimientosHabeasData => Set<ConsentimientoHabeasData>();
    public DbSet<SolicitudArco> SolicitudesArco => Set<SolicitudArco>();

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
            entity.Property(u => u.Activo).HasColumnName("activo").HasDefaultValue(true);
            entity.Property(u => u.FechaCreacion).HasColumnName("fecha_creacion").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(u => u.DataPolicyAccepted).HasColumnName("data_policy_accepted").HasDefaultValue(false);
            entity.Property(u => u.DataPolicyAcceptedAt).HasColumnName("data_policy_accepted_at");
            entity.Property(u => u.DataPolicyVersion).HasColumnName("data_policy_version").HasMaxLength(20);
            entity.Property(u => u.DataPolicyEvidence).HasColumnName("data_policy_evidence").HasColumnType("text");
            entity.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count").HasDefaultValue(0);
            entity.Property(u => u.LockoutEnd).HasColumnName("lockout_end");
            entity.Property(u => u.SessionToken).HasColumnName("session_token").HasMaxLength(255);
            entity.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(u => u.DeletedAt).HasColumnName("deleted_at");
            entity.Property(u => u.AnonymizedAt).HasColumnName("anonymized_at");

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
            entity.Property(c => c.Activo).HasColumnName("activo").HasDefaultValue(true);

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
            entity.Property(t => t.Activo).HasColumnName("activo").HasDefaultValue(true);

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
            entity.Property(a => a.TamanoBytes).HasColumnName("tamano_bytes").IsRequired();
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
            // recurso_url puede ser NULL en la BD (ej. materiales de texto puro): se mapea como nullable
            // y se asigna DEFAULT '' para evitar InvalidCastException en lecturas con Npgsql.
            entity.Property(m => m.RecursoUrl).HasColumnName("recurso_url").HasMaxLength(500).IsRequired(false).HasDefaultValue("");
            entity.Property(m => m.FechaPublicacion).HasColumnName("fecha_publicacion").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(m => m.Activo).HasColumnName("activo").HasDefaultValue(true);

            entity.HasIndex(m => m.CursoId)
                  .HasDatabaseName("idx_materiales_curso");

            entity.HasOne(m => m.Curso)
                  .WithMany(c => c.Materiales)
                  .HasForeignKey(m => m.CursoId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_materiales_cursos");
        });

        // 12. AUDIT LOGS (Persistencia Ley 1581 / Ley 1098)
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(a => a.Timestamp).HasColumnName("timestamp").IsRequired();
            entity.Property(a => a.Level).HasColumnName("level").HasMaxLength(20).IsRequired();
            entity.Property(a => a.Method).HasColumnName("method").HasMaxLength(10).IsRequired();
            entity.Property(a => a.Path).HasColumnName("path").HasMaxLength(500).IsRequired();
            entity.Property(a => a.StatusCode).HasColumnName("status_code").IsRequired();
            entity.Property(a => a.ElapsedMs).HasColumnName("elapsed_ms").IsRequired();
            entity.Property(a => a.Ip).HasColumnName("ip").HasMaxLength(50).IsRequired();
            entity.Property(a => a.UserId).HasColumnName("user_id").HasMaxLength(100);
            entity.Property(a => a.UserEmail).HasColumnName("user_email").HasMaxLength(200);
            entity.Property(a => a.UserRole).HasColumnName("user_role").HasMaxLength(50);
            entity.Property(a => a.Action).HasColumnName("action").HasMaxLength(250).IsRequired();
            entity.Property(a => a.Details).HasColumnName("details");

            entity.HasIndex(a => a.Timestamp).HasDatabaseName("idx_audit_logs_timestamp");
            entity.HasIndex(a => a.Level).HasDatabaseName("idx_audit_logs_level");
        });

        // 13. CONSENTIMIENTO HABEAS DATA (Ley 1581 de 2012 Art. 7 y 9 / Decreto 1377 de 2013 / Ley 1098 de 2006)
        modelBuilder.Entity<ConsentimientoHabeasData>(entity =>
        {
            entity.ToTable("consentimientos_habeas_data");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(c => c.AlumnoId).HasColumnName("alumno_id").IsRequired();
            entity.Property(c => c.UsuarioId).HasColumnName("usuario_id");
            entity.Property(c => c.AcudienteNombre).HasColumnName("acudiente_nombre").HasMaxLength(150).IsRequired();
            entity.Property(c => c.AcudienteDocumento).HasColumnName("acudiente_documento").HasMaxLength(50).IsRequired();
            entity.Property(c => c.TipoDocumento).HasColumnName("tipo_documento").HasMaxLength(20).HasDefaultValue("CC").IsRequired();
            entity.Property(c => c.VersionPolitica).HasColumnName("version_politica").HasMaxLength(32).IsRequired();
            entity.Property(c => c.HashTextoPolitica).HasColumnName("hash_texto_politica").HasMaxLength(128).IsRequired();
            entity.Property(c => c.AutorizaServicioEducativo).HasColumnName("autoriza_servicio_educativo").IsRequired();
            entity.Property(c => c.AutorizaDatosOpcionales).HasColumnName("autoriza_datos_opcionales").HasDefaultValue(false);
            entity.Property(c => c.AutorizaTransmisionInternacional).HasColumnName("autoriza_transmision_internacional").HasDefaultValue(false);
            entity.Property(c => c.IpOrigen).HasColumnName("ip_origen").HasMaxLength(64).IsRequired();
            entity.Property(c => c.UserAgent).HasColumnName("user_agent").HasMaxLength(300).IsRequired();
            entity.Property(c => c.FechaAutorizacionUtc).HasColumnName("fecha_autorizacion_utc").IsRequired();
            entity.Property(c => c.FechaRevocatoriaUtc).HasColumnName("fecha_revocatoria_utc");

            entity.HasIndex(c => c.AlumnoId).HasDatabaseName("idx_consentimiento_alumno");
            entity.HasIndex(c => new { c.AlumnoId, c.FechaRevocatoriaUtc }).HasDatabaseName("idx_consentimiento_alumno_vigente");

            entity.HasOne(c => c.Alumno)
                  .WithMany(a => a.ConsentimientosHabeasData)
                  .HasForeignKey(c => c.AlumnoId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_consentimientos_alumnos");

            entity.HasOne(c => c.Usuario)
                  .WithMany(u => u.ConsentimientosHabeasData)
                  .HasForeignKey(c => c.UsuarioId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("fk_consentimientos_usuarios");
        });

        // 14. SOLICITUDES ARCO (Ley 1581 de 2012 Art. 8, 14 y 15)
        modelBuilder.Entity<SolicitudArco>(entity =>
        {
            entity.ToTable("solicitudes_arco");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(s => s.UsuarioId).HasColumnName("usuario_id");
            entity.Property(s => s.AlumnoId).HasColumnName("alumno_id");
            entity.Property(s => s.SolicitanteNombre).HasColumnName("solicitante_nombre").HasMaxLength(150).IsRequired();
            entity.Property(s => s.SolicitanteDocumento).HasColumnName("solicitante_documento").HasMaxLength(50).IsRequired();
            entity.Property(s => s.TipoSolicitud).HasColumnName("tipo_solicitud").HasMaxLength(30).IsRequired();
            entity.Property(s => s.Descripcion).HasColumnName("descripcion").HasMaxLength(2000).IsRequired();
            entity.Property(s => s.Estado).HasColumnName("estado").HasMaxLength(30).HasDefaultValue("RADICADA").IsRequired();
            entity.Property(s => s.FechaSolicitudUtc).HasColumnName("fecha_solicitud_utc").IsRequired();
            entity.Property(s => s.FechaLimiteLegalUtc).HasColumnName("fecha_limite_legal_utc").IsRequired();
            entity.Property(s => s.FechaRespuestaUtc).HasColumnName("fecha_respuesta_utc");
            entity.Property(s => s.Respuesta).HasColumnName("respuesta").HasMaxLength(2000);
            entity.Property(s => s.IpOrigen).HasColumnName("ip_origen").HasMaxLength(64);

            entity.HasIndex(s => s.UsuarioId).HasDatabaseName("idx_solicitudes_arco_usuario");
            entity.HasIndex(s => new { s.Estado, s.FechaLimiteLegalUtc }).HasDatabaseName("idx_solicitudes_arco_estado_limite");

            entity.HasOne(s => s.Usuario)
                  .WithMany(u => u.SolicitudesArco)
                  .HasForeignKey(s => s.UsuarioId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("fk_solicitudes_arco_usuarios");

            entity.HasOne(s => s.Alumno)
                  .WithMany(a => a.SolicitudesArco)
                  .HasForeignKey(s => s.AlumnoId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("fk_solicitudes_arco_alumnos");
        });
    }
}
