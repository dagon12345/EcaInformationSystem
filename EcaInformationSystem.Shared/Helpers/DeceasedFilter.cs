namespace EcaInformationSystem.Shared.Helpers
{
    // The Deceased filter shared by the Beneficiary grid and the Statistics
    // page (BeneficiaryFilterDto.DeceasedStatus / StatisticsRequestDto.DeceasedStatus).
    // null = no filter.
    public static class DeceasedFilter
    {
        public const int Living = 0;
        public const int Deceased = 1;
        // Deceased AND died before reaching the milestone age the existing
        // milestone algorithm assigns them — see
        // EcaEligibilityHelper.DiedBeforeReachingMilestone.
        public const int DeceasedBeforeMilestone = 2;

        public static string Label(int status) => status switch
        {
            Living => "Living",
            Deceased => "Deceased",
            DeceasedBeforeMilestone => "Deceased before milestone age",
            _ => status.ToString()
        };

        // Part of every cache key that includes this filter. "Deceased before
        // milestone age" depends on today's date (which milestone the grantee
        // has reached as of today), so today is part of its token — a result
        // cached yesterday is never served after midnight.
        public static string CacheToken(int? status) => status switch
        {
            null => "null",
            DeceasedBeforeMilestone => $"{status}@{DateTime.Today:yyyyMMdd}",
            _ => status.Value.ToString()
        };
    }
}
