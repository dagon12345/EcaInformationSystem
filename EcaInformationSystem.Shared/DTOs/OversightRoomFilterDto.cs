namespace EcaInformationSystem.Shared.DTOs
{
    public class OversightRoomFilterDto
    {
        public string? SearchTerm { get; set; } // matches either participant's name
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
