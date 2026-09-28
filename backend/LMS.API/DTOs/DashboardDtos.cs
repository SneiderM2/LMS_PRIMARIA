namespace LMS.API.DTOs;

// ==========================================
// DTOs para Módulo de Logs Amigable
// ==========================================
public class ParsedLogEntryDto
{
    public string Id { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Level { get; set; } = "INFO"; // INFO, WARNING, ERROR
    public string User { get; set; } = "-";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ClientIp { get; set; }
    public string? HttpMethod { get; set; }
    public string? Path { get; set; }
    public int? StatusCode { get; set; }
    public long? ElapsedMs { get; set; }
}

// ==========================================
// DTOs para Rol Estudiante
// ==========================================
public class StudentMetricsDto
{
    public int EnrolledCoursesCount { get; set; }
    public decimal CumulativeGpa { get; set; } // Promedio acumulado (ej. 4.5 / 5.0 o sobre 100)
    public int PendingActivitiesCount { get; set; }
    public decimal ProgressPercentage { get; set; } // Porcentaje completado (0 - 100)
}

public class StudentCourseItemDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Grado { get; set; } = string.Empty;
    public string Grupo { get; set; } = string.Empty;
    public string DocenteNombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool IsEnrolled { get; set; }
    public string EnrollmentStatus { get; set; } = "INACTIVA"; // ACTIVA, INACTIVA
    public DateTime? FechaInscripcion { get; set; }
}

public class StudentCoursesCatalogDto
{
    public List<StudentCourseItemDto> MyActiveCourses { get; set; } = new();
    public List<StudentCourseItemDto> AvailableCourses { get; set; } = new();
}

// ==========================================
// DTOs para Rol Profesor
// ==========================================
public class TeacherMetricsDto
{
    public int TotalStudents { get; set; }
    public int PendingGradingCount { get; set; }
    public int ActiveTasksCount { get; set; }
    public decimal SubmissionRate { get; set; } // Porcentaje de entregas (0 - 100)
}

public class TeacherActivityDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = "Tarea"; // "Tarea" o "Material"
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? MaxScore { get; set; }
    public bool IsActive { get; set; }
    public int SubmissionsCount { get; set; }
    public int PendingGradingCount { get; set; }
    public string? ResourceUrl { get; set; }
    public string? ResourceType { get; set; }
}

public class UpdateActivityDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? MaxScore { get; set; }
    public string? ResourceUrl { get; set; }
}

// ==========================================
// DTOs para Rol Administrador
// ==========================================
public class AdminMetricsDto
{
    public int TotalActiveUsers { get; set; }
    public int ActiveTeachersCount { get; set; }
    public int ActiveStudentsCount { get; set; }
    public string SystemStatus { get; set; } = "Operativo";
    public int RecentErrorsCount { get; set; }
    public int TotalCoursesCount { get; set; }
}

public class AdminUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? GradeLevel { get; set; }
    public bool Activo { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class CreateAdminUserDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Role { get; set; } = "Student"; // "Student" | "Teacher" | "Admin"
    public string? GradeLevel { get; set; } // Opcional, para estudiantes
}

public class UpdateAdminUserDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? GradeLevel { get; set; }
    public string? Password { get; set; } // Opcional para reset de contraseña
}

public class AdminCourseDto
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

public class CreateAdminCourseDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Grado { get; set; } = "1°";
    public string Grupo { get; set; } = "A";
    public int? DocenteId { get; set; }
    public string? Descripcion { get; set; }
}

public class UpdateAdminCourseDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Grado { get; set; } = "1°";
    public string Grupo { get; set; } = "A";
    public int? DocenteId { get; set; }
    public string? Descripcion { get; set; }
}
