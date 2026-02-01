using System.ComponentModel.DataAnnotations;

namespace CognitoEmailOtpSample.Models;

public class OtpChallengeViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Session { get; set; } = string.Empty;

    [Required(ErrorMessage = "OTP コードを入力してください")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP コードは6桁です")]
    public string OtpCode { get; set; } = string.Empty;
}
