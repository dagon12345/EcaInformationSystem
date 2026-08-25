using EcaInformationSystem.Application.Interfaces;

namespace EcaInformationSystem.Api.BackgroundServices
{
    // Auto-posts a birthday greeting to the feed for every active account
    // celebrating today, so coworkers can drop a comment on it. Polls every
    // 30 minutes instead of scheduling a single next-midnight delay — same
    // reasoning as LeaderboardWeeklyResetBackgroundService: IIS in-process
    // hosting can stop the worker process during an idle period, which would
    // kill a long-sleeping delay outright. BirthdayGreetingService itself is
    // idempotent per calendar year (LastBirthdayGreetedYear), so re-checking
    // on every poll never double-posts.
    public class BirthdayGreetingBackgroundService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BirthdayGreetingBackgroundService> _logger;

        public BirthdayGreetingBackgroundService(
            IServiceScopeFactory scopeFactory, ILogger<BirthdayGreetingBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var birthdayService = scope.ServiceProvider.GetRequiredService<IBirthdayGreetingService>();
                    await birthdayService.CheckAndPostTodaysBirthdaysAsync();
                }
                catch (OperationCanceledException)
                {
                    break; // normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Birthday greeting check failed");
                    // Non-fatal — the next poll will retry.
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }
}
