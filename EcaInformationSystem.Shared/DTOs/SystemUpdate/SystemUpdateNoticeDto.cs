namespace EcaInformationSystem.Shared.DTOs.SystemUpdate
{
    public class SystemUpdateNoticeDto
    {
        public Guid Id { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Changes { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public string PublishedByName { get; set; } = string.Empty;
    }
}
