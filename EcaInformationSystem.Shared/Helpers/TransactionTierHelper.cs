namespace EcaInformationSystem.Shared.Helpers
{
    // Gamified "how much have you actually done in this system" ladder — driven
    // by the count of Log entries a user has generated since the current weekly
    // season started (see LogRepository.TransactionLogsQuery, excludes bulk-op
    // activity but does count Logins). Levels/thresholds and naming are a
    // deliberate design choice, not derived from anything else.
    public static class TransactionTierHelper
    {
        public record TierDefinition(int Level, string Name, int MinCount);

        // Rescaled for the weekly leaderboard reset (see LeaderboardSeasonService) —
        // the count these thresholds measure against is now "since this week's
        // season started", not an ever-growing all-time total, and average
        // weekly activity per active encoder is ~5,000 transactions. God Tier
        // is capped at that average, so hitting a full typical week's worth of
        // activity is already the top of the ladder — the tiers below break
        // that climb into achievable weekly milestones instead of everyone
        // being stuck at Rookie under the old all-time-tuned numbers.
        public static readonly List<TierDefinition> Tiers = new()
        {
            new(1, "Rookie",   0),
            new(2, "Bronze",   50),
            new(3, "Silver",   200),
            new(4, "Gold",     500),
            new(5, "Platinum", 1000),
            new(6, "Diamond",  1600),
            new(7, "Master",   2400),
            new(8, "Legend",   3600),
            new(9, "God Tier", 5000),
        };

        public static TierDefinition GetCurrentTier(int transactionCount) =>
            Tiers.Last(t => transactionCount >= t.MinCount);

        public static TierDefinition? GetNextTier(int transactionCount)
        {
            var current = GetCurrentTier(transactionCount);
            return Tiers.FirstOrDefault(t => t.MinCount > current.MinCount);
        }

        // 0-100, or 100 flat once the max tier is reached (nothing left to climb toward).
        public static double GetProgressPercent(int transactionCount)
        {
            var current = GetCurrentTier(transactionCount);
            var next = GetNextTier(transactionCount);
            if (next is null) return 100;

            var span = next.MinCount - current.MinCount;
            if (span <= 0) return 100;

            var into = transactionCount - current.MinCount;
            return Math.Clamp(into * 100.0 / span, 0, 100);
        }
    }
}
