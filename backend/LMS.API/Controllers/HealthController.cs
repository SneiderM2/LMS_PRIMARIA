using LMS.API.Data;
using LMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly LMSDbContext _context;
    private readonly IFileStorageService _storage;

    public HealthController(LMSDbContext context, IFileStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    /// <summary>
    /// Verificación de salud completa (Base de datos y Almacenamiento).
    /// GET: /api/health
    /// </summary>
    [HttpGet]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> CheckHealth(CancellationToken cancellationToken)
    {
        var dbOk = false;
        try
        {
            dbOk = await _context.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            dbOk = false;
        }

        var result = new
        {
            status = dbOk ? "Healthy" : "Degraded",
            database = dbOk ? "Healthy" : "Unhealthy",
            storage = _storage.ProviderName,
            timestamp = DateTime.UtcNow
        };

        if (!dbOk)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Verificación instantánea de vida (Liveness probe para Render / CI / keepalive).
    /// No toca la base de datos para no consumir recursos innecesarios.
    /// GET: /api/health/live
    /// </summary>
    [HttpGet("live")]
    [ResponseCache(NoStore = true)]
    public IActionResult CheckLive()
    {
        return Ok(new
        {
            status = "Alive",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Ping rápido para pre-calentamiento (warmup) del frontend tras inactividad.
    /// GET: /api/ping
    /// </summary>
    [HttpGet("/api/ping")]
    [ResponseCache(NoStore = true)]
    public IActionResult Ping()
    {
        return Ok(new
        {
            status = "Pong",
            timestamp = DateTime.UtcNow
        });
    }
}
