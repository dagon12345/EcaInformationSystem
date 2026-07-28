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

        public async Task<List<UpcomingBirthdayDto>> GetUpcomingBirthdaysAsync(int withinDays = 7)
        {
            var users = await _userRepo.GetAllAsync();
            var regions = await _regionRepo.GetAllAsync();
            var today = DateTime.Today;

            var results = new List<UpcomingBirthdayDto>();

            foreach (var user in users)
            {
                // Default(DateTime) means "never set" for this non-nullable column —
                // and only surface birthdays for accounts that are actually active.
                if (user.BirthDate == default || !user.IsActivated || user.ApprovalStatus != 1)
                    continue;

                var nextOccurrence = NextBirthdayOccurrence(user.BirthDate, today);
                var daysUntil = (nextOccurrence - today).Days;
                if (daysUntil < 0 || daysUntil > withinDays) continue;

                results.Add(new UpcomingBirthdayDto
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Position = user.Position,
                    RegionName = user.Region.HasValue
                        ? regions.FirstOrDefault(r => r.PsgcCodeRegion == user.Region.Value)?.Name
                        : null,
                    BirthDate = user.BirthDate,
                    DaysUntil = daysUntil,
                    TurningAge = nextOccurrence.Year - user.BirthDate.Year
                });
            }

            return results.OrderBy(r => r.DaysUntil).ToList();
        }

        // Finds the next calendar occurrence of a birthday from "today" (inclusive) —
        // Feb 29 falls back to Feb 28 in non-leap years rather than skipping the year.
        private static DateTime NextBirthdayOccurrence(DateTime birthDate, DateTime today)
        {
            DateTime BuildDate(int year) =>
                birthDate.Month == 2 && birthDate.Day == 29 && !DateTime.IsLeapYear(year)
                    ? new DateTime(year, 2, 28)
                    : new DateTime(year, birthDate.Month, birthDate.Day);

            var candidate = BuildDate(today.Year);
            return candidate < today ? BuildDate(today.Year + 1) : candidate;
        }
    }
}
