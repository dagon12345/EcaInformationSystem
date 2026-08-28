namespace EcaInformationSystem.Shared.DTOs
{
    // Public, anonymous-safe leaderboard row — GET api/transaction-tier/public-leaderboard.
    // Deliberately smaller than UserLeaderboardEntryDto: no UserId (nothing to
    // link to without logging in), no real UserName, and DisplayName is
    // already masked server-side before this ever leaves the API.
    public class PublicLeaderboardEntryDto
    {
        public int Rank { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int TierLevel { get; set; }
        public string TierName { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public int CurrentStreakDays { get; set; }
        public bool IsOnFire { get; set; }
    }
}
