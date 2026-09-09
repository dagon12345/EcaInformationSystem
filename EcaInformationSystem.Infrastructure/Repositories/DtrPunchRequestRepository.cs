using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class DtrPunchRequestRepository : IDtrPunchRequestRepository
    {
        private readonly AppDbContext _context;

        public DtrPunchRequestRepository(AppDbContext context) => _context = context;

        public async Task<DtrPunchEditRequest> AddAsync(DtrPunchEditRequest request)
        {
            await _context.DtrPunchEditRequests.AddAsync(request);
            return request;
        }

        public Task<DtrPunchEditRequest?> GetByIdAsync(Guid id) =>
            _context.DtrPunchEditRequests.FirstOrDefaultAsync(r => r.Id == id);

        public Task<List<DtrPunchEditRequest>> GetPendingAsync() =>
            _context.DtrPunchEditRequests
                .Where(r => r.Status == DtrPunchRequestStatus.Pending)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
