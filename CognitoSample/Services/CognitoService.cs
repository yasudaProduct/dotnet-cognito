using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;

namespace CognitoSample.Services;

public interface ICognitoService
{
    Task<SignUpResult> SignUpAsync(string email, string password);
    Task<ConfirmSignUpResponse> ConfirmSignUpAsync(string email, string confirmationCode);
    Task<AuthenticationResult> SignInAsync(string email, string password);
    Task<AuthenticationResult> RespondToMfaChallengeAsync(string session, string mfaCode, string email);
    Task<AssociateSoftwareTokenResponse> SetupMfaAsync(string accessToken);
    Task<VerifySoftwareTokenResponse> VerifyMfaSetupAsync(string accessToken, string totpCode);
    Task SetMfaPreferenceAsync(string accessToken);
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
    public string? SecretCode { get; set; }
}

public class CognitoService : ICognitoService
{
    private readonly AmazonCognitoIdentityProviderClient _client;
    private readonly string _userPoolId;
    private readonly string _clientId;
    private readonly ILogger<CognitoService> _logger;

    public CognitoService(IConfiguration configuration, ILogger<CognitoService> logger)
    {
        _logger = logger;
        var region = configuration["AWS:Cognito:Region"] ?? "ap-northeast-1";
        _userPoolId = configuration["AWS:Cognito:UserPoolId"] ?? throw new ArgumentNullException("UserPoolId is required");
        _clientId = configuration["AWS:Cognito:ClientId"] ?? throw new ArgumentNullException("ClientId is required");

        _client = new AmazonCognitoIdentityProviderClient(RegionEndpoint.GetBySystemName(region));
    }

    public async Task<SignUpResult> SignUpAsync(string email, string password)
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
            ConfirmationCode = confirmationCode
        };

        return await _client.ConfirmSignUpAsync(request);
    }

    public async Task<AuthenticationResult> SignInAsync(string email, string password)
    {
        var request = new InitiateAuthRequest
        {
            AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
            ClientId = _clientId,
            AuthParameters = new Dictionary<string, string>
            {
                { "USERNAME", email },
                { "PASSWORD", password }
            }
        };

        var response = await _client.InitiateAuthAsync(request);

        if (response.ChallengeName == ChallengeNameType.SOFTWARE_TOKEN_MFA)
        {
            return new AuthenticationResult
            {
                Success = false,
                ChallengeName = "SOFTWARE_TOKEN_MFA",
                Session = response.Session
            };
        }

        if (response.ChallengeName == ChallengeNameType.MFA_SETUP)
        {
            var associateResponse = await _client.AssociateSoftwareTokenAsync(new AssociateSoftwareTokenRequest
            {
                Session = response.Session
            });

            return new AuthenticationResult
            {
                Success = false,
                ChallengeName = "MFA_SETUP",
                Session = associateResponse.Session,
                SecretCode = associateResponse.SecretCode
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

    public async Task<AuthenticationResult> RespondToMfaChallengeAsync(string session, string mfaCode, string email)
    {
        var request = new RespondToAuthChallengeRequest
        {
            ClientId = _clientId,
            ChallengeName = ChallengeNameType.SOFTWARE_TOKEN_MFA,
            Session = session,
            ChallengeResponses = new Dictionary<string, string>
            {
                { "USERNAME", email },
                { "SOFTWARE_TOKEN_MFA_CODE", mfaCode }
            }
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

    public async Task<AssociateSoftwareTokenResponse> SetupMfaAsync(string accessToken)
    {
        var request = new AssociateSoftwareTokenRequest
        {
            AccessToken = accessToken
        };

        return await _client.AssociateSoftwareTokenAsync(request);
    }

    public async Task<VerifySoftwareTokenResponse> VerifyMfaSetupAsync(string accessToken, string totpCode)
    {
        var request = new VerifySoftwareTokenRequest
        {
            AccessToken = accessToken,
            UserCode = totpCode
        };

        return await _client.VerifySoftwareTokenAsync(request);
    }

    public async Task SetMfaPreferenceAsync(string accessToken)
    {
        var request = new SetUserMFAPreferenceRequest
        {
            AccessToken = accessToken,
            SoftwareTokenMfaSettings = new SoftwareTokenMfaSettingsType
            {
                Enabled = true,
                PreferredMfa = true
            }
        };

        await _client.SetUserMFAPreferenceAsync(request);
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
