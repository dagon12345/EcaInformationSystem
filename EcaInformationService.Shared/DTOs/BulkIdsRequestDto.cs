namespace EcaInformationSystem.Shared.DTOs
{
    public class BulkIdsRequestDto
    {
        public List<Guid> Ids { get; set; } = new();
    }
}
