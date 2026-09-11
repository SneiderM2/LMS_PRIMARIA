using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace LMS.API.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly string[] _allowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".webp" };
    private const long MaxFileSize = 25 * 1024 * 1024; // 25 MB

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<(bool Success, string FileUrl, string OriginalFileName, string ErrorMessage)> SaveFileAsync(IFormFile file, string subFolder)
    {
        if (file == null || file.Length == 0)
        {
            return (false, string.Empty, string.Empty, "No se seleccionó ningún archivo.");
        }

        if (file.Length > MaxFileSize)
        {
            return (false, string.Empty, string.Empty, "El archivo supera el tamaño máximo permitido de 25 MB.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(ext))
        {
            return (false, string.Empty, string.Empty, $"Formato no permitido. Formatos aceptados: {string.Join(", ", _allowedExtensions)}");
        }

        // Ruta base: wwwroot/uploads/{subFolder}
        var uploadsRoot = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", subFolder);
        if (!Directory.Exists(uploadsRoot))
        {
            Directory.CreateDirectory(uploadsRoot);
        }

        var uniqueFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsRoot, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativeUrl = $"/uploads/{subFolder}/{uniqueFileName}";
        return (true, relativeUrl, file.FileName, string.Empty);
    }
}
