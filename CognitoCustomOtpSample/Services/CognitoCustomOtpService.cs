using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using System.Security.Cryptography;
using System.Text;

namespace CognitoCustomOtpSample.Services;

public class CognitoCustomOtpService : ICognitoCustomOtpService
{
    private readonly AmazonCognitoIdentityProviderClient _cognitoClient;
    private readonly string _userPoolId;
    private readonly string _clientId;
    private readonly string? _clientSecret;
    private readonly ILogger<CognitoCustomOtpService> _logger;

    public CognitoCustomOtpService(IConfiguration configuration, ILogger<CognitoCustomOtpService> logger)
    {
        var region = configuration["AWS:Cognito:Region"];
        _userPoolId = configuration["AWS:Cognito:UserPoolId"] ?? throw new ArgumentNullException("UserPoolId");
        _clientId = configuration["AWS:Cognito:ClientId"] ?? throw new ArgumentNullException("ClientId");
        _clientSecret = configuration["AWS:Cognito:ClientSecret"];
        _logger = logger;

        _cognitoClient = new AmazonCognitoIdentityProviderClient(
            Amazon.RegionEndpoint.GetBySystemName(region));
    }

    public async Task<(string Session, string ChallengeName)> StartCustomOtpAsync(string email)
    {
        try
        {
            var authParameters = new Dictionary<string, string>
            {
                { "USERNAME", email }
            };

            if (!string.IsNullOrEmpty(_clientSecret))
            {
                authParameters["SECRET_HASH"] = CalculateSecretHash(email);
            }

            var request = new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.CUSTOM_AUTH,
                ClientId = _clientId,
                AuthParameters = authParameters
            };

            _logger.LogInformation("Initiating CUSTOM_AUTH for user: {Email}", email);
            var response = await _cognitoClient.InitiateAuthAsync(request);

            _logger.LogInformation("Challenge received: {ChallengeName}", response.ChallengeName);

            return (response.Session, response.ChallengeName.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start custom OTP for user: {Email}", email);
            throw;
        }
    }

    public async Task<bool> VerifyCustomOtpAsync(string session, string email, string otp)
    {
        try
        {
            var challengeResponses = new Dictionary<string, string>
            {
                { "USERNAME", email },
                { "ANSWER", otp }
            };

            if (!string.IsNullOrEmpty(_clientSecret))
            {
                challengeResponses["SECRET_HASH"] = CalculateSecretHash(email);
            }

            var request = new RespondToAuthChallengeRequest
            {
                ChallengeName = ChallengeNameType.CUSTOM_CHALLENGE,
                ClientId = _clientId,
                Session = session,
                ChallengeResponses = challengeResponses
            };

            _logger.LogInformation("Verifying custom OTP for user: {Email}", email);
            var response = await _cognitoClient.RespondToAuthChallengeAsync(request);

            if (response.AuthenticationResult != null)
            {
                _logger.LogInformation("OTP verification successful for user: {Email}", email);
                return true;
            }

            _logger.LogWarning("OTP verification failed for user: {Email}", email);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify custom OTP for user: {Email}", email);
            return false;
        }
    }

    private string CalculateSecretHash(string username)
    {
        if (string.IsNullOrEmpty(_clientSecret))
        {
            throw new InvalidOperationException("Client secret is not configured");
        }

        var message = username + _clientId;
        var keyBytes = Encoding.UTF8.GetBytes(_clientSecret);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToBase64String(hashBytes);
    }
}
