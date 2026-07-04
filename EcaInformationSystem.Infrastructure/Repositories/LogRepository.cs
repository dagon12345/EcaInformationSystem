using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class LogRepository : ILogRepository
    {
        private readonly AppDbContext _context;
        public LogRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(Log log)
        {
            await _context.Logs.AddAsync(log);
        }

        public async Task AddRangeAsync(IEnumerable<Log> logs)
        {
            await _context.Logs.AddRangeAsync(logs);
        }

        public async Task<(List<LogEntryDto> Items, int TotalCount)> GetAllLogsAsync(LogFilterDto filter)
        {
            var query = _context.Logs.AsNoTracking().AsQueryable();

            // ── Date Created range filter — the primary ask ─────────────────────────
            if (filter.DateFrom.HasValue)
                query = query.Where(l => l.CreatedAt >= filter.DateFrom.Value.Date);

            if (filter.DateTo.HasValue)
                query = query.Where(l => l.CreatedAt < filter.DateTo.Value.Date.AddDays(1)); // inclusive end-of-day

            if (!string.IsNullOrWhiteSpace(filter.UserName))
                query = query.Where(l => l.UserName.Contains(filter.UserName));

            if (!string.IsNullOrWhiteSpace(filter.Search))
                query = query.Where(l => l.Activity.Contains(filter.Search));

            var totalCount = await query.CountAsync();

            var pageItems = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(l => new
                {
                    l.Id,
                    l.BeneficiaryInformationId,
                    l.Activity,
                    l.UserName,
                    l.CreatedAt
                })
                .ToListAsync();

            // ── Resolve beneficiary names in one follow-up query instead of a JOIN —
            // same pattern used elsewhere in this codebase to avoid join fanout ──────
            var beneficiaryIds = pageItems.Select(x => x.BeneficiaryInformationId).Distinct().ToList();
            var names = await _context.BeneficiaryInformations
                .Where(b => beneficiaryIds.Contains(b.Id))
                .Select(b => new { b.Id, b.FirstName, b.LastName })
                .ToListAsync();
            var nameLookup = names.ToDictionary(x => x.Id, x => $"{x.LastName}, {x.FirstName}");

            var items = pageItems.Select(x => new LogEntryDto
            {
                Id = x.Id,
                // LogRepository.GetAllLogsAsync — adjust the fallback label
                BeneficiaryName = x.BeneficiaryInformationId.HasValue
                     ? (nameLookup.TryGetValue(x.BeneficiaryInformationId.Value, out var n) ? n : "(record deleted)")
                     : "(system/chat activity)",
                Activity = x.Activity,
                UserName = x.UserName,
                CreatedAt = x.CreatedAt
            }).ToList();

            return (items, totalCount);
        }

        public async Task<IEnumerable<LogSummaryResultDto>> GetLogSummaryAsync(Guid beneficiaryId)
        {
            var query = from log in _context.Logs

                        join b in _context.BeneficiaryInformations on log.BeneficiaryInformationId
                        equals b.Id into logBeneficiary
                        from beneficiary in logBeneficiary.DefaultIfEmpty()
                        where log.BeneficiaryInformationId == beneficiaryId
                        select new LogSummaryResultDto
                        {
                            Id = log.Id,
                            Activity = log.Activity,
                            UserName = log.UserName,
                            CreatedAt = log.CreatedAt,
                            BeneficiaryInformationId = beneficiary != null ? beneficiary.Id : Guid.Empty
                        };
            var result =  await query
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
            return result;

        }
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
