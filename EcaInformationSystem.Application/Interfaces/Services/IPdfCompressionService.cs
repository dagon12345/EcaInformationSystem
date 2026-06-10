namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IPdfCompressionService
    {
        Task<byte[]> CompressAsync(Stream inputStream);
    }
}