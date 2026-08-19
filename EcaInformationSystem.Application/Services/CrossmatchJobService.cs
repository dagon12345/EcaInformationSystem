using System.Collections.Concurrent;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public enum CrossmatchJobStatus
    {
        Running,
        Completed,
        Failed,
        Cancelled
    }

    public class CrossmatchJobState
    {
        public Guid JobId { get; set; }
        public CrossmatchJobStatus Status { get; set; } = CrossmatchJobStatus.Running;
        public int Processed;
        public int Total;
        public CrossmatchResultDto? Result { get; set; }
        public string? Error { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public CancellationTokenSource Cts { get; } = new();
    }

    // ✅ NEW — in-memory job tracker for long-running crossmatch scans. The
    // upload request returns immediately with a job id; the actual parsing +
    // matching runs on a background task (in its own DI scope, since the
    // request's scoped DbContext is disposed the moment the request ends).
    // The client polls GetStatus for progress and grabs the result once done.
    public class CrossmatchJobService
    {
        private readonly ConcurrentDictionary<Guid, CrossmatchJobState> _jobs = new();
        private static readonly TimeSpan JobRetention = TimeSpan.FromMinutes(30);

        public CrossmatchJobState Create()
        {
            CleanupOldJobs();
            var job = new CrossmatchJobState { JobId = Guid.NewGuid() };
            _jobs[job.JobId] = job;
            return job;
        }

        public CrossmatchJobState? Get(Guid jobId) =>
            _jobs.TryGetValue(jobId, out var job) ? job : null;

        public bool Cancel(Guid jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var job)) return false;
            job.Cts.Cancel();
            return true;
        }

        private void CleanupOldJobs()
        {
            var cutoff = DateTime.UtcNow - JobRetention;
            foreach (var kv in _jobs)
            {
                if (kv.Value.CreatedAt < cutoff)
                    _jobs.TryRemove(kv.Key, out _);
            }
        }
    }
}
