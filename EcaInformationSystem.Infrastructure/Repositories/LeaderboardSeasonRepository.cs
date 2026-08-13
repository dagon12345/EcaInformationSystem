using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class LeaderboardSeasonRepository : ILeaderboardSeasonRepository
    {
        private readonly AppDbContext _context;

        public LeaderboardSeasonRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<LeaderboardSeason?> GetActiveSeasonAsync() =>
            await _context.LeaderboardSeasons
                .Where(s => s.EndedAtUtc == null)
                .OrderByDescending(s => s.SeasonNumber)
                .FirstOrDefaultAsync();

        public async Task<int> GetLatestSeasonNumberAsync()
        {
            var any = await _context.LeaderboardSeasons.AnyAsync();
            if (!any) return 0;
            return await _context.LeaderboardSeasons.MaxAsync(s => s.SeasonNumber);
        }

        public async Task AddAsync(LeaderboardSeason season) =>
            await _context.LeaderboardSeasons.AddAsync(season);

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
