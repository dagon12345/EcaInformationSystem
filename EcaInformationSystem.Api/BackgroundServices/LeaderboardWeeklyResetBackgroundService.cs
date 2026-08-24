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
    //
    // Polls every 5 minutes instead of sleeping via a single up-to-7-day
    // Task.Delay (like ActivityReminderResyncService does for the same
    // reason). On IIS in-process hosting, an idle app pool (default 20-minute
    // idle timeout, no "Always On"/preload) stops w3wp.exe when there's no
    // traffic, which kills any in-flight Task.Delay outright. A long single
    // delay computed once at startup never gets a chance to fire if the
    // process dies mid-wait — on the next request IIS just spins the process
    // back up and the service recomputes the *next* Sunday, silently
    // skipping the one that was missed. That's why the very first reset
    // (Season 1 -> 2) fired but the following week's did not. Checking
    // "is it past the scheduled time yet?" on a short interval means a
    // restart within the same week still catches an overdue reset instead
    // of skipping straight to the following one.
    public class LeaderboardWeeklyResetBackgroundService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

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
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var seasonService = scope.ServiceProvider.GetRequiredService<ILeaderboardSeasonService>();
                        var seasonInfo = await seasonService.GetActiveSeasonInfoAsync();

                        if (DateTime.UtcNow >= seasonInfo.NextAutoResetAtUtc)
                        {
                            var broadcaster = scope.ServiceProvider.GetRequiredService<ITransactionTierBroadcaster>();

                            var result = await seasonService.ResetSeasonAsync(resetBy: null, isAutomatic: true);
                            await broadcaster.NotifyLeaderboardResetAsync(result);

                            _logger.LogInformation(
                                "Leaderboard auto-reset: Season {Ended} -> Season {New}",
                                result.EndedSeasonNumber, result.NewSeasonNumber);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break; // normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Weekly leaderboard auto-reset check failed");
                    // Non-fatal — the next poll (in 5 minutes) will retry.
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }
}
