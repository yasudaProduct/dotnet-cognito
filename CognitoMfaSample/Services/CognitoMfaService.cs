using System.Security.Cryptography;
using System.Text;
using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;

namespace CognitoMfaSample.Services;

public class CognitoMfaService : ICognitoMfaService
{
    private readonly AmazonCognitoIdentityProviderClient _client;
    private readonly string _userPoolId;
    private readonly string _clientId;
    private readonly string? _clientSecret;
    private readonly ILogger<CognitoMfaService> _logger;

    public CognitoMfaService(IConfiguration configuration, ILogger<CognitoMfaService> logger)
    {
        _logger = logger;
        var region = configuration["AWS:Cognito:Region"] ?? "ap-northeast-1";
        _userPoolId = configuration["AWS:Cognito:UserPoolId"] ?? throw new ArgumentNullException("UserPoolId is required");
        _clientId = configuration["AWS:Cognito:ClientId"] ?? throw new ArgumentNullException("ClientId is required");
        _clientSecret = configuration["AWS:Cognito:ClientSecret"];

        _client = new AmazonCognitoIdentityProviderClient(RegionEndpoint.GetBySystemName(region));
    }

    public async Task<(bool Success, string? Error)> CreateCognitoUserAsync(string email, string password)
    {
        try
        {
            var request = new SignUpRequest
            {
                ClientId = _clientId,
                Username = email,
                Password = password,
                UserAttributes = new List<AttributeType>
                {
                    new AttributeType { Name = "email", Value = email }
                }
            };

            var secretHash = ComputeSecretHash(email);
            if (!string.IsNullOrEmpty(secretHash))
            {
                request.SecretHash = secretHash;
            }

            await _client.SignUpAsync(request);
            _logger.LogInformation("Cognito user created for {Email}", email);
            return (true, null);
        }
        catch (UsernameExistsException)
        {
            return (false, "このメールアドレスは既にCognitoに登録されています");
        }
        catch (InvalidPasswordException ex)
        {
            return (false, $"パスワードが要件を満たしていません: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Cognito user for {Email}", email);
            return (false, $"Cognitoユーザーの作成に失敗しました: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> ConfirmSignUpAsync(string email, string confirmationCode)
    {
        try
        {
            var request = new ConfirmSignUpRequest
            {
                ClientId = _clientId,
                Username = email,
                ConfirmationCode = confirmationCode
            };

            var secretHash = ComputeSecretHash(email);
            if (!string.IsNullOrEmpty(secretHash))
            {
                request.SecretHash = secretHash;
            }

            await _client.ConfirmSignUpAsync(request);
            _logger.LogInformation("Cognito user confirmed for {Email}", email);
            return (true, null);
        }
        catch (CodeMismatchException)
        {
            return (false, "確認コードが正しくありません");
        }
        catch (ExpiredCodeException)
        {
            return (false, "確認コードの有効期限が切れています");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm Cognito user for {Email}", email);
            return (false, $"確認に失敗しました: {ex.Message}");
        }
    }

    public async Task<MfaResult> InitiateMfaAsync(string email, string password)
    {
        try
        {
            var authParameters = new Dictionary<string, string>
            {
                { "USERNAME", email },
                { "PASSWORD", password }
            };

            var secretHash = ComputeSecretHash(email);
            if (!string.IsNullOrEmpty(secretHash))
            {
                authParameters["SECRET_HASH"] = secretHash;
            }

            var request = new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                ClientId = _clientId,
                AuthParameters = authParameters
            };

            var response = await _client.InitiateAuthAsync(request);

            if (response.ChallengeName?.Value == "EMAIL_OTP")
            {
                _logger.LogInformation("Email OTP challenge initiated for {Email}", email);
                return new MfaResult
                {
                    Success = false,
                    ChallengeName = "EMAIL_OTP",
                    Session = response.Session
                };
            }

            // MFAなしで認証完了した場合
            if (response.AuthenticationResult != null)
            {
                return new MfaResult
                {
                    Success = true,
                    AccessToken = response.AuthenticationResult.AccessToken,
                    IdToken = response.AuthenticationResult.IdToken,
                    RefreshToken = response.AuthenticationResult.RefreshToken
                };
            }

            return new MfaResult
            {
                Success = false,
                ErrorMessage = $"Unknown challenge: {response.ChallengeName?.Value}"
            };
        }
        catch (UserNotConfirmedException)
        {
            return new MfaResult
            {
                Success = false,
                ErrorMessage = "メールアドレスの確認が完了していません"
            };
        }
        catch (NotAuthorizedException)
        {
            return new MfaResult
            {
                Success = false,
                ErrorMessage = "メールアドレスまたはパスワードが正しくありません"
            };
        }
        catch (UserNotFoundException)
        {
            return new MfaResult
            {
                Success = false,
                ErrorMessage = "ユーザーが見つかりません"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate MFA for {Email}", email);
            return new MfaResult
            {
                Success = false,
                ErrorMessage = $"認証に失敗しました: {ex.Message}"
            };
        }
    }

    public async Task<MfaResult> RespondToEmailOtpAsync(string session, string email, string otpCode)
    {
        try
        {
            var challengeResponses = new Dictionary<string, string>
            {
                { "USERNAME", email },
                { "EMAIL_OTP_CODE", otpCode }
            };

            var secretHash = ComputeSecretHash(email);
            if (!string.IsNullOrEmpty(secretHash))
            {
                challengeResponses["SECRET_HASH"] = secretHash;
            }

            var request = new RespondToAuthChallengeRequest
            {
                ClientId = _clientId,
                ChallengeName = new ChallengeNameType("EMAIL_OTP"),
                Session = session,
                ChallengeResponses = challengeResponses
            };

            var response = await _client.RespondToAuthChallengeAsync(request);

            if (response.AuthenticationResult != null)
            {
                _logger.LogInformation("Email OTP verified for {Email}", email);
                return new MfaResult
                {
                    Success = true,
                    AccessToken = response.AuthenticationResult.AccessToken,
                    IdToken = response.AuthenticationResult.IdToken,
                    RefreshToken = response.AuthenticationResult.RefreshToken
                };
            }

            return new MfaResult
            {
                Success = false,
                ErrorMessage = "認証に失敗しました"
            };
        }
        catch (CodeMismatchException)
        {
            return new MfaResult
            {
                Success = false,
                ErrorMessage = "ワンタイムパスワードが正しくありません"
            };
        }
        catch (ExpiredCodeException)
        {
            return new MfaResult
            {
                Success = false,
                ErrorMessage = "ワンタイムパスワードの有効期限が切れています"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify Email OTP for {Email}", email);
            return new MfaResult
            {
                Success = false,
                ErrorMessage = $"検証に失敗しました: {ex.Message}"
            };
        }
    }

    private string? ComputeSecretHash(string username)
    {
        if (string.IsNullOrEmpty(_clientSecret))
        {
            return null;
        }

        var message = username + _clientId;
        var keyBytes = Encoding.UTF8.GetBytes(_clientSecret);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToBase64String(hashBytes);
    }
}
