using LMS.API.Configuration;
using LMS.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LMS.API.Services;

/// <summary>
/// Tarea programada en segundo plano que purga registros de auditoría más antiguos
/// que el plazo de retención legal (Ley 1581, Privacy:LogRetentionDays).
/// </summary>
public sealed class AuditLogRetentionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<PrivacyOptions> _privacyOptions;
    private readonly ILogger<AuditLogRetentionBackgroundService> _logger;

    public AuditLogRetentionBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<PrivacyOptions> privacyOptions,
        ILogger<AuditLogRetentionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _privacyOptions = privacyOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Espera inicial de 1 minuto tras el arranque
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var days = _privacyOptions.Value.LogRetentionDays;
                if (days <= 0) days = 90;

                var cutoff = DateTime.UtcNow.AddDays(-days);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<LMSDbContext>();

                var deletedCount = await context.AuditLogs
                    .Where(a => a.Timestamp < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Purga de auditoría Ley 1581: se eliminaron {Count} registros anteriores a {Cutoff}.", deletedCount, cutoff);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al ejecutar purga periódica de registros de auditoría.");
            }

            // Ejecutar cada 24 horas
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
