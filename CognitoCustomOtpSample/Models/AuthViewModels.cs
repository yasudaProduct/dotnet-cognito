using System.ComponentModel.DataAnnotations;

namespace CognitoCustomOtpSample.Models;

public class SignInViewModel
{
    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "パスワードは必須です")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class OtpChallengeViewModel
{
    [Required]
    public string Session { get; set; } = string.Empty;

    [Required]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "ワンタイムパスワードは必須です")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "ワンタイムパスワードは6桁で入力してください")]
    public string OtpCode { get; set; } = string.Empty;
}

public class UserProfileViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
