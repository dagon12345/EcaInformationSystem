namespace EcaInformationSystem.Domain.Entities
{
    // A snapshot of a user enrolled directly on the ZKTeco device — synced by
    // the ZKTecoAgent Windows service, not entered through this app. Lets
    // SuperAdmin/Finance see who's actually registered on the physical
    // terminal without needing local network access to it themselves.
    public class BiometricDeviceUser
    {
        public int Id { get; set; }
        public int RegionCode { get; set; }
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public string BiometricUserId { get; set; } = string.Empty; // PIN
        public string Name { get; set; } = string.Empty;
        public int Privilege { get; set; }
        public string? CardNumber { get; set; }
        public DateTime LastSyncedAt { get; set; } = DateTime.Now;
    }
}
