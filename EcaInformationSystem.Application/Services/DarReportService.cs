using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.DailyAccomplishmentReport;

namespace EcaInformationSystem.Application.Services
{
    public class DarReportService : IDarReportService
    {
        private readonly IDarReportRepository _repo;

        public DarReportService(IDarReportRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<DarReportListItemDto>> GetMineAsync(Guid userId)
        {
            var reports = await _repo.GetByUserAsync(userId);
            return reports.Select(ToListItemDto).ToList();
        }

        public async Task<DarReportDto?> GetByIdAsync(Guid id, Guid userId)
        {
            var report = await _repo.GetByIdAsync(id, userId);
            return report is null ? null : ToDto(report);
        }

        public async Task<DarReportDto> UpsertAsync(Guid userId, DarReportUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PreparedByName))
                throw new InvalidOperationException("Prepared-by name is required.");

            var start = dto.PeriodStart.Date;
            var end = dto.PeriodEnd.Date;

            if (end < start)
                throw new InvalidOperationException("Period end must be on or after period start.");
            if ((end - start).TotalDays > 31)
                throw new InvalidOperationException("Period cannot span more than 31 days.");
            if (await _repo.HasOverlapAsync(userId, start, end, dto.Id))
                throw new InvalidOperationException("This period overlaps an existing report.");

            DarReport report;
            if (dto.Id.HasValue)
            {
                report = await _repo.GetByIdAsync(dto.Id.Value, userId)
                    ?? throw new KeyNotFoundException("Report not found.");
                report.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                report = new DarReport
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                await _repo.AddAsync(report);
            }

            report.PeriodStart = start;
            report.PeriodEnd = end;
            report.PreparedByName = dto.PreparedByName.Trim();
            report.PreparedByPosition = dto.PreparedByPosition.Trim();
            report.NotedByName = dto.NotedByName?.Trim() ?? string.Empty;
            report.NotedByPosition = dto.NotedByPosition?.Trim() ?? string.Empty;
            report.UseSharedEssentialFunctions = dto.UseSharedEssentialFunctions;
            report.SharedEssentialFunctions = string.IsNullOrWhiteSpace(dto.SharedEssentialFunctions)
                ? null : dto.SharedEssentialFunctions.Trim();

            ReconcileEntries(report, start, end, dto.Entries);

            await _repo.SaveChangesAsync();
            return ToDto(report);
        }

        public async Task DeleteAsync(Guid userId, Guid id)
        {
            var report = await _repo.GetByIdAsync(id, userId);
            if (report is null) return; // already gone — deleting is idempotent from the caller's view

            _repo.Remove(report);
            await _repo.SaveChangesAsync();
        }

        // Regenerates the report's entries to exactly match the calendar days in
        // [start, end], preserving any existing entry (by Date) whose date still
        // falls inside the new range so in-progress bullet text isn't lost when
        // the period is nudged.
        private void ReconcileEntries(DarReport report, DateTime start, DateTime end, List<DarEntryDto> incoming)
        {
            var incomingByDate = incoming
                .GroupBy(e => e.Date.Date)
                .ToDictionary(g => g.Key, g => g.First());

            var days = new List<DateTime>();
            for (var d = start; d <= end; d = d.AddDays(1)) days.Add(d);
            var daySet = days.ToHashSet();

            foreach (var stale in report.Entries.Where(e => !daySet.Contains(e.Date.Date)).ToList())
            {
                report.Entries.Remove(stale);
                _repo.RemoveEntry(stale);
            }

            foreach (var day in days)
            {
                var existing = report.Entries.FirstOrDefault(e => e.Date.Date == day);
                incomingByDate.TryGetValue(day, out var src);

                if (existing is null)
                {
                    existing = new DarEntry { Id = Guid.NewGuid(), Date = day };
                    report.Entries.Add(existing);
                }

                existing.EssentialFunctionsOverride = string.IsNullOrWhiteSpace(src?.EssentialFunctionsOverride)
                    ? null : src.EssentialFunctionsOverride.Trim();
                existing.AccomplishmentText = src?.AccomplishmentBullets is { Count: > 0 }
                    ? string.Join('\n', src.AccomplishmentBullets.Where(b => !string.IsNullOrWhiteSpace(b)))
                    : null;
                existing.IsWorkFromHome = src?.IsWorkFromHome ?? false;
            }
        }

        private static DarReportListItemDto ToListItemDto(DarReport r) => new()
        {
            Id = r.Id,
            PeriodStart = r.PeriodStart,
            PeriodEnd = r.PeriodEnd,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };

        private static DarReportDto ToDto(DarReport r) => new()
        {
            Id = r.Id,
            PeriodStart = r.PeriodStart,
            PeriodEnd = r.PeriodEnd,
            PreparedByName = r.PreparedByName,
            PreparedByPosition = r.PreparedByPosition,
            NotedByName = r.NotedByName,
            NotedByPosition = r.NotedByPosition,
            UseSharedEssentialFunctions = r.UseSharedEssentialFunctions,
            SharedEssentialFunctions = r.SharedEssentialFunctions,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            Entries = r.Entries
                .OrderBy(e => e.Date)
                .Select(e => new DarEntryDto
                {
                    Id = e.Id,
                    Date = e.Date,
                    EssentialFunctionsOverride = e.EssentialFunctionsOverride,
                    AccomplishmentBullets = string.IsNullOrWhiteSpace(e.AccomplishmentText)
                        ? new List<string>()
                        : e.AccomplishmentText.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    IsWorkFromHome = e.IsWorkFromHome
                })
                .ToList()
        };
    }
}
