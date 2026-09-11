namespace LMS.API.Entities;

public class Tarea
{
    public int Id { get; set; }
    public int CursoId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaLimite { get; set; }
    public decimal PuntajeMaximo { get; set; } = 100.00m;
    public bool Activo { get; set; } = true;

    // Navegaciones
    public Curso Curso { get; set; } = null!;
    public ICollection<Entrega> Entregas { get; set; } = new List<Entrega>();
}
