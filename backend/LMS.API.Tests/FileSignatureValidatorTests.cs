using System.Text;
using LMS.API.Services;
using Xunit;

namespace LMS.API.Tests;

public class FileSignatureValidatorTests
{
    private static MemoryStream StreamFrom(string content)
        => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Accepts_real_pdf_signature()
    {
        await using var stream = StreamFrom("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF");

        Assert.True(await FileSignatureValidator.HasValidSignatureAsync(stream, ".pdf"));
    }

    [Fact]
    public async Task Accepts_png_signature()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        await using var stream = new MemoryStream(bytes);

        Assert.True(await FileSignatureValidator.HasValidSignatureAsync(stream, ".png"));
    }

    [Fact]
    public async Task Rejects_text_renamed_as_pdf()
    {
        await using var stream = StreamFrom("esto no es un PDF");

        Assert.False(await FileSignatureValidator.HasValidSignatureAsync(stream, ".pdf"));
    }

    [Fact]
    public async Task Rejects_unknown_extension()
    {
        await using var stream = StreamFrom("%PDF-1.7");

        Assert.False(await FileSignatureValidator.HasValidSignatureAsync(stream, ".exe"));
        Assert.False(FileSignatureValidator.IsAllowedExtension(".html"));
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\temp\\tarea.pdf", "tarea.pdf")]
    [InlineData("   ", "archivo")]
    [InlineData(null, "archivo")]
    public void Sanitizes_original_file_name(string? input, string expected)
    {
        Assert.Equal(expected, FileSignatureValidator.SanitizeOriginalFileName(input!));
    }

    [Fact]
    public void Returns_safe_content_type()
    {
        Assert.Equal("application/pdf", FileSignatureValidator.GetContentType(".pdf"));
        Assert.Equal("application/octet-stream", FileSignatureValidator.GetContentType(".exe"));
    }

    [Theory]
    [InlineData("application/pdf", ".pdf", true)]
    [InlineData("image/jpeg", ".jpg", true)]
    [InlineData("image/png", ".png", true)]
    [InlineData("image/webp", ".webp", true)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx", true)]
    [InlineData("application/msword", ".doc", true)]
    [InlineData("text/html", ".pdf", false)]
    [InlineData("image/png", ".pdf", false)]
    [InlineData("application/x-msdownload", ".exe", false)]
    [InlineData("", ".pdf", false)]
    public void Validates_declared_mime_type_against_extension(string mimeType, string extension, bool expected)
    {
        Assert.Equal(expected, FileSignatureValidator.IsAllowedMimeType(mimeType, extension));
    }
}
