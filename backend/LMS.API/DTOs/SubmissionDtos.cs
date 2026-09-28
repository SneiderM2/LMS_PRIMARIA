using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LMS.API.DTOs;

public class UploadAssignmentDto
{
    [Required]
    public string AssignmentId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Por favor selecciona o arrastra el archivo de tu tarea")]
    public IFormFile File { get; set; } = null!;
}

public class UploadResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public StudentSubmissionDto? Submission { get; set; }
}

public class GradeSubmissionDto
{
    [Range(0, 100, ErrorMessage = "La nota debe estar entre 0 y 100")]
    public decimal Grade { get; set; }

    [MaxLength(1000)]
    public string? Feedback { get; set; }
}
