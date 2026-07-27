using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class ResolvedDuplicatePairRepository : IResolvedDuplicatePairRepository
    {
        private readonly AppDbContext _context;

        public ResolvedDuplicatePairRepository(AppDbContext context)
        {
            _context = context;
        }

        // A pair scanned as (A, B) must resolve to the same row as (B, A) —
        // normalizing to (lower, higher) makes the lookup order-independent.
        private static (Guid, Guid) Normalize(Guid a, Guid b) =>
            a.CompareTo(b) <= 0 ? (a, b) : (b, a);

        public async Task<Dictionary<(Guid, Guid), ResolvedDuplicatePair>> GetForPairsAsync(IEnumerable<(Guid Record1Id, Guid Record2Id)> pairs)
        {
            var normalized = pairs.Select(p => Normalize(p.Record1Id, p.Record2Id)).Distinct().ToList();
            if (normalized.Count == 0) return new();

            var record1Ids = normalized.Select(p => p.Item1).ToList();

            var candidates = await _context.ResolvedDuplicatePairs
                .AsNoTracking()
                .Where(x => record1Ids.Contains(x.Record1Id))
                .ToListAsync();

            var lookup = new HashSet<(Guid, Guid)>(normalized);

            return candidates
                .Where(x => lookup.Contains((x.Record1Id, x.Record2Id)))
                .ToDictionary(x => (x.Record1Id, x.Record2Id));
        }

        public async Task<ResolvedDuplicatePair> ResolveAsync(Guid record1Id, Guid record2Id, string? remarks, string resolvedBy)
        {
            var (lo, hi) = Normalize(record1Id, record2Id);

            var existing = await _context.ResolvedDuplicatePairs
                .FirstOrDefaultAsync(x => x.Record1Id == lo && x.Record2Id == hi);

            if (existing is null)
            {
                existing = new ResolvedDuplicatePair
                {
                    Id = Guid.NewGuid(),
                    Record1Id = lo,
                    Record2Id = hi
                };
                await _context.ResolvedDuplicatePairs.AddAsync(existing);
            }

            existing.IsResolved = true;
            existing.Remarks = remarks;
            existing.ResolvedAt = DateTime.UtcNow;
            existing.ResolvedBy = resolvedBy;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<ResolvedDuplicatePair> UnresolveAsync(Guid record1Id, Guid record2Id, string unresolvedBy)
        {
            var (lo, hi) = Normalize(record1Id, record2Id);

            var existing = await _context.ResolvedDuplicatePairs
                .FirstOrDefaultAsync(x => x.Record1Id == lo && x.Record2Id == hi);

            if (existing is null)
            {
                existing = new ResolvedDuplicatePair
                {
                    Id = Guid.NewGuid(),
                    Record1Id = lo,
                    Record2Id = hi
                };
                await _context.ResolvedDuplicatePairs.AddAsync(existing);
            }

            existing.IsResolved = false;
            existing.UnresolvedAt = DateTime.UtcNow;
            existing.UnresolvedBy = unresolvedBy;

            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
