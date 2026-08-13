using EcaInformationSystem.Application.Common;
using EcaInformationSystem.Application.Interfaces;

namespace EcaInformationSystem.Api.BackgroundServices
{
    // Automatically ends the current weekly leaderboard season and opens the
    // next one every Sunday 11:59 PM Philippine Time — the same reset a
    // SuperAdmin can trigger manually via UserTransactionTierController's
    // POST /reset. ILeaderboardSeasonService/ITransactionTierBroadcaster are
    // scoped services, so a DI scope is created per reset since this
    // background service itself is a singleton.
    public class LeaderboardWeeklyResetBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LeaderboardWeeklyResetBackgroundService> _logger;

        public LeaderboardWeeklyResetBackgroundService(
            IServiceScopeFactory scopeFactory, ILogger<LeaderboardWeeklyResetBackgroundService> logger)
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
                    var nextResetUtc = WeeklyResetScheduleHelper.NextSundayElevenFiftyNinePmUtc(DateTime.UtcNow);
                    var delay = nextResetUtc - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, stoppingToken);

                    using var scope = _scopeFactory.CreateScope();
                    var seasonService = scope.ServiceProvider.GetRequiredService<ILeaderboardSeasonService>();
                    var broadcaster = scope.ServiceProvider.GetRequiredService<ITransactionTierBroadcaster>();

                    var result = await seasonService.ResetSeasonAsync(resetBy: null, isAutomatic: true);
                    await broadcaster.NotifyLeaderboardResetAsync(result);

                    _logger.LogInformation(
                        "Leaderboard auto-reset: Season {Ended} -> Season {New}",
                        result.EndedSeasonNumber, result.NewSeasonNumber);
                }
                catch (OperationCanceledException)
                {
                    break; // normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Weekly leaderboard auto-reset failed");
                    // Avoid a tight retry loop if something's persistently broken —
                    // the next natural Sunday-23:59 computation will retry in ~a week,
                    // but back off briefly here in case of a transient DB blip so we
                    // don't miss a reset entirely due to one failed attempt landing
                    // right at the boundary.
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }
    }
}
