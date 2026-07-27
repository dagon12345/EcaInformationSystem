using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly IPendingUserRegistrationRepository _userRepo;
        private readonly IUserProfileRepository _profileRepo;
        private readonly IRegionRepository _regionRepo;
        private readonly IPostImageProcessingService _imageProcessor;

        public UserProfileService(
            IPendingUserRegistrationRepository userRepo,
            IUserProfileRepository profileRepo,
            IRegionRepository regionRepo,
            IPostImageProcessingService imageProcessor)
        {
            _userRepo = userRepo;
            _profileRepo = profileRepo;
            _regionRepo = regionRepo;
            _imageProcessor = imageProcessor;
        }

        public async Task<UserProfileDto?> GetMyProfileAsync(Guid userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null) return null;

            var picture = await _profileRepo.GetPictureAsync(userId);
            var regionName = await ResolveRegionNameAsync(user.Region);

            return new UserProfileDto
            {
                Id = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                Position = user.Position,
                BirthDate = user.BirthDate,
                Role = user.Role,
                Region = user.Region,
                RegionName = regionName,
                IsMfaEnabled = user.IsMfaEnabled,
                HasProfilePicture = picture is not null
            };
        }

        public async Task<PublicUserProfileDto?> GetPublicProfileAsync(Guid userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null) return null;

            var picture = await _profileRepo.GetPictureAsync(userId);
            var regionName = await ResolveRegionNameAsync(user.Region);

            return new PublicUserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Position = user.Position,
                Role = user.Role,
                RegionName = regionName,
                HasProfilePicture = picture is not null
            };
        }

        public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request)
        {
            var user = await _userRepo.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            user.FullName = request.FullName.Trim();
            user.Position = request.Position.Trim();
            user.BirthDate = request.BirthDate;

            await _userRepo.SaveChangesAsync();

            return (await GetMyProfileAsync(userId))!;
        }

        public async Task UploadProfilePictureAsync(Guid userId, Stream imageStream, string uploadedBy)
        {
            var (fullData, thumbData, _, _) = await _imageProcessor.ProcessAsync(imageStream);
            await _profileRepo.UpsertPictureAsync(userId, fullData, thumbData, "image/jpeg", uploadedBy);
        }

        public async Task RemoveProfilePictureAsync(Guid userId)
        {
            await _profileRepo.RemovePictureAsync(userId);
        }

        public async Task<(byte[] Data, string ContentType)?> GetProfilePictureAsync(Guid userId, bool thumbnail)
        {
            var picture = await _profileRepo.GetPictureAsync(userId);
            if (picture is null) return null;

            return (thumbnail ? picture.ThumbnailData : picture.ImageData, picture.ContentType);
        }

        private async Task<string?> ResolveRegionNameAsync(int? regionCode)
        {
            if (!regionCode.HasValue) return null;
            var regions = await _regionRepo.GetAllAsync();
            return regions.FirstOrDefault(r => r.PsgcCodeRegion == regionCode.Value)?.Name;
        }
    }
}
