namespace EcaInformationSystem.Shared.DTOs.Auth
{
    public class SessionDto
    {
        public Guid Id { get; set; }
        public string DeviceLabel { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public DateTime LoginAt { get; set; }
        public DateTime LastActiveAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsCurrent { get; set; }
    }
}
