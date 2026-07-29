using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class WfpEcaRepository : IWfpEcaRepository
    {
        private readonly AppDbContext _context;

        public WfpEcaRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<WfpEcaEntry>> GetAsync(int regionCode, int fiscalYear)
        {
            return await _context.WfpEcaEntries
                .AsNoTracking()
                .Where(e => e.RegionCode == regionCode && e.FiscalYear == fiscalYear)
                .OrderBy(e => e.SortOrder)
                .ToListAsync();
        }

        public async Task<WfpEcaSaveResult> UpsertAsync(int regionCode, int fiscalYear, IEnumerable<WfpEcaLineInput> lines, string userName)
        {
            var existingById = await _context.WfpEcaEntries
                .Where(e => e.RegionCode == regionCode && e.FiscalYear == fiscalYear)
                .ToDictionaryAsync(e => e.Id);

            var now = DateTime.UtcNow;
            var result = new List<WfpEcaEntry>();
            var added = new List<WfpEcaEntry>();
            var updated = new List<WfpEcaEntry>();
            var seenIds = new HashSet<Guid>();
            var sortOrder = 0;

            foreach (var line in lines)
            {
                WfpEcaEntry? entry = line.Id is Guid id && existingById.TryGetValue(id, out var found) ? found : null;

                if (entry is not null)
                {
                    var changed = entry.UacsCode != line.UacsCode
                        || entry.UacsName != line.UacsName
                        || entry.Allotment != line.Allotment
                        || entry.Obligation != line.Obligation
                        || entry.Remarks != line.Remarks;

                    entry.UacsCode = line.UacsCode;
                    entry.UacsName = line.UacsName;
                    entry.Allotment = line.Allotment;
                    entry.Obligation = line.Obligation;
                    entry.Remarks = line.Remarks;
                    entry.SortOrder = sortOrder;

                    if (changed)
                    {
                        entry.DateModified = now;
                        entry.ModifiedBy = userName;
                        updated.Add(entry);
                    }

                    seenIds.Add(entry.Id);
                }
                else
                {
                    entry = new WfpEcaEntry
                    {
                        Id = Guid.NewGuid(),
                        RegionCode = regionCode,
                        FiscalYear = fiscalYear,
                        UacsCode = line.UacsCode,
                        UacsName = line.UacsName,
                        SortOrder = sortOrder,
                        Allotment = line.Allotment,
                        Obligation = line.Obligation,
                        Remarks = line.Remarks,
                        DateSet = now,
                        SetBy = userName
                    };
                    await _context.WfpEcaEntries.AddAsync(entry);
                    added.Add(entry);
                    seenIds.Add(entry.Id);
                }

                result.Add(entry);
                sortOrder++;
            }

            // Rows that existed before this save but weren't in the submitted
            // grid were removed by the admin — delete them so the table stays
            // in sync with what was actually shown/edited.
            var removed = existingById.Values.Where(e => !seenIds.Contains(e.Id)).ToList();
            if (removed.Count > 0)
                _context.WfpEcaEntries.RemoveRange(removed);

            await _context.SaveChangesAsync();
            return new WfpEcaSaveResult(result, added, updated, removed);
        }
    }
}
