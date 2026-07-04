namespace EcaInformationSystem.Shared.DTOs
{
    public class LogFilterDto
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? UserName { get; set; }   // optional — filter by who did it
        public string? Search { get; set; }     // optional — filter by activity text
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
