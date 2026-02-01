using System.ComponentModel.DataAnnotations;

namespace CognitoEmailOtpSample.Models;

public class OtpChallengeViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Session { get; set; } = string.Empty;

    [Required(ErrorMessage = "OTP コードを入力してください")]
    [StringLength(8, MinimumLength = 6, ErrorMessage = "OTP コードは6桁または8桁で入力してください")]
    [RegularExpression(@"^\d{6,8}$", ErrorMessage = "OTP コードは6～8桁の数字で入力してください")]
    public string OtpCode { get; set; } = string.Empty;
}
