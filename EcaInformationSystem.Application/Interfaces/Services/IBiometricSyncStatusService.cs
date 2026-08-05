using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IBiometricSyncStatusService
    {
        Task ReportAsync(string deviceSerialNumber, BiometricSyncStatusReportDto report, int regionCode);

        // Lighter-weight than ReportAsync — for the "Connect" button's cheap
        // reachability check. Updates IsConnected/LastAttemptAt/LastErrorMessage
        // only; deliberately does NOT touch LastSuccessAt, LastSuccessByName, or
        // the record/user counts, since those describe actual DATA freshness
        // from a real sync, not just "the device answered."
        Task ReportConnectivityAsync(string deviceSerialNumber, bool success, string? errorMessage, int regionCode);

        Task<List<BiometricSyncStatusDto>> GetAllAsync(int regionCode);

        // Self-service — just enough for any user's DTR page to know whether
        // attendance data might be stale, without exposing device serials or
        // error details to non-admins.
        Task<SyncFreshnessDto> GetFreshnessAsync(int regionCode);
    }
}
