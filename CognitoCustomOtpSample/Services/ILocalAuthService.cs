namespace CognitoCustomOtpSample.Services;

public interface ILocalAuthService
{
    Task<bool> ValidateCredentialsAsync(string email, string password);
}
