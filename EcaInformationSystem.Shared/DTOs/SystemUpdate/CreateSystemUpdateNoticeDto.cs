namespace EcaInformationSystem.Shared.DTOs.SystemUpdate
{
    public class CreateSystemUpdateNoticeDto
    {
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Changes { get; set; } = string.Empty;
    }
}
