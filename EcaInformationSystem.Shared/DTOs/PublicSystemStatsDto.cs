namespace EcaInformationSystem.Shared.DTOs
{
    public class PublicSystemStatsDto
    {
        public int GranteeCount { get; set; }
        public int TransactionCount { get; set; }
        public int LivenessVerifiedCount { get; set; }
        public int ReadyForEftCount { get; set; }
    }
}
