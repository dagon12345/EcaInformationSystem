using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IFileShrinkService
    {
        bool CanShrink(string contentType);

        Task<byte[]> ShrinkAsync(byte[] data, string contentType, ShrinkQuality quality, CancellationToken ct = default);
    }
}
