using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using CognitoMfaSample.Filters;
using CognitoMfaSample.Models;
using CognitoMfaSample.Services;

namespace CognitoMfaSample.Controllers;

public class AuthController : Controller
{
    private readonly IUserService _userService;
    private readonly ICognitoMfaService _cognitoMfaService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService,
        ICognitoMfaService cognitoMfaService,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _cognitoMfaService = cognitoMfaService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult SignUp()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SignUp(SignUpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // EFでユーザー作成
        var (efSuccess, efError) = await _userService.CreateUserAsync(model.Email, model.Password);
        if (!efSuccess)
        {
            ModelState.AddModelError(string.Empty, efError!);
            return View(model);
        }

        // Cognitoでユーザー作成（MFA用）
        var (cognitoSuccess, cognitoError) = await _cognitoMfaService.CreateCognitoUserAsync(model.Email, model.Password);
        if (!cognitoSuccess)
        {
            ModelState.AddModelError(string.Empty, cognitoError!);
            return View(model);
        }

        TempData["SuccessMessage"] = "アカウントを作成しました。メールに送信された確認コードを入力してください。";
        return RedirectToAction("ConfirmSignUp", new { email = model.Email });
    }

    [HttpGet]
    public IActionResult ConfirmSignUp(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return RedirectToAction("SignUp");
        }
        return View(new ConfirmSignUpViewModel { Email = email });
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmSignUp(ConfirmSignUpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error) = await _cognitoMfaService.ConfirmSignUpAsync(model.Email, model.ConfirmationCode);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error!);
            return View(model);
        }

        TempData["SuccessMessage"] = "メールアドレスの確認が完了しました。ログインしてください。";
        return RedirectToAction("SignIn");
    }

    [HttpGet]
    public IActionResult SignIn()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SignIn(SignInViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // まずEFで認証
        var user = await _userService.AuthenticateAsync(model.Email, model.Password);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "メールアドレスまたはパスワードが正しくありません");
            return View(model);
        }

        // CognitoでMFAを開始
        var mfaResult = await _cognitoMfaService.InitiateMfaAsync(model.Email, model.Password);

        if (mfaResult.ChallengeName == "EMAIL_OTP")
        {
            _logger.LogInformation("Email OTP challenge detected for {Email}", model.Email);
            TempData["InfoMessage"] = "ワンタイムパスワードをメールに送信しました。";
            return RedirectToAction("EmailOtpChallenge", new { session = mfaResult.Session, email = model.Email });
        }

        if (mfaResult.Success)
        {
            // MFAなしで認証完了（通常は発生しない）
            await SignInUserAsync(model.Email);
            return RedirectToAction("Index", "Dashboard");
        }

        ModelState.AddModelError(string.Empty, mfaResult.ErrorMessage ?? "認証に失敗しました");
        return View(model);
    }

    [HttpGet]
    public IActionResult EmailOtpChallenge(string session, string email)
    {
        if (string.IsNullOrEmpty(session) || string.IsNullOrEmpty(email))
        {
            return RedirectToAction("SignIn");
        }
        return View(new EmailOtpChallengeViewModel { Session = session, Email = email });
    }

    [HttpPost]
    public async Task<IActionResult> EmailOtpChallenge(EmailOtpChallengeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _cognitoMfaService.RespondToEmailOtpAsync(model.Session, model.Email, model.OtpCode);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "認証に失敗しました");
            return View(model);
        }

        // Cookie認証でサインイン
        await SignInUserAsync(model.Email);

        TempData["SuccessMessage"] = "ログインしました";
        return RedirectToAction("Index", "Dashboard");
    }

    [AuthRequired]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
        var user = await _userService.GetUserByEmailAsync(email);

        return View(new UserProfileViewModel
        {
            Email = user?.Email ?? email
        });
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = "ログアウトしました";
        return RedirectToAction("Index", "Home");
    }

    private async Task SignInUserAsync(string email)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, email)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
    }
}
