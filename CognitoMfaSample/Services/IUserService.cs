using CognitoMfaSample.Models;

namespace CognitoMfaSample.Services;

public interface IUserService
{
    Task<(bool Success, string? Error)> CreateUserAsync(string email, string password);
    Task<ApplicationUser?> AuthenticateAsync(string email, string password);
    Task<ApplicationUser?> GetUserByEmailAsync(string email);
}
