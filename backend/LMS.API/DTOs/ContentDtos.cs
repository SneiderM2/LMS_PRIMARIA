using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace LMS.API.DTOs;

public class ContentDto
{
    public string Id { get; set; } = string.Empty;
    public int RealId { get; set; }
    public int? CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Video", "Pdf", "Assignment"
    public string Subject { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? MaxScore { get; set; }
    public string GradeLevel { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool HasSubmitted { get; set; } // Flag para estudiante
    public StudentSubmissionDto? MySubmission { get; set; }
}

public class CreateContentDto
{
    [Required(ErrorMessage = "El título es obligatorio")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = "Assignment"; // "Video", "Pdf", "Assignment"

    [Required]
    [MaxLength(100)]
    public string Subject { get; set; } = "General";

    [MaxLength(500)]
    public string? FileUrl { get; set; }

    public DateTime? DueDate { get; set; }

    [Range(1, 100, ErrorMessage = "El puntaje máximo debe estar entre 1 y 100")]
    public decimal? MaxScore { get; set; }

    public int? CourseId { get; set; }

    [Required]
    [MaxLength(50)]
    public string GradeLevel { get; set; } = "2° Primaria";
}

public class StudentSubmissionDto
{
    public int SubmissionId { get; set; }
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? StudentUsername { get; set; }
    public string? StudentAvatarUrl { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? StudentComment { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? GradedAt { get; set; }
    public string? Feedback { get; set; }
    public decimal? Grade { get; set; }
    public decimal? MaxScore { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsLate { get; set; }
    public string Status { get; set; } = "ENVIADA";
}

/// <summary>
/// Codifica y decodifica de manera transparente el recurso guía adjunto de una tarea
/// dentro del texto de descripción sin requerir migración de base de datos.
/// </summary>
public static class TaskDescriptionMetadata
{
    private static readonly Regex ResourceTagRegex = new(
        @"\[RECURSO_ADJUNTO:([^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Pack(string? description, string? resourceUrl)
    {
        var (cleanDesc, _) = Unpack(description);
        if (string.IsNullOrWhiteSpace(resourceUrl))
        {
            return cleanDesc;
        }

        return string.IsNullOrWhiteSpace(cleanDesc)
            ? $"[RECURSO_ADJUNTO:{resourceUrl.Trim()}]"
            : $"{cleanDesc}\n[RECURSO_ADJUNTO:{resourceUrl.Trim()}]";
    }

    public static (string CleanDescription, string? ResourceUrl) Unpack(string? rawDescription)
    {
        if (string.IsNullOrWhiteSpace(rawDescription))
        {
            return (string.Empty, null);
        }

        string? resourceUrl = null;
        var match = ResourceTagRegex.Match(rawDescription);
        if (match.Success)
        {
            resourceUrl = match.Groups[1].Value.Trim();
        }

        var clean = ResourceTagRegex.Replace(rawDescription, string.Empty).Trim();
        return (clean, resourceUrl);
    }
}
