using LMS.API.Configuration;
using LMS.API.Entities;
using LMS.API.Services;
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

        return new ConsentService(options);
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

    [Fact]
    public void Validates_guardian_consent_and_verifies_hmac_signature()
    {
        Assert.False(ConsentService.ValidateGuardianConsent(false, "Maria Gomez", "1029384756").IsValid);
        Assert.False(ConsentService.ValidateGuardianConsent(true, "Ana", "1029384756").IsValid);
        Assert.True(ConsentService.ValidateGuardianConsent(true, "Maria Gomez", "1029384756").IsValid);

        var service = CreateService();
        var evidence = service.BuildEvidence(CreateUser(), "registration", "127.0.0.1", "UnitTest", "Maria Gomez", "1029384756");
        Assert.True(service.VerifyEvidenceSignature(evidence));
        Assert.False(service.VerifyEvidenceSignature(evidence.Replace("Maria Gomez", "Alterado")));
    }

    [Fact]
    public void SolicitudArco_CalculatesColombianBusinessDays_WithLeyEmilianiAndEasterHolidays()
    {
        // Lunes 2 de marzo de 2026:
        // En 2026, el 19 de marzo (San José, jueves) se traslada por Ley Emiliani al lunes 23 de marzo.
        var startMonday = new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc);

        var limiteConsulta = SolicitudArco.CalcularFechaLimiteLegalColombia(startMonday, "CONOCER");
        var limiteReclamo = SolicitudArco.CalcularFechaLimiteLegalColombia(startMonday, "SUPRIMIR");

        // 10 días hábiles desde lunes 2 de marzo -> lunes 16 de marzo de 2026
        Assert.Equal(new DateTime(2026, 3, 16).Date, limiteConsulta.Date);
        // 15 días hábiles desde lunes 2 de marzo -> martes 24 de marzo de 2026 (salta lunes festivo Emiliani 23 de marzo)
        Assert.Equal(new DateTime(2026, 3, 24).Date, limiteReclamo.Date);

        // Verificar Semana Santa dinámica en 2026 (Domingo de Pascua: 5 de abril de 2026; Jueves Santo: 2 abril, Viernes Santo: 3 abril)
        Assert.False(SolicitudArco.EsDiaHabilColombia(new DateTime(2026, 4, 2)));
        Assert.False(SolicitudArco.EsDiaHabilColombia(new DateTime(2026, 4, 3)));
    }

    [Fact]
    public void CreateHabeasDataRecord_PopulatesRequiredLegalFieldsAndMasksDocument()
    {
        var service = CreateService("2026-10-19");
        var record = service.CreateHabeasDataRecord(
            alumnoId: 50,
            usuarioId: 50,
            guardianFullName: "Carlos Perez",
            guardianDocument: "1020304050",
            tipoDocumento: "CC",
            autorizaServicioEducativo: true,
            autorizaDatosOpcionales: false,
            autorizaTransmisionInternacional: true,
            ip: "190.10.20.30",
            userAgent: "Mozilla/5.0");

        Assert.Equal(50, record.AlumnoId);
        Assert.Equal("CC", record.TipoDocumento);
        Assert.Equal("*******050", record.AcudienteDocumento);
        Assert.Equal("2026-10-19", record.VersionPolitica);
        Assert.Equal(64, record.HashTextoPolitica.Length);
        Assert.True(record.EstaVigente);
    }
}
