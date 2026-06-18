using System.Threading.Channels;
using EcaInformationSystem.Application.Interfaces.Services;

namespace EcaInformationSystem.Infrastructure.Services
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue =
            Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>();

        public void QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, Task> workItem)
        {
            if (workItem == null) throw new ArgumentNullException(nameof(workItem));
            _queue.Writer.TryWrite(workItem);
        }

        // ✅ Internal-use accessor for the hosted service that drains this queue.
        // Not part of IBackgroundTaskQueue — that interface only exposes the
        // write side, since only Infrastructure's own hosted service should read.
        public ChannelReader<Func<IServiceProvider, CancellationToken, Task>> Reader => _queue.Reader;
    }
}