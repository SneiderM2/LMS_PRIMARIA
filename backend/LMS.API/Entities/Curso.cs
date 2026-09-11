namespace LMS.API.Entities;

public class Curso
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int GradoId { get; set; }
    public string Grupo { get; set; } = string.Empty;
    public int DocenteId { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public bool Activo { get; set; } = true;

    // Navegaciones
    public Grado Grado { get; set; } = null!;
    public Usuario Docente { get; set; } = null!;
    public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
    public ICollection<Tarea> Tareas { get; set; } = new List<Tarea>();
    public ICollection<Material> Materiales { get; set; } = new List<Material>();
}
