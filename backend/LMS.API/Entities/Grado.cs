namespace LMS.API.Entities;

public class Grado
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // Navegaciones
    public ICollection<Alumno> Alumnos { get; set; } = new List<Alumno>();
    public ICollection<Curso> Cursos { get; set; } = new List<Curso>();
}
