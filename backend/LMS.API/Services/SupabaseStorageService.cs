using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LMS.API.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LMS.API.Services;

/// <summary>
/// Almacenamiento seguro en la nube (Supabase Storage) con fallback local automático.
/// Protege los adjuntos escolares de la pérdida en contenedores con disco efímero.
/// </summary>
public sealed class SupabaseStorageService : IFileStorageService
{
    private static readonly Regex StorageKeyPattern = new(
        @"^(submissions|resources)/[a-f0-9]{32}\.(pdf|jpg|jpeg|png|webp|docx|doc)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] AllowedScopes = { "submissions", "resources" };

    private readonly HttpClient _httpClient;
    private readonly StorageOptions _options;
    private readonly LocalFileStorageService _localFallback;
    private readonly ILogger<SupabaseStorageService> _logger;
    private readonly string _baseUrl;
    private readonly string _serviceKey;
    private readonly string _bucket;
    private bool _bucketChecked;

    public string ProviderName => "Supabase";

    public SupabaseStorageService(
        IHttpClientFactory httpClientFactory,
        IOptions<StorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<SupabaseStorageService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("SupabaseStorage");
        _options = options.Value;
        _logger = logger;
        _localFallback = new LocalFileStorageService(environment, options);

        var url = !string.IsNullOrWhiteSpace(_options.Supabase.Url)
            ? _options.Supabase.Url
            : Environment.GetEnvironmentVariable("SUPABASE_URL") ?? string.Empty;

        var key = !string.IsNullOrWhiteSpace(_options.Supabase.ServiceRoleKey)
            ? _options.Supabase.ServiceRoleKey
            : Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") ?? string.Empty;

        var bucket = !string.IsNullOrWhiteSpace(_options.Supabase.Bucket)
            ? _options.Supabase.Bucket
            : Environment.GetEnvironmentVariable("SUPABASE_STORAGE_BUCKET") ?? "lms-uploads";

        _baseUrl = url.TrimEnd('/');
        _serviceKey = key.Trim();
        _bucket = string.IsNullOrWhiteSpace(bucket) ? "lms-uploads" : bucket.Trim();
    }

    private bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrl) && !string.IsNullOrWhiteSpace(_serviceKey);

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        if (_bucketChecked || !IsConfigured) return;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/storage/v1/bucket");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
            req.Headers.Add("apikey", _serviceKey);

            var body = JsonSerializer.Serialize(new { id = _bucket, name = _bucket, @public = false });
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var res = await _httpClient.SendAsync(req, cancellationToken);
            _bucketChecked = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible verificar la existencia del bucket en almacenamiento en la nube.");
        }
    }

    public async Task<StoredFile> SaveFileAsync(
        IFormFile file,
        string scope,
        int ownerUserId,
        string ownerRole,
        CancellationToken cancellationToken = default)
    {
        // Si no está configurada la nube, delegar al almacenamiento local
        if (!IsConfigured)
        {
            return await _localFallback.SaveFileAsync(file, scope, ownerUserId, ownerRole, cancellationToken);
        }

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
            await using var signatureStream = file.OpenReadStream();
            if (!await FileSignatureValidator.HasValidSignatureAsync(signatureStream, extension, cancellationToken))
            {
                return Fail("El contenido binario (Magic Bytes) del archivo no corresponde a un formato permitido.");
            }
        }
        catch (Exception)
        {
            return Fail("No fue posible leer el archivo. Intenta de nuevo.");
        }

        var storageKey = $"{normalizedScope}/{Guid.NewGuid():N}{extension}";
        var originalName = FileSignatureValidator.SanitizeOriginalFileName(file.FileName);
        var contentType = FileSignatureValidator.GetContentType(extension);

        try
        {
            await EnsureBucketExistsAsync(cancellationToken);

            string sha256;
            byte[] fileBytes;

            await using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, cancellationToken);
                fileBytes = ms.ToArray();
                using var hash = SHA256.Create();
                sha256 = Convert.ToHexString(hash.ComputeHash(fileBytes)).ToLowerInvariant();
            }

            // 1. Subir archivo a Supabase Storage
            using (var uploadReq = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/storage/v1/object/{_bucket}/{storageKey}"))
            {
                uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
                uploadReq.Headers.Add("apikey", _serviceKey);
                uploadReq.Headers.Add("x-upsert", "true");
                uploadReq.Content = new ByteArrayContent(fileBytes);
                uploadReq.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                var uploadRes = await _httpClient.SendAsync(uploadReq, cancellationToken);
                if (!uploadRes.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Fallo al subir a almacenamiento en la nube ({StatusCode}). Usando fallback local.", uploadRes.StatusCode);
                    return await _localFallback.SaveFileAsync(file, scope, ownerUserId, ownerRole, cancellationToken);
                }
            }

            // 2. Subir metadatos JSON
            var metadata = new StoredFileMetadata
            {
                Scope = normalizedScope,
                OwnerUserId = ownerUserId,
                OwnerRole = ownerRole,
                OriginalName = originalName,
                ContentType = contentType,
                Sha256 = sha256,
                Size = file.Length,
                UploadedAt = DateTime.UtcNow
            };

            var metaJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            using (var metaReq = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/storage/v1/object/{_bucket}/{storageKey}.meta.json"))
            {
                metaReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
                metaReq.Headers.Add("apikey", _serviceKey);
                metaReq.Headers.Add("x-upsert", "true");
                metaReq.Content = new StringContent(metaJson, Encoding.UTF8, "application/json");

                await _httpClient.SendAsync(metaReq, cancellationToken);
            }

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción durante la subida a la nube. Guardando en almacenamiento local.");
            return await _localFallback.SaveFileAsync(file, scope, ownerUserId, ownerRole, cancellationToken);
        }
    }

    public async Task<StoredFileMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(storageKey);

        if (IsConfigured)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/storage/v1/object/{_bucket}/{key}.meta.json");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
                req.Headers.Add("apikey", _serviceKey);

                var res = await _httpClient.SendAsync(req, cancellationToken);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync(cancellationToken);
                    return JsonSerializer.Deserialize<StoredFileMetadata>(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al recuperar metadatos desde almacenamiento en la nube.");
            }
        }

        return await _localFallback.GetMetadataAsync(storageKey, cancellationToken);
    }

    public async Task<Stream?> OpenReadStreamAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(storageKey);

        // Primero verificar si existe en la nube
        if (IsConfigured)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/storage/v1/object/{_bucket}/{key}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
                req.Headers.Add("apikey", _serviceKey);

                var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (res.IsSuccessStatusCode)
                {
                    return await res.Content.ReadAsStreamAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al descargar objeto de almacenamiento en la nube.");
            }
        }

        // Fallback a archivo local
        return await _localFallback.OpenReadStreamAsync(storageKey, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string? urlOrStorageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(urlOrStorageKey)) return false;

        var key = NormalizeKey(urlOrStorageKey);

        if (IsConfigured)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Delete, $"{_baseUrl}/storage/v1/object/{_bucket}/{key}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
                req.Headers.Add("apikey", _serviceKey);
                await _httpClient.SendAsync(req, cancellationToken);

                using var reqMeta = new HttpRequestMessage(HttpMethod.Delete, $"{_baseUrl}/storage/v1/object/{_bucket}/{key}.meta.json");
                reqMeta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
                reqMeta.Headers.Add("apikey", _serviceKey);
                await _httpClient.SendAsync(reqMeta, cancellationToken);
            }
            catch
            {
                // Limpieza silenciosa
            }
        }

        return await _localFallback.DeleteAsync(urlOrStorageKey, cancellationToken);
    }

    public string? ResolvePhysicalPath(string storageKey) => _localFallback.ResolvePhysicalPath(storageKey);

    public string? ResolveLegacyPhysicalPath(string legacyPath) => _localFallback.ResolveLegacyPhysicalPath(legacyPath);

    public string GetUrl(string storageKey) => $"/api/files/{NormalizeKey(storageKey)}";

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var key = value.Trim().Replace('\\', '/');
        if (key.StartsWith("/api/files/", StringComparison.OrdinalIgnoreCase))
            key = key["/api/files/".Length..];
        else if (key.StartsWith("api/files/", StringComparison.OrdinalIgnoreCase))
            key = key["api/files/".Length..];
        return key.TrimStart('/');
    }

    private static StoredFile Fail(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}
