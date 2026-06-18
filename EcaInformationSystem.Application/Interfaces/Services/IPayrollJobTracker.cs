using EcaInformationSystem.Domain.Models;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    /// <summary>
    /// Tracks the lifecycle of background payroll generation jobs.
    /// Implementation detail (in-memory, DB, distributed cache, etc.) lives in Infrastructure.
    /// </summary>
    public interface IPayrollJobTracker
    {
        Guid CreateJob(int totalRecords);
        void MarkProcessing(Guid jobId);
        void MarkCompleted(Guid jobId, string filePath, string fileName);
        void MarkFailed(Guid jobId, string errorMessage);
        PayrollJobStatus? GetJob(Guid jobId);
        void RemoveJob(Guid jobId);
    }
}