namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IPostImageProcessingService
    {
        Task<(byte[] fullData, byte[] thumbData, int width, int height)> ProcessAsync(Stream input);
    }
}
