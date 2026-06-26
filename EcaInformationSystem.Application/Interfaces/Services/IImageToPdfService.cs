namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IImageToPdfService
    {
        Task<byte[]> ConvertToPdfAsync(List<byte[]> imageBytesList);
    }
}