namespace LMS.API.Entities;

public class ArchivoEntrega
{
    public int Id { get; set; }
    public int EntregaId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public ulong TamanoBytes { get; set; }
    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;

    // Navegación
    public Entrega Entrega { get; set; } = null!;
}
