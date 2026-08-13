namespace EcaInformationSystem.Domain.Entities
{
    // One row per weekly transaction-leaderboard season. Exactly one row has
    // EndedAtUtc == null at any time — the currently-active season, whose
    // StartedAtUtc is the dynamic replacement for the old hardcoded
    // LogRepository.TransactionCountingStartUtc constant. SeasonNumber is
    // sequential (1, 2, 3, ...) so the UI can show "Season N" without needing
    // a full per-user history table.
    public class LeaderboardSeason
    {
        public Guid Id { get; set; }
        public int SeasonNumber { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }
        // "Automatic" (weekly background job) or "Manual" (SuperAdmin reset button)
        public string ResetType { get; set; } = "Automatic";
        // Full name of the SuperAdmin who triggered a manual reset; null for automatic resets
        public string? ResetBy { get; set; }
    }
}
