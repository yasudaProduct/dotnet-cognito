namespace CognitoEmailOtpSample.Services;

/// <summary>
/// 簡易的なローカル認証サービス（インメモリ実装）
/// 本番環境ではデータベース等を使用してください
/// </summary>
public class LocalAuthService : ILocalAuthService
{
    private readonly ILogger<LocalAuthService> _logger;

    // デモ用のユーザーデータ（本番環境ではデータベース等を使用）
    private readonly Dictionary<string, string> _users = new()
    {
        { "user@example.com", "Password123!" },
        { "test@example.com", "Test123!" }
    };

    public LocalAuthService(ILogger<LocalAuthService> logger)
    {
        _logger = logger;
    }

    public Task<bool> ValidateCredentialsAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("メールアドレスまたはパスワードが空です");
            return Task.FromResult(false);
        }

        if (_users.TryGetValue(email, out var storedPassword))
        {
            if (storedPassword == password)
            {
                _logger.LogInformation("ローカル認証成功: {Email}", email);
                return Task.FromResult(true);
            }
        }

        _logger.LogWarning("ローカル認証失敗: {Email}", email);
        return Task.FromResult(false);
    }
}
