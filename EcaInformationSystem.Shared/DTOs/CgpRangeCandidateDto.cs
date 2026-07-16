namespace EcaInformationSystem.Shared.DTOs
{
    public class CgpRangeCandidateDto
    {
        public Guid CgpGenerationId { get; set; }   // ✅ NEW — replaces CgpPrefix as the lookup key
        public string CgpPrefix { get; set; } = string.Empty;
        public int? CgpPageNumber { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public int MilestoneYear { get; set; }
    }
}