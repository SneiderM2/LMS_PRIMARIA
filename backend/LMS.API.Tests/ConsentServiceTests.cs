using LMS.API.Configuration;
using LMS.API.Entities;
using LMS.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace LMS.API.Tests;

public class ConsentServiceTests
{
    private static ConsentService CreateService(string policyVersion = "test-2026")
    {
        var options = Options.Create(new PrivacyOptions
        {
            PolicyVersion = policyVersion,
            EvidenceHmacKey = new string('k', 64)
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = new string('j', 64)
            })
            .Build();

        return new ConsentService(options, configuration);
    }

    private static Usuario CreateUser() => new()
    {
        Id = 7,
        Username = "estudiante1",
        Nombre = "Ana",
        Apellido = "Gomez",
        SessionToken = "abc"
    };

    [Fact]
    public void Builds_signed_evidence_with_version_and_masked_guardian_document()
    {
        var service = CreateService();
        var evidence = service.BuildEvidence(
            CreateUser(),
            "registration",
            "127.0.0.1",
            "UnitTest/1.0",
            "Maria Gomez",
            "1029384756");

        Assert.Contains("test-2026", evidence);
        Assert.Contains("HMAC-SHA256", evidence);
        Assert.Contains("*******756", evidence);
        Assert.Contains("registration", evidence);
    }

    [Fact]
    public void Requires_current_version_and_accepted_flag()
    {
        var service = CreateService();
        var user = CreateUser();

        Assert.False(service.IsCurrent(user));

        user.DataPolicyAccepted = true;
        user.DataPolicyVersion = "otra-version";
        Assert.False(service.IsCurrent(user));

        user.DataPolicyVersion = "test-2026";
        Assert.True(service.IsCurrent(user));
    }

    [Fact]
    public void Masks_documents_without_exposing_them()
    {
        Assert.Equal("***", ConsentService.MaskDocument("12"));
        Assert.Equal("*******756", ConsentService.MaskDocument("1029384756"));
        Assert.Equal(string.Empty, ConsentService.MaskDocument(null));
    }
}
