using LMS.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LMS.API.Tests;

public class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "LMS.API";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}

public class GoogleRecaptchaServiceTests
{
    private static GoogleRecaptchaService CreateService(
        string environmentName,
        Dictionary<string, string?> configValues,
        HttpMessageHandler? handler = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var env = new TestHostEnvironment { EnvironmentName = environmentName };
        var client = handler != null ? new HttpClient(handler) : new HttpClient();
        var logger = NullLogger<GoogleRecaptchaService>.Instance;

        return new GoogleRecaptchaService(client, config, env, logger);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejects_empty_or_whitespace_token(string? token)
    {
        var service = CreateService(Environments.Development, new());
        var result = await service.VerifyTokenAsync(token, "127.0.0.1");
        Assert.False(result);
    }

    [Theory]
    [InlineData("DEV_TEST_TOKEN")]
    [InlineData("BYPASS_CAPTCHA_DEV")]
    public async Task Allows_dev_bypass_tokens_in_development(string token)
    {
        var service = CreateService(Environments.Development, new()
        {
            ["Recaptcha:AllowDevBypass"] = "true"
        });

        var result = await service.VerifyTokenAsync(token, "127.0.0.1");
        Assert.True(result);
    }

    [Fact]
    public async Task Allows_dev_bypass_when_no_secret_key_in_development()
    {
        var service = CreateService(Environments.Development, new()
        {
            ["Recaptcha:AllowDevBypass"] = "true",
            ["Recaptcha:SecretKey"] = ""
        });

        var result = await service.VerifyTokenAsync("some-token", "127.0.0.1");
        Assert.True(result);
    }

    [Fact]
    public async Task Rejects_token_in_production_when_secret_key_missing_from_server()
    {
        var service = CreateService(Environments.Production, new()
        {
            ["Recaptcha:SecretKey"] = ""
        });

        // Fail-closed: sin secreto el servidor no puede validar y no debe permitir el acceso.
        var result = await service.VerifyTokenAsync("client-submitted-recaptcha-token", "203.0.113.1");
        Assert.False(result);
    }

    [Fact]
    public async Task Rejects_token_in_development_when_bypass_is_not_explicitly_enabled()
    {
        var service = CreateService(Environments.Development, new()
        {
            ["Recaptcha:SecretKey"] = ""
        });

        var result = await service.VerifyTokenAsync("some-token", "127.0.0.1");
        Assert.False(result);
    }
}
