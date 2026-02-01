using System.Security.Cryptography;
using System.Text;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;

namespace CognitoEmailOtpSample.Services;

public class CognitoEmailOtpService : ICognitoEmailOtpService
{
    private readonly IAmazonCognitoIdentityProvider _cognitoClient;
    private readonly string _userPoolId;
    private readonly string _clientId;
    private readonly string? _clientSecret;
    private readonly ILogger<CognitoEmailOtpService> _logger;

    public CognitoEmailOtpService(
        IAmazonCognitoIdentityProvider cognitoClient,
        IConfiguration configuration,
        ILogger<CognitoEmailOtpService> logger)
    {
        _cognitoClient = cognitoClient;
        _userPoolId = configuration["AWS:Cognito:UserPoolId"]
            ?? throw new InvalidOperationException("UserPoolId が設定されていません");
        _clientId = configuration["AWS:Cognito:ClientId"]
            ?? throw new InvalidOperationException("ClientId が設定されていません");
        _clientSecret = configuration["AWS:Cognito:ClientSecret"];
        _logger = logger;
    }

    /// <summary>
    /// SECRET_HASH を計算する（ClientSecret がある場合のみ必要）
    /// </summary>
    private string? ComputeSecretHash(string username)
    {
        if (string.IsNullOrWhiteSpace(_clientSecret))
        {
            return null;
        }

        var message = Encoding.UTF8.GetBytes(username + _clientId);
        var key = Encoding.UTF8.GetBytes(_clientSecret);

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(message);
        return Convert.ToBase64String(hash);
    }

    public async Task<(string ChallengeName, string Session)> StartEmailOtpAsync(string email)
    {
        try
        {
            var authParameters = new Dictionary<string, string>
            {
                { "USERNAME", email },
                { "PREFERRED_CHALLENGE", "EMAIL_OTP" }
            };

            // SECRET_HASH を追加（必要な場合）
            var secretHash = ComputeSecretHash(email);
            if (secretHash != null)
            {
                authParameters["SECRET_HASH"] = secretHash;
            }

            var request = new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.USER_AUTH,
                ClientId = _clientId,
                AuthParameters = authParameters
            };

            _logger.LogInformation("Email OTP チャレンジを開始します: {Email}", email);

            var response = await _cognitoClient.InitiateAuthAsync(request);

            string? challengeName = response.ChallengeName?.Value;
            string? session = response.Session;

            // SELECT_CHALLENGE が返された場合、EMAIL_OTP を選択
            if (response.ChallengeName == ChallengeNameType.SELECT_CHALLENGE)
            {
                _logger.LogInformation("SELECT_CHALLENGE を受信、EMAIL_OTP を選択します");

                var selectChallengeResponses = new Dictionary<string, string>
                {
                    { "ANSWER", "EMAIL_OTP" },
                    { "USERNAME", email }
                };

                // SECRET_HASH を追加（必要な場合）
                if (secretHash != null)
                {
                    selectChallengeResponses["SECRET_HASH"] = secretHash;
                }

                var selectRequest = new RespondToAuthChallengeRequest
                {
                    ChallengeName = ChallengeNameType.SELECT_CHALLENGE,
                    ClientId = _clientId,
                    Session = response.Session,
                    ChallengeResponses = selectChallengeResponses
                };

                var selectResponse = await _cognitoClient.RespondToAuthChallengeAsync(selectRequest);
                challengeName = selectResponse.ChallengeName?.Value;
                session = selectResponse.Session;
            }

            if (challengeName == "EMAIL_OTP")
            {
                _logger.LogInformation("Email OTP チャレンジが正常に開始されました");
                return (challengeName, session!);
            }

            _logger.LogWarning("予期しないチャレンジタイプ: {ChallengeName}", challengeName);
            throw new InvalidOperationException($"予期しないチャレンジタイプ: {challengeName}");
        }
        catch (UserNotFoundException ex)
        {
            _logger.LogWarning("ユーザーが見つかりません: {Email}", email);
            throw new InvalidOperationException("ユーザーが見つかりません。Cognito にユーザーが存在することを確認してください。", ex);
        }
        catch (NotAuthorizedException ex)
        {
            _logger.LogWarning("認証が拒否されました: {Message}", ex.Message);
            throw new InvalidOperationException("認証が拒否されました。App Client の設定を確認してください。", ex);
        }
        catch (InvalidParameterException ex) when (ex.Message.Contains("Password Challenge is Required"))
        {
            _logger.LogError(ex, "パスワードチャレンジが必須です");
            throw new InvalidOperationException(
                "Cognito ユーザーにパスワードが設定されているか、ユーザーのステータスが FORCE_CHANGE_PASSWORD になっています。" +
                "以下のいずれかの対処を行ってください：\n" +
                "1. AWS コンソールでユーザーを削除し、パスワードなしで再作成\n" +
                "2. ユーザーのステータスを CONFIRMED に変更\n" +
                "3. AdminSetUserPassword API でパスワードを設定（Permanent=true）\n" +
                "詳細は README.md を参照してください。", ex);
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Cognito API エラー: {Message}", ex.Message);
            throw new InvalidOperationException($"Cognito エラー: {ex.Message}", ex);
        }
    }

    public async Task<bool> VerifyEmailOtpAsync(string session, string email, string code)
    {
        try
        {
            var challengeResponses = new Dictionary<string, string>
            {
                { "EMAIL_OTP_CODE", code },
                { "USERNAME", email }
            };

            // SECRET_HASH を追加（必要な場合）
            var secretHash = ComputeSecretHash(email);
            if (secretHash != null)
            {
                challengeResponses["SECRET_HASH"] = secretHash;
            }

            var request = new RespondToAuthChallengeRequest
            {
                ChallengeName = ChallengeNameType.EMAIL_OTP,
                ClientId = _clientId,
                Session = session,
                ChallengeResponses = challengeResponses
            };

            _logger.LogInformation("Email OTP コードを検証します: {Email}", email);

            var response = await _cognitoClient.RespondToAuthChallengeAsync(request);

            if (response.AuthenticationResult != null)
            {
                _logger.LogInformation("Email OTP 検証成功");
                _logger.LogDebug("IdToken: {IdToken}", response.AuthenticationResult.IdToken?.Substring(0, 20) + "...");
                _logger.LogDebug("AccessToken: {AccessToken}", response.AuthenticationResult.AccessToken?.Substring(0, 20) + "...");
                return true;
            }

            _logger.LogWarning("OTP 検証が成功しましたが、認証結果が返されませんでした");
            return false;
        }
        catch (CodeMismatchException)
        {
            _logger.LogWarning("OTP コードが一致しません: {Email}", email);
            return false;
        }
        catch (ExpiredCodeException ex)
        {
            _logger.LogWarning("OTP コードの有効期限が切れています: {Email}", email);
            throw new InvalidOperationException("OTP コードの有効期限が切れています。再度サインインしてください。", ex);
        }
        catch (NotAuthorizedException ex)
        {
            _logger.LogWarning("認証が拒否されました: {Message}", ex.Message);
            return false;
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Cognito API エラー: {Message}", ex.Message);
            throw new InvalidOperationException($"Cognito エラー: {ex.Message}", ex);
        }
    }
}
