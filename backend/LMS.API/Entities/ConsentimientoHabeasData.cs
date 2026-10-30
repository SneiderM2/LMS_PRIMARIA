using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.API.Entities;

/// <summary>
/// Registro probatorio inmutable de autorización de tratamiento de datos personales de menores
/// de edad otorgado por su representante legal/acudiente (Ley 1581 de 2012 Art. 7 y 9,
/// Decreto 1377 de 2013 Art. 12 y Ley 1098 de 2006 Código de la Infancia y la Adolescencia).
/// </summary>
[Table("consentimientos_habeas_data")]
public class ConsentimientoHabeasData
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// Identificador del alumno menor de edad (FK a alumnos.usuario_id).
    /// </summary>
    [Required]
    [Column("alumno_id")]
    public int AlumnoId { get; set; }

    /// <summary>
    /// Identificador de la cuenta de usuario asociada al titular/acudiente (FK a usuarios.id).
    /// </summary>
    [Column("usuario_id")]
    public int? UsuarioId { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("acudiente_nombre")]
    public string AcudienteNombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("acudiente_documento")]
    public string AcudienteDocumento { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de documento del representante legal (CC, CE, TI, PEP, PPT, PASAPORTE).
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("tipo_documento")]
    public string TipoDocumento { get; set; } = "CC";

    [Required]
    [MaxLength(32)]
    [Column("version_politica")]
    public string VersionPolitica { get; set; } = string.Empty;

    /// <summary>
    /// Hash criptográfico SHA-256 del texto de la política de privacidad aceptada.
    /// </summary>
    [Required]
    [MaxLength(128)]
    [Column("hash_texto_politica")]
    public string HashTextoPolitica { get; set; } = string.Empty;

    /// <summary>
    /// Finalidad esencial obligatoria: prestación del servicio educativo y gestión académica.
    /// </summary>
    [Required]
    [Column("autoriza_servicio_educativo")]
    public bool AutorizaServicioEducativo { get; set; }

    /// <summary>
    /// Finalidad opcional: analítica pedagógica no esencial, boletines o actividades extracurriculares.
    /// </summary>
    [Column("autoriza_datos_opcionales")]
    public bool AutorizaDatosOpcionales { get; set; }

    /// <summary>
    /// Autorización informada para transmisión internacional de datos a infraestructura cloud (Art. 26 Ley 1581).
    /// </summary>
    [Column("autoriza_transmision_internacional")]
    public bool AutorizaTransmisionInternacional { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("ip_origen")]
    public string IpOrigen { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    [Column("user_agent")]
    public string UserAgent { get; set; } = string.Empty;

    [Required]
    [Column("fecha_autorizacion_utc")]
    public DateTime FechaAutorizacionUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Si no es nulo, indica que el acudiente revocó la autorización y el menor no puede operar.
    /// </summary>
    [Column("fecha_revocatoria_utc")]
    public DateTime? FechaRevocatoriaUtc { get; set; }

    // Propiedades de navegación
    public Alumno? Alumno { get; set; }
    public Usuario? Usuario { get; set; }

    [NotMapped]
    public bool EstaVigente => AutorizaServicioEducativo && FechaRevocatoriaUtc == null;
}
