namespace CognitoEmailOtpSample.Services;

public interface ICognitoEmailOtpService
{
    /// <summary>
    /// Email OTP チャレンジを開始する
    /// </summary>
    /// <param name="email">ユーザーのメールアドレス</param>
    /// <returns>チャレンジ名とセッション情報</returns>
    Task<(string ChallengeName, string Session)> StartEmailOtpAsync(string email);

    /// <summary>
    /// Email OTP コードを検証する
    /// </summary>
    /// <param name="session">Cognito セッション</param>
    /// <param name="email">ユーザーのメールアドレス</param>
    /// <param name="code">OTP コード</param>
    /// <returns>検証成功時は true、失敗時は false</returns>
    Task<bool> VerifyEmailOtpAsync(string session, string email, string code);
}
