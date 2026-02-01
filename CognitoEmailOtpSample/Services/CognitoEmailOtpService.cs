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

            if (response.ChallengeName == ChallengeNameType.EMAIL_OTP)
            {
                _logger.LogInformation("Email OTP チャレンジが正常に開始されました");
                return (response.ChallengeName.Value, response.Session);
            }

            _logger.LogWarning("予期しないチャレンジタイプ: {ChallengeName}", response.ChallengeName?.Value);
            throw new InvalidOperationException($"予期しないチャレンジタイプ: {response.ChallengeName?.Value}");
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
