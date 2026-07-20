namespace EcaInformationSystem.Application.Common.Models
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? FullName { get; set; }
        public string? Position { get; set; }
        public string Token { get; set; } = string.Empty; // This generates our token
        
        // ✅ NEW — attempt tracking for UI display
        public int? AttemptsRemaining { get; set; }
        public bool IsLockedOut { get; set; }
        public DateTime? LockoutEndsAt { get; set; }

        public bool MfaRequired { get; set; }
        public bool ShowMfaPrompt { get; set; }
        public static AuthResult Failed(string message, int? attemptsRemaining = null, bool isLockedOut = false, DateTime? lockoutEndsAt = null)
         => new()
         {
             Success = false,
             Message = message,
             AttemptsRemaining = attemptsRemaining,
             IsLockedOut = isLockedOut,
             LockoutEndsAt = lockoutEndsAt
         };
        public static AuthResult Passed(string message, string userId, string userName, string fullName, string position)
        {
            return new AuthResult
            {
                Success = true,
                Message = message,
                UserId = userId,
                UserName = userName,
                FullName = fullName,
                Position = position
            };
        }
        // ✅ NEW — password verified, but MFA code still required before a token is issued
        public static AuthResult NeedsMfa(string userId)
            => new()
            {
                Success = false,
                MfaRequired = true,
                UserId = userId,
                Message = "MFA verification required."
            };
    }
}
