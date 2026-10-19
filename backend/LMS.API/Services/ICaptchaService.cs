using Microsoft.Extensions.Hosting;

namespace LMS.API.Services;

public interface ICaptchaService
{
    Task<bool> VerifyTokenAsync(string? token, string? remoteIp);
}

public class GoogleRecaptchaService : ICaptchaService
{
    private const string DevBypassToken = "DEV_TEST_TOKEN";

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

    public async Task<bool> VerifyTokenAsync(string? token, string? remoteIp)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Intento de autenticación sin token de reCAPTCHA.");
            return false;
        }

        // Bypass SOLO en desarrollo y habilitado explícitamente por configuración.
        if (_environment.IsDevelopment()
            && _configuration.GetValue<bool>("Recaptcha:AllowDevBypass")
            && token == DevBypassToken)
        {
            _logger.LogWarning("Bypass de reCAPTCHA aplicado en entorno de desarrollo.");
            return true;
        }

        var secretKey = _configuration["Recaptcha:SecretKey"]
                        ?? Environment.GetEnvironmentVariable("RECAPTCHA_SECRET_KEY");

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogError("Recaptcha:SecretKey no está configurada. Se rechaza la verificación (fail-closed).");
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
                return false;
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
                if (!string.Equals(hostname, expectedHostname, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("reCAPTCHA resuelto para un dominio no autorizado: {Hostname}", hostname);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción durante la verificación del token de reCAPTCHA.");
            return false;
        }
    }
}
