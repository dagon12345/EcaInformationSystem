using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class FormDocumentRepository : IFormDocumentRepository
    {
        private readonly AppDbContext _context;

        public FormDocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(FormDocument doc)
            => await _context.FormDocuments.AddAsync(doc);

        public async Task<FormDocument?> GetByIdAsync(Guid id)
            => await _context.FormDocuments
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        public async Task<List<FormDocument>> GetAllAsync()
            => await _context.FormDocuments
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .ToListAsync();

        public Task UpdateAsync(FormDocument doc)
        {
            _context.FormDocuments.Update(doc);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
            => await _context.SaveChangesAsync();
    }
}