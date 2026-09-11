using LMS.API.Entities;

namespace LMS.API.Services;

public interface ITokenService
{
    (string Token, DateTime Expiration) GenerateToken(
        Usuario user, 
        string? fullName = null, 
        string? roleName = null, 
        string? gradeName = null, 
        string? avatarUrl = null);
}
