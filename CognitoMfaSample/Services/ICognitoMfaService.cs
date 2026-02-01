namespace CognitoMfaSample.Services;

public class MfaResult
{
    public bool Success { get; set; }
    public string? Session { get; set; }
    public string? ChallengeName { get; set; }
    public string? AccessToken { get; set; }
    public string? IdToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface ICognitoMfaService
{
    Task<(bool Success, string? Error)> CreateCognitoUserAsync(string email, string password);
    Task<(bool Success, string? Error)> ConfirmSignUpAsync(string email, string confirmationCode);
    Task<MfaResult> InitiateMfaAsync(string email, string password);
    Task<MfaResult> RespondToEmailOtpAsync(string session, string email, string otpCode);
}
