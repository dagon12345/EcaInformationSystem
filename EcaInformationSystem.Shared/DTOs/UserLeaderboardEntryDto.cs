namespace EcaInformationSystem.Shared.DTOs
{
    // One row of the transaction-tier leaderboard shown on the feed page.
    public class UserLeaderboardEntryDto
    {
        public int Rank { get; set; }
        public Guid? UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Position { get; set; }
        // Nationwide leaderboard — every region is already merged into one
        // board, this is just shown as a small tag on each row/card so a
        // name from another region reads as "someone new", not a mystery.
        public string? RegionName { get; set; }
        public int TransactionCount { get; set; }
        public int TierLevel { get; set; }
        public string TierName { get; set; } = string.Empty;
        public bool IsMe { get; set; }

        // Consecutive-day activity streak this season — see StreakHelper.
        public int CurrentStreakDays { get; set; }
        public bool IsOnFire { get; set; }
    }
}
