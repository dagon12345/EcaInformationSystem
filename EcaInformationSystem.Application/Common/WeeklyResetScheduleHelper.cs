namespace EcaInformationSystem.Application.Common
{
    // Shared "next Sunday 11:59 PM Philippine Time" math — used by both the
    // automatic weekly-reset background job (to know how long to sleep) and
    // LeaderboardSeasonService (to show the countdown in the UI), so the two
    // can never drift apart.
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
    }
}
