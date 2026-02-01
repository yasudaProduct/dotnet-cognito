using System.ComponentModel.DataAnnotations;

namespace CognitoEmailOtpSample.Models;

public class SignInViewModel
{
    [Required(ErrorMessage = "メールアドレスを入力してください")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "パスワードを入力してください")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
