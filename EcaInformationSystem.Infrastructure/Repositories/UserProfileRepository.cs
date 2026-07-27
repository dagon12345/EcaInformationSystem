using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class UserProfileRepository : IUserProfileRepository
    {
        private readonly AppDbContext _context;

        public UserProfileRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserProfilePicture?> GetPictureAsync(Guid userId)
        {
            return await _context.UserProfilePictures
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);
        }

        public async Task UpsertPictureAsync(Guid userId, byte[] imageData, byte[] thumbnailData, string contentType, string uploadedBy)
        {
            var existing = await _context.UserProfilePictures
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (existing is null)
            {
                await _context.UserProfilePictures.AddAsync(new UserProfilePicture
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ImageData = imageData,
                    ThumbnailData = thumbnailData,
                    ContentType = contentType,
                    UploadedAt = DateTime.UtcNow,
                    UploadedBy = uploadedBy
                });
            }
            else
            {
                existing.ImageData = imageData;
                existing.ThumbnailData = thumbnailData;
                existing.ContentType = contentType;
                existing.UploadedAt = DateTime.UtcNow;
                existing.UploadedBy = uploadedBy;
            }

            await _context.SaveChangesAsync();
        }

        public async Task RemovePictureAsync(Guid userId)
        {
            var existing = await _context.UserProfilePictures
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (existing is null) return;

            _context.UserProfilePictures.Remove(existing);
            await _context.SaveChangesAsync();
        }
    }
}
