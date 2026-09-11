using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LMS.API.DTOs;

public class UploadAssignmentDto
{
    [Required]
    public Guid AssignmentId { get; set; }

    [Required(ErrorMessage = "Por favor selecciona o arrastra el archivo de tu tarea")]
    public IFormFile File { get; set; } = null!;
}

public class UploadResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public StudentSubmissionDto? Submission { get; set; }
}
