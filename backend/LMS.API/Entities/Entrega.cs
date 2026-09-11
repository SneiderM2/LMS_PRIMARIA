namespace LMS.API.Entities;

public class Entrega
{
    public int Id { get; set; }
    public int TareaId { get; set; }
    public int AlumnoId { get; set; }
    public string? ContenidoTexto { get; set; }
    public DateTime FechaEntrega { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = "ENVIADA";
    public decimal? Calificacion { get; set; }
    public string? Retroalimentacion { get; set; }
    public DateTime? FechaCalificacion { get; set; }

    // Navegaciones
    public Tarea Tarea { get; set; } = null!;
    public Alumno Alumno { get; set; } = null!;
    public ICollection<ArchivoEntrega> Archivos { get; set; } = new List<ArchivoEntrega>();
}
