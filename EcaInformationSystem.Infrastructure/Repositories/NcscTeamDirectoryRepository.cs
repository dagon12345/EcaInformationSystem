using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Domain.Exceptions;
using EcaInformationSystem.Infrastructure.Persistence;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class NcscTeamDirectoryRepository : INcscTeamDirectoryRepository
    {
        private readonly AppDbContext _context;
        private readonly IPsgcNameCache _psgcNameCache;

        public NcscTeamDirectoryRepository(AppDbContext context, IPsgcNameCache psgcNameCache)
        {
            _context = context;
            _psgcNameCache = psgcNameCache;
        }

        public async Task<List<NcscTeamDirectoryEntryDto>> GetAllAsync()
        {
            var entries = await _context.NcscTeamDirectoryEntries
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .ToListAsync();

            return entries
                .Select(MapToDto)
                .OrderBy(x => x.RegionName)
                .ThenBy(x => x.Position)
                .ThenBy(x => x.FullName)
                .ToList();
        }

        public async Task<NcscTeamDirectoryEntryDto?> GetByIdAsync(Guid id)
        {
            var x = await _context.NcscTeamDirectoryEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

            return x is null ? null : MapToDto(x);
        }

        public async Task<NcscTeamDirectoryEntry?> GetEntityByIdAsync(Guid id) =>
            await _context.NcscTeamDirectoryEntries.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

        public async Task<int?> GetRegionCodeByNameAsync(string name)
        {
            var normalized = name.Trim().ToLower();
            return await _context.Regions.AsNoTracking()
                .Where(r => r.Name != null && r.Name.ToLower() == normalized)
                .Select(r => (int?)r.PsgcCodeRegion)
                .FirstOrDefaultAsync();
        }

        public async Task AddAsync(NcscTeamDirectoryEntry entry) =>
            await _context.NcscTeamDirectoryEntries.AddAsync(entry);

        public void SetOriginalRowVersion(NcscTeamDirectoryEntry entity, byte[] rowVersion) =>
            _context.Entry(entity).Property(x => x.RowVersion).OriginalValue = rowVersion;

        public async Task SaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConcurrencyException(
                    "This directory entry was modified by another user. Please refresh and try again.", ex);
            }
        }

        public async Task AddLogAsync(Guid? entryId, string activity, string userName)
        {
            await _context.Logs.AddAsync(new Log
            {
                Id = Guid.NewGuid(),
                Category = "NcscTeamDirectory",
                NcscTeamDirectoryEntryId = entryId,
                Activity = activity,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task<List<NcscTeamDirectoryHistoryDto>> GetHistoryAsync(Guid entryId) =>
            await _context.Logs
                .AsNoTracking()
                .Where(l => l.NcscTeamDirectoryEntryId == entryId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new NcscTeamDirectoryHistoryDto
                {
                    Id = l.Id,
                    Activity = l.Activity,
                    UserName = l.UserName,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync();

        private NcscTeamDirectoryEntryDto MapToDto(NcscTeamDirectoryEntry x) => new()
        {
            Id = x.Id,
            PsgcCodeRegion = x.PsgcCodeRegion,
            RegionName = _psgcNameCache.GetRegionName(x.PsgcCodeRegion),
            Position = x.Position,
            FullName = x.FullName,
            Nickname = x.Nickname,
            Email = x.Email,
            MobileNumber = x.MobileNumber,
            CreatedAt = x.CreatedAt,
            CreatedBy = x.CreatedBy,
            UpdatedAt = x.UpdatedAt,
            UpdatedBy = x.UpdatedBy,
            RowVersion = x.RowVersion
        };
    }
}
