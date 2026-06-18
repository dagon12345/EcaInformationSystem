namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IPayrollFileStorageService
    {
        Task<string> SaveAsync(Guid jobId, byte[] fileBytes, CancellationToken cancellationToken = default);
        byte[] ReadAndDelete(string filePath);
        bool Exists(string filePath);
    }
}