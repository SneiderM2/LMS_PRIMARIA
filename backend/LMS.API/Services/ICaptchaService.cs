using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LMS.API.Services;

public interface ICaptchaService
{
    Task<bool> VerifyTokenAsync(string? token, string? remoteIp);
}

public class GoogleRecaptchaService : ICaptchaService
{
    private const string DevBypassToken = "DEV_TEST_TOKEN";
    private const string AltDevBypassToken = "BYPASS_CAPTCHA_DEV";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GoogleRecaptchaService> _logger;

    public GoogleRecaptchaService(
        HttpClient httpClient,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<GoogleRecaptchaService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>
    /// El bypass solo existe en Development y debe habilitarse explícitamente.
    /// En Production, Staging o cualquier otro entorno nunca se permite.
    /// </summary>
    private bool IsDevBypassEnabled()
        => _environment.IsDevelopment()
           && _configuration.GetValue<bool>("Recaptcha:AllowDevBypass");

    public async Task<bool> VerifyTokenAsync(string? token, string? remoteIp)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Intento de autenticación sin token de reCAPTCHA.");
            return false;
        }

        // 1. Bypass controlado exclusivamente en desarrollo.
        if (IsDevBypassEnabled())
        {
            if (token == DevBypassToken || token == AltDevBypassToken)
            {
                _logger.LogInformation("Bypass de reCAPTCHA aplicado en entorno de desarrollo con token de prueba.");
                return true;
            }

            var devSecret = FirstNonEmpty(
                _configuration["Recaptcha:SecretKey"],
                _configuration["Recaptcha__SecretKey"],
                Environment.GetEnvironmentVariable("Recaptcha__SecretKey"),
                Environment.GetEnvironmentVariable("RECAPTCHA_SECRET_KEY"));

            if (string.IsNullOrWhiteSpace(devSecret))
            {
                _logger.LogWarning("Bypass de reCAPTCHA explícito en desarrollo (sin clave secreta configurada).");
                return true;
            }
        }

        // 2. Obtener clave secreta con resolución robusta (variables de entorno o configuración)
        var secretKey = FirstNonEmpty(
            Environment.GetEnvironmentVariable("RECAPTCHA_SECRET_KEY"),
            Environment.GetEnvironmentVariable("Recaptcha__SecretKey"),
            _configuration["Recaptcha:SecretKey"],
            _configuration["Recaptcha__SecretKey"]);

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            // Fail-closed: nunca se permite el acceso si el servidor no puede validar el token.
            _logger.LogError("Recaptcha:SecretKey no está configurada. Se rechaza la autenticación para evitar un bypass.");
            return false;
        }

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "secret", secretKey },
                { "response", token },
                { "remoteip", remoteIp ?? string.Empty }
            });

            using var response = await _httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Endpoint de reCAPTCHA devolvió HTTP {Status}.", response.StatusCode);
                return IsDevBypassEnabled();
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            {
                if (doc.RootElement.TryGetProperty("error-codes", out var errorCodes))
                {
                    _logger.LogWarning("Validación de reCAPTCHA denegada. Códigos: {Errors}", errorCodes.ToString());
                }

                return false;
            }

            // Validación del dominio esperado (protege si la site key se reutiliza en otro sitio).
            var expectedHostname = _configuration["Recaptcha:ExpectedHostname"];
            if (!string.IsNullOrWhiteSpace(expectedHostname))
            {
                if (!doc.RootElement.TryGetProperty("hostname", out var hostnameProp))
                {
                    _logger.LogWarning("reCAPTCHA no devolvió hostname; se rechaza porque ExpectedHostname está configurado.");
                    return false;
                }

                var hostname = hostnameProp.GetString();
                var isTestDomain = _environment.IsDevelopment()
                    || string.Equals(hostname, "testkey.google.com", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(hostname, "localhost", StringComparison.OrdinalIgnoreCase);

                if (!isTestDomain && !string.Equals(hostname, expectedHostname, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("reCAPTCHA resuelto para un dominio no autorizado: {Hostname} (esperado: {Expected})", hostname, expectedHostname);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción durante la verificación del token de reCAPTCHA.");
            return IsDevBypassEnabled();
        }
    }
}
