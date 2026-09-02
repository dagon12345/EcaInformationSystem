using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class LivenessCheckRepository : ILivenessCheckRepository
    {
        private readonly AppDbContext _context;

        public LivenessCheckRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(LivenessCheckRecord record)
        {
            await _context.LivenessCheckRecords.AddAsync(record);
        }

        public Task DeleteAsync(LivenessCheckRecord record)
        {
            _context.LivenessCheckRecords.Remove(record);
            return Task.CompletedTask;
        }

        public async Task<LivenessCheckRecord?> GetByIdAsync(Guid id)
        {
            return await _context.LivenessCheckRecords.FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<LivenessCheckRecord?> GetByTokenAsync(string token)
        {
            return await _context.LivenessCheckRecords.FirstOrDefaultAsync(r => r.Token == token);
        }

        public async Task<LivenessCheckRecord?> GetByBeneficiaryIdAsync(Guid beneficiaryId)
        {
            return await _context.LivenessCheckRecords
                .FirstOrDefaultAsync(r => r.BeneficiaryInformationId == beneficiaryId);
        }

        public async Task<List<LivenessCheckRecord>> GetHistoryByBeneficiaryIdAsync(Guid beneficiaryId)
        {
            return await _context.LivenessCheckRecords
                .AsNoTracking()
                .Where(r => r.BeneficiaryInformationId == beneficiaryId)
                .OrderByDescending(r => r.GeneratedDate)
                .ToListAsync();
        }

        public async Task<List<(LivenessCheckRecord Record, BeneficiaryInformation Beneficiary)>> GetSubmittedForReviewAsync(List<int>? allowedMunicipalityCodes)
        {
            var query =
                from r in _context.LivenessCheckRecords.AsNoTracking()
                join b in _context.BeneficiaryInformations.AsNoTracking() on r.BeneficiaryInformationId equals b.Id
                where r.Status == LivenessCheckStatus.Submitted
                select new { r, b };

            if (allowedMunicipalityCodes is not null)
                query = query.Where(x => allowedMunicipalityCodes.Contains(x.b.Municipality));

            var rows = await query.OrderByDescending(x => x.r.SubmittedDate).ToListAsync();
            return rows.Select(x => (x.r, x.b)).ToList();
        }

        public async Task<BeneficiaryInformation?> GetBeneficiaryAsync(Guid beneficiaryId)
        {
            return await _context.BeneficiaryInformations.FirstOrDefaultAsync(b => b.Id == beneficiaryId);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
