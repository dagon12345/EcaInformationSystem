using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class BeneficiaryFindingRepository : IBeneficiaryFindingRepository
    {
        private readonly AppDbContext _appDbContext;
        public BeneficiaryFindingRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }
        public async Task<BeneficiaryFinding?> GetByBeneficiaryIdAsync(Guid beneficiaryId)
        {
            var result = await _appDbContext.BeneficiaryFindings
                .FirstOrDefaultAsync(x => x.BeneficiaryInformationId == beneficiaryId);
            return result;
        }

        // ✅ Untracked — used by GetByBeneficiaryIdAsync (read-only GET endpoint)
        public async Task<BeneficiaryFinding?> GetByBeneficiaryIdAsNoTrackingAsync(Guid beneficiaryId)
            => await _appDbContext.BeneficiaryFindings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BeneficiaryInformationId == beneficiaryId);
        public async Task AddAsync(BeneficiaryFinding finding)
        {
            await _appDbContext.BeneficiaryFindings.AddAsync(finding);
        }
        public async Task SaveChangesAsync()
        {
            await _appDbContext.SaveChangesAsync();
        }
    }
}
