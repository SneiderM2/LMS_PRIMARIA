using System.Security.Claims;
using LMS.API.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LMS.API.Tests;

public class SystemFilesSecurityTests
{
    private static SystemFilesController CreateController(string role, bool isDev = false)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:AllowLogPurge"] = "false"
            })
            .Build();

        var env = new StubWebHostEnv(isDev ? "Development" : "Production");
        var controller = new SystemFilesController(
            env,
            config,
            NullLogger<SystemFilesController>.Instance);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, $"user.{role.ToLowerInvariant()}"),
            new Claim(ClaimTypes.Name, $"user.{role.ToLowerInvariant()}"),
            new Claim(ClaimTypes.Role, role)
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("ALUMNO")]
    [InlineData("DOCENTE")]
    public async Task SystemFilesEndpoints_WithNonAdminRole_ReturnForbid(string nonAdminRole)
    {
        var controller = CreateController(nonAdminRole);

        var htaccessResult = await controller.GetHtaccess();
        Assert.IsType<ForbidResult>(htaccessResult);

        var listResult = controller.GetLogFiles();
        Assert.IsType<ForbidResult>(listResult);

        var contentResult = await controller.GetLogContent("access.log");
        Assert.IsType<ForbidResult>(contentResult);

        var parsedResult = await controller.GetParsedLogs("access.log");
        Assert.IsType<ForbidResult>(parsedResult);

        var clearResult = await controller.ClearLog("access.log");
        Assert.IsType<ForbidResult>(clearResult);
    }

    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("..\\..\\Program.cs")]
    [InlineData("../logs/access.log")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("access.log%00.json")]
    public async Task GetLogContent_And_GetParsedLogs_BlockPathTraversalPayloads(string payload)
    {
        var controller = CreateController("Admin");

        var contentRes = await controller.GetLogContent(payload);
        Assert.IsType<BadRequestObjectResult>(contentRes);

        var parsedRes = await controller.GetParsedLogs(payload);
        Assert.IsType<BadRequestObjectResult>(parsedRes);
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Local.json")]
    [InlineData("LMS.API.dll")]
    [InlineData("secret.env")]
    [InlineData("backup.sql")]
    public async Task GetLogContent_And_GetParsedLogs_RejectNonLogExtensions(string forbiddenFile)
    {
        var controller = CreateController("Admin");

        var contentRes = await controller.GetLogContent(forbiddenFile);
        Assert.IsType<BadRequestObjectResult>(contentRes);

        var parsedRes = await controller.GetParsedLogs(forbiddenFile);
        Assert.IsType<BadRequestObjectResult>(parsedRes);
    }

    [Theory]
    [InlineData("app-audit.log")]
    [InlineData("audit.log")]
    [InlineData("security-audit.log")]
    public async Task ClearLog_RejectsClearingAuditLogs_ToPreserveForensicIntegrity(string auditLogName)
    {
        var controller = CreateController("Admin", isDev: true);

        var res = await controller.ClearLog(auditLogName);

        Assert.IsType<BadRequestObjectResult>(res);
    }

    [Fact]
    public async Task ClearLog_InProductionWithoutAllowLogPurge_Returns403Forbidden()
    {
        var controller = CreateController("Admin", isDev: false);

        var res = await controller.ClearLog("access.log");

        var forbidden = Assert.IsType<ObjectResult>(res);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private sealed class StubWebHostEnv : IWebHostEnvironment
    {
        public StubWebHostEnv(string envName) => EnvironmentName = envName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "LMS.API";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
