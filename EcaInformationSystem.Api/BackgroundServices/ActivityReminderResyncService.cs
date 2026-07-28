using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Shared.DTOs.Activity;

namespace EcaInformationSystem.Api.BackgroundServices
{
    // Not the primary trigger. ActivityReminderScheduler fires reminders instantly
    // via in-memory timers. This service exists only to re-arm those timers after
    // an app restart/deploy, when in-memory state was lost. Runs every 5 minutes,
    // looks 24h ahead, and re-schedules anything not yet reminded.
    public class ActivityReminderResyncService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ActivityReminderScheduler _scheduler;

        public ActivityReminderResyncService(
            IServiceScopeFactory scopeFactory,
            ActivityReminderScheduler scheduler)
        {
            _scopeFactory = scopeFactory;
            _scheduler = scheduler;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Run once immediately on startup, then every 5 minutes.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ResyncAsync();
                }
                catch
                {
                    // a missed sweep isn't fatal — next one will catch up
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task ResyncAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

            var upcoming = await repo.GetUpcomingAsync(DateTime.Now, DateTime.Now.AddHours(24));

            foreach (var activity in upcoming)
            {
                var dto = new ActivityDto
                {
                    Id = activity.Id,
                    Title = activity.Title,
                    Description = activity.Description,
                    StartDate = activity.StartDate,
                    EndDate = activity.EndDate,
                    IsAllDay = activity.IsAllDay,
                    Type = activity.Type,
                    Priority = activity.Priority,
                    Location = activity.Location,
                    PsgcCodeProvince = activity.PsgcCodeProvince,
                    PsgcCodeMunicipality = activity.PsgcCodeMunicipality,
                    IsCancelled = activity.IsCancelled
                };

                _scheduler.Schedule(dto);
            }
        }
    }
}