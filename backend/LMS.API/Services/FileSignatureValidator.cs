namespace LMS.API.Services;

/// <summary>
/// Valida la firma binaria real ("magic numbers") de los archivos subidos.
/// La extensión y el Content-Type enviados por el navegador NO son confiables y no
/// deben usarse como único control (OWASP File Upload Cheat Sheet).
/// </summary>
public static class FileSignatureValidator
{
    public const int MaxSignatureBytes = 12;

    private static readonly Dictionary<string, string[]> AllowedSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = new[] { "25504446" },                       // %PDF
        [".jpg"] = new[] { "FFD8FF" },                         // JPEG
        [".jpeg"] = new[] { "FFD8FF" },
        [".png"] = new[] { "89504E470D0A1A0A" },               // PNG
        [".webp"] = new[] { "52494646" },                      // RIFF....WEBP (se valida además el offset 8)
        [".docx"] = new[] { "504B0304", "504B0506", "504B0708" }, // DOCX = contenedor ZIP/OPC
        [".doc"] = new[] { "D0CF11E0A1B11AE1" }                // DOC OLE2 (legado)
    };

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".doc"] = "application/msword"
    };

    public static IReadOnlyCollection<string> AllowedExtensions => AllowedSignatures.Keys;

    public static bool IsAllowedExtension(string? extension)
        => !string.IsNullOrWhiteSpace(extension) && AllowedSignatures.ContainsKey(extension);

    public static string GetContentType(string extension)
        => ContentTypes.TryGetValue(extension, out var contentType) ? contentType : "application/octet-stream";

    /// <summary>
    /// Comprueba que los primeros bytes del archivo correspondan a la extensión declarada.
    /// El stream debe ser legible y posicionado al inicio; se restaura su posición.
    /// </summary>
    public static async Task<bool> HasValidSignatureAsync(Stream stream, string extension, CancellationToken cancellationToken = default)
    {
        if (stream == null || !stream.CanRead || string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        if (!AllowedSignatures.TryGetValue(extension, out var signatures))
        {
            return false;
        }

        var buffer = new byte[MaxSignatureBytes];
        var originalPosition = stream.CanSeek ? stream.Position : 0;

        try
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read < 4)
            {
                return false;
            }

            var hex = Convert.ToHexString(buffer, 0, read);

            if (!signatures.Any(signature => hex.StartsWith(signature, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Validación adicional para WEBP: "RIFF" + 4 bytes de tamaño + "WEBP".
            if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            {
                return read >= 12 && hex.Substring(8, 8).Equals("57454250", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }

    /// <summary>
    /// Limpia el nombre original para almacenarlo y mostrarlo sin rutas ni caracteres de control.
    /// </summary>
    public static string SanitizeOriginalFileName(string? fileName, string fallback = "archivo")
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return fallback;
        }

        var raw = fileName.Trim().Replace('\\', '/');
        var name = Path.GetFileName(raw);
        var cleaned = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        cleaned = cleaned.Replace('/', '_').Replace('\\', '_');

        if (cleaned.Length == 0)
        {
            return fallback;
        }

        return cleaned.Length <= 200 ? cleaned : cleaned[..200];
    }
}
