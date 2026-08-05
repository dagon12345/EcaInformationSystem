namespace EcaInformationSystem.Api.ZkDevice
{
    // Static commissioning config for this instance — gates whether it's
    // allowed to talk to the device at all (only ever true for the LocalSync
    // launch profile, never the deployed API), plus device-protocol details
    // that basically never change. The actual DeviceHost/Port/SerialNumber
    // are NOT here — they live in BiometricDeviceSetting (DB), editable by
    // SuperAdmin/Finance from Attendance Management, so IP changes don't
    // require touching this file.
    public class ZkDirectOptions
    {
        public const string SectionName = "ZkDirect";

        public bool Enabled { get; set; } = false;

        // Which region's device this particular office's local instance
        // serves. Unlike DeviceHost/Port/SerialNumber, this genuinely is a
        // one-time-per-office setting — a given physical machine at a given
        // office is never going to suddenly belong to a different region —
        // so it's fine to be a static config value instead of DB-editable.
        public int RegionCode { get; set; }

        public int CommKey { get; set; } = 0;
        public int PollIntervalSeconds { get; set; } = 300;
        public int TimeoutMs { get; set; } = 20000;
    }
}
