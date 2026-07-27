using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class AnnualGranteeTargetRepository : IAnnualGranteeTargetRepository
    {
        private readonly AppDbContext _context;

        public AnnualGranteeTargetRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AnnualGranteeTarget?> GetAsync(int regionCode, int fiscalYear)
        {
            return await _context.AnnualGranteeTargets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.RegionCode == regionCode && t.FiscalYear == fiscalYear);
        }

        public async Task<AnnualGranteeTarget> UpsertAsync(int regionCode, int fiscalYear, int[] monthlyTargets, string userName)
        {
            var existing = await _context.AnnualGranteeTargets
                .FirstOrDefaultAsync(t => t.RegionCode == regionCode && t.FiscalYear == fiscalYear);

            if (existing is null)
            {
                existing = new AnnualGranteeTarget
                {
                    Id = Guid.NewGuid(),
                    RegionCode = regionCode,
                    FiscalYear = fiscalYear,
                    DateSet = DateTime.UtcNow,
                    SetBy = userName
                };
                existing.SetMonthlyTargets(monthlyTargets);
                await _context.AnnualGranteeTargets.AddAsync(existing);
            }
            else
            {
                existing.SetMonthlyTargets(monthlyTargets);
                existing.DateModified = DateTime.UtcNow;
                existing.ModifiedBy = userName;
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<Dictionary<int, int>> GetMonthlyPaidCountsAsync(int regionCode, int fiscalYear)
        {
            var counts = await _context.BeneficiaryPaymentHistories
                .AsNoTracking()
                .Where(h => h.PaymentStatus == 2
                    && h.PaymentDate.HasValue
                    && h.PaymentDate.Value.Year == fiscalYear
                    && h.Beneficiary!.Region == regionCode
                    && !h.Beneficiary.IsDeleted)
                .GroupBy(h => h.PaymentDate!.Value.Month)
                .Select(g => new { Month = g.Key, Count = g.Select(h => h.BeneficiaryInformationId).Distinct().Count() })
                .ToListAsync();

            return counts.ToDictionary(x => x.Month, x => x.Count);
        }
    }
}
