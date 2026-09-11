namespace LMS.API.Entities;

public class Rol
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // Navegación
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
