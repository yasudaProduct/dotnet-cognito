using Microsoft.AspNetCore.Mvc;
using CognitoSample.Models;
using CognitoSample.Services;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;

namespace CognitoSample.Controllers;

public class AuthController : Controller
{
    private readonly ICognitoService _cognitoService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ICognitoService cognitoService, ILogger<AuthController> logger)
    {
        _cognitoService = cognitoService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult SignUp()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SignUp(SignUpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await _cognitoService.SignUpAsync(model.Email, model.Password);

            if (result.UserConfirmed)
            {
                TempData["SuccessMessage"] = "アカウントが作成されました。ログインしてください。";
                return RedirectToAction("SignIn");
            }

            TempData["InfoMessage"] = "確認コードをメールに送信しました。";
            return RedirectToAction("ConfirmSignUp", new { email = model.Email });
        }
        catch (UsernameExistsException)
        {
            ModelState.AddModelError("", "このメールアドレスは既に登録されています。");
            return View(model);
        }
        catch (InvalidPasswordException ex)
        {
            ModelState.AddModelError("Password", $"パスワードの要件を満たしていません: {ex.Message}");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sign up failed");
            ModelState.AddModelError("", "アカウント作成中にエラーが発生しました。");
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult ConfirmSignUp(string email)
    {
        return View(new ConfirmSignUpViewModel { Email = email });
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmSignUp(ConfirmSignUpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _cognitoService.ConfirmSignUpAsync(model.Email, model.ConfirmationCode);
            TempData["SuccessMessage"] = "メールアドレスの確認が完了しました。ログインしてください。";
            return RedirectToAction("SignIn");
        }
        catch (CodeMismatchException)
        {
            ModelState.AddModelError("ConfirmationCode", "確認コードが正しくありません。");
            return View(model);
        }
        catch (ExpiredCodeException)
        {
            ModelState.AddModelError("ConfirmationCode", "確認コードの有効期限が切れています。");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Confirm sign up failed");
            ModelState.AddModelError("", "確認処理中にエラーが発生しました。");
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult SignIn()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SignIn(SignInViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await _cognitoService.SignInAsync(model.Email, model.Password);

            if (result.Success)
            {
                HttpContext.Session.SetString("AccessToken", result.AccessToken!);
                HttpContext.Session.SetString("IdToken", result.IdToken!);
                HttpContext.Session.SetString("RefreshToken", result.RefreshToken!);
                HttpContext.Session.SetString("UserEmail", model.Email);

                TempData["SuccessMessage"] = "ログインしました。";
                return RedirectToAction("Profile");
            }

            if (result.ChallengeName == "SOFTWARE_TOKEN_MFA")
            {
                return RedirectToAction("MfaChallenge", new { session = result.Session, email = model.Email });
            }

            if (result.ChallengeName == "MFA_SETUP")
            {
                var qrCodeUri = $"otpauth://totp/CognitoSample:{model.Email}?secret={result.SecretCode}&issuer=CognitoSample";
                return RedirectToAction("MfaSetup", new
                {
                    secretCode = result.SecretCode,
                    qrCodeUri = qrCodeUri,
                    session = result.Session,
                    email = model.Email
                });
            }

            ModelState.AddModelError("", "ログインに失敗しました。");
            return View(model);
        }
        catch (UserNotConfirmedException)
        {
            TempData["InfoMessage"] = "メールアドレスの確認が必要です。";
            return RedirectToAction("ConfirmSignUp", new { email = model.Email });
        }
        catch (NotAuthorizedException)
        {
            ModelState.AddModelError("", "メールアドレスまたはパスワードが正しくありません。");
            return View(model);
        }
        catch (UserNotFoundException)
        {
            ModelState.AddModelError("", "メールアドレスまたはパスワードが正しくありません。");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sign in failed");
            ModelState.AddModelError("", "ログイン中にエラーが発生しました。");
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult MfaChallenge(string session, string email)
    {
        return View(new MfaChallengeViewModel { Session = session, Email = email });
    }

    [HttpPost]
    public async Task<IActionResult> MfaChallenge(MfaChallengeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await _cognitoService.RespondToMfaChallengeAsync(model.Session, model.MfaCode, model.Email);

            if (result.Success)
            {
                HttpContext.Session.SetString("AccessToken", result.AccessToken!);
                HttpContext.Session.SetString("IdToken", result.IdToken!);
                HttpContext.Session.SetString("RefreshToken", result.RefreshToken!);
                HttpContext.Session.SetString("UserEmail", model.Email);

                TempData["SuccessMessage"] = "ログインしました。";
                return RedirectToAction("Profile");
            }

            ModelState.AddModelError("", "MFA認証に失敗しました。");
            return View(model);
        }
        catch (CodeMismatchException)
        {
            ModelState.AddModelError("MfaCode", "MFAコードが正しくありません。");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MFA challenge failed");
            ModelState.AddModelError("", "MFA認証中にエラーが発生しました。");
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult MfaSetup(string secretCode, string qrCodeUri, string session, string email)
    {
        return View(new MfaSetupViewModel
        {
            SecretCode = secretCode,
            QrCodeUri = qrCodeUri,
            Session = session,
            Email = email
        });
    }

    [HttpPost]
    public async Task<IActionResult> MfaSetup(MfaSetupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var verifyRequest = new RespondToAuthChallengeRequest
            {
                ClientId = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["AWS:Cognito:ClientId"],
                ChallengeName = ChallengeNameType.MFA_SETUP,
                Session = model.Session,
                ChallengeResponses = new Dictionary<string, string>
                {
                    { "USERNAME", model.Email },
                    { "SOFTWARE_TOKEN_MFA_CODE", model.VerificationCode }
                }
            };

            var client = new Amazon.CognitoIdentityProvider.AmazonCognitoIdentityProviderClient(
                Amazon.RegionEndpoint.GetBySystemName(
                    HttpContext.RequestServices.GetRequiredService<IConfiguration>()["AWS:Cognito:Region"] ?? "ap-northeast-1"
                )
            );

            var response = await client.RespondToAuthChallengeAsync(verifyRequest);

            if (response.AuthenticationResult != null)
            {
                HttpContext.Session.SetString("AccessToken", response.AuthenticationResult.AccessToken);
                HttpContext.Session.SetString("IdToken", response.AuthenticationResult.IdToken);
                HttpContext.Session.SetString("RefreshToken", response.AuthenticationResult.RefreshToken);
                HttpContext.Session.SetString("UserEmail", model.Email);

                TempData["SuccessMessage"] = "MFAの設定が完了しました。";
                return RedirectToAction("Profile");
            }

            ModelState.AddModelError("", "MFAの設定に失敗しました。");
            return View(model);
        }
        catch (CodeMismatchException)
        {
            ModelState.AddModelError("VerificationCode", "確認コードが正しくありません。");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MFA setup failed");
            ModelState.AddModelError("", "MFA設定中にエラーが発生しました。");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var accessToken = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(accessToken))
        {
            return RedirectToAction("SignIn");
        }

        try
        {
            var user = await _cognitoService.GetUserAsync(accessToken);
            var email = user.UserAttributes.FirstOrDefault(a => a.Name == "email")?.Value ?? "";

            return View(new UserProfileViewModel
            {
                Email = email,
                Username = user.Username,
                MfaEnabled = user.UserMFASettingList?.Contains("SOFTWARE_TOKEN_MFA") ?? false
            });
        }
        catch (NotAuthorizedException)
        {
            HttpContext.Session.Clear();
            TempData["InfoMessage"] = "セッションが期限切れです。再度ログインしてください。";
            return RedirectToAction("SignIn");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get profile failed");
            return RedirectToAction("SignIn");
        }
    }

    [HttpGet]
    public async Task<IActionResult> SetupMfa()
    {
        var accessToken = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(accessToken))
        {
            return RedirectToAction("SignIn");
        }

        try
        {
            var response = await _cognitoService.SetupMfaAsync(accessToken);
            var email = HttpContext.Session.GetString("UserEmail") ?? "";
            var qrCodeUri = $"otpauth://totp/CognitoSample:{email}?secret={response.SecretCode}&issuer=CognitoSample";

            return View(new MfaSetupViewModel
            {
                SecretCode = response.SecretCode,
                QrCodeUri = qrCodeUri
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MFA setup initiation failed");
            TempData["ErrorMessage"] = "MFA設定の開始に失敗しました。";
            return RedirectToAction("Profile");
        }
    }

    [HttpPost]
    public async Task<IActionResult> SetupMfa(MfaSetupViewModel model)
    {
        var accessToken = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(accessToken))
        {
            return RedirectToAction("SignIn");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var verifyResponse = await _cognitoService.VerifyMfaSetupAsync(accessToken, model.VerificationCode);

            if (verifyResponse.Status == Amazon.CognitoIdentityProvider.VerifySoftwareTokenResponseType.SUCCESS)
            {
                await _cognitoService.SetMfaPreferenceAsync(accessToken);
                TempData["SuccessMessage"] = "MFAの設定が完了しました。";
                return RedirectToAction("Profile");
            }

            ModelState.AddModelError("", "MFAの検証に失敗しました。");
            return View(model);
        }
        catch (CodeMismatchException)
        {
            ModelState.AddModelError("VerificationCode", "確認コードが正しくありません。");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MFA verification failed");
            ModelState.AddModelError("", "MFA設定中にエラーが発生しました。");
            return View(model);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        var accessToken = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(accessToken))
        {
            try
            {
                await _cognitoService.SignOutAsync(accessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sign out failed");
            }
        }

        HttpContext.Session.Clear();
        TempData["SuccessMessage"] = "ログアウトしました。";
        return RedirectToAction("SignIn");
    }
}
