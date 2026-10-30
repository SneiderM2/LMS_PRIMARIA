using LMS.API.Data;
using LMS.API.Entities;
using LMS.API.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LMS.API.Services;

/// <summary>
/// Procesa y almacena en base de datos los eventos de auditoría en segundo plano
/// para garantizar persistencia y cumplimiento de Ley 1581 / Ley 1098.
/// </summary>
public sealed class AuditLogDbBackgroundService : BackgroundService
{
    private readonly AuditLogQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditLogDbBackgroundService> _logger;

    public AuditLogDbBackgroundService(
        AuditLogQueue queue,
        IServiceProvider serviceProvider,
        ILogger<AuditLogDbBackgroundService> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AuditLogEntry>();
        var lastFlush = DateTime.UtcNow;

        try
        {
            await foreach (var entry in _queue.ReadAllAsync(stoppingToken))
            {
                batch.Add(entry);

                if (batch.Count >= 20 || (DateTime.UtcNow - lastFlush).TotalSeconds >= 2)
                {
                    await FlushBatchAsync(batch, stoppingToken);
                    batch.Clear();
                    lastFlush = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado normal del servidor
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el procesador de auditoría en segundo plano.");
        }
        finally
        {
            if (batch.Count > 0)
            {
                try
                {
                    await FlushBatchAsync(batch, CancellationToken.None);
                }
                catch
                {
                    // Ignorado al apagar
                }
            }
        }
    }

    private async Task FlushBatchAsync(List<AuditLogEntry> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LMSDbContext>();

            var entities = batch.Select(e =>
            {
                DateTime parsedTime = DateTime.UtcNow;
                if (DateTime.TryParse(e.Timestamp, out var dt))
                {
                    parsedTime = dt.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                        : dt.ToUniversalTime();
                }

                return new AuditLog
                {
                    Timestamp = parsedTime,
                    Level = e.Level ?? "INFO",
                    Method = e.Method ?? "GET",
                    Path = RequestLoggingMiddleware.SanitizeSensitiveText(e.Path) ?? "/",
                    StatusCode = e.StatusCode,
                    ElapsedMs = e.ElapsedMs,
                    Ip = e.Ip ?? "0.0.0.0",
                    UserId = e.UserId,
                    UserEmail = e.UserEmail,
                    UserRole = e.UserRole,
                    Action = RequestLoggingMiddleware.SanitizeSensitiveText(e.Action) ?? "Operación",
                    Details = RequestLoggingMiddleware.SanitizeSensitiveText(e.Details)
                };
            }).ToList();

            await context.AuditLogs.AddRangeAsync(entities, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible persistir lote de auditoría en la base de datos.");
        }
    }
}
