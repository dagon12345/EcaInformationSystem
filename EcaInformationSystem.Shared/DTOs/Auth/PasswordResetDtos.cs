namespace EcaInformationSystem.Shared.DTOs.Auth
{
    public class RequestPasswordResetDto
    {
        public string UserName { get; set; } = string.Empty;
    }

    public class ResetPasswordDto
    {
        public string UserName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ApprovePasswordResetDto
    {
        public string? Remarks { get; set; }
    }

    public class RejectPasswordResetDto
    {
        public string? Remarks { get; set; }
    }

    public class PasswordResetRequestListDto
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public string? Remarks { get; set; }
        public DateTime? CodeExpiresAt { get; set; }
    }

    public class ApprovePasswordResetResultDto
    {
        public string Code { get; set; } = string.Empty; // plain — shown once to the admin
        public DateTime ExpiresAt { get; set; }
    }
}
