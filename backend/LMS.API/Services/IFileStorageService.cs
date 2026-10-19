using Microsoft.AspNetCore.Http;

namespace LMS.API.Services;

/// <summary>
/// Resultado de un almacenamiento exitoso. La URL es una ruta de API autenticada,
/// nunca una ruta estática pública.
/// </summary>
public sealed class StoredFile
{
    public bool Success { get; init; }
    public string Url { get; init; } = string.Empty;
    public string StorageKey { get; init; } = string.Empty;
    public string OriginalName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public string Sha256 { get; init; } = string.Empty;
    public long Size { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}

/// <summary>
/// Metadatos privados que acompañan cada archivo (sidecar .meta.json).
/// Permiten autorizar la descarga sin exponer rutas físicas.
/// </summary>
public sealed class StoredFileMetadata
{
    public string Scope { get; set; } = string.Empty;
    public int OwnerUserId { get; set; }
    public string OwnerRole { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string Sha256 { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; }
}

public interface IFileStorageService
{
    Task<StoredFile> SaveFileAsync(
        IFormFile file,
        string scope,
        int ownerUserId,
        string ownerRole,
        CancellationToken cancellationToken = default);

    Task<StoredFileMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string? urlOrStorageKey, CancellationToken cancellationToken = default);

    /// <summary>Resuelve la ruta física de una clave validada; null si es inválida o inexistente.</summary>
    string? ResolvePhysicalPath(string storageKey);

    /// <summary>Resuelve la ruta física de un archivo legado servido antes desde /uploads.</summary>
    string? ResolveLegacyPhysicalPath(string legacyPath);

    string GetUrl(string storageKey);
}
