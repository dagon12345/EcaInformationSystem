using System.Collections.Concurrent;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Models;

namespace EcaInformationSystem.Infrastructure.Services
{
    public class PayrollJobTracker : IPayrollJobTracker
    {
        /// <summary>
        /// In-memory job tracker. Singleton lifetime — survives across HTTP requests
        /// within the same app pool process. Lost on app pool recycle; clients
        /// polling a stale jobId simply get a 404 and re-trigger generation.
        /// </summary>

        private readonly ConcurrentDictionary<Guid, PayrollJobStatus> _jobs = new();

        public Guid CreateJob(int totalRecords)
        {
            var job = new PayrollJobStatus
            {
                JobId = Guid.NewGuid(),
                State = PayrollJobState.Queued,
                TotalRecords = totalRecords
            };
            _jobs[job.JobId] = job;
            return job.JobId;
        }

        public void MarkProcessing(Guid jobId)
        {
            if (_jobs.TryGetValue(jobId, out var job))
                job.State = PayrollJobState.Processing;
        }

        public void MarkCompleted(Guid jobId, string filePath, string fileName)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.State = PayrollJobState.Completed;
                job.FilePath = filePath;
                job.FileName = fileName;
                job.CompletedAt = DateTime.UtcNow;
            }
        }

        public void MarkFailed(Guid jobId, string errorMessage)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.State = PayrollJobState.Failed;
                job.ErrorMessage = errorMessage;
                job.CompletedAt = DateTime.UtcNow;
            }
        }

        public PayrollJobStatus? GetJob(Guid jobId)
            => _jobs.TryGetValue(jobId, out var job) ? job : null;

        public void RemoveJob(Guid jobId)
            => _jobs.TryRemove(jobId, out _);

    }
}