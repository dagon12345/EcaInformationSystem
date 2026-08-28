namespace EcaInformationSystem.Shared.Helpers
{
    // How many CONSECUTIVE days (ending today, UTC) a user has had at least one
    // qualifying transaction — powers the leaderboard's streak/"on fire" badge
    // and the profile page's stat row. Shared between UserTransactionTierService
    // (per-user tier card) and the leaderboard's per-row computation so both
    // agree on exactly the same rule.
    public static class StreakHelper
    {
        // A user is "on fire" once they've kept a 3+ day streak going — short
        // enough to be reachable within a single weekly season, long enough to
        // actually mean something rather than firing on day one.
        public const int OnFireThresholdDays = 3;

        public static int ComputeCurrentStreak(IEnumerable<DateTime> activeDatesUtc)
        {
            var dates = new HashSet<DateTime>(activeDatesUtc.Select(d => d.Date));
            if (dates.Count == 0) return 0;

            var day = DateTime.UtcNow.Date;

            // Nothing logged yet today shouldn't zero out a streak that's still
            // very much alive as of yesterday — only actually break once a full
            // day has passed with no activity at all.
            if (!dates.Contains(day))
                day = day.AddDays(-1);

            var streak = 0;
            while (dates.Contains(day))
            {
                streak++;
                day = day.AddDays(-1);
            }

            return streak;
        }

        public static bool IsOnFire(int currentStreakDays) => currentStreakDays >= OnFireThresholdDays;
    }
}
