using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using LMS.API.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LMS.API.Services;

/// <summary>
/// Almacenamiento local privado de adjuntos escolares.
///
/// Cambios de seguridad frente a la versión anterior:
///  - Los archivos ya NO se guardan dentro de wwwroot (dejaban de ser públicos).
///  - Se valida la firma binaria real del archivo, no solo la extensión.
///  - Se calcula SHA-256 y se guardan metadatos para autorizar cada descarga.
///  - El nombre original se sanea y el nombre físico es un GUID (sin path traversal).
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private static readonly Regex StorageKeyPattern = new(
        @"^(submissions|resources)/[a-f0-9]{32}\.(pdf|jpg|jpeg|png|webp|docx|doc)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] AllowedScopes = { "submissions", "resources" };

    private readonly IWebHostEnvironment _environment;
    private readonly StorageOptions _options;
    private readonly string _rootPath;

    public string ProviderName => "Local";

    public LocalFileStorageService(IWebHostEnvironment environment, IOptions<StorageOptions> options)
    {
        _environment = environment;
        _options = options.Value;

        _rootPath = string.IsNullOrWhiteSpace(_options.RootPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "uploads")
            : Path.GetFullPath(_options.RootPath);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFile> SaveFileAsync(
        IFormFile file,
        string scope,
        int ownerUserId,
        string ownerRole,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return Fail("No se seleccionó ningún archivo.");
        }

        if (!AllowedScopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
        {
            return Fail("Carpeta de destino no válida.");
        }

        var normalizedScope = scope.ToLowerInvariant();

        if (file.Length > _options.MaxFileSizeBytes)
        {
            var maxMb = _options.MaxFileSizeBytes / (1024 * 1024);
            return Fail($"El archivo supera el tamaño máximo permitido de {maxMb} MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!FileSignatureValidator.IsAllowedExtension(extension))
        {
            return Fail($"Formato no permitido. Formatos aceptados: {string.Join(", ", FileSignatureValidator.AllowedExtensions)}");
        }

        if (!FileSignatureValidator.IsAllowedMimeType(file.ContentType, extension))
        {
            return Fail($"El tipo MIME del archivo ('{file.ContentType ?? "desconocido"}') no es válido o no corresponde a la extensión {extension}.");
        }

        try
        {
            await using (var signatureStream = file.OpenReadStream())
            {
                if (!await FileSignatureValidator.HasValidSignatureAsync(signatureStream, extension, cancellationToken))
                {
                    return Fail("El contenido binario (Magic Bytes) del archivo no corresponde a un formato permitido. Verifica que no esté dañado o renombrado.");
                }
            }
        }
        catch (Exception)
        {
            return Fail("No fue posible leer el archivo. Intenta de nuevo.");
        }

        var storageKey = $"{normalizedScope}/{Guid.NewGuid():N}{extension}";
        var physicalPath = ResolvePhysicalPath(storageKey);
        if (physicalPath == null)
        {
            return Fail("No fue posible determinar la ruta de almacenamiento.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        var tempPath = physicalPath + ".tmp";
        var sha256 = string.Empty;

        try
        {
            await using (var input = file.OpenReadStream())
            await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    hash.AppendData(buffer, 0, read);
                }

                sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }

            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }

            System.IO.File.Move(tempPath, physicalPath);

            var metadata = new StoredFileMetadata
            {
                Scope = normalizedScope,
                OwnerUserId = ownerUserId,
                OwnerRole = ownerRole,
                OriginalName = FileSignatureValidator.SanitizeOriginalFileName(file.FileName),
                ContentType = FileSignatureValidator.GetContentType(extension),
                Sha256 = sha256,
                Size = file.Length,
                UploadedAt = DateTime.UtcNow
            };

            await System.IO.File.WriteAllTextAsync(
                physicalPath + ".meta.json",
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);

            return new StoredFile
            {
                Success = true,
                Url = GetUrl(storageKey),
                StorageKey = storageKey,
                OriginalName = metadata.OriginalName,
                ContentType = metadata.ContentType,
                Sha256 = sha256,
                Size = file.Length
            };
        }
        catch (Exception)
        {
            TryDelete(tempPath);
            return Fail("No fue posible guardar el archivo. Intenta de nuevo.");
        }
    }

    public async Task<StoredFileMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var physicalPath = ResolvePhysicalPath(storageKey);
        if (physicalPath == null)
        {
            return null;
        }

        var metadataPath = physicalPath + ".meta.json";
        if (!System.IO.File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            var json = await System.IO.File.ReadAllTextAsync(metadataPath, cancellationToken);
            return JsonSerializer.Deserialize<StoredFileMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    public Task<Stream?> OpenReadStreamAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var physicalPath = ResolvePhysicalPath(storageKey) ?? ResolveLegacyPhysicalPath(storageKey);
        if (physicalPath == null || !System.IO.File.Exists(physicalPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        try
        {
            Stream stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return Task.FromResult<Stream?>(stream);
        }
        catch
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task<bool> DeleteAsync(string? urlOrStorageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(urlOrStorageKey))
        {
            return Task.FromResult(false);
        }

        var key = NormalizeKey(urlOrStorageKey);
        var physicalPath = ResolvePhysicalPath(key) ?? ResolveLegacyPhysicalPath(urlOrStorageKey);
        if (physicalPath == null)
        {
            return Task.FromResult(false);
        }

        TryDelete(physicalPath);
        TryDelete(physicalPath + ".meta.json");

        return Task.FromResult(true);
    }

    public string? ResolvePhysicalPath(string storageKey)
    {
        var key = NormalizeKey(storageKey);
        if (!StorageKeyPattern.IsMatch(key))
        {
            return null;
        }

        var candidate = Path.GetFullPath(Path.Combine(_rootPath, key.Replace('/', Path.DirectorySeparatorChar)));

        return IsInsideRoot(candidate, _rootPath) ? candidate : null;
    }

    public string? ResolveLegacyPhysicalPath(string legacyPath)
    {
        if (string.IsNullOrWhiteSpace(legacyPath))
        {
            return null;
        }

        var normalized = legacyPath.Replace('\\', '/').TrimStart('/');

        // Solo se aceptan rutas heredadas dentro de /uploads/.
        if (!normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;

        var candidate = Path.GetFullPath(Path.Combine(webRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));

        return IsInsideRoot(candidate, webRoot) ? candidate : null;
    }

    public string GetUrl(string storageKey) => $"/api/files/{NormalizeKey(storageKey)}";

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var key = value.Trim().Replace('\\', '/');

        if (key.StartsWith("/api/files/", StringComparison.OrdinalIgnoreCase))
        {
            key = key["/api/files/".Length..];
        }
        else if (key.StartsWith("api/files/", StringComparison.OrdinalIgnoreCase))
        {
            key = key["api/files/".Length..];
        }
        else if (key.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return key; // Se resuelve por la ruta heredada.
        }

        return key.TrimStart('/');
    }

    private static bool IsInsideRoot(string candidate, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch
        {
            // La limpieza no debe interrumpir el flujo principal.
        }
    }

    private static StoredFile Fail(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}
