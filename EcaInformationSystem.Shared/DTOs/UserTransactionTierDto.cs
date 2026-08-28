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

        // ── Activity breakdown, all scoped to the CURRENT (weekly) season window,
        // same as TransactionCount above — shown as a compact stat row on the
        // profile page. Classified from the existing Log.Activity text (a Log row
        // already starting with "Created"/"Added" counts as DataCreatedCount, one
        // starting with "Updated"/"Edited" as DataEditedCount) rather than a new
        // Log.Category, so this works retroactively on logs recorded before this
        // feature existed too — see LogRepository.GetUserActivityStatsAsync. ─────
        public int LoginCount { get; set; }
        public int DataCreatedCount { get; set; }
        public int DataEditedCount { get; set; }
        public int DocumentsTrackedCount { get; set; }

        // ── Streak — consecutive days (ending today) with at least one
        // qualifying transaction. See StreakHelper. ────────────────────────────
        public int CurrentStreakDays { get; set; }
        public bool IsOnFire { get; set; }

        // Transactions per elapsed day of the CURRENT season (capped at 7,
        // since seasons are weekly) — a "pace" number, not a flat 7-day
        // average, so it's meaningful from day one of a fresh week instead of
        // looking artificially low. Compared against last week's daily pace
        // (LastSeasonTransactionCount / 7) to produce TrendDirection.
        public double WeeklyAverage { get; set; }
        // "Up" | "Down" | "Flat" — null-safe default "Flat" when there's no
        // last-season data yet to compare against.
        public string TrendDirection { get; set; } = "Flat";
    }
}
