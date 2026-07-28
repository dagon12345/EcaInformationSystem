namespace EcaInformationSystem.Shared.DTOs
{
    public class UpcomingBirthdayDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Position { get; set; }
        public string? RegionName { get; set; }
        public DateTime BirthDate { get; set; }

        // 0 = today
        public int DaysUntil { get; set; }
        public int TurningAge { get; set; }
    }
}
