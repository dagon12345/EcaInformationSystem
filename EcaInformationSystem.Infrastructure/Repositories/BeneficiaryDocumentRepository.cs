using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class BeneficiaryDocumentRepository : IBeneficiaryDocumentRepository
    {
        private readonly AppDbContext _context;

        public BeneficiaryDocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(BeneficiaryDocument document)
        {
            await _context.BeneficiaryDocuments.AddAsync(document);
        }

        public async Task<BeneficiaryDocument?> GetByIdAsync(Guid id)
        {
            return await _context.BeneficiaryDocuments
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }

        public async Task<List<BeneficiaryDocument>> GetByBeneficiaryIdAsync(
            Guid beneficiaryId)
        {
            return await _context.BeneficiaryDocuments
                .Where(x => x.BeneficiaryInformationId == beneficiaryId
                         && !x.IsDeleted)
                .OrderByDescending(x => x.UploadedAt)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
        public Task UpdateAsync(BeneficiaryDocument beneficiaryDocument)
        {
            _context.BeneficiaryDocuments.Update(beneficiaryDocument);
            return Task.CompletedTask;
        }
    }
}