using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IUserProfileRepository
    {
        Task<UserProfilePicture?> GetPictureAsync(Guid userId);
        Task UpsertPictureAsync(Guid userId, byte[] imageData, byte[] thumbnailData, string contentType, string uploadedBy);
        Task RemovePictureAsync(Guid userId);
    }
}
