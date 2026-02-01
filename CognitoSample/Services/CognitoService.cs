using System.Security.Cryptography;
using System.Text;
using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;

namespace CognitoSample.Services;

public interface ICognitoService
{
    Task<SignUpResult> SignUpAsync(string email, string password);
    Task<ConfirmSignUpResponse> ConfirmSignUpAsync(string email, string confirmationCode);
    Task<AuthenticationResult> SignInAsync(string email, string password);
    Task<AuthenticationResult> RespondToEmailOtpChallengeAsync(string session, string otpCode, string email);
    Task<GlobalSignOutResponse> SignOutAsync(string accessToken);
    Task<GetUserResponse> GetUserAsync(string accessToken);
}

public class SignUpResult
{
    public bool UserConfirmed { get; set; }
    public string? UserSub { get; set; }
}

public class AuthenticationResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? IdToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ChallengeName { get; set; }
    public string? Session { get; set; }
}

public class CognitoService : ICognitoService
{
    private readonly AmazonCognitoIdentityProviderClient _client;
    private readonly string _userPoolId;
    private readonly string _clientId;
    private readonly string? _clientSecret;
    private readonly ILogger<CognitoService> _logger;

    public CognitoService(IConfiguration configuration, ILogger<CognitoService> logger)
    {
        _logger = logger;
        var region = configuration["AWS:Cognito:Region"] ?? "ap-northeast-1";
        _userPoolId = configuration["AWS:Cognito:UserPoolId"] ?? throw new ArgumentNullException("UserPoolId is required");
        _clientId = configuration["AWS:Cognito:ClientId"] ?? throw new ArgumentNullException("ClientId is required");
        _clientSecret = configuration["AWS:Cognito:ClientSecret"];

        var regionEndpoint = RegionEndpoint.GetBySystemName(region);
        _client = new AmazonCognitoIdentityProviderClient(regionEndpoint);
        _logger.LogInformation("Using AWS CLI credentials (default credentials chain)");

        if (!string.IsNullOrEmpty(_clientSecret))
        {
            _logger.LogInformation("Client secret is configured, SECRET_HASH will be used");
        }
    }

    private string? ComputeSecretHash(string username)
    {
        if (string.IsNullOrEmpty(_clientSecret))
            return null;

        var message = username + _clientId;
        var keyBytes = Encoding.UTF8.GetBytes(_clientSecret);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToBase64String(hashBytes);
    }

    public async Task<SignUpResult> SignUpAsync(string email, string password)
    {
        var request = new SignUpRequest
        {
            ClientId = _clientId,
            Username = email,
            Password = password,
            SecretHash = ComputeSecretHash(email),
            UserAttributes = new List<AttributeType>
            {
                new AttributeType { Name = "email", Value = email }
            }
        };

        var response = await _client.SignUpAsync(request);
        return new SignUpResult
        {
            UserConfirmed = response.UserConfirmed,
            UserSub = response.UserSub
        };
    }

    public async Task<ConfirmSignUpResponse> ConfirmSignUpAsync(string email, string confirmationCode)
    {
        var request = new ConfirmSignUpRequest
        {
            ClientId = _clientId,
            Username = email,
            ConfirmationCode = confirmationCode,
            SecretHash = ComputeSecretHash(email)
        };

        return await _client.ConfirmSignUpAsync(request);
    }

    public async Task<AuthenticationResult> SignInAsync(string email, string password)
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
            return new AuthenticationResult
            {
                Success = false,
                ChallengeName = "EMAIL_OTP",
                Session = response.Session
            };
        }

        if (response.AuthenticationResult != null)
        {
            return new AuthenticationResult
            {
                Success = true,
                AccessToken = response.AuthenticationResult.AccessToken,
                IdToken = response.AuthenticationResult.IdToken,
                RefreshToken = response.AuthenticationResult.RefreshToken
            };
        }

        return new AuthenticationResult
        {
            Success = false,
            ChallengeName = response.ChallengeName?.Value
        };
    }

    public async Task<AuthenticationResult> RespondToEmailOtpChallengeAsync(string session, string otpCode, string email)
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
            return new AuthenticationResult
            {
                Success = true,
                AccessToken = response.AuthenticationResult.AccessToken,
                IdToken = response.AuthenticationResult.IdToken,
                RefreshToken = response.AuthenticationResult.RefreshToken
            };
        }

        return new AuthenticationResult
        {
            Success = false,
            ChallengeName = response.ChallengeName?.Value
        };
    }

    public async Task<GlobalSignOutResponse> SignOutAsync(string accessToken)
    {
        var request = new GlobalSignOutRequest
        {
            AccessToken = accessToken
        };

        return await _client.GlobalSignOutAsync(request);
    }

    public async Task<GetUserResponse> GetUserAsync(string accessToken)
    {
        var request = new GetUserRequest
        {
            AccessToken = accessToken
        };

        return await _client.GetUserAsync(request);
    }
}
