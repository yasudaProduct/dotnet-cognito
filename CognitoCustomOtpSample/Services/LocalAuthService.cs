namespace CognitoCustomOtpSample.Services;

public class LocalAuthService : ILocalAuthService
{
    // インメモリのユーザーデータ（実際の実装ではデータベースを使用）
    private readonly Dictionary<string, string> _users = new()
    {
        { "user@example.com", "Password123!" },
        { "test@example.com", "Test123456!" }
    };

    public Task<bool> ValidateCredentialsAsync(string email, string password)
    {
        if (_users.TryGetValue(email, out var storedPassword))
        {
            return Task.FromResult(storedPassword == password);
        }

        return Task.FromResult(false);
    }
}
