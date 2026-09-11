namespace LMS.API.Entities;

public class Usuario
{
    public int Id { get; set; }
    public int RolId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    
    // Campos de datos personales unificados (reemplaza tabla perfiles)
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Propiedad calculada
    public string FullName => $"{Nombre} {Apellido}".Trim();

    // Navegaciones (sin tabla Perfil)
    public Rol Rol { get; set; } = null!;
    public Alumno? Alumno { get; set; }
    public ICollection<Curso> CursosDocente { get; set; } = new List<Curso>();
}
