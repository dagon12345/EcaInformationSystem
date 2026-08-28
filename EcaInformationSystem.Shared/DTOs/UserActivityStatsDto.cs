namespace EcaInformationSystem.Shared.DTOs
{
    // Repository-level result for LogRepository.GetUserActivityStatsAsync —
    // UserTransactionTierService copies these straight onto UserTransactionTierDto.
    public class UserActivityStatsDto
    {
        public int LoginCount { get; set; }
        public int DataCreatedCount { get; set; }
        public int DataEditedCount { get; set; }
        public int DocumentsTrackedCount { get; set; }

        // Distinct UTC dates (no time component) this user had at least one
        // qualifying transaction this season — feeds StreakHelper.
        public List<DateTime> ActiveDates { get; set; } = new();
    }
}
