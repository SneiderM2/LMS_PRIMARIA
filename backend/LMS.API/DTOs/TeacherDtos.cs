using System.ComponentModel.DataAnnotations;

namespace LMS.API.DTOs;

public class CreateStudentDto
{
    [Required(ErrorMessage = "El Documento o Carnet es obligatorio")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "El Nombre Completo es obligatorio")]
    public string FullName { get; set; } = string.Empty;

    public string? Password { get; set; }

    [Required(ErrorMessage = "El Grado escolar es obligatorio")]
    public string Grade { get; set; } = "1°";
}

public class EnrollStudentDto
{
    /// <summary>
    /// Puede ser el id numérico del usuario o el username/carnet del estudiante
    /// </summary>
    [Required(ErrorMessage = "El identificador del estudiante es obligatorio")]
    public string StudentId { get; set; } = string.Empty;
}

public class TeacherStudentDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<EnrolledCourseSummaryDto> EnrolledCourses { get; set; } = new();
}

public class EnrolledCourseSummaryDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVA";
}

public class CourseDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Grado { get; set; } = string.Empty;
    public string Grupo { get; set; } = string.Empty;
    public int DocenteId { get; set; }
    public string DocenteNombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int TotalStudents { get; set; }
    public DateTime FechaCreacion { get; set; }
    public bool Activo { get; set; }
}

public class CreateCourseDto
{
    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    public string Grado { get; set; } = "1°";

    [Required]
    [MaxLength(10)]
    public string Grupo { get; set; } = "A";

    public string? Descripcion { get; set; }
}
