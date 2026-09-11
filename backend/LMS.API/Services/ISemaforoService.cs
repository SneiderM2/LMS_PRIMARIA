using LMS.API.DTOs;
using LMS.API.Entities;

namespace LMS.API.Services;

public interface ISemaforoService
{
    StudentStatusDto CalculateStatus(Usuario student, DateTime? lastActivityDate = null);
    List<StudentStatusDto> CalculateStatusList(IEnumerable<Usuario> students, Dictionary<int, DateTime?>? lastActivityMap = null);
    SemaforoSummaryMetricsDto CalculateMetrics(IEnumerable<StudentStatusDto> studentsStatus);
}
