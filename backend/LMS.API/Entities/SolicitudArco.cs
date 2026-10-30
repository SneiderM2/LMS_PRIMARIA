using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.API.Entities;

/// <summary>
/// Entidad para gestionar solicitudes de derechos ARCO (Conocer, Actualizar, Rectificar y Suprimir)
/// y revocatoria del consentimiento conforme a los artículos 8, 14 y 15 de la Ley 1581 de 2012.
/// </summary>
[Table("solicitudes_arco")]
public class SolicitudArco
{
    public const int DiasHabilesConsulta = 10;
    public const int DiasHabilesReclamo = 15;

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("usuario_id")]
    public int? UsuarioId { get; set; }

    [Column("alumno_id")]
    public int? AlumnoId { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("solicitante_nombre")]
    public string SolicitanteNombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("solicitante_documento")]
    public string SolicitanteDocumento { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de derecho ejercido: CONOCER (Consulta - 10 días hábiles),
    /// ACTUALIZAR, RECTIFICAR, SUPRIMIR, REVOCAR (Reclamos - 15 días hábiles).
    /// </summary>
    [Required]
    [MaxLength(30)]
    [Column("tipo_solicitud")]
    public string TipoSolicitud { get; set; } = "CONOCER";

    [Required]
    [MaxLength(2000)]
    [Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Estado del trámite: RADICADA, EN_TRAMITE, RESUELTA, RECHAZADA.
    /// </summary>
    [Required]
    [MaxLength(30)]
    [Column("estado")]
    public string Estado { get; set; } = "RADICADA";

    [Required]
    [Column("fecha_solicitud_utc")]
    public DateTime FechaSolicitudUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha límite legal en Colombia (Art. 14 Ley 1581: 10 días hábiles para Consultas;
    /// Art. 15 Ley 1581: 15 días hábiles para Reclamos/Supresión/Rectificación/Actualización).
    /// </summary>
    [Required]
    [Column("fecha_limite_legal_utc")]
    public DateTime FechaLimiteLegalUtc { get; set; }

    [Column("fecha_respuesta_utc")]
    public DateTime? FechaRespuestaUtc { get; set; }

    [MaxLength(2000)]
    [Column("respuesta")]
    public string? Respuesta { get; set; }

    [MaxLength(64)]
    [Column("ip_origen")]
    public string? IpOrigen { get; set; }

    public Usuario? Usuario { get; set; }
    public Alumno? Alumno { get; set; }

    /// <summary>
    /// Determina si el tipo de solicitud corresponde a una Consulta (10 días hábiles, Art. 14 Ley 1581)
    /// o a un Reclamo/Supresión/Rectificación/Actualización (15 días hábiles, Art. 15 Ley 1581).
    /// </summary>
    public static int ObtenerTerminoDiasHabiles(string? tipoSolicitud)
    {
        var normalizado = (tipoSolicitud ?? string.Empty).Trim().ToUpperInvariant();
        return normalizado is "CONSULTA" or "CONOCER" or "ACCESO"
            ? DiasHabilesConsulta
            : DiasHabilesReclamo;
    }

    /// <summary>
    /// Calcula la fecha límite legal en días hábiles colombianos (excluyendo sábados, domingos
    /// y festivos nacionales fijos en Colombia).
    /// </summary>
    public static DateTime CalcularFechaLimiteLegalColombia(DateTime fechaInicioUtc, string? tipoSolicitud)
    {
        var diasHabilesRequeridos = ObtenerTerminoDiasHabiles(tipoSolicitud);
        var cursor = fechaInicioUtc.Date;
        var diasContados = 0;

        while (diasContados < diasHabilesRequeridos)
        {
            cursor = cursor.AddDays(1);
            if (EsDiaHabilColombia(cursor))
            {
                diasContados++;
            }
        }

        // Vence al final del último día hábil en horario legal colombiano (UTC-5 -> 23:59:59)
        return DateTime.SpecifyKind(cursor.AddHours(23).AddMinutes(59).AddSeconds(59), DateTimeKind.Utc);
    }

    public static bool EsDiaHabilColombia(DateTime fecha)
    {
        if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var festivos = ObtenerFestivosColombia(fecha.Year);
        return !festivos.Contains(fecha.Date);
    }

    /// <summary>
    /// Calcula los 18 festivos nacionales de Colombia para cualquier año, aplicando:
    /// 1) Festivos de fecha fija inamovibles,
    /// 2) Festivos trasladables al lunes siguiente por la Ley 51 de 1983 (Ley Emiliani),
    /// 3) Festivos religiosos móviles calculados dinámicamente a partir del Domingo de Pascua.
    /// </summary>
    public static HashSet<DateTime> ObtenerFestivosColombia(int year)
    {
        var festivos = new HashSet<DateTime>
        {
            // 1. Festivos de fecha fija inamovibles
            new(year, 1, 1),   // Año Nuevo
            new(year, 5, 1),   // Día del Trabajo
            new(year, 7, 20),  // Día de la Independencia
            new(year, 8, 7),   // Batalla de Boyacá
            new(year, 12, 8),  // Inmaculada Concepción
            new(year, 12, 25), // Navidad

            // 2. Festivos sujetos a la Ley Emiliani (se trasladan al siguiente lunes si no caen en lunes)
            TrasladarLunesEmiliani(new DateTime(year, 1, 6)),   // Epifanía (Reyes Magos)
            TrasladarLunesEmiliani(new DateTime(year, 3, 19)),  // Día de San José
            TrasladarLunesEmiliani(new DateTime(year, 6, 29)),  // San Pedro y San Pablo
            TrasladarLunesEmiliani(new DateTime(year, 8, 15)),  // Asunción de la Virgen
            TrasladarLunesEmiliani(new DateTime(year, 10, 12)), // Día de la Raza
            TrasladarLunesEmiliani(new DateTime(year, 11, 1)),  // Todos los Santos
            TrasladarLunesEmiliani(new DateTime(year, 11, 11))  // Independencia de Cartagena
        };

        // 3. Festivos móviles calculados dinámicamente respecto al Domingo de Pascua
        var domingoPascua = CalcularDomingoPascua(year);
        festivos.Add(domingoPascua.AddDays(-3)); // Jueves Santo
        festivos.Add(domingoPascua.AddDays(-2)); // Viernes Santo
        festivos.Add(TrasladarLunesEmiliani(domingoPascua.AddDays(39))); // Ascensión del Señor (lunes Emiliani)
        festivos.Add(TrasladarLunesEmiliani(domingoPascua.AddDays(60))); // Corpus Christi (lunes Emiliani)
        festivos.Add(TrasladarLunesEmiliani(domingoPascua.AddDays(68))); // Sagrado Corazón de Jesús (lunes Emiliani)

        return festivos;
    }

    private static DateTime TrasladarLunesEmiliani(DateTime fecha)
    {
        return fecha.DayOfWeek == DayOfWeek.Monday
            ? fecha.Date
            : fecha.Date.AddDays(((int)DayOfWeek.Monday - (int)fecha.DayOfWeek + 7) % 7);
    }

    /// <summary>
    /// Algoritmo astronómico de Meeus/Jones/Butcher para obtener el Domingo de Pascua en cualquier año gregoriano.
    /// </summary>
    public static DateTime CalcularDomingoPascua(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;

        return new DateTime(year, month, day);
    }
}
