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

        public async Task<AnnualGranteeTarget> UpsertAsync(int regionCode, int fiscalYear, int[] quarterlyTargets, string userName)
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
                existing.SetQuarterlyTargets(quarterlyTargets);
                await _context.AnnualGranteeTargets.AddAsync(existing);
            }
            else
            {
                existing.SetQuarterlyTargets(quarterlyTargets);
                existing.DateModified = DateTime.UtcNow;
                existing.ModifiedBy = userName;
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        // Grouped by the stored PayrollQuarter/FiscalYear columns on the payment
        // history row itself — not by bucketing PaymentDate into a calendar
        // quarter — so this always matches whatever quarter/year the payroll
        // was actually run under, same as the rest of the Statistics page.
        public async Task<Dictionary<int, int>> GetQuarterlyPaidCountsAsync(int regionCode, int fiscalYear)
        {
            var counts = await _context.BeneficiaryPaymentHistories
                .AsNoTracking()
                .Where(h => h.PaymentStatus == 2
                    && h.PayrollQuarter.HasValue
                    && h.FiscalYear == fiscalYear
                    && h.Beneficiary!.Region == regionCode
                    && !h.Beneficiary.IsDeleted)
                .GroupBy(h => h.PayrollQuarter!.Value)
                .Select(g => new { Quarter = g.Key, Count = g.Select(h => h.BeneficiaryInformationId).Distinct().Count() })
                .ToListAsync();

            return counts.ToDictionary(x => x.Quarter, x => x.Count);
        }
    }
}
