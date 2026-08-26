namespace EcaInformationSystem.Client.Services
{
    // Static, offline list of Philippine national holidays used to shade the
    // Activity Calendar grid. Covers what can be determined by a fixed rule
    // (a fixed calendar date, "last Monday of August", or an offset from
    // Easter Sunday, computed via the standard Anonymous Gregorian algorithm)
    // plus a small manually-curated table for Chinese New Year, whose date is
    // fixed by the lunar calendar years in advance and published by PAGASA/
    // Malacañang well ahead of time.
    //
    // NOT covered: Eid'l Fitr and Eid'l Adha (Islamic calendar, confirmed only
    // by moon sighting shortly before the date) and any one-off special
    // non-working day the President proclaims for a specific year (e.g. an
    // extra bridge holiday). Those require a yearly proclamation lookup this
    // static table can't provide — update _chineseNewYear / add a proclamation
    // source if exact coverage for those becomes necessary.
    public static class PhilippineHolidays
    {
        public sealed record Holiday(DateTime Date, string Name, bool IsSpecial);

        // Year -> Chinese New Year date (Gregorian). Extend as new years are published.
        private static readonly Dictionary<int, DateTime> _chineseNewYear = new()
        {
            [2024] = new DateTime(2024, 2, 10),
            [2025] = new DateTime(2025, 1, 29),
            [2026] = new DateTime(2026, 2, 17),
            [2027] = new DateTime(2027, 2, 6),
            [2028] = new DateTime(2028, 1, 26),
            [2029] = new DateTime(2029, 2, 13),
            [2030] = new DateTime(2030, 2, 3),
        };

        public static List<Holiday> GetHolidays(int year)
        {
            var easterSunday = ComputeEasterSunday(year);
            var maundyThursday = easterSunday.AddDays(-3);
            var goodFriday = easterSunday.AddDays(-2);
            var blackSaturday = easterSunday.AddDays(-1);
            var lastMondayOfAugust = LastMondayOf(year, 8);

            var holidays = new List<Holiday>
            {
                // ── Regular holidays ──────────────────────────────────────
                new(new DateTime(year, 1, 1), "New Year's Day", false),
                new(maundyThursday, "Maundy Thursday", false),
                new(goodFriday, "Good Friday", false),
                new(new DateTime(year, 4, 9), "Araw ng Kagitingan", false),
                new(new DateTime(year, 5, 1), "Labor Day", false),
                new(new DateTime(year, 6, 12), "Independence Day", false),
                new(lastMondayOfAugust, "National Heroes Day", false),
                new(new DateTime(year, 11, 30), "Bonifacio Day", false),
                new(new DateTime(year, 12, 25), "Christmas Day", false),
                new(new DateTime(year, 12, 30), "Rizal Day", false),

                // ── Special (non-working) days ────────────────────────────
                new(new DateTime(year, 2, 25), "EDSA People Power Anniversary", true),
                new(blackSaturday, "Black Saturday", true),
                new(new DateTime(year, 8, 21), "Ninoy Aquino Day", true),
                new(new DateTime(year, 11, 1), "All Saints' Day", true),
                new(new DateTime(year, 11, 2), "All Souls' Day", true),
                new(new DateTime(year, 12, 8), "Feast of the Immaculate Conception", true),
                new(new DateTime(year, 12, 24), "Christmas Eve", true),
                new(new DateTime(year, 12, 31), "Last Day of the Year", true),
            };

            if (_chineseNewYear.TryGetValue(year, out var cny))
                holidays.Add(new Holiday(cny, "Chinese New Year", true));

            return holidays;
        }

        private static DateTime LastMondayOf(int year, int month)
        {
            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            var offset = ((int)lastDay.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return lastDay.AddDays(-offset);
        }

        // Anonymous Gregorian algorithm (Meeus/Jones/Butcher).
        private static DateTime ComputeEasterSunday(int year)
        {
            var a = year % 19;
            var b = year / 100;
            var c = year % 100;
            var d = b / 4;
            var e = b % 4;
            var f = (b + 8) / 25;
            var g = (b - f + 1) / 3;
            var h = (19 * a + b - d - g + 15) % 30;
            var i = c / 4;
            var k = c % 4;
            var l = (32 + 2 * e + 2 * i - h - k) % 7;
            var m = (a + 11 * h + 22 * l) / 451;
            var month = (h + l - 7 * m + 114) / 31;
            var day = (h + l - 7 * m + 114) % 31 + 1;
            return new DateTime(year, month, day);
        }
    }
}
