namespace EcaInformationSystem.Shared.DTOs
{
    public class UserTransactionTierDto
    {
        public string UserName { get; set; } = string.Empty;
        // Leaderboard position among all real (non-"System") users with at
        // least one qualifying transaction — null when viewing "/me" wasn't
        // asked to include it, or the user has zero qualifying transactions
        // and therefore isn't ranked at all.
        public int? Rank { get; set; }
        public int TransactionCount { get; set; }
        public int TierLevel { get; set; }
        public string TierName { get; set; } = string.Empty;
        public int TierMinCount { get; set; }
        public int? NextTierMinCount { get; set; } // null once at the max tier
        public string? NextTierName { get; set; }
        public double ProgressPercent { get; set; }
    }
}
