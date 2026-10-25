using LMS.API.Configuration;
using LMS.API.Data;
using LMS.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LMS.API.Services;

/// <summary>
/// Aplica la retención por inactividad declarada en la política de privacidad.
/// Por seguridad permanece deshabilitado y en modo simulación hasta que el responsable
/// del tratamiento lo autorice expresamente.
/// </summary>
public sealed class InactivityAnonymizationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<PrivacyOptions> _privacyOptions;
    private readonly ILogger<InactivityAnonymizationBackgroundService> _logger;

    public InactivityAnonymizationBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<PrivacyOptions> privacyOptions,
        ILogger<InactivityAnonymizationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _privacyOptions = privacyOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en la anonimización por inactividad.");
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado normal del host.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var options = _privacyOptions.Value;
        if (!options.EnableInactivityAnonymization)
        {
            _logger.LogDebug("Anonimización por inactividad deshabilitada (Privacy:EnableInactivityAnonymization=false).");
            return;
        }

        var months = options.InactivityAnonymizationMonths;
        if (months <= 0) months = 36;

        var cutoff = DateTime.UtcNow.AddMonths(-months);
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LMSDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        var candidates = await context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.Alumno)
            .Where(u => u.Activo
                        && u.AnonymizedAt == null
                        && u.Rol.Nombre != "ADMINISTRADOR"
                        && u.Rol.Nombre != ".admin"
                        && ((u.LastLoginAt != null && u.LastLoginAt < cutoff)
                            || (u.LastLoginAt == null && u.FechaCreacion < cutoff)))
            .OrderBy(u => u.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            _logger.LogInformation("Anonimización por inactividad: no hay cuentas candidatas antes de {Cutoff}.", cutoff);
            return;
        }

        _logger.LogWarning("Anonimización por inactividad: {Count} cuentas candidatas antes de {Cutoff}. Modo simulación: {DryRun}.",
            candidates.Count, cutoff, options.InactivityAnonymizationDryRun);

        if (options.InactivityAnonymizationDryRun)
        {
            foreach (var user in candidates)
            {
                _logger.LogInformation("Simulación: se anonimizaría la cuenta interna {UserId} (último acceso: {LastLogin}).",
                    user.Id, user.LastLoginAt);
            }
            return;
        }

        foreach (var user in candidates)
        {
            await AnonymizeAsync(context, storage, user, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task AnonymizeAsync(
        LMSDbContext context,
        IFileStorageService storage,
        Usuario user,
        CancellationToken cancellationToken)
    {
        var alumnoId = user.Alumno?.UsuarioId;
        var archivos = new List<ArchivoEntrega>();
        var entregas = new List<Entrega>();

        if (alumnoId.HasValue)
        {
            archivos = await context.ArchivosEntrega
                .Include(a => a.Entrega)
                .Where(a => a.Entrega.AlumnoId == alumnoId.Value)
                .ToListAsync(cancellationToken);

            entregas = await context.Entregas
                .Where(e => e.AlumnoId == alumnoId.Value)
                .ToListAsync(cancellationToken);
        }

        foreach (var archivo in archivos)
        {
            await storage.DeleteAsync(archivo.RutaArchivo, cancellationToken);
        }

        context.ArchivosEntrega.RemoveRange(archivos);

        foreach (var entrega in entregas)
        {
            entrega.ContenidoTexto = null;
        }

        user.Username = $"suprimido-{user.Id}-{Guid.NewGuid():N}";
        user.Nombre = "Titular";
        user.Apellido = "Suprimido";
        user.AvatarUrl = null;
        user.Activo = false;
        user.SessionToken = null;
        user.LockoutEnd = null;
        user.LastLoginAt = null;
        user.DataPolicyAccepted = false;
        user.DataPolicyAcceptedAt = null;
        user.DataPolicyVersion = null;
        user.DataPolicyEvidence = null;
        user.AccessFailedCount = 0;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), workFactor: 12);
        user.DeletedAt = DateTime.UtcNow;
        user.AnonymizedAt = DateTime.UtcNow;
    }
}
