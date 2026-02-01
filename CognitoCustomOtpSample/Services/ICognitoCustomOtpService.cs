namespace CognitoCustomOtpSample.Services;

public interface ICognitoCustomOtpService
{
    Task<(string Session, string ChallengeName)> StartCustomOtpAsync(string email);
    Task<bool> VerifyCustomOtpAsync(string session, string email, string otp);
}
