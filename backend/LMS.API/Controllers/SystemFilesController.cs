using System.Security.Claims;
using LMS.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Controlador protegido para inspección y auditoría de archivos de configuración (.htaccess)
/// y registros de actividad del sistema (*.log).
/// Acceso restringido exclusivamente a roles administrativos (.admin / Admin / ADMINISTRADOR).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAdmin")]
public class SystemFilesController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SystemFilesController> _logger;

    public SystemFilesController(IWebHostEnvironment env, ILogger<SystemFilesController> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Verifica adicionalmente que el usuario cuente con los claims de administrador.
    /// </summary>
    private bool IsAdminUser()
    {
        if (User?.Identity == null || !User.Identity.IsAuthenticated)
            return false;

        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".admin", "admin", "administrador"
        };

        return User.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type == "role") &&
            allowedRoles.Contains(c.Value.Trim()));
    }

    /// <summary>
    /// Resuelve la ruta canónica del archivo .htaccess.
    /// </summary>
    private string GetHtaccessPath()
    {
        // 1. En la raíz del proyecto backend
        var pathInRoot = Path.Combine(_env.ContentRootPath, ".htaccess");
        if (System.IO.File.Exists(pathInRoot)) return pathInRoot;

        // 2. En wwwroot si existe
        var pathInWwwroot = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), ".htaccess");
        if (System.IO.File.Exists(pathInWwwroot)) return pathInWwwroot;

        // 3. En la raíz del repositorio o base directory
        var pathInBase = Path.Combine(AppContext.BaseDirectory, ".htaccess");
        if (System.IO.File.Exists(pathInBase)) return pathInBase;

        return pathInRoot;
    }

    /// <summary>
    /// Resuelve el directorio de logs del servidor.
    /// </summary>
    private string GetLogsDirectory()
    {
        var configured = FileLogger.LogDirectory;
        if (Directory.Exists(configured)) return configured;

        var inContentRoot = Path.Combine(_env.ContentRootPath, "logs");
        if (Directory.Exists(inContentRoot)) return inContentRoot;

        var inBase = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(inBase);
        return inBase;
    }

    /// <summary>
    /// Lee e inspecciona el archivo .htaccess de configuración del servidor.
    /// GET: /api/systemfiles/htaccess
    /// </summary>
    [HttpGet("htaccess")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHtaccess()
    {
        if (!IsAdminUser())
        {
            _logger.LogWarning("Intento no autorizado de acceder a .htaccess por el usuario {User}", User.Identity?.Name);
            return Forbid();
        }

        var filePath = GetHtaccessPath();
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "El archivo .htaccess no se encuentra en el servidor." });
        }

        var fileInfo = new FileInfo(filePath);
        var content = await System.IO.File.ReadAllTextAsync(filePath);

        _logger.LogInformation("Auditoría: .htaccess consultado por administrador {User}", User.Identity?.Name);

        return Ok(new
        {
            fileName = ".htaccess",
            filePath = Path.GetFileName(filePath),
            size = fileInfo.Length,
            lastModified = fileInfo.LastWriteTimeUtc,
            content
        });
    }

    /// <summary>
    /// Lista todos los archivos de registros (.log) disponibles en el servidor.
    /// GET: /api/systemfiles/logs
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetLogFiles()
    {
        if (!IsAdminUser())
        {
            return Forbid();
        }

        var logsDir = GetLogsDirectory();
        if (!Directory.Exists(logsDir))
        {
            return Ok(new { directory = logsDir, files = Array.Empty<object>() });
        }

        var dirInfo = new DirectoryInfo(logsDir);
        var files = dirInfo.GetFiles("*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new
            {
                name = f.Name,
                size = f.Length,
                lastModified = f.LastWriteTimeUtc
            })
            .ToList();

        return Ok(new
        {
            directory = logsDir,
            files
        });
    }

    /// <summary>
    /// Obtiene el contenido de un archivo de log específico con protección estricta contra Path Traversal.
    /// GET: /api/systemfiles/logs/content?fileName=access.log
    /// </summary>
    [HttpGet("logs/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogContent([FromQuery] string fileName)
    {
        if (!IsAdminUser())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest(new { message = "El parámetro 'fileName' es requerido." });
        }

        // Sanitización contra Path Traversal
        var safeFileName = Path.GetFileName(fileName);
        if (safeFileName != fileName || safeFileName.Contains(".."))
        {
            _logger.LogWarning("Posible intento de Path Traversal detectado: {FileName}", fileName);
            return BadRequest(new { message = "Nombre de archivo no permitido." });
        }

        // Solo permitir extensiones .log o .txt
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (extension != ".log" && extension != ".txt")
        {
            return BadRequest(new { message = "Únicamente se permite la inspección de archivos .log" });
        }

        var logsDir = GetLogsDirectory();
        var fullPath = Path.GetFullPath(Path.Combine(logsDir, safeFileName));
        var fullDir = Path.GetFullPath(logsDir);

        // Asegurar que la ruta resultante está estrictamente dentro del directorio de logs
        if (!fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Acceso fuera del directorio de logs denegado: {FullPath}", fullPath);
            return BadRequest(new { message = "Ruta de archivo no autorizada." });
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { message = $"El archivo de log '{safeFileName}' no existe." });
        }

        var fileInfo = new FileInfo(fullPath);

        // Para evitar problemas de bloqueo concurrente con FileLogger, abrir con FileShare.ReadWrite
        string content;
        using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(fs))
        {
            content = await reader.ReadToEndAsync();
        }

        _logger.LogInformation("Auditoría: Log '{LogName}' inspeccionado por administrador {User}", safeFileName, User.Identity?.Name);

        return Ok(new
        {
            fileName = safeFileName,
            size = fileInfo.Length,
            lastModified = fileInfo.LastWriteTimeUtc,
            content
        });
    }

    /// <summary>
    /// Limpia/trunca un archivo de log específico.
    /// POST: /api/systemfiles/logs/clear?fileName=access.log
    /// </summary>
    [HttpPost("logs/clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearLog([FromQuery] string fileName)
    {
        if (!IsAdminUser())
        {
            return Forbid();
        }

        var safeFileName = Path.GetFileName(fileName);
        if (safeFileName != fileName || !safeFileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Nombre de archivo no válido." });
        }

        var logsDir = GetLogsDirectory();
        var fullPath = Path.Combine(logsDir, safeFileName);

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { message = "Archivo no encontrado." });
        }

        await System.IO.File.WriteAllTextAsync(fullPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Archivo de log reiniciado por administrador: {User.Identity?.Name}\n");

        _logger.LogWarning("Auditoría: Log '{LogName}' vaciado por administrador {User}", safeFileName, User.Identity?.Name);

        return Ok(new { message = $"El archivo '{safeFileName}' ha sido reiniciado.", fileName = safeFileName });
    }
}
