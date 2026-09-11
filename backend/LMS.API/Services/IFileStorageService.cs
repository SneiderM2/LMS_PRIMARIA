using Microsoft.AspNetCore.Http;

namespace LMS.API.Services;

public interface IFileStorageService
{
    Task<(bool Success, string FileUrl, string OriginalFileName, string ErrorMessage)> SaveFileAsync(IFormFile file, string subFolder);
}
