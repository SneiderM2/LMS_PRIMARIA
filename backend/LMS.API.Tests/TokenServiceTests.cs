using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LMS.API.Entities;
using LMS.API.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LMS.API.Tests;

public class TokenServiceTests
{
    private static TokenService CreateService(int expirationMinutes = 60)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = new string('x', 64),
                ["Jwt:Issuer"] = "LMS.API",
                ["Jwt:Audience"] = "LMS.Client",
                ["Jwt:ExpirationMinutes"] = expirationMinutes.ToString()
            })
            .Build();

        return new TokenService(configuration);
    }

    [Fact]
    public void Generates_token_with_canonical_role_and_session_claim()
    {
        var service = CreateService();
        var user = new Usuario
        {
            Id = 42,
            Username = "alumno1",
            Nombre = "Ana",
            Apellido = "Gomez",
            SessionToken = "session-abc"
        };

        var (token, expiration) = service.GenerateToken(user, "Ana Gomez", "ALUMNO", "3°", null);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Student");
        Assert.Contains(jwt.Claims, c => c.Type == "session_token" && c.Value == "session-abc");
        Assert.Contains(jwt.Claims, c => c.Type == "grade_level" && c.Value == "3°");
        Assert.True(expiration > DateTime.UtcNow.AddMinutes(55));
        Assert.True(expiration < DateTime.UtcNow.AddMinutes(65));
    }

    [Fact]
    public void Rejects_missing_or_short_secret()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "corta"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new TokenService(configuration));
    }

    [Theory]
    [InlineData("ADMINISTRADOR", "Admin")]
    [InlineData("DOCENTE", "Teacher")]
    [InlineData("ALUMNO", "Student")]
    [InlineData("Teacher", "Teacher")]
    public void Normalizes_historical_role_names(string input, string expected)
    {
        Assert.Equal(expected, TokenService.NormalizeRole(input));
    }
}
