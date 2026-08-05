using EcaInformationSystem.Api.ZkDevice;
using Microsoft.Extensions.Options;

namespace EcaInformationSystem.Api.BackgroundServices
{
    // Timer-driven wrapper around ZkSyncRunner — polls the device on a fixed
    // interval when ZkDirect:Enabled is true (only meaningful for a locally
    // run instance with LAN access to the device; see ZkDirectOptions). The
    // local sync tool's manual "Sync Now" button uses the same ZkSyncRunner
    // directly, on demand, instead of this timer.
    public class ZkDirectPollingService : BackgroundService
    {
        private readonly ZkDirectOptions _options;
        private readonly ZkSyncRunner _syncRunner;
        private readonly ILogger<ZkDirectPollingService> _logger;

        public ZkDirectPollingService(
            IOptions<ZkDirectOptions> options,
            ZkSyncRunner syncRunner,
            ILogger<ZkDirectPollingService> logger)
        {
            _options = options.Value;
            _syncRunner = syncRunner;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("ZkDirect polling is disabled (ZkDirect:Enabled=false) — set DeviceHost and enable it once the device's port is forwarded.");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _syncRunner.RunOnceAsync(_options.RegionCode, syncedByName: null, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // RunOnceAsync already catches its own errors — this is
                    // just a last-resort guard so an unexpected failure logs
                    // and retries next cycle instead of crashing the whole
                    // API process (a hosted service's unhandled exception
                    // takes the entire host down with it).
                    _logger.LogError(ex, "Unexpected error in ZK polling cycle");
                }

                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
        }
    }
}
