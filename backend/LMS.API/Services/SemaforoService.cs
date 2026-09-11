using LMS.API.DTOs;
using LMS.API.Entities;

namespace LMS.API.Services;

public class SemaforoService : ISemaforoService
{
    public StudentStatusDto CalculateStatus(Usuario student, DateTime? lastActivityDate = null)
    {
        var activity = lastActivityDate;
        var info = SemaforoInfo.Calculate(activity);

        var name = !string.IsNullOrWhiteSpace(student.FullName) ? student.FullName : student.Username;
        var grade = student.Alumno?.Grado?.Nombre ?? "Sin Grado";
        var avatar = student.AvatarUrl ?? $"https://api.dicebear.com/7.x/bottts/svg?seed={student.Username}";

        return new StudentStatusDto
        {
            Id = student.Username,
            FullName = name,
            GradeLevel = grade,
            AvatarUrl = avatar,
            LastLoginDate = activity,
            InactiveDays = info.InactiveDays,
            SemaforoColor = info.Color.ToString(), // "Green", "Yellow", "Red"
            SemaforoLabel = info.Label,
            SemaforoEmoji = info.Emoji,
            Description = info.Description
        };
    }

    public List<StudentStatusDto> CalculateStatusList(IEnumerable<Usuario> students, Dictionary<int, DateTime?>? lastActivityMap = null)
    {
        return students.Select(s =>
        {
            DateTime? lastAct = null;
            if (lastActivityMap != null && lastActivityMap.TryGetValue(s.Id, out var act))
            {
                lastAct = act;
            }
            return CalculateStatus(s, lastAct);
        }).ToList();
    }

    public SemaforoSummaryMetricsDto CalculateMetrics(IEnumerable<StudentStatusDto> studentsStatus)
    {
        var list = studentsStatus.ToList();
        return new SemaforoSummaryMetricsDto
        {
            TotalStudents = list.Count,
            GreenCount = list.Count(s => s.SemaforoColor == SemaforoColor.Green.ToString()),
            YellowCount = list.Count(s => s.SemaforoColor == SemaforoColor.Yellow.ToString()),
            RedCount = list.Count(s => s.SemaforoColor == SemaforoColor.Red.ToString())
        };
    }
}
