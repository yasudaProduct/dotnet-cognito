using System.ComponentModel.DataAnnotations;

namespace CognitoMfaSample.Models;

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

public class EmailOtpChallengeViewModel
{
    public string Session { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "ワンタイムパスワードは必須です")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "ワンタイムパスワードは6桁で入力してください")]
    public string OtpCode { get; set; } = string.Empty;
}

public class UserProfileViewModel
{
    public string Email { get; set; } = string.Empty;
}
