using EcaInformationSystem.Infrastructure.Caching;

namespace EcaInformationSystem.Api.BackgroundServices
{
    public class PsgcCacheRefreshBackgroundService : BackgroundService
    {
        private readonly IPsgcNameCache _cache;
        private readonly ILogger<PsgcCacheRefreshBackgroundService> _logger;
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(6);

        public PsgcCacheRefreshBackgroundService(
            IPsgcNameCache cache, ILogger<PsgcCacheRefreshBackgroundService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(RefreshInterval, stoppingToken);
                    await _cache.RefreshAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PSGC cache background refresh failed");
                }
            }
        }
    }
}