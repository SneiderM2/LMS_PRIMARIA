using LMS.API.Services;
using Xunit;

namespace LMS.API.Tests;

public class PasswordPolicyTests
{
    [Fact]
    public void Accepts_password_with_length_letters_and_digits()
    {
        var result = PasswordPolicy.Validate("ColegioSeguro2026x", "sneider", "Sneider Martinez");

        Assert.True(result.IsValid, result.Message);
    }

    [Theory]
    [InlineData("Corta1")]
    [InlineData("sololetraslargas")]
    [InlineData("123456789012345")]
    [InlineData("Colegio Seguro 2026")]
    public void Rejects_weak_passwords(string password)
    {
        var result = PasswordPolicy.Validate(password, "sneider", "Sneider Martinez");

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void Rejects_common_password()
    {
        var result = PasswordPolicy.Validate("Password123", "sneider", "Sneider Martinez");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_password_containing_identity()
    {
        var result = PasswordPolicy.Validate("Sneider2026Seguro", "sneider", "Sneider Martinez");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Generates_temporary_password_with_expected_length()
    {
        var password = PasswordPolicy.GenerateTemporaryPassword();

        Assert.Equal(16, password.Length);
        Assert.Contains(password, c => char.IsLetter(c));
        Assert.Contains(password, c => char.IsDigit(c));
    }
}
