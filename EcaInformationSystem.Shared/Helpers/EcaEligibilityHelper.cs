namespace EcaInformationSystem.Shared.Helpers
{
    // Single source of truth for the ECA (R.A. 11982) milestone-year and
    // program-start-cutoff rules, shared by the client form and the server
    // so both agree on the same edge case: a grantee whose 80th birthday
    // fell BEFORE the program's actual start date (not just before the
    // start YEAR) never had a window to enter the program.
    public static class EcaEligibilityHelper
    {
        public static readonly DateTime ProgramStartDate = new(2024, 3, 17);

        private static readonly int[] Milestones = { 100, 95, 90, 85, 80 };

        public static int ComputeAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
        }

        // Highest milestone (80/85/90/95/100) the grantee has reached as of
        // `asOf` (defaults to today), or 0 if none reached yet. A milestone
        // only counts if its birthday falls on/after ProgramStartDate —
        // reaching 80 on, say, Jan 2024 does NOT count, since the program
        // didn't exist yet.
        public static int ComputeMilestoneYear(DateTime birthDate, DateTime? asOfOverride = null)
        {
            if (birthDate == default) return 0;

            var today = DateTime.Today;
            var asOf = asOfOverride.HasValue && asOfOverride.Value.Date > today
                ? asOfOverride.Value.Date
                : today;

            foreach (var milestone in Milestones)
            {
                var milestoneYear = birthDate.Year + milestone;
                if (milestoneYear < ProgramStartDate.Year) continue;

                var milestoneBirthday = new DateTime(milestoneYear, birthDate.Month, birthDate.Day);
                if (milestoneBirthday < ProgramStartDate) continue;
                if (milestoneBirthday <= asOf) return milestoneYear;
            }

            return 0;
        }

        // True when the grantee is already 80+ but has never reached a
        // qualifying milestone — i.e. their 80th (and every later) birthday
        // fell before the program existed. These grantees have no path into
        // the program; ever.
        public static bool MissedProgramStartCutoff(DateTime birthDate)
        {
            if (birthDate == default) return false;
            return ComputeAge(birthDate) >= 80 && ComputeMilestoneYear(birthDate) == 0;
        }
    }
}
