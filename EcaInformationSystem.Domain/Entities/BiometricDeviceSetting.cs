namespace EcaInformationSystem.Domain.Entities
{
    // Single-row table — the biometric device's LAN address, editable by
    // SuperAdmin/Finance from Attendance Management instead of being baked
    // into appsettings. Lets the IP be changed (e.g. after a router reset)
    // without anyone touching config files or restarting anything.
    public class BiometricDeviceSetting
    {
        public int Id { get; set; }

        // One row per region — each region's SuperAdmin/Finance manage their
        // own office's device independently; a region's own local sync tool
        // only ever reads/writes the row matching its own RegionCode.
        public int RegionCode { get; set; }
        public string DeviceHost { get; set; } = string.Empty;
        public int DevicePort { get; set; } = 4370;
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedByName { get; set; }
    }
}
