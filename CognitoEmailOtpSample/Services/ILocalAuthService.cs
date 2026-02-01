namespace CognitoEmailOtpSample.Services;

public interface ILocalAuthService
{
    /// <summary>
    /// ローカル認証（既存の独自パスワード認証）
    /// </summary>
    /// <param name="email">メールアドレス</param>
    /// <param name="password">パスワード</param>
    /// <returns>認証成功時は true、失敗時は false</returns>
    Task<bool> ValidateCredentialsAsync(string email, string password);
}
