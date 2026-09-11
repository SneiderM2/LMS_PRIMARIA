namespace LMS.API.Entities;

public class Inscripcion
{
    public int Id { get; set; }
    public int CursoId { get; set; }
    public int AlumnoId { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = "ACTIVA";

    // Navegaciones
    public Curso Curso { get; set; } = null!;
    public Alumno Alumno { get; set; } = null!;
}
