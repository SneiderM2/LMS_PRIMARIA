namespace LMS.API.Entities;

public class Alumno
{
    public int UsuarioId { get; set; }
    public int GradoId { get; set; }

    // Navegaciones
    public Usuario Usuario { get; set; } = null!;
    public Grado Grado { get; set; } = null!;
    public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
    public ICollection<Entrega> Entregas { get; set; } = new List<Entrega>();
    public ICollection<ConsentimientoHabeasData> ConsentimientosHabeasData { get; set; } = new List<ConsentimientoHabeasData>();
    public ICollection<SolicitudArco> SolicitudesArco { get; set; } = new List<SolicitudArco>();
}
