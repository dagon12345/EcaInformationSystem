namespace EcaInformationSystem.Domain.Entities
{
    // One row per device — the ZKTecoAgent Windows service reports here after
    // every poll cycle (success or failure) so the web app can show whether
    // it's actually able to reach the biometric terminal.
    public class BiometricSyncStatus
    {
        public int Id { get; set; }
        public int RegionCode { get; set; }
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public DateTime LastAttemptAt { get; set; }
        public DateTime? LastSuccessAt { get; set; }
        public string? LastErrorMessage { get; set; }
        public int LastAttendanceRecordsSynced { get; set; }
        public int LastEnrolledUsersSynced { get; set; }

        // Who ran the sync (typed into the local sync tool at the time),
        // for the last SUCCESSFUL attempt specifically.
        public string? LastSuccessByName { get; set; }
    }
}
