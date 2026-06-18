using EcaInformationSystem.Infrastructure.Services;

namespace EcaInformationSystem.Api.BackgroundServices
{
    /// <summary>
    /// Drains the payroll background queue one job at a time. Sequential processing
    /// avoids multiple multi-thousand-record ClosedXML generations competing for
    /// CPU/memory simultaneously on the same IIS worker process.
    /// </summary>
    public class PayrollQueueProcessor : BackgroundService
    {
        private readonly BackgroundTaskQueue _queue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PayrollQueueProcessor> _logger;

        public PayrollQueueProcessor(
            BackgroundTaskQueue queue,
            IServiceProvider serviceProvider,
            ILogger<PayrollQueueProcessor> logger)
        {
            _queue = queue;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (await _queue.Reader.WaitToReadAsync(stoppingToken))
            {
                while (_queue.Reader.TryRead(out var workItem))
                {
                    try
                    {
                        // ✅ New DI scope per job — fresh DbContext, repositories, etc.,
                        // since the HTTP request that queued the job is long gone.
                        using var scope = _serviceProvider.CreateScope();
                        await workItem(scope.ServiceProvider, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled error while processing a background payroll job.");
                    }
                }
            }
        }
    }
}