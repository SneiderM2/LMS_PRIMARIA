using System.ComponentModel.DataAnnotations;

namespace LMS.API.DTOs;

public class ContentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Video", "Pdf", "Assignment"
    public string Subject { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public DateTime? DueDate { get; set; }
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

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = "Assignment"; // "Video", "Pdf", "Assignment"

    [Required]
    [MaxLength(100)]
    public string Subject { get; set; } = "General";

    [MaxLength(500)]
    public string? FileUrl { get; set; }

    public DateTime? DueDate { get; set; }

    [Required]
    [MaxLength(50)]
    public string GradeLevel { get; set; } = "2° Primaria";
}

public class StudentSubmissionDto
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string? Feedback { get; set; }
    public decimal? Grade { get; set; }
}
