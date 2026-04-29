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
        public static AuthResult Failed(string message)
        {
            return new AuthResult
            {
                Success = false,
                Message = message
            };
        }
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
    }
}
