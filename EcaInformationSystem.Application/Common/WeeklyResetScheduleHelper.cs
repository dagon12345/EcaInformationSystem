namespace EcaInformationSystem.Application.Common
{
    // Shared "Sunday 11:59 PM Philippine Time" math — used by both the
    // automatic weekly-reset background job (to decide whether a reset is due)
    // and LeaderboardSeasonService (to show the countdown in the UI), so the
    // two can never drift apart.
    public static class WeeklyResetScheduleHelper
    {
        private static readonly TimeZoneInfo PhilippineTimeZone = ResolvePhilippineTimeZone();

        private static TimeZoneInfo ResolvePhilippineTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); // Linux/macOS IANA id
            }
            catch (TimeZoneNotFoundException)
            {
                // Windows uses a different id set. The Philippines doesn't
                // observe DST, so Singapore Standard Time (fixed UTC+8) is an
                // exact equivalent, not an approximation.
                return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            }
        }

        // Next Sunday 11:59 PM Philippine Time strictly after `fromUtc`, returned in UTC.
        public static DateTime NextSundayElevenFiftyNinePmUtc(DateTime fromUtc)
        {
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, PhilippineTimeZone);
            var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)nowLocal.DayOfWeek + 7) % 7;
            var candidateLocal = nowLocal.Date.AddDays(daysUntilSunday).AddHours(23).AddMinutes(59);

            if (candidateLocal <= nowLocal)
                candidateLocal = candidateLocal.AddDays(7);

            return TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(candidateLocal, DateTimeKind.Unspecified),
                PhilippineTimeZone);
        }

        // When the season that began at `seasonStartedAtUtc` is due to end: the
        // first Sunday 11:59 PM Philippine Time after it began. A season that
        // started mid-week (a manual reset) ends that coming Sunday; one that
        // started right after a Sunday reset ends the following Sunday.
        public static DateTime SeasonDueAtUtc(DateTime seasonStartedAtUtc)
            => NextSundayElevenFiftyNinePmUtc(seasonStartedAtUtc);

        // True once the active season has passed its due time. This MUST be
        // measured from when the season STARTED, not from "now": the next
        // Sunday 11:59 PM computed from the current moment is, by definition,
        // always still in the future, so comparing "now" to it can never be
        // true — which is exactly how the automatic reset used to never fire.
        // Anchored to the season start it stays true until the reset happens,
        // so a reset that was missed (the app pool was idle at the time) is
        // still caught on the very next check instead of skipped.
        public static bool IsResetDue(DateTime seasonStartedAtUtc, DateTime nowUtc)
            => nowUtc >= SeasonDueAtUtc(seasonStartedAtUtc);
    }
}
