namespace LMS.API.Entities;

public class Material
{
    public int Id { get; set; }
    public int CursoId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Tipo { get; set; } = "DOCUMENTO"; // 'DOCUMENTO', 'VIDEO', 'IMAGEN', 'ENLACE', 'OTRO'
    public string RecursoUrl { get; set; } = string.Empty;
    public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;
    public bool Activo { get; set; } = true;

    // Navegación
    public Curso Curso { get; set; } = null!;
}
