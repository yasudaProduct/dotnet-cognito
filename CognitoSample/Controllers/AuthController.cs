using Microsoft.AspNetCore.Mvc;
using CognitoSample.Models;
using CognitoSample.Services;
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

            if (result.ChallengeName == "EMAIL_OTP")
            {
                _logger.LogInformation("Email OTP challenge detected");
                TempData["InfoMessage"] = "ワンタイムパスワードをメールに送信しました。";
                return RedirectToAction("EmailOtpChallenge", new { session = result.Session, email = model.Email });
            }

            _logger.LogError("Unknown challenge detected: {ChallengeName}", result.ChallengeName);
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
    public IActionResult EmailOtpChallenge(string session, string email)
    {
        return View(new EmailOtpChallengeViewModel { Session = session, Email = email });
    }

    [HttpPost]
    public async Task<IActionResult> EmailOtpChallenge(EmailOtpChallengeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await _cognitoService.RespondToEmailOtpChallengeAsync(model.Session, model.OtpCode, model.Email);

            if (result.Success)
            {
                HttpContext.Session.SetString("AccessToken", result.AccessToken!);
                HttpContext.Session.SetString("IdToken", result.IdToken!);
                HttpContext.Session.SetString("RefreshToken", result.RefreshToken!);
                HttpContext.Session.SetString("UserEmail", model.Email);

                TempData["SuccessMessage"] = "ログインしました。";
                return RedirectToAction("Profile");
            }

            ModelState.AddModelError("", "認証に失敗しました。");
            return View(model);
        }
        catch (CodeMismatchException)
        {
            ModelState.AddModelError("OtpCode", "ワンタイムパスワードが正しくありません。");
            return View(model);
        }
        catch (ExpiredCodeException)
        {
            ModelState.AddModelError("OtpCode", "ワンタイムパスワードの有効期限が切れています。再度ログインしてください。");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email OTP challenge failed");
            ModelState.AddModelError("", "認証中にエラーが発生しました。");
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
                Username = user.Username
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
