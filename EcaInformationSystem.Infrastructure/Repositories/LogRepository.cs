using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class LogRepository : ILogRepository
    {
        private readonly AppDbContext _context;
        private readonly ITransactionTierBroadcaster _tierBroadcaster;
        public LogRepository(AppDbContext context, ITransactionTierBroadcaster tierBroadcaster)
        {
            _context = context;
            _tierBroadcaster = tierBroadcaster;
        }
        public async Task AddAsync(Log log)
        {
            await _context.Logs.AddAsync(log);

            // Live leaderboard push — skip the same bulk activity this repo
            // already excludes from the tier count itself, so a 40k-row Excel
            // import doesn't spam every connected client with 40k SignalR
            // events for activity that isn't even counted. Logins DO qualify
            // now, same as the count query below.
            if (!(log.UserName ?? string.Empty).StartsWith("System") &&
                !log.Activity.StartsWith(CommonConstants.ImportedBeneficiaryFromExcel) &&
                !log.Activity.StartsWith(CommonConstants.ExcelUpdate) &&
                !log.Activity.StartsWith("New payment record") &&
                !log.Activity.StartsWith("Reference number assigned:") &&
                !log.Activity.StartsWith("Bulk update:") &&
                !log.Activity.StartsWith("Bulk CO Status update") &&
                !log.Activity.StartsWith("CGP Number assigned:"))
            {
                await _tierBroadcaster.NotifyTransactionRecordedAsync(log.UserName);
            }
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
                query = query.Where(l => l.CreatedAt < filter.DateTo.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(filter.UserName))
                query = query.Where(l => l.UserName.Contains(filter.UserName));

            if (!string.IsNullOrWhiteSpace(filter.Search))
                query = query.Where(l => l.Activity.Contains(filter.Search));

            if (!string.IsNullOrWhiteSpace(filter.BeneficiaryName))
            {
                // ✅ Same normalization as the beneficiary search filter's FullName
                // matching — strip commas/periods and collapse whitespace, so
                // "ABAA, ASINDINA" matches the same way "ABAA ASINDINA" does.
                var term = NormalizeSearchTerm(filter.BeneficiaryName);

                var matchingBeneficiaryIds = await _context.BeneficiaryInformations
                    .Where(b =>
                        (b.LastName + " " + b.FirstName + " " + b.MiddleName).ToLower().Contains(term) ||
                        (b.FirstName + " " + b.MiddleName + " " + b.LastName).ToLower().Contains(term))
                    .Select(b => b.Id)
                    .ToListAsync();

                query = query.Where(l => l.BeneficiaryInformationId.HasValue
                    && matchingBeneficiaryIds.Contains(l.BeneficiaryInformationId.Value));
            }

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

        // Excludes bulk-operation activity — anything where a service loops over
        // many records and writes one Log row PER RECORD, so a single bulk action
        // doesn't dwarf everyone else's real, one-click-at-a-time activity.
        // Logins now DO count (per request) — signing in is still activity.
        // Bulk activity prefixes covered here, all from loops in
        // BeneficiaryInformationService: Excel import/update, bulk payment
        // history, bulk ref-number assignment, bulk eligibility/batch-code
        // update, bulk CO status update, and CGP number assignment (payroll
        // generation — also excluded defensively even though it's logged
        // under the non-real "System (Payroll Generation)" username).
        // `seasonStartUtc` is the CURRENT leaderboard season's start (see
        // LeaderboardSeasonService/LeaderboardSeason) — this used to be a
        // hardcoded constant here, but the weekly reset feature needs it to be
        // dynamic (whatever the active season's StartedAtUtc is), so callers
        // now fetch and pass it in instead.
        private IQueryable<Log> TransactionLogsQuery(DateTime seasonStartUtc) =>
            _context.Logs.AsNoTracking().Where(l =>
                l.CreatedAt >= seasonStartUtc &&
                !l.Activity.StartsWith(CommonConstants.ImportedBeneficiaryFromExcel) &&
                !l.Activity.StartsWith(CommonConstants.ExcelUpdate) &&
                !l.Activity.StartsWith("New payment record") &&
                !l.Activity.StartsWith("Reference number assigned:") &&
                !l.Activity.StartsWith("Bulk update:") &&
                !l.Activity.StartsWith("Bulk CO Status update") &&
                !l.Activity.StartsWith("CGP Number assigned:"));

        public async Task<int> CountUserTransactionsAsync(string userName, DateTime seasonStartUtc)
        {
            return await TransactionLogsQuery(seasonStartUtc)
                .Where(l => l.UserName == userName)
                .CountAsync();
        }

        // Powers the leaderboard — one row per user who has at least one
        // qualifying log entry, ordered highest-count first. Usernames starting
        // with "System" (the bare fallback "System" several controllers write
        // when User.Identity.Name is unavailable, plus variants like "System
        // (Payroll Generation)" for specific background jobs) aren't real
        // accounts, so they're excluded from the ranking.
        public async Task<List<(string UserName, int Count)>> GetTransactionCountsByUserAsync(DateTime seasonStartUtc)
        {
            var grouped = await TransactionLogsQuery(seasonStartUtc)
                .Where(l => !l.UserName.StartsWith("System"))
                .GroupBy(l => l.UserName)
                .Select(g => new { UserName = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            return grouped.Select(g => (g.UserName, g.Count)).ToList();
        }
        // Classifies a Log row's free-text Activity as a create/edit — every
        // create/edit site across the app (Beneficiary, WFP-ECA, Senior Citizens
        // Directory, NCSC Team Directory, ...) already writes "Created "/"Added "
        // for creates and "Updated "/"Edited " for edits (see CommonConstants and
        // each service's AddLogAsync call), so this works retroactively on
        // existing logs with no new Log.Category needed.
        private static readonly string[] CreatedPrefixes = { "Created", "Added" };
        private static readonly string[] EditedPrefixes = { "Updated", "Edited" };

        public async Task<UserActivityStatsDto> GetUserActivityStatsAsync(string userName, string? fullName, DateTime seasonStartUtc)
        {
            // Log.UserName is free text, not a foreign key, and a few call sites
            // (Application/Document Tracking) write the acting user's FullName
            // into it instead of their login UserName — match either so this
            // person's tracking activity isn't silently dropped from their own stats.
            var names = string.IsNullOrWhiteSpace(fullName) || string.Equals(fullName, userName, StringComparison.OrdinalIgnoreCase)
                ? new[] { userName }
                : new[] { userName, fullName };

            var rows = await TransactionLogsQuery(seasonStartUtc)
                .Where(l => names.Contains(l.UserName))
                .Select(l => new { l.Category, l.Activity, l.CreatedAt })
                .ToListAsync();

            return new UserActivityStatsDto
            {
                LoginCount = rows.Count(r => r.Category == "Login"),
                DataCreatedCount = rows.Count(r => CreatedPrefixes.Any(p => r.Activity.StartsWith(p))),
                DataEditedCount = rows.Count(r => EditedPrefixes.Any(p => r.Activity.StartsWith(p))),
                DocumentsTrackedCount = rows.Count(r => r.Category == "ApplicationTracking" || r.Category == "DocumentTracking"),
                ActiveDates = rows.Select(r => r.CreatedAt.Date).Distinct().ToList()
            };
        }

        public async Task<List<(string UserName, DateTime Date)>> GetActiveDatesByUserAsync(DateTime seasonStartUtc)
        {
            var rows = await TransactionLogsQuery(seasonStartUtc)
                .Where(l => !l.UserName.StartsWith("System"))
                .Select(l => new { l.UserName, l.CreatedAt })
                .Distinct()
                .ToListAsync();

            return rows.Select(r => (r.UserName, r.CreatedAt.Date)).Distinct().ToList();
        }

        // Strips commas/periods and collapses whitespace so "ABAA, ASINDINA" and
        // "Abaa Asindina" both normalize to the same searchable form as the
        // space-joined LastName+FirstName+MiddleName concatenation used in the query.
        private static string NormalizeSearchTerm(string input)
        {
            var normalized = input.Trim().ToLower()
                .Replace(",", " ")
                .Replace(".", " ");

            while (normalized.Contains("  "))
                normalized = normalized.Replace("  ", " ");

            return normalized.Trim();
        }
    }
}
