public class LoginOutcome
{
    public bool IsSuccess { get; set; }
    public bool MfaRequired { get; set; }
    public string? MfaUserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? AttemptsRemaining { get; set; }
    public bool IsLockedOut { get; set; }
    public int? RetryAfterSeconds { get; set; }
    public bool ShowMfaPrompt { get; set; }

    public static LoginOutcome Success(bool showMfaPrompt = false) => new() { IsSuccess = true, ShowMfaPrompt = showMfaPrompt };

    public static LoginOutcome NeedsMfa(string userId) => new()
    {
        MfaRequired = true,
        MfaUserId = userId
    };

    public static LoginOutcome Failure(string message, int? attemptsRemaining = null, bool isLockedOut = false, int? retryAfterSeconds = null)
        => new()
        {
            Message = message,
            AttemptsRemaining = attemptsRemaining,
            IsLockedOut = isLockedOut,
            RetryAfterSeconds = retryAfterSeconds
        };
}