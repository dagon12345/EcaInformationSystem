namespace EcaInformationSystem.Shared.DTOs
{
    public class BulkUpdateRefNumberRequestDto
    {
        public List<Guid> Ids { get; set; } = new();
        public int Quarter { get; set; }
        public string Batch { get; set; } = string.Empty;
        public int RefYear { get; set; }
    }
}