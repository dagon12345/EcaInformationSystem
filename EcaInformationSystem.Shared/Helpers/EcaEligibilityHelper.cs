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
        private static readonly int[] MilestonesAscending = { 80, 85, 90, 95, 100 };

        public static int ComputeAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
        }

        // ✅ NEW — a grantee born February 29 has no literal birthday in the
        // three years out of four that aren't leap years. `new DateTime(...)`
        // throws ArgumentOutOfRangeException for e.g. (2025, 2, 29); this
        // clamps to the last real day of that month instead (Feb 28), the
        // conventional way to handle a leap-day birthday. Every milestone
        // date built below goes through this instead of the constructor
        // directly.
        private static DateTime SafeDate(int year, int month, int day)
            => new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

        // Highest milestone (80/85/90/95/100) the grantee has reached as of
        // `asOf` (defaults to today), or 0 if none reached yet. A milestone
        // only counts if its birthday falls on/after ProgramStartDate —
        // reaching 80 on, say, Jan 2024 does NOT count, since the program
        // didn't exist yet. Applied uniformly to every milestone, including
        // 100 — a pre-cutoff 100th birthday doesn't count either, same as
        // 80/85/90/95.
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

                var milestoneBirthday = SafeDate(milestoneYear, birthDate.Month, birthDate.Day);
                if (milestoneBirthday < ProgramStartDate) continue;
                if (milestoneBirthday <= asOf) return milestoneYear;
            }

            return 0;
        }

        // True when the grantee has a milestone (past, present, or future)
        // whose birthday falls on/after ProgramStartDate — i.e. NOT
        // permanently excluded. Same cutoff rule as ComputeMilestoneYear,
        // applied uniformly to every milestone including 100 — but unlike
        // ComputeMilestoneYear this doesn't require the milestone to have
        // been reached YET, only that a valid one exists somewhere on their
        // timeline (past or future).
        private static bool HasAnyValidMilestone(DateTime birthDate)
        {
            foreach (var milestone in Milestones)
            {
                var milestoneYear = birthDate.Year + milestone;
                if (milestoneYear < ProgramStartDate.Year) continue;

                var milestoneBirthday = SafeDate(milestoneYear, birthDate.Month, birthDate.Day);
                if (milestoneBirthday < ProgramStartDate) continue;

                return true; // this milestone (reached or not yet) is valid
            }

            return false;
        }

        // ✅ FIXED — true only when NO milestone (80 through 100) can EVER
        // validly land this grantee in the program, i.e. every one of their
        // milestone birthdays falls before ProgramStartDate. The cutoff is
        // applied the same way to every milestone, including 100 — a
        // pre-cutoff 100th birthday is excluded too, just like 80/85/90/95.
        //
        // This used to be `ComputeMilestoneYear(birthDate) == 0`, which also
        // returns 0 for a grantee who simply hasn't had their birthday yet
        // this year (e.g. today is August and their valid 2026 milestone
        // birthday is in September) — that grantee was wrongly shown as
        // "permanently ineligible / cutoff" when they're just pending, not
        // cut off. HasAnyValidMilestone (above) checks for a valid milestone
        // regardless of whether it's been reached yet, fixing that case
        // while still correctly excluding someone whose only remaining
        // possible milestones (up to and including 100) are all pre-cutoff.
        public static bool MissedProgramStartCutoff(DateTime birthDate)
        {
            if (birthDate == default) return false;
            return ComputeAge(birthDate) >= 80 && !HasAnyValidMilestone(birthDate);
        }

        // True when the grantee's 80th birthday (their nearest/earliest possible
        // milestone) fell before ProgramStartDate, but a LATER milestone
        // (85/90/95/100) is still valid — e.g. born 1944-03-16: their 80th
        // birthday was 2024-03-16, one day before the program started, but
        // their 85th (2029) is fine. Purely informational — unlike
        // MissedProgramStartCutoff, this does NOT make them permanently
        // ineligible, so it must never affect IsEligible/PaymentStatus.
        // Milestone birthdays are strictly increasing with birth date, so if
        // the 80th missed the cutoff every later one by definition didn't —
        // this case can only ever be about the 80th specifically.
        public static bool MissedNearestMilestoneOnly(DateTime birthDate)
        {
            if (birthDate == default) return false;
            if (ComputeAge(birthDate) < 80) return false;
            if (MissedProgramStartCutoff(birthDate)) return false;

            var eightiethBirthday = SafeDate(birthDate.Year + 80, birthDate.Month, birthDate.Day);
            return eightiethBirthday < ProgramStartDate;
        }

        // The next milestone (age, year) a grantee flagged by
        // MissedNearestMilestoneOnly will actually qualify under — e.g. (85,
        // 2029) for someone born 1944-03-16. Returns (0, 0) when not applicable.
        public static (int Age, int Year) NextValidMilestoneAfterCutoffMiss(DateTime birthDate)
        {
            if (!MissedNearestMilestoneOnly(birthDate)) return (0, 0);

            foreach (var milestone in MilestonesAscending)
            {
                var year = birthDate.Year + milestone;
                var milestoneBirthday = SafeDate(year, birthDate.Month, birthDate.Day);
                if (milestoneBirthday >= ProgramStartDate) return (milestone, year);
            }

            return (0, 0);
        }
    }
}
