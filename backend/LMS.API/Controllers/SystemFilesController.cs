using System.Security.Claims;
using LMS.API.DTOs;
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

    /// <summary>
    /// Lee y parsea de forma segura el archivo de log especificado en una lista de objetos estructurados.
    /// Sanitiza rutas absolutas de servidor y extrae fecha, nivel (INFO/WARNING/ERROR), usuario y mensaje.
    /// GET: /api/systemfiles/logs/parsed?fileName=access.log&level=ALL&user=&date=
    /// </summary>
    [HttpGet("logs/parsed")]
    [ProducesResponseType(typeof(List<ParsedLogEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetParsedLogs(
        [FromQuery] string fileName = "access.log",
        [FromQuery] string? level = null,
        [FromQuery] string? user = null,
        [FromQuery] string? date = null)
    {
        if (!IsAdminUser())
        {
            return Forbid();
        }

        var safeFileName = Path.GetFileName(fileName);
        if (safeFileName != fileName || safeFileName.Contains(".."))
        {
            return BadRequest(new { message = "Nombre de archivo no permitido." });
        }

        var logsDir = GetLogsDirectory();
        var fullPath = Path.GetFullPath(Path.Combine(logsDir, safeFileName));
        var fullDir = Path.GetFullPath(logsDir);

        if (!fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Ruta no autorizada." });
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { message = $"El archivo de log '{safeFileName}' no existe." });
        }

        var entries = new List<ParsedLogEntryDto>();

        using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(fs))
        {
            string? line;
            int counter = 0;
            ParsedLogEntryDto? currentEntry = null;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Si la línea comienza con espacios / tabuladores (como StackTrace: ...), anexar al detalle de la entrada anterior
                if (line.StartsWith(" ") || line.StartsWith("\t") || line.StartsWith("StackTrace:"))
                {
                    if (currentEntry != null)
                    {
                        currentEntry.Details = (currentEntry.Details == null)
                            ? SanitizePath(line.Trim())
                            : currentEntry.Details + "\n" + SanitizePath(line.Trim());
                    }
                    continue;
                }

                // Detectar si la línea comienza con marca de tiempo [YYYY-MM-DD HH:mm:ss]
                if (line.StartsWith("[") && line.Length >= 21 && line[20] == ']')
                {
                    var timestamp = line.Substring(1, 19);
                    var remainder = line.Substring(22).Trim();

                    var entry = ParseLogLine(timestamp, remainder, ++counter);

                    // Filtrado
                    if (!string.IsNullOrWhiteSpace(level) && !level.Equals("ALL", StringComparison.OrdinalIgnoreCase) && !entry.Level.Equals(level, StringComparison.OrdinalIgnoreCase))
                    {
                        currentEntry = null;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(user) && !entry.User.Contains(user, StringComparison.OrdinalIgnoreCase))
                    {
                        currentEntry = null;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(date) && !entry.Timestamp.StartsWith(date, StringComparison.OrdinalIgnoreCase))
                    {
                        currentEntry = null;
                        continue;
                    }

                    entries.Add(entry);
                    currentEntry = entry;
                }
                else
                {
                    // Registro simple sin formato estricto
                    if (currentEntry != null)
                    {
                        currentEntry.Details = (currentEntry.Details == null) ? SanitizePath(line) : currentEntry.Details + "\n" + SanitizePath(line);
                    }
                }
            }
        }

        entries.Reverse(); // Ordenar del más reciente al más antiguo
        return Ok(entries);
    }

    private static ParsedLogEntryDto ParseLogLine(string timestamp, string remainder, int index)
    {
        var entry = new ParsedLogEntryDto
        {
            Id = $"log-{index}-{DateTime.UtcNow.Ticks % 100000}",
            Timestamp = timestamp,
            Level = "INFO",
            User = "-"
        };

        // Caso 1: [EXCEPTION] IP | METHOD PATH | ELAPSEDms | Exception: message
        if (remainder.StartsWith("[EXCEPTION]"))
        {
            entry.Level = "ERROR";
            var parts = remainder.Substring(11).Trim().Split('|');
            if (parts.Length > 0) entry.ClientIp = parts[0].Trim();
            if (parts.Length > 1) ParseMethodPath(parts[1].Trim(), entry);
            if (parts.Length > 2 && parts[2].Contains("ms"))
            {
                var msStr = parts[2].Replace("ms", "").Trim();
                if (long.TryParse(msStr, out var ms)) entry.ElapsedMs = ms;
            }
            if (parts.Length > 3)
            {
                entry.Message = SanitizePath(string.Join(" | ", parts.Skip(3)).Trim());
            }
            else
            {
                entry.Message = "Excepción no controlada en el servidor.";
            }
            return entry;
        }

        // Caso 2: [HTTP-4xx / 5xx] IP | METHOD PATH | ELAPSEDms
        if (remainder.StartsWith("[HTTP-"))
        {
            var endBracket = remainder.IndexOf(']');
            if (endBracket > 6)
            {
                var codeStr = remainder.Substring(6, endBracket - 6);
                if (int.TryParse(codeStr, out var status))
                {
                    entry.StatusCode = status;
                    entry.Level = status >= 500 ? "ERROR" : "WARNING";
                }
            }
            var subParts = remainder.Substring(endBracket + 1).Trim().Split('|');
            if (subParts.Length > 0) entry.ClientIp = subParts[0].Trim();
            if (subParts.Length > 1) ParseMethodPath(subParts[1].Trim(), entry);
            entry.Message = $"Respuesta HTTP con código de error {entry.StatusCode}";
            return entry;
        }

        // Caso 3: [INFO] / [WARNING] / [ERROR] mensaje simple
        if (remainder.StartsWith("[") && remainder.Contains("]"))
        {
            var endBracket = remainder.IndexOf(']');
            var tag = remainder.Substring(1, endBracket - 1).ToUpperInvariant();
            if (tag == "ERROR" || tag == "WARNING" || tag == "INFO")
            {
                entry.Level = tag;
                entry.Message = SanitizePath(remainder.Substring(endBracket + 1).Trim());
                return entry;
            }
        }

        // Caso 4: Formato AccessLog: {ip} | {method} {path} | {status} | {elapsedMs}ms | User:{userId} | {userAgent}
        var accessTokens = remainder.Split('|');
        if (accessTokens.Length >= 4)
        {
            entry.ClientIp = accessTokens[0].Trim();
            ParseMethodPath(accessTokens[1].Trim(), entry);

            if (int.TryParse(accessTokens[2].Trim(), out var code))
            {
                entry.StatusCode = code;
                if (code >= 500) entry.Level = "ERROR";
                else if (code >= 400) entry.Level = "WARNING";
                else entry.Level = "INFO";
            }

            if (accessTokens.Length > 3)
            {
                var msStr = accessTokens[3].Replace("ms", "").Trim();
                if (long.TryParse(msStr, out var ms)) entry.ElapsedMs = ms;
            }

            if (accessTokens.Length > 4 && accessTokens[4].Contains("User:"))
            {
                var userPart = accessTokens[4].Replace("User:", "").Trim();
                if (!string.IsNullOrWhiteSpace(userPart) && userPart != "-")
                {
                    entry.User = userPart;
                }
            }

            if (accessTokens.Length > 5)
            {
                entry.Details = SanitizePath(accessTokens[5].Trim());
            }

            entry.Message = $"{entry.HttpMethod} {entry.Path} (HTTP {entry.StatusCode})";
            return entry;
        }

        // Si no cumple patrón específico, dejar como mensaje general
        entry.Message = SanitizePath(remainder);
        return entry;
    }

    private static void ParseMethodPath(string text, ParsedLogEntryDto entry)
    {
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0) entry.HttpMethod = parts[0].Trim();
        if (parts.Length > 1) entry.Path = SanitizePath(parts[1].Trim());
    }

    private static string SanitizePath(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sanitized = System.Text.RegularExpressions.Regex.Replace(input, @"[A-Za-z]:\\[^:\s\r\n]+", "[SERVER_INTERNAL_PATH]");
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"/(?:var|home|usr|etc)/[^\s\r\n]+", "[SERVER_INTERNAL_PATH]");
        return sanitized;
    }
}

