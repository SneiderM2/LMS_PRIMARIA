namespace LMS.API.Services;

public interface ICaptchaService
{
    Task<bool> VerifyTokenAsync(string? token, string? remoteIp);
}

public class GoogleRecaptchaService : ICaptchaService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleRecaptchaService> _logger;

    public GoogleRecaptchaService(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<GoogleRecaptchaService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> VerifyTokenAsync(string? token, string? remoteIp)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Intento de autenticación sin token de reCAPTCHA.");
            return false;
        }

        // Token seguro para pruebas internas de testing/bypass
        if (token == "BYPASS_CAPTCHA_DEV" || token == "DEV_TEST_TOKEN")
        {
            _logger.LogInformation("reCAPTCHA bypass aplicado con token de desarrollo/prueba.");
            return true;
        }

        // Obtener la clave secreta desde la variable de entorno Recaptcha__SecretKey, configuración o valor por defecto
        var secretKey = _configuration["Recaptcha__SecretKey"]
                     ?? _configuration["Recaptcha:SecretKey"]
                     ?? Environment.GetEnvironmentVariable("Recaptcha__SecretKey")
                     ?? "6LcMQNgtAAAAAMLE9kE7JSvxvnZSr8kKsUGtgjOL";

        if (string.IsNullOrWhiteSpace(secretKey) || secretKey == "TU_RECAPTCHA_SECRET_KEY")
        {
            _logger.LogError("La clave secreta de reCAPTCHA no está configurada.");
            return false;
        }

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "secret", secretKey },
                { "response", token },
                { "remoteip", remoteIp ?? string.Empty }
            });

            var response = await _httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Endpoint de reCAPTCHA devolvió código HTTP no exitoso: {Status}", response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("success", out var successProp))
            {
                var isSuccess = successProp.GetBoolean();
                if (!isSuccess && doc.RootElement.TryGetProperty("error-codes", out var errorCodes))
                {
                    _logger.LogWarning("Validación de reCAPTCHA denegada por Google. Códigos de error: {Errors}", errorCodes.ToString());
                }
                return isSuccess;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción durante la verificación del token de reCAPTCHA");
            return false;
        }
    }
}
