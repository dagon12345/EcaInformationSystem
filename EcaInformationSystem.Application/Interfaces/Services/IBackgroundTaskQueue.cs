namespace EcaInformationSystem.Application.Interfaces.Services
{
    /// <summary>
    /// Abstraction over a background work queue. Application layer schedules
    /// work without knowing how it's actually executed (channel, hosted service, etc.)
    /// </summary>
    public interface IBackgroundTaskQueue
    {
        void QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, Task> workItem);
    }
}