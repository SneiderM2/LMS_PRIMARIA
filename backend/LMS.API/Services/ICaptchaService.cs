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

    public async Task<bool> VerifyTokenAsync(string? token, string? remoteIp)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Intento de autenticación sin token de reCAPTCHA.");
            return false;
        }

        // 1. Bypass en entorno de desarrollo o token de prueba interno
        if (_environment.IsDevelopment())
        {
            if (token == DevBypassToken || token == AltDevBypassToken)
            {
                _logger.LogInformation("Bypass de reCAPTCHA aplicado en entorno de desarrollo con token de prueba.");
                return true;
            }

            if (_configuration.GetValue<bool>("Recaptcha:AllowDevBypass", true))
            {
                var devSecret = FirstNonEmpty(
                    _configuration["Recaptcha:SecretKey"],
                    _configuration["Recaptcha__SecretKey"],
                    Environment.GetEnvironmentVariable("Recaptcha__SecretKey"),
                    Environment.GetEnvironmentVariable("RECAPTCHA_SECRET_KEY"));

                // Si no hay clave configurada en desarrollo, permitir para no bloquear el trabajo local
                if (string.IsNullOrWhiteSpace(devSecret))
                {
                    _logger.LogWarning("Bypass de reCAPTCHA automático en desarrollo (sin clave secreta configurada).");
                    return true;
                }
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
            // Modo tolerante cuando el servidor no tiene configurada la clave en Render,
            // pero el cliente envió un token no vacío (evita caída general del sistema escolar).
            _logger.LogWarning("Recaptcha:SecretKey no está configurada en el servidor. " +
                               "Para validación completa en producción defina Recaptcha__SecretKey. " +
                               "Permitiendo acceso porque el cliente proporcionó token.");
            return true;
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
                return _environment.IsDevelopment();
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

            // Validación opcional de dominio esperado (protege si la site key se reutiliza).
            var expectedHostname = _configuration["Recaptcha:ExpectedHostname"];
            if (!string.IsNullOrWhiteSpace(expectedHostname)
                && doc.RootElement.TryGetProperty("hostname", out var hostnameProp))
            {
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
            return _environment.IsDevelopment();
        }
    }
}
