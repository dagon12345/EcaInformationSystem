using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IUserProfileService
    {
        Task<UserProfileDto?> GetMyProfileAsync(Guid userId);
        Task<PublicUserProfileDto?> GetPublicProfileAsync(Guid userId);

        Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request);

        Task UploadProfilePictureAsync(Guid userId, Stream imageStream, string uploadedBy);
        Task RemoveProfilePictureAsync(Guid userId);

        // thumbnail: true = small preview (avatars everywhere), false = full-size (profile page display)
        Task<(byte[] Data, string ContentType)?> GetProfilePictureAsync(Guid userId, bool thumbnail);
    }
}
