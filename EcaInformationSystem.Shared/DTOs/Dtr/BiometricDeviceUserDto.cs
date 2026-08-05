namespace EcaInformationSystem.Shared.DTOs.Dtr
{
    public class BiometricDeviceUserDto
    {
        public int Id { get; set; }
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public string BiometricUserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Privilege { get; set; }
        public string? CardNumber { get; set; }
        public DateTime LastSyncedAt { get; set; }

        // Filled in server-side when this PIN matches a PendingUserRegistration.BiometricUserId.
        public string? LinkedSystemUserName { get; set; }
    }

    // Payload the ZKTecoAgent pushes after pulling the device's enrolled-user list.
    public class DeviceUserSyncRequestDto
    {
        public List<DeviceUserSyncItemDto> Users { get; set; } = new();
    }

    public class DeviceUserSyncItemDto
    {
        public string BiometricUserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Privilege { get; set; }
        public string? CardNumber { get; set; }
    }

    // Reported after every poll cycle (scheduled or manually triggered via
    // the local sync tool), success or failure.
    public class BiometricSyncStatusReportDto
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int AttendanceRecordsSynced { get; set; }
        public int EnrolledUsersSynced { get; set; }

        // Who ran it — typed into the local sync tool. Null for the
        // background timer's own automatic cycles.
        public string? SyncedByName { get; set; }
    }

    public class BiometricSyncStatusDto
    {
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public DateTime LastAttemptAt { get; set; }
        public DateTime? LastSuccessAt { get; set; }
        public string? LastErrorMessage { get; set; }
        public int LastAttendanceRecordsSynced { get; set; }
        public int LastEnrolledUsersSynced { get; set; }
        public string? LastSuccessByName { get; set; }
    }

    // Self-service — any user's DTR page uses this to know whether attendance
    // data might be stale, without exposing device serials/error details.
    // LastAttemptSucceeded reflects the DEVICE's reachability on the most
    // recent sync attempt — deliberately independent of the viewer's own
    // SignalR socket state, which only says whether THEIR browser is
    // currently connected to the API, not whether the device is reachable.
    public class SyncFreshnessDto
    {
        public DateTime? LastSuccessAt { get; set; }
        public bool IsStale { get; set; }
        public bool LastAttemptSucceeded { get; set; }
        public string? LastSuccessByName { get; set; }
    }

    // Request body for the local sync tool's "Sync Now" button.
    public class TriggerSyncRequestDto
    {
        public string? SyncedByName { get; set; }
    }

    public class TriggerSyncResultDto
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int AttendanceRecordsSynced { get; set; }
        public int EnrolledUsersSynced { get; set; }
    }

    // The device's LAN address — set by SuperAdmin/Finance from Attendance
    // Management instead of appsettings, so it can be changed (new router,
    // new IP) without touching config files or restarting anything.
    public class BiometricDeviceSettingDto
    {
        public string DeviceHost { get; set; } = string.Empty;
        public int DevicePort { get; set; } = 4370;
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedByName { get; set; }
    }

    public class SaveDeviceSettingRequestDto
    {
        public string DeviceHost { get; set; } = string.Empty;
        public int DevicePort { get; set; } = 4370;
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public string? UpdatedByName { get; set; }
    }

    // Result of the "Connect" button — a cheap reachability check (no data
    // pull) so admins/HR get instant feedback after saving a new IP.
    public class ConnectionTestResultDto
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? DeviceUserCount { get; set; }
        public int? DeviceRecordCount { get; set; }
    }
}
