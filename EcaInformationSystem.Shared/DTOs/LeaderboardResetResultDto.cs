namespace EcaInformationSystem.Shared.DTOs
{
    // Returned right after a reset (manual or automatic) so the caller/broadcast
    // can show a "Season N ended — congrats to the top 3!" style summary.
    public class LeaderboardResetResultDto
    {
        public int EndedSeasonNumber { get; set; }
        public int NewSeasonNumber { get; set; }
        public string ResetType { get; set; } = string.Empty; // "Automatic" | "Manual"
        public string? ResetBy { get; set; }
        public DateTime ResetAtUtc { get; set; }
        public List<LeaderboardTopFinisherDto> TopThree { get; set; } = new();

        // The auto-posted feed announcement for this reset's top 3 — null if
        // nobody had qualifying activity this season (nothing worth posting).
        public PostDto? PodiumPost { get; set; }
    }

    public class LeaderboardTopFinisherDto
    {
        public int Rank { get; set; }
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
    }
}
