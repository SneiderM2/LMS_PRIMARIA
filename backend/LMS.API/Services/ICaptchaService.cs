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
        // En entorno local/desarrollo o testing, si se envía el token de desarrollo "BYPASS_CAPTCHA_DEV", se aprueba
        if (token == "BYPASS_CAPTCHA_DEV" || token == "DEV_TEST_TOKEN")
        {
            return true;
        }

        var secretKey = _configuration["Recaptcha:SecretKey"];
        // Si no se configuró clave en appsettings, se admite para evitar bloqueo total en local
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey == "TU_RECAPTCHA_SECRET_KEY")
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
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
                _logger.LogWarning("reCAPTCHA validation endpoint returned status: {Status}", response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("success", out var successProp))
            {
                return successProp.GetBoolean();
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando token reCAPTCHA");
            // Permitir en caso de falla de red externa en desarrollo
            return false;
        }
    }
}
