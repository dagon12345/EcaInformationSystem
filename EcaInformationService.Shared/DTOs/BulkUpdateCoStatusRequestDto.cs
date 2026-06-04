namespace EcaInformationService.Shared.DTOs
{
    public class BulkUpdateCoStatusRequestDto
    {
        public List<Guid> Ids { get; set; } = new();
        public int? CoStatus { get; set; }
        public DateTime? CoDateEndorsed { get; set; }
        public DateTime? CoDateApproved { get; set; }
        public Dictionary<Guid, byte[]> RowVersions { get; set; } = new();
    }
}