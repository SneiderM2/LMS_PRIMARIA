using System.Security.Claims;
using LMS.API.Configuration;
using LMS.API.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LMS.API.Filters;

/// <summary>
/// Filtro de acción que garantiza que ningún perfil de menor de edad (Student/ALUMNO)
/// pueda operar en la plataforma si no cuenta con un registro activo y vigente en
/// <see cref="Entities.ConsentimientoHabeasData"/> firmado por su representante legal
/// (Ley 1581 de 2012 Art. 7, Decreto 1377 de 2013 Art. 12 y Ley 1098 de 2006).
/// </summary>
public class MinorConsentActionFilter : IAsyncActionFilter
{
    private readonly LMSDbContext _dbContext;
    private readonly PrivacyOptions _privacyOptions;

    public MinorConsentActionFilter(LMSDbContext dbContext, IOptions<PrivacyOptions> privacyOptions)
    {
        _dbContext = dbContext;
        _privacyOptions = privacyOptions.Value;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userPrincipal = context.HttpContext.User;
        if (userPrincipal?.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var roleClaim = userPrincipal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var isStudent = roleClaim.Equals("Student", StringComparison.OrdinalIgnoreCase)
                        || roleClaim.Equals("ALUMNO", StringComparison.OrdinalIgnoreCase);

        if (!isStudent)
        {
            await next();
            return;
        }

        var username = userPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(username))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                code = "SESSION_INVALID",
                message = "Sesión de estudiante inválida."
            });
            return;
        }

        var studentUser = await _dbContext.Usuarios
            .AsNoTracking()
            .Where(u => u.Username.ToLower() == username.ToLower())
            .Select(u => new
            {
                u.Id,
                u.DataPolicyAccepted,
                u.DataPolicyVersion
            })
            .FirstOrDefaultAsync();

        if (studentUser == null)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                code = "STUDENT_NOT_FOUND",
                message = "Estudiante no encontrado."
            });
            return;
        }

        var hasActiveHabeasData = await _dbContext.ConsentimientosHabeasData
            .AsNoTracking()
            .AnyAsync(c => c.AlumnoId == studentUser.Id
                           && c.AutorizaServicioEducativo
                           && c.FechaRevocatoriaUtc == null
                           && c.VersionPolitica == _privacyOptions.PolicyVersion);

        if (!hasActiveHabeasData)
        {
            context.Result = new ObjectResult(new
            {
                status = StatusCodes.Status403Forbidden,
                code = "MINOR_CONSENT_REQUIRED",
                message = "El estudiante menor de edad no cuenta con autorización vigente de Habeas Data firmada por su representante legal (Ley 1581 de 2012 Art. 7 y Ley 1098 de 2006).",
                currentVersion = _privacyOptions.PolicyVersion
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
