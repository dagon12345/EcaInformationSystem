namespace EcaInformationSystem.Shared.DTOs
{
    public class MfaSetupResult
    {
        public string Secret { get; set; } = string.Empty; // plain, shown once for manual entry
        public string QrCodeImageBase64 { get; set; } = string.Empty;
    }

    public class ConfirmMfaRequest
    {
        public string Code { get; set; } = string.Empty;
    }

    public class VerifyMfaRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
    public class ResetMfaRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
    }
}
