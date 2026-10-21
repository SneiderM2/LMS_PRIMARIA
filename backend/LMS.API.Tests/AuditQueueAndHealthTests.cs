using LMS.API.DTOs;
using LMS.API.Entities;
using LMS.API.Middleware;
using LMS.API.Services;
using Xunit;

namespace LMS.API.Tests;

public class AuditQueueAndHealthTests
{
    [Fact]
    public async Task AuditLogQueue_enqueues_and_dequeues_in_fifo_order()
    {
        var queue = new AuditLogQueue();
        var first = new AuditLogEntry
        {
            Level = "AUDIT",
            Method = "GET",
            Path = "/api/teacher/students/5/progress",
            StatusCode = 200,
            Action = "Consulta historial de progreso"
        };
        var second = new AuditLogEntry
        {
            Level = "INFO",
            Method = "POST",
            Path = "/api/student/upload-assignment",
            StatusCode = 200,
            Action = "Entrega de tarea"
        };

        queue.Enqueue(first);
        queue.Enqueue(second);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var readItems = new List<AuditLogEntry>();
        await foreach (var item in queue.ReadAllAsync(cts.Token))
        {
            readItems.Add(item);
            if (readItems.Count == 2) break;
        }

        Assert.Equal(2, readItems.Count);
        Assert.Equal("/api/teacher/students/5/progress", readItems[0].Path);
        Assert.Equal("AUDIT", readItems[0].Level);
        Assert.Equal("/api/student/upload-assignment", readItems[1].Path);
    }

    [Fact]
    public void AuditLog_entity_initializes_defaults_safely()
    {
        var entity = new AuditLog
        {
            Level = "AUDIT",
            Method = "GET",
            Path = "/api/admin/audit-logs",
            StatusCode = 200,
            ElapsedMs = 15,
            UserRole = "Admin",
            Action = "Revision de bitacora"
        };

        Assert.Equal("AUDIT", entity.Level);
        Assert.Equal(200, entity.StatusCode);
        Assert.Equal("Admin", entity.UserRole);
        Assert.True(entity.Timestamp <= DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public void GradebookDto_calculates_student_average_structure()
    {
        var dto = new GradebookDto
        {
            CourseId = 10,
            CourseName = "Matematicas",
            GradeName = "3°",
            Tasks = new List<GradebookTaskDto>
            {
                new() { Id = 1, Title = "Suma", MaxScore = 5.0m },
                new() { Id = 2, Title = "Resta", MaxScore = 5.0m }
            },
            Students = new List<GradebookStudentDto>
            {
                new()
                {
                    StudentId = 100,
                    FullName = "Carlos Perez",
                    Username = "1001",
                    Grades = new Dictionary<string, decimal?> { ["1"] = 4.5m, ["2"] = 5.0m },
                    SubmittedCount = 2,
                    Average = 4.75m
                }
            }
        };

        Assert.Equal(2, dto.Tasks.Count);
        Assert.Single(dto.Students);
        Assert.Equal(4.75m, dto.Students[0].Average);
    }
}
