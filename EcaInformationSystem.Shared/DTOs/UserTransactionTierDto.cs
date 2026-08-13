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

        // ── Weekly leaderboard season — see LeaderboardSeasonService ───────────
        public int SeasonNumber { get; set; }
        public DateTime NextAutoResetAtUtc { get; set; }
        // Cumulative Top-3 finishes across all past seasons.
        public int TotalWins { get; set; }
        // Last ENDED season's result — null until this user has been through
        // at least one reset. LastSeasonRank stays null if they had zero
        // qualifying activity that season even though a season number/count exist.
        public int? LastSeasonNumber { get; set; }
        public int? LastSeasonRank { get; set; }
        public int? LastSeasonTransactionCount { get; set; }
        // Server-generated nudge — what to do this week to climb the board
        // (correct/follow-up on grantee records, etc.), tailored to last week's result.
        public string MotivationMessage { get; set; } = string.Empty;
    }
}
