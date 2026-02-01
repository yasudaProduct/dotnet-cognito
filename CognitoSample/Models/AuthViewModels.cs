using System.ComponentModel.DataAnnotations;

namespace CognitoSample.Models;

public class SignUpViewModel
{
    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "パスワードは必須です")]
    [MinLength(8, ErrorMessage = "パスワードは8文字以上で入力してください")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "確認用パスワードは必須です")]
    [Compare("Password", ErrorMessage = "パスワードが一致しません")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ConfirmSignUpViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "確認コードは必須です")]
    public string ConfirmationCode { get; set; } = string.Empty;
}

public class SignInViewModel
{
    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "パスワードは必須です")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class MfaChallengeViewModel
{
    public string Session { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "MFAコードは必須です")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "MFAコードは6桁で入力してください")]
    public string MfaCode { get; set; } = string.Empty;
}

public class MfaSetupViewModel
{
    public string SecretCode { get; set; } = string.Empty;
    public string QrCodeUri { get; set; } = string.Empty;
    public string Session { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "確認コードは必須です")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "確認コードは6桁で入力してください")]
    public string VerificationCode { get; set; } = string.Empty;
}

public class UserProfileViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool MfaEnabled { get; set; }
}
