using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Shared.DTOs.Activity;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace EcaInformationSystem.Api.BackgroundServices
{
    public class ActivityReminderScheduler
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<ActivityHub> _hub;
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _scheduled = new();

        public ActivityReminderScheduler(IServiceScopeFactory scopeFactory, IHubContext<ActivityHub> hub)
        {
            _scopeFactory = scopeFactory;
            _hub = hub;
        }

        public void Schedule(ActivityDto activity)
        {
            Cancel(activity.Id);

            if (activity.IsAllDay || activity.IsCancelled)
                return;

            var delay = activity.StartDate - DateTime.Now;

            if (delay <= TimeSpan.Zero || delay > TimeSpan.FromDays(1))
                return;

            var cts = new CancellationTokenSource();
            _scheduled[activity.Id] = cts;
            var token = cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, token);
                    if (!token.IsCancellationRequested)
                        await FireAsync(activity.Id);
                }
                catch (TaskCanceledException)
                {
                    // expected when Cancel() is called (edit/delete) — no action needed
                }
                finally
                {
                    _scheduled.TryRemove(activity.Id, out _);
                }
            }, token);
        }

        public void Cancel(int activityId)
        {
            if (_scheduled.TryRemove(activityId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        private async Task FireAsync(int activityId)
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

            var activity = await repo.GetByIdAsync(activityId);
            if (activity is null || activity.IsCancelled || activity.ReminderSent)
                return;

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

            await repo.MarkReminderSentAsync(activityId);
            await _hub.Clients.All.SendAsync("ActivityStarting", dto);
        }
    }
}