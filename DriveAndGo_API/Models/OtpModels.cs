namespace DriveAndGo_API.Models
{
    public class Verify2FaRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }

    public class SendResetOtpRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordWithOtpRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class RequestPasswordChangeOtpRequest
    {
        public int UserId { get; set; }
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordWithOtpRequest
    {
        public int UserId { get; set; }
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }

    public class UpdateSecuritySettingsRequest
    {
        public int UserId { get; set; }
        public bool? TwoFactorEnabled { get; set; }
        public bool? LoginAlertsEnabled { get; set; }
        public bool? PinRequired { get; set; }
    }
}
