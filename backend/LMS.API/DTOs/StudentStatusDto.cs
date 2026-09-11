namespace LMS.API.DTOs;

public class StudentStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public DateTime? LastLoginDate { get; set; }
    public int InactiveDays { get; set; }
    public string SemaforoColor { get; set; } = string.Empty; // "Green", "Yellow", "Red"
    public string SemaforoLabel { get; set; } = string.Empty;
    public string SemaforoEmoji { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class SemaforoSummaryMetricsDto
{
    public int TotalStudents { get; set; }
    public int GreenCount { get; set; }
    public int YellowCount { get; set; }
    public int RedCount { get; set; }
}
