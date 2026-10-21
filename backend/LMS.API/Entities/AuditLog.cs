namespace LMS.API.Entities;

/// <summary>
/// Registro persistente de auditoría en base de datos (cumplimiento Ley 1581 de 2012 / Ley 1098 de 2006).
/// Resiste reinicios de contenedores en disco efímero.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "INFO";
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long ElapsedMs { get; set; }
    public string Ip { get; set; } = "0.0.0.0";
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserRole { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
}
