using EcaInformationSystem.Application.Interfaces.Services;

namespace EcaInformationSystem.Infrastructure.Services
{
    /// <summary>
    /// Disk-based temp storage for generated payroll ZIPs. All System.IO usage
    /// for this feature is isolated here — Application layer never touches the filesystem.
    /// </summary>
    public class PayrollFileStorageService : IPayrollFileStorageService
    {
        private static readonly string TempDir =
            Path.Combine(Path.GetTempPath(), "EcaResPayrollJobs");

        public async Task<string> SaveAsync(Guid jobId, byte[] fileBytes, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(TempDir);

            var filePath = Path.Combine(TempDir, $"{jobId}.zip");
            await File.WriteAllBytesAsync(filePath, fileBytes, cancellationToken);
            return filePath;
        }

        public byte[] ReadAndDelete(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);

            try { File.Delete(filePath); }
            catch { /* best-effort cleanup; download still succeeds */ }

            return bytes;
        }

        public bool Exists(string filePath) => File.Exists(filePath);
    }
}