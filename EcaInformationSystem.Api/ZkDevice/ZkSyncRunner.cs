using System.Text;
using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs.Dtr;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace EcaInformationSystem.Api.ZkDevice
{
    // The actual "connect to the device, pull, push, report status" cycle —
    // shared by ZkDirectPollingService's timer loop and DtrController's
    // "Connect"/"Sync DTR" endpoints (POST api/dtr/test-connection,
    // POST api/dtr/sync-now), so both go through identical logic instead of
    // duplicating it. The device's host/port/serial are NOT fixed config —
    // they're read from BiometricDeviceSetting (DB) on every call, so
    // SuperAdmin/Finance can change the IP from Attendance Management
    // without anyone touching appsettings or restarting anything.
    //
    // RegionCode is passed in per-call rather than read from _options,
    // because the two callers need it from different places: the button
    // endpoints derive it from the calling user's own JWT claim (so it
    // works from ANY instance — production, a plain `dotnet watch`, doesn't
    // matter, as long as ZkDirect:Enabled is true there), while the
    // unattended background timer has no request/claims to read, so it
    // falls back to the static ZkDirectOptions.RegionCode.
    public class ZkSyncRunner
    {
        private readonly ZkDirectOptions _options;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<BiometricStatusHub> _hub;
        private readonly ILogger<ZkSyncRunner> _logger;

        // Registered as a singleton, so these persist across polling cycles —
        // lets us skip the expensive full pull (chunked read of 10k+ records)
        // when the device's own counts haven't moved since last time. Cheap
        // at a 5-minute interval; matters a lot more at 1 minute.
        private int? _lastKnownRecordCount;
        private int? _lastKnownUserCount;

        // The physical device only accepts one active connection at a time —
        // without this, a manual "Connect" or "Sync DTR" click landing at the
        // same moment as the background poller's own cycle (or two manual
        // clicks in a row) can collide, and whichever one loses the race gets
        // a bogus connection failure that overwrites the other's success in
        // the status table moments later.
        private readonly SemaphoreSlim _deviceLock = new(1, 1);

        public ZkSyncRunner(
            IOptions<ZkDirectOptions> options,
            IServiceProvider serviceProvider,
            IHubContext<BiometricStatusHub> hub,
            ILogger<ZkSyncRunner> logger)
        {
            _options = options.Value;
            _serviceProvider = serviceProvider;
            _hub = hub;
            _logger = logger;
        }

        // Cheap reachability check for the "Connect" button — no full pull,
        // no writes to attendance/status tables, just proves the device
        // answers at the currently-saved IP.
        public async Task<ConnectionTestResultDto> TestConnectionAsync(int regionCode, CancellationToken ct)
        {
            var settings = await GetDeviceSettingsAsync(regionCode);
            if (settings is null || string.IsNullOrWhiteSpace(settings.DeviceHost))
            {
                return new ConnectionTestResultDto { Success = false, ErrorMessage = "No device IP address saved yet." };
            }

            await _deviceLock.WaitAsync(ct);
            try
            {
                // Short timeout here on purpose — this is a "just tell me
                // now" reachability check for the Connect button, not the
                // full sync (which uses _options.TimeoutMs and can
                // legitimately take longer reading 10k+ records).
                const int connectTestTimeoutMs = 5000;
                using var client = new ZkClient(settings.DeviceHost, settings.DevicePort, _options.CommKey, connectTestTimeoutMs);
                await client.ConnectAsync(ct);
                try
                {
                    var (userCount, recordCount) = await client.GetDeviceCountsAsync(ct);
                    await ReportConnectivityAsync(settings.DeviceSerialNumber, regionCode, success: true, errorMessage: null, ct);
                    return new ConnectionTestResultDto { Success = true, DeviceUserCount = userCount, DeviceRecordCount = recordCount };
                }
                finally
                {
                    await client.DisconnectAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Connection test to {Host}:{Port} failed", settings.DeviceHost, settings.DevicePort);
                await ReportConnectivityAsync(settings.DeviceSerialNumber, regionCode, success: false, errorMessage: ex.Message, ct);
                return new ConnectionTestResultDto { Success = false, ErrorMessage = ex.Message };
            }
            finally
            {
                _deviceLock.Release();
            }
        }

        public async Task<TriggerSyncResultDto> RunOnceAsync(int regionCode, string? syncedByName, CancellationToken ct)
        {
            // Everything here — including the DB read for settings — is
            // inside the try. A background timer calls this unattended;
            // letting ANY exception (a transient DB hiccup, not just device
            // errors) escape unhandled would crash the whole hosted service,
            // and by extension the whole API process.
            BiometricDeviceSettingDto? settings = null;
            try
            {
                settings = await GetDeviceSettingsAsync(regionCode);
                if (settings is null || string.IsNullOrWhiteSpace(settings.DeviceHost))
                {
                    return new TriggerSyncResultDto
                    {
                        Success = false,
                        ErrorMessage = "No device IP address saved yet. Set it in Attendance Management first."
                    };
                }

                var (attendanceCount, userCount) = await PollOnceAsync(settings, regionCode, ct);
                await ReportStatusAsync(settings.DeviceSerialNumber, regionCode, success: true, errorMessage: null, attendanceCount, userCount, syncedByName, ct);
                return new TriggerSyncResultDto { Success = true, AttendanceRecordsSynced = attendanceCount, EnrolledUsersSynced = userCount };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ZK sync cycle failed");
                if (settings is not null)
                    await ReportStatusAsync(settings.DeviceSerialNumber, regionCode, success: false, errorMessage: ex.Message, 0, 0, syncedByName, ct);
                return new TriggerSyncResultDto { Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<BiometricDeviceSettingDto?> GetDeviceSettingsAsync(int regionCode)
        {
            using var scope = _serviceProvider.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<IBiometricDeviceSettingService>();
            return await settingService.GetAsync(regionCode);
        }

        private async Task<(int AttendanceCount, int UserCount)> PollOnceAsync(BiometricDeviceSettingDto settings, int regionCode, CancellationToken ct)
        {
            _logger.LogInformation("Connecting to device {Host}:{Port}", settings.DeviceHost, settings.DevicePort);

            List<ZkAttendanceRecord> records;
            List<ZkEnrolledUser> users;
            await _deviceLock.WaitAsync(ct);
            try
            {
                using var client = new ZkClient(settings.DeviceHost, settings.DevicePort, _options.CommKey, _options.TimeoutMs);
                await client.ConnectAsync(ct);
                try
                {
                    var (deviceUserCount, deviceRecordCount) = await client.GetDeviceCountsAsync(ct);

                    if (deviceRecordCount == _lastKnownRecordCount && deviceUserCount == _lastKnownUserCount)
                    {
                        _logger.LogInformation(
                            "Device counts unchanged ({Records} record(s), {Users} user(s)) — skipping full pull",
                            deviceRecordCount, deviceUserCount);
                        records = [];
                        users = [];
                    }
                    else
                    {
                        await client.DisableDeviceAsync(ct);
                        records = await client.GetAttendanceLogsAsync(_logger, ct);
                        users = await client.GetEnrolledUsersAsync(_logger, ct);

                        _lastKnownRecordCount = deviceRecordCount;
                        _lastKnownUserCount = deviceUserCount;
                    }
                }
                finally
                {
                    await client.EnableDeviceAsync(ct);
                    await client.DisconnectAsync(ct);
                }
            }
            finally
            {
                _deviceLock.Release();
            }

            if (records.Count == 0 && users.Count == 0)
                return (0, 0);

            _logger.LogInformation("Pulled {Count} attendance record(s) and {UserCount} enrolled user(s) from device",
                records.Count, users.Count);

            using var scope = _serviceProvider.CreateScope();

            if (records.Count > 0)
            {
                var attendanceService = scope.ServiceProvider.GetRequiredService<IAttendanceLogService>();
                var body = new StringBuilder();
                foreach (var r in records)
                {
                    body.Append(r.BiometricUserId).Append('\t')
                        .Append(r.PunchTime.ToString("yyyy-MM-dd HH:mm:ss")).Append('\t')
                        .Append(r.Status).Append('\t')
                        .Append(r.VerifyMode).Append('\n');
                }

                var imported = await attendanceService.ImportAttLogAsync(settings.DeviceSerialNumber, body.ToString());
                _logger.LogInformation("Imported {Count} new attendance record(s)", imported);
            }

            if (users.Count > 0)
            {
                var deviceUserService = scope.ServiceProvider.GetRequiredService<IBiometricDeviceUserService>();
                var items = users.Select(u => new DeviceUserSyncItemDto
                {
                    BiometricUserId = u.BiometricUserId,
                    Name = u.Name,
                    Privilege = u.Privilege,
                    CardNumber = u.CardNumber
                }).ToList();

                await deviceUserService.SyncAsync(settings.DeviceSerialNumber, items, regionCode);
                _logger.LogInformation("Synced {Count} enrolled user(s)", items.Count);
            }

            return (records.Count, users.Count);
        }

        private async Task ReportStatusAsync(string deviceSerialNumber, int regionCode, bool success, string? errorMessage, int attendanceCount, int userCount, string? syncedByName, CancellationToken ct)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var statusService = scope.ServiceProvider.GetRequiredService<IBiometricSyncStatusService>();
                await statusService.ReportAsync(deviceSerialNumber, new BiometricSyncStatusReportDto
                {
                    Success = success,
                    ErrorMessage = errorMessage,
                    AttendanceRecordsSynced = attendanceCount,
                    EnrolledUsersSynced = userCount,
                    SyncedByName = syncedByName
                }, regionCode);

                // Live push so Attendance Management/My DTR update without
                // polling. Only the non-sensitive freshness shape goes out —
                // matches GET api/dtr/sync-freshness, since every
                // authenticated user (not just SuperAdmin/Finance) receives
                // this broadcast. Scoped to this region's SignalR group only
                // (see BiometricStatusHub) — otherwise every region would see
                // every other region's sync activity flash across their screen.
                var freshness = await statusService.GetFreshnessAsync(regionCode);
                await _hub.Clients.Group($"region-{regionCode}").SendAsync("DeviceStatusChanged", freshness, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record sync status");
            }
        }

        // Records the "Connect" button's cheap reachability check — separate
        // from ReportStatusAsync above so a connectivity check never
        // overwrites the record/user counts or attribution left behind by
        // the last REAL sync (see ReportConnectivityAsync on the service).
        private async Task ReportConnectivityAsync(string deviceSerialNumber, int regionCode, bool success, string? errorMessage, CancellationToken ct)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var statusService = scope.ServiceProvider.GetRequiredService<IBiometricSyncStatusService>();
                await statusService.ReportConnectivityAsync(deviceSerialNumber, success, errorMessage, regionCode);

                var freshness = await statusService.GetFreshnessAsync(regionCode);
                await _hub.Clients.Group($"region-{regionCode}").SendAsync("DeviceStatusChanged", freshness, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record connectivity status");
            }
        }
    }
}
