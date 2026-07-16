using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Domain.Exceptions;
using EcaInformationSystem.Infrastructure.Persistence;
using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Collections.Immutable;
using System.Text.Json;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class BeneficiaryInformationRepository : IBeneficiaryInformationRepository
    {
        private readonly AppDbContext _context;
        private readonly IPsgcNameCache _psgcNameCache;

        public BeneficiaryInformationRepository(AppDbContext context, IPsgcNameCache psgcNameCache)
        {
            _context = context;
            _psgcNameCache = psgcNameCache;
        }
        public async Task SetCurrentPaymentHistoryAsync(Guid beneficiaryId, Guid historyId, string userName)
        {
            var beneficiary = await _context.BeneficiaryInformations
                .FirstOrDefaultAsync(b => b.Id == beneficiaryId);
            if (beneficiary == null)
                throw new Exception("Beneficiary not found.");

            var entry = await _context.BeneficiaryPaymentHistories
                .FirstOrDefaultAsync(h => h.Id == historyId && h.BeneficiaryInformationId == beneficiaryId);
            if (entry == null)
                throw new Exception("Payment history entry not found for this beneficiary.");

            // ✅ Mirror this entry's values onto the flat columns — same pattern as
            // BulkAddPaymentHistoryAsync/EditPaymentHistoryEntryAsync, so
            // Payroll/Export/CDR/Statistics stay consistent with whichever entry
            // is now marked current.
            beneficiary.CurrentPaymentHistoryId = entry.Id;
            beneficiary.PayrollQuarter = entry.PayrollQuarter;
            beneficiary.FiscalYear = entry.FiscalYear;
            beneficiary.PaymentStatus = entry.PaymentStatus;
            beneficiary.ModeOfPayment = entry.ModeOfPayment;
            beneficiary.PaymentDate = entry.PaymentDate;

            await _context.SaveChangesAsync();
        }
        // ✅ FIXED — now takes the actual generation ID as a parameter instead of
        // generating a fresh random Guid on every call (which previously guaranteed
        // this always returned an empty list, since nothing could ever match a
        // brand-new random Guid).
        public async Task<List<CgpRangeMemberDto>> GetCgpRangeMembersAsync(Guid cgpGenerationId, int municipalityCode, int milestoneYear)
        {
            if (cgpGenerationId == Guid.Empty) return new();

            // ✅ Deliberately unfiltered by PaymentStatus — the whole point of this
            // view is to show everyone on the page, Paid and Unpaid alike, for
            // audit transparency.
            var raw = await _context.BeneficiaryInformations
                .AsNoTracking()
                .Where(b => !b.IsDeleted
                    && b.CgpGenerationId == cgpGenerationId
                    && b.Municipality == municipalityCode)
                .Select(b => new
                {
                    b.Id,
                    b.LastName,
                    b.FirstName,
                    b.MiddleName,
                    b.CgpPageNumber,
                    b.PaymentStatus,
                    b.OscaIdNumber,
                    b.PaymentDate,
                    b.BirthDate
                })
                .ToListAsync();

            return raw
                .Select(r => new
                {
                    r.Id,
                    FullName = FormatDuplicateName(r.LastName, r.FirstName, r.MiddleName),
                    r.CgpPageNumber,
                    r.PaymentStatus,
                    OscaId = r.OscaIdNumber ?? string.Empty,
                    r.PaymentDate,
                    MilestoneYear = ComputeMilestoneYear(r.BirthDate)
                })
                .Where(x => x.MilestoneYear == milestoneYear)
                .OrderBy(x => x.CgpPageNumber)
                .ThenBy(x => x.FullName)
                .Select(x => new CgpRangeMemberDto
                {
                    Id = x.Id,
                    FullName = x.FullName,
                    CgpPageNumber = x.CgpPageNumber,
                    PaymentStatus = x.PaymentStatus,
                    OscaIdNumber = x.OscaId,
                    PaymentDate = x.PaymentDate,
                    MilestoneYear = x.MilestoneYear
                })
                .ToList();
        }
        // ✅ FIXED — now keyed by CgpGenerationId (guaranteed unique per Payroll run)
        // instead of CgpPrefix (which can coincidentally collide across two separate
        // generation runs if settings + the alphabetically-first record's milestone
        // year happen to match). This is what the Paid+Unpaid range math in
        // BuildCdrRowsAsync relies on to avoid blending two unrelated runs together.
        public async Task<List<CgpRangeCandidateDto>> GetCgpRangeCandidatesAsync(List<(Guid CgpGenerationId, int MunicipalityCode)> keys)
        {
            if (keys == null || !keys.Any()) return new();

            var generationIds = keys.Select(k => k.CgpGenerationId).Distinct().ToList();
            var municipalityCodes = keys.Select(k => k.MunicipalityCode).Distinct().ToList();

            // ✅ Deliberately NOT filtering by PaymentStatus — we need Paid AND
            // Unpaid beneficiaries who share the same payroll page-block, so the
            // printed CGP range reflects the true full span, not just who's Paid.
            var raw = await _context.BeneficiaryInformations
                .AsNoTracking()
                .Where(b => !b.IsDeleted
                    && b.CgpGenerationId.HasValue
                    && generationIds.Contains(b.CgpGenerationId!.Value)
                    && municipalityCodes.Contains(b.Municipality))
                .Select(b => new
                {
                    b.CgpGenerationId,
                    b.CgpPrefix,
                    b.CgpPageNumber,
                    b.Municipality,
                    b.BirthDate
                })
                .ToListAsync();

            // Exact-pair filter in memory — EF can't do tuple .Contains() directly.
            var keySet = keys.ToHashSet();

            return raw
                .Where(r => keySet.Contains((r.CgpGenerationId!.Value, r.Municipality)))
                .Select(r => new CgpRangeCandidateDto
                {
                    CgpGenerationId = r.CgpGenerationId!.Value,
                    CgpPrefix = r.CgpPrefix ?? string.Empty,
                    CgpPageNumber = r.CgpPageNumber,
                    PsgcCodeMunicipality = r.Municipality,
                    MilestoneYear = ComputeMilestoneYear(r.BirthDate)
                })
                .ToList();
        }
        public async Task<PaymentHistoryDeletedInfoDto> DeletePaymentHistoryAsync(Guid historyId)
        {
            var entry = await _context.BeneficiaryPaymentHistories.FindAsync(historyId);
            if (entry == null)
                throw new Exception("Payment history record not found.");

            var info = new PaymentHistoryDeletedInfoDto
            {
                BeneficiaryId = entry.BeneficiaryInformationId,
                PaymentStatus = entry.PaymentStatus,
                PayrollQuarter = entry.PayrollQuarter,
                FiscalYear = entry.FiscalYear
            };

            var beneficiary = await _context.BeneficiaryInformations
                .FirstOrDefaultAsync(b => b.Id == entry.BeneficiaryInformationId);

            bool wasCurrent = beneficiary != null && beneficiary.CurrentPaymentHistoryId == historyId;

            _context.BeneficiaryPaymentHistories.Remove(entry);

            // ✅ If we're deleting the CURRENT entry, promote the next most recent
            // remaining entry to current — never leave a beneficiary pointing at a
            // deleted row. If none remain, clear the pointer and the flat-column mirror.
            if (wasCurrent && beneficiary != null)
            {
                var next = await _context.BeneficiaryPaymentHistories
                    .Where(h => h.BeneficiaryInformationId == beneficiary.Id && h.Id != historyId)
                    .OrderByDescending(h => h.PaymentDate ?? h.DateCreated)
                    .ThenByDescending(h => h.DateCreated)
                    .FirstOrDefaultAsync();

                if (next != null)
                {
                    beneficiary.CurrentPaymentHistoryId = next.Id;
                    beneficiary.PayrollQuarter = next.PayrollQuarter;
                    beneficiary.FiscalYear = next.FiscalYear;
                    beneficiary.PaymentStatus = next.PaymentStatus;
                    beneficiary.ModeOfPayment = next.ModeOfPayment;
                    beneficiary.PaymentDate = next.PaymentDate;
                }
                else
                {
                    beneficiary.CurrentPaymentHistoryId = null;
                    beneficiary.PayrollQuarter = null;
                    beneficiary.FiscalYear = null;
                    beneficiary.PaymentStatus = 0;
                    beneficiary.ModeOfPayment = 0;
                    beneficiary.PaymentDate = null;
                }
            }

            await _context.SaveChangesAsync();
            return info;
        }
        public async Task EditPaymentHistoryEntryAsync(Guid historyId, int? payrollQuarter, int? fiscalYear,
         int paymentStatus, int? modeOfPayment, DateTime? paymentDate, string? remarks, string userName)
        {
            var entry = await _context.BeneficiaryPaymentHistories.FindAsync(historyId);
            if (entry == null)
                throw new Exception("Payment history record not found.");

            entry.PayrollQuarter = payrollQuarter;
            entry.FiscalYear = fiscalYear;
            entry.PaymentStatus = paymentStatus;
            entry.ModeOfPayment = paymentStatus == 2 ? (modeOfPayment ?? 0) : 0;
            entry.PaymentDate = paymentStatus == 2 ? paymentDate : null;
            entry.Remarks = remarks;
            entry.DateModified = DateTime.UtcNow;
            entry.ModifiedBy = userName;

            // ✅ NEW — only mirror if this entry is the beneficiary's CURRENT one
            var beneficiary = await _context.BeneficiaryInformations
                .FirstOrDefaultAsync(b => b.CurrentPaymentHistoryId == historyId);

            if (beneficiary != null)
            {
                beneficiary.PayrollQuarter = entry.PayrollQuarter;
                beneficiary.FiscalYear = entry.FiscalYear;
                beneficiary.PaymentStatus = entry.PaymentStatus;
                beneficiary.ModeOfPayment = entry.ModeOfPayment;
                beneficiary.PaymentDate = entry.PaymentDate;
            }

            // Note: this does NOT touch CurrentPaymentHistoryId. If the entry being
            // corrected happens to be the current one, it stays current — correcting
            // a typo shouldn't demote a record from "current" status. If you're
            // instead recording a genuinely NEW payment event, use
            // BulkAddPaymentHistoryAsync (with a single-item list), not this method.
            await _context.SaveChangesAsync();
        }
        public async Task BulkAddPaymentHistoryAsync(List<Guid> beneficiaryIds, int? payrollQuarter, int? fiscalYear,
         int paymentStatus, int? modeOfPayment, DateTime? paymentDate, string? remarks, string userName)
        {
            var beneficiaries = await _context.BeneficiaryInformations
                .Where(b => beneficiaryIds.Contains(b.Id) && !b.IsDeleted)
                .ToListAsync();

            var newHistoryEntries = new List<BeneficiaryPaymentHistory>();

            foreach (var b in beneficiaries)
            {
                var history = new BeneficiaryPaymentHistory
                {
                    Id = Guid.NewGuid(),
                    BeneficiaryInformationId = b.Id,
                    PayrollQuarter = payrollQuarter,
                    FiscalYear = fiscalYear,
                    PaymentStatus = paymentStatus,
                    ModeOfPayment = paymentStatus == 2 ? (modeOfPayment ?? 0) : 0,
                    PaymentDate = paymentStatus == 2 ? paymentDate : null,
                    Remarks = remarks,
                    DateCreated = DateTime.UtcNow,
                    CreatedBy = userName
                };

                newHistoryEntries.Add(history);

                // ── Assigning Id here, before SaveChangesAsync(), works because we
                // generated the Guid ourselves (Guid.NewGuid()) rather than letting
                // the database generate it. EF Core will insert both the history
                // row and this updated pointer in the SAME SaveChangesAsync() call,
                // in the correct dependency order (history row first, since
                // CurrentPaymentHistoryId's FK requires it to exist), without
                // needing a second round-trip to look up the new Id.
                b.CurrentPaymentHistoryId = history.Id;

                // ✅ NEW — mirror onto the flat columns so Payroll/Export/CDR/Statistics
                // (which still read these directly) stay in sync with the current history entry.
                b.PayrollQuarter = history.PayrollQuarter;
                b.FiscalYear = history.FiscalYear;
                b.PaymentStatus = history.PaymentStatus;
                b.ModeOfPayment = history.ModeOfPayment;
                b.PaymentDate = history.PaymentDate;
            }

            await _context.BeneficiaryPaymentHistories.AddRangeAsync(newHistoryEntries);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var conflictedNames = GetConflictedRecordNames(ex);
                throw new ConcurrencyException(
                    $"The following record(s) were modified by another user: {conflictedNames}. Please refresh and try again.", ex);
            }
        }
        public async Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(Guid beneficiaryId)
        {
            var currentId = await _context.BeneficiaryInformations
                .AsNoTracking()
                .Where(b => b.Id == beneficiaryId)
                .Select(b => b.CurrentPaymentHistoryId)
                .FirstOrDefaultAsync();

            return await _context.BeneficiaryPaymentHistories
                .AsNoTracking()
                .Where(h => h.BeneficiaryInformationId == beneficiaryId)
                // ✅ CHANGED — sort strictly by when the entry was added (DateCreated),
                // not by PaymentDate. A future-dated PaymentDate no longer jumps an
                // older entry above a more recently recorded one.
                .OrderByDescending(h => h.DateCreated)
                .Select(h => new PaymentHistoryDto
                {
                    Id = h.Id,
                    BeneficiaryInformationId = h.BeneficiaryInformationId,
                    PayrollQuarter = h.PayrollQuarter,
                    FiscalYear = h.FiscalYear,
                    PaymentStatus = h.PaymentStatus,
                    ModeOfPayment = h.ModeOfPayment,
                    PaymentDate = h.PaymentDate,
                    Remarks = h.Remarks,
                    DateCreated = h.DateCreated,
                    CreatedBy = h.CreatedBy,
                    DateModified = h.DateModified,
                    ModifiedBy = h.ModifiedBy,
                    IsCurrent = h.Id == currentId
                })
                .ToListAsync();
        }
        // Fallback fuzzy name search. Only call this when the normal exact/Contains
        // search already returned zero results and the search term looks name-like
        // (not a batch code, date, or status keyword). Pulls a narrow Id+Name
        // projection only — not full entities — so scoring the active population
        // in memory stays fast even without full-text search infrastructure.
        public async Task<List<Guid>> FindSimilarNameIdsAsync(string term, int maxResults = 50, double minScore = 0.75)
        {
            if (string.IsNullOrWhiteSpace(term))
                return new List<Guid>();

            var candidates = await _context.BeneficiaryInformations
                .AsNoTracking()
                .Where(b => !b.IsDeleted)
                .Select(b => new { b.Id, b.LastName, b.FirstName, b.MiddleName })
                .ToListAsync();

            var normalizedTerm = NormalizeSearchTerm(term);

            var scored = candidates
                .Select(c =>
                {
                    var fullName = $"{c.LastName} {c.FirstName} {c.MiddleName}".Trim();

                    // Whole-string similarity — catches close full-name matches
                    var wholeScore = ComputeNameSimilarity(normalizedTerm, fullName);

                    // Token-level similarity — catches a single mistyped word inside
                    // an otherwise-correct name, e.g. searching "Lance" against
                    // "URIARTE LENCE MICHAELA" should match on the "LENCE" token
                    // even though the whole string doesn't look alike overall.
                    var tokenScore = fullName
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(tok => ComputeNameSimilarity(normalizedTerm, tok))
                        .DefaultIfEmpty(0)
                        .Max();

                    return new { c.Id, Score = Math.Max(wholeScore, tokenScore) };
                })
                .Where(x => x.Score >= minScore)
                .OrderByDescending(x => x.Score)
                .Take(maxResults)
                .Select(x => x.Id)
                .ToList();

            return scored;
        }
        public async Task BulkSetCgpAssignmentsAsync(List<CgpAssignmentDto> assignments)
        {
            if (assignments == null || !assignments.Any()) return;

            var ids = assignments.Select(a => a.BeneficiaryId).Distinct().ToList();
            var beneficiaries = await _context.BeneficiaryInformations
                .Where(x => ids.Contains(x.Id))
                .ToListAsync();

            var lookup = assignments.ToDictionary(a => a.BeneficiaryId);

            foreach (var b in beneficiaries)
            {
                if (lookup.TryGetValue(b.Id, out var assignment))
                {
                    b.CgpPageNumber = assignment.CgpPageNumber;
                    b.CgpPrefix = assignment.CgpPrefix;
                    b.CgpGenerationId = assignment.CgpGenerationId;
                }
            }
            // SaveChangesAsync is called by the service, after logs are added
        }

        public async Task AddAsync(BeneficiaryInformation beneficiaryInformation)
        {
            await _context.BeneficiaryInformations.AddAsync(beneficiaryInformation);
        }
        public async Task<List<BeneficiaryInformation>> GetEntitiesByIdsAsync(List<Guid> ids)
        {
            return await _context.BeneficiaryInformations
                .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync();
        }

        public async Task<StatisticsReportDto> GetStatisticsReportAsync(StatisticsRequestDto request)
        {
            var query = _context.BeneficiaryInformations.AsNoTracking().Where(b => !b.IsDeleted);

            if (request.Region.HasValue && request.Region.Value > 0)
                query = query.Where(b => b.Region == request.Region.Value);
            if (request.Province.HasValue && request.Province.Value > 0)
                query = query.Where(b => b.Province == request.Province.Value);
            if (request.Municipality.HasValue && request.Municipality.Value > 0)
                query = query.Where(b => b.Municipality == request.Municipality.Value);

            if (request.MilestoneYear > 0)
            {
                var milestones = new[] { 80, 85, 90, 95, 100 };
                var birthYears = milestones.Select(m => request.MilestoneYear - m).ToList();
                query = query.Where(b => birthYears.Contains(b.BirthDate.Year));
            }
            if (request.MilestoneAge > 0)
            {
                var birthYear = DateTime.Today.Year - request.MilestoneAge;
                query = query.Where(b => b.BirthDate.Year == birthYear);
            }

            // ✅ NEW — when a specific period (Quarter/FiscalYear) or Payment Status is
            // requested, resolve it against the FULL payment history, and remember
            // WHICH history row matched each beneficiary so we can report on THAT
            // record's status/date rather than whatever their current record says.
            // This is what makes "Q1 2026" report exact Paid counts for Q1 2026, even
            // for people who have since been repaid/corrected into Q2 2026.
            bool hasPeriodFilter = request.PayrollQuarter.HasValue || request.FiscalYear.HasValue || request.PaymentStatus >= 0;
            Dictionary<Guid, BeneficiaryPaymentHistory> historyLookup = new();

            if (hasPeriodFilter)
            {
                var historyQuery = _context.BeneficiaryPaymentHistories.AsNoTracking().AsQueryable();

                if (request.PayrollQuarter.HasValue)
                    historyQuery = historyQuery.Where(h => h.PayrollQuarter == request.PayrollQuarter.Value);
                if (request.FiscalYear.HasValue)
                    historyQuery = historyQuery.Where(h => h.FiscalYear == request.FiscalYear.Value);
                if (request.PaymentStatus >= 0)
                    historyQuery = historyQuery.Where(h => h.PaymentStatus == request.PaymentStatus);

                var matches = await historyQuery
                    .OrderByDescending(h => h.DateModified ?? h.DateCreated)
                    .ToListAsync();

                // If a beneficiary somehow has more than one row matching (e.g. two
                // separate corrections both landing on Q1 2026), keep the most
                // recently touched one as authoritative for this report.
                historyLookup = matches
                    .GroupBy(h => h.BeneficiaryInformationId)
                    .ToDictionary(g => g.Key, g => g.First());

                query = query.Where(b => historyLookup.Keys.Contains(b.Id));
            }

            var allData = await query.ToListAsync();

            // ✅ Effective status/quarter/fiscal-year per beneficiary — pulled from the
            // matched historical entry when a period filter is active, otherwise
            // falls back to the beneficiary's current flat columns (unchanged
            // behavior for "no filter = show me current state" queries).
            int EffectiveStatus(BeneficiaryInformation b) =>
                hasPeriodFilter && historyLookup.TryGetValue(b.Id, out var h) ? h.PaymentStatus : b.PaymentStatus;
            int EffectiveQuarter(BeneficiaryInformation b) =>
                hasPeriodFilter && historyLookup.TryGetValue(b.Id, out var h) ? (h.PayrollQuarter ?? 0) : (b.PayrollQuarter ?? 0);

            var report = new StatisticsReportDto
            {
                TotalBeneficiaries = allData.Count,
                TotalMale = allData.Count(b => b.Sex == 1),
                TotalFemale = allData.Count(b => b.Sex == 2),
                PaidCount = allData.Count(b => EffectiveStatus(b) == 2),
                UnpaidCount = allData.Count(b => EffectiveStatus(b) == 1),
                PendingCount = allData.Count(b => EffectiveStatus(b) == 3),
                NotApplicableCount = allData.Count(b => EffectiveStatus(b) == 0),
                TotalDisbursement = allData
                    .Where(b => EffectiveStatus(b) == 2)
                    .Sum(b => PayrollSettingsDto.CalculateCashGiftAmount(ComputeAge(b.BirthDate))),
                FiscalYearBreakdown = allData
                    .Where(b => b.FiscalYear.HasValue)
                    .GroupBy(b => b.FiscalYear!.Value)
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            var milestoneAges = new[] { 80, 85, 90, 95, 100 };
            report.AgeDistribution = milestoneAges
                .Select(m => new AgeDistributionDto
                {
                    Age = m,
                    Count = allData.Count(b =>
                    {
                        var age = ComputeAge(b.BirthDate);
                        return age >= m && age < m + 5;
                    })
                })
                .ToList();

            var milestoneYears = new[] { 2024, 2025, 2026 };
            report.MilestoneYearSummary = milestoneYears
                .Select(year =>
                {
                    var birthYears = milestoneAges.Select(m => year - m).ToList();
                    var records = allData.Where(b => birthYears.Contains(b.BirthDate.Year)).ToList();
                    return new MilestoneYearSummaryDto
                    {
                        Year = year,
                        TotalCount = records.Count,
                        Age80Count = records.Count(b => b.BirthDate.Year == year - 80),
                        Age85Count = records.Count(b => b.BirthDate.Year == year - 85),
                        Age90Count = records.Count(b => b.BirthDate.Year == year - 90),
                        Age95Count = records.Count(b => b.BirthDate.Year == year - 95),
                        Age100Count = records.Count(b => b.BirthDate.Year == year - 100)
                    };
                })
                .ToList();

            var provinceGroups = allData.GroupBy(b => b.Province)
                .Select(g => new { ProvinceCode = g.Key, Items = g.ToList() }).ToList();

            report.ProvinceBreakdowns = provinceGroups
                .Select(g =>
                {
                    var provinceName = _psgcNameCache.GetProvinceName(g.ProvinceCode) ?? g.ProvinceCode.ToString();
                    var items = g.Items;
                    var paidItems = items.Where(b => EffectiveStatus(b) == 2).ToList();

                    return new ProvinceStatisticsDto
                    {
                        ProvinceName = provinceName,
                        TotalCount = items.Count,
                        Age80Count = items.Count(b => ComputeAge(b.BirthDate) >= 80 && ComputeAge(b.BirthDate) < 85),
                        Age85Count = items.Count(b => ComputeAge(b.BirthDate) >= 85 && ComputeAge(b.BirthDate) < 90),
                        Age90Count = items.Count(b => ComputeAge(b.BirthDate) >= 90 && ComputeAge(b.BirthDate) < 95),
                        Age95Count = items.Count(b => ComputeAge(b.BirthDate) >= 95 && ComputeAge(b.BirthDate) < 100),
                        Age100Count = items.Count(b => ComputeAge(b.BirthDate) >= 100),
                        MaleCount = items.Count(b => b.Sex == 1),
                        FemaleCount = items.Count(b => b.Sex == 2),
                        PaidCount = items.Count(b => EffectiveStatus(b) == 2),
                        UnpaidCount = items.Count(b => EffectiveStatus(b) == 1),
                        PendingCount = items.Count(b => EffectiveStatus(b) == 3),
                        NotApplicableCount = items.Count(b => EffectiveStatus(b) == 0),
                        TotalDisbursement = paidItems.Sum(b => PayrollSettingsDto.CalculateCashGiftAmount(ComputeAge(b.BirthDate)))
                    };
                })
                .OrderBy(p => p.ProvinceName)
                .ToList();

            var municipalityGroups = allData.GroupBy(b => new { b.Province, b.Municipality })
                .Select(g => new { g.Key.Province, g.Key.Municipality, Items = g.ToList() }).ToList();

            report.MunicipalityBreakdowns = municipalityGroups
                .Select(g =>
                {
                    var provinceName = _psgcNameCache.GetProvinceName(g.Province) ?? g.Province.ToString();
                    var municipalityName = _psgcNameCache.GetMunicipalityName(g.Municipality) ?? g.Municipality.ToString();
                    var items = g.Items;
                    var paidItems = items.Where(b => EffectiveStatus(b) == 2).ToList();

                    return new MunicipalityStatisticsDto
                    {
                        ProvinceName = provinceName,
                        MunicipalityName = municipalityName,
                        TotalCount = items.Count,
                        Age80Count = items.Count(b => ComputeAge(b.BirthDate) >= 80 && ComputeAge(b.BirthDate) < 85),
                        Age85Count = items.Count(b => ComputeAge(b.BirthDate) >= 85 && ComputeAge(b.BirthDate) < 90),
                        Age90Count = items.Count(b => ComputeAge(b.BirthDate) >= 90 && ComputeAge(b.BirthDate) < 95),
                        Age95Count = items.Count(b => ComputeAge(b.BirthDate) >= 95 && ComputeAge(b.BirthDate) < 100),
                        Age100Count = items.Count(b => ComputeAge(b.BirthDate) >= 100),
                        MaleCount = items.Count(b => b.Sex == 1),
                        FemaleCount = items.Count(b => b.Sex == 2),
                        TotalDisbursement = paidItems.Sum(b => PayrollSettingsDto.CalculateCashGiftAmount(ComputeAge(b.BirthDate)))
                    };
                })
                .OrderBy(m => m.ProvinceName).ThenBy(m => m.MunicipalityName)
                .ToList();

            report.TotalAge80 = report.ProvinceBreakdowns.Sum(p => p.Age80Count);
            report.TotalAge85 = report.ProvinceBreakdowns.Sum(p => p.Age85Count);
            report.TotalAge90 = report.ProvinceBreakdowns.Sum(p => p.Age90Count);
            report.TotalAge95 = report.ProvinceBreakdowns.Sum(p => p.Age95Count);
            report.TotalAge100 = report.ProvinceBreakdowns.Sum(p => p.Age100Count);

            report.PayrollQuarterBreakdown = allData
                .GroupBy(b => EffectiveQuarter(b))
                .Select(g => new PayrollQuarterStatisticsDto
                {
                    Quarter = g.Key,
                    Count = g.Count(),
                    PaidCount = g.Count(b => EffectiveStatus(b) == 2),
                    TotalDisbursement = g.Where(b => EffectiveStatus(b) == 2)
                        .Sum(b => PayrollSettingsDto.CalculateCashGiftAmount(ComputeAge(b.BirthDate)))
                })
                .OrderBy(q => q.Quarter)
                .ToList();

            return report;
        }
        public async Task<List<PossibleDuplicatePairDto>> FindAllPossibleDuplicatesAsync(
            BeneficiaryFilterDto filter,
            int maxPairs = 50,
            CancellationToken cancellationToken = default)
        {
            // ✅ Don't modify the filter - use it as-is
            // The filter already contains ALL the user's filters
            // Just make sure PageSize is large enough
            var scanFilter = new BeneficiaryFilterDto
            {
                // Copy all properties from the incoming filter
                PsgcCodeRegion = filter.PsgcCodeRegion,
                PsgcCodeProvinces = filter.PsgcCodeProvinces,
                PsgcCodeMunicipalities = filter.PsgcCodeMunicipalities,
                PsgcCodeBarangay = filter.PsgcCodeBarangay,
                LastName = filter.LastName,
                FirstName = filter.FirstName,
                FullName = filter.FullName,
                PaymentStatuses = filter.PaymentStatuses,
                PaymentDate = filter.PaymentDate,
                PaymentDateFrom = filter.PaymentDateFrom,
                PaymentDateTo = filter.PaymentDateTo,
                IsEligible = filter.IsEligible,
                EligibilityMode = filter.EligibilityMode,
                IsCompliant = filter.IsCompliant,
                ComplianceMode = filter.ComplianceMode,
                CoStatus = filter.CoStatus,
                FindingStatus = filter.FindingStatus,
                Sex = filter.Sex,
                FilterModeOfPayment = filter.FilterModeOfPayment,
                SpecificAge = filter.SpecificAge,
                MilestoneYear = filter.MilestoneYear,
                SpecificBirthday = filter.SpecificBirthday,
                BirthdayFrom = filter.BirthdayFrom,
                BirthdayTo = filter.BirthdayTo,
                FilterQuarter = filter.FilterQuarter,
                FilterBatch = filter.FilterBatch,
                FilterRefYear = filter.FilterRefYear,
                FilterRegionRoman = filter.FilterRegionRoman,
                FilterPayrollQuarter = filter.FilterPayrollQuarter,   // ✅ new — this was the actual bug
                FilterPayrollQuarters = filter.FilterPayrollQuarters, // ✅ new
                DateAddedFrom = filter.DateAddedFrom,
                DateAddedTo = filter.DateAddedTo,
                Validator = filter.Validator,
                BatchCode = filter.BatchCode,
                GeneralSearch = filter.GeneralSearch,  // ✅ CRITICAL: Include GeneralSearch
                DataQualityIssue = filter.DataQualityIssue,   // ✅ ADD
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            // ✅ Use BuildNarrowFilterQuery which already handles ALL filters including GeneralSearch
            var query = await BuildNarrowFilterQuery(scanFilter);

            var candidates = await query
                .Select(b => new
                {
                    b.Id,
                    b.FirstName,
                    b.LastName,
                    b.MiddleName,
                    b.BirthDate,
                    b.OscaIdNumber,
                    b.PaymentStatus,
                    b.Municipality,
                    b.Barangay
                })
                .ToListAsync(cancellationToken);

            if (candidates.Count < 2)
                return new List<PossibleDuplicatePairDto>();

            var byBirthYear = candidates
                .GroupBy(x => x.BirthDate.Year)
                .Where(g => g.Count() > 1)
                .ToList();

            var pairs = new List<PossibleDuplicatePairDto>();
            var seen = new HashSet<string>();

            foreach (var yearGroup in byBirthYear)
            {
                var group = yearGroup.ToList();

                for (int i = 0; i < group.Count; i++)
                {
                    for (int j = i + 1; j < group.Count; j++)
                    {
                        if (pairs.Count >= maxPairs)
                            goto Done;

                        var a = group[i];
                        var b = group[j];

                        var pairKey = string.Join("|", new[] { a.Id, b.Id }.OrderBy(x => x));
                        if (!seen.Add(pairKey)) continue;

                        var daysDiff = Math.Abs((a.BirthDate - b.BirthDate).TotalDays);
                        if (daysDiff > 365) continue;

                        var lastNameScore = ComputeNameSimilarity(a.LastName?.Trim(), b.LastName?.Trim());
                        if (lastNameScore < 0.60) continue;

                        var firstNameScore = ComputeNameSimilarity(a.FirstName?.Trim(), b.FirstName?.Trim());
                        if (firstNameScore < 0.60) continue;

                        bool aHasMiddle = !string.IsNullOrWhiteSpace(a.MiddleName);
                        bool bHasMiddle = !string.IsNullOrWhiteSpace(b.MiddleName);
                        bool neitherHas = !aHasMiddle && !bHasMiddle;
                        bool oneHas = aHasMiddle ^ bHasMiddle;

                        double middleScore;
                        MiddleNameStatus middleStatus;

                        if (neitherHas)
                        {
                            middleStatus = MiddleNameStatus.BothBlank;
                            middleScore = 1.0;
                        }
                        else if (oneHas)
                        {
                            middleStatus = MiddleNameStatus.OneBlank;
                            middleScore = 0.80;
                        }
                        else
                        {
                            middleScore = ComputeNameSimilarity(a.MiddleName!.Trim(), b.MiddleName!.Trim());
                            middleStatus = middleScore >= 0.75
                                ? MiddleNameStatus.Similar
                                : middleScore >= 0.40
                                    ? MiddleNameStatus.PartiallyDifferent
                                    : MiddleNameStatus.Conflicting;
                        }

                        double finalScore = neitherHas || oneHas
                            ? (lastNameScore * 0.50) + (firstNameScore * 0.50)
                            : (lastNameScore * 0.40) + (firstNameScore * 0.40) + (middleScore * 0.20);

                        bool firstLastExact = lastNameScore >= 0.99 && firstNameScore >= 0.99;

                        if (firstLastExact && middleStatus == MiddleNameStatus.Conflicting)
                            finalScore = Math.Max(finalScore, 0.80);

                        if (finalScore < 0.75) continue;

                        var reason = BuildDuplicateReason(firstLastExact, middleStatus, daysDiff, a.MiddleName, b.MiddleName);

                        pairs.Add(new PossibleDuplicatePairDto
                        {
                            Record1Id = a.Id,
                            Record1FullName = FormatDuplicateName(a.LastName, a.FirstName, a.MiddleName),
                            Record1BirthDate = a.BirthDate.ToString("MMMM dd, yyyy"),
                            Record1Municipality = _psgcNameCache.GetMunicipalityName(a.Municipality) ?? string.Empty,
                            Record1Barangay = _psgcNameCache.GetBarangayName(a.Barangay) ?? string.Empty,
                            Record1OscaId = a.OscaIdNumber ?? string.Empty,
                            Record1PaymentStatus = a.PaymentStatus,

                            Record2Id = b.Id,
                            Record2FullName = FormatDuplicateName(b.LastName, b.FirstName, b.MiddleName),
                            Record2BirthDate = b.BirthDate.ToString("MMMM dd, yyyy"),
                            Record2Municipality = _psgcNameCache.GetMunicipalityName(b.Municipality) ?? string.Empty,
                            Record2Barangay = _psgcNameCache.GetBarangayName(b.Barangay) ?? string.Empty,
                            Record2OscaId = b.OscaIdNumber ?? string.Empty,
                            Record2PaymentStatus = b.PaymentStatus,

                            MatchScore = Math.Round(finalScore, 2),
                            MatchReason = reason
                        });
                    }
                }
            }

        Done:
            return pairs.OrderByDescending(x => x.MatchScore).ToList();
        }
        public async Task<List<SoftDuplicateCandidateDto>> FindSoftDuplicatesAsync(string? firstName, string? lastName, DateTime birthDate, int birthdateToleranceDays = 365)
        {
            // ✅ Guard: if either name is missing there is nothing meaningful to compare
            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                return new List<SoftDuplicateCandidateDto>();

            var candidates = await _context.BeneficiaryInformations
                .Where(b => !b.IsDeleted
                    && b.BirthDate >= birthDate.AddDays(-birthdateToleranceDays)
                    && b.BirthDate <= birthDate.AddDays(birthdateToleranceDays))
                .Select(b => new
                {
                    b.Id,
                    b.FirstName,
                    b.LastName,
                    b.MiddleName,
                    b.BirthDate,
                    b.OscaIdNumber,
                    ProvinceName = _context.Provinces.Where(p => p.PsgcCodeProvince == b.Province)
                    .Select(p => p.Name)
                    .FirstOrDefault(),
                    MunicipalityName = _context.Municipalities.Where(m => m.PsgcCodeMunicipality == b.Municipality)
                    .Select(m => m.Name)
                    .FirstOrDefault(),
                    BarangayName = _context.Barangays.Where(br => br.PsgcCodeBarangay == b.Barangay)
                    .Select(br => br.Name)
                    .FirstOrDefault()
                })
                .ToListAsync();

            // ✅ After — no stray spaces
            var incomingFullName = string.Join(" ",
                new[] { firstName?.Trim(), lastName?.Trim() }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            return candidates
                .Select(b =>
                {
                    // ✅ Same for each candidate inside .Select()
                    var existingFullName = string.Join(" ",
                        new[] { b.FirstName?.Trim(), b.LastName?.Trim() }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));

                    // ✅ If existing record has no name at all, score zero — skip it
                    if (string.IsNullOrWhiteSpace(existingFullName))
                        return new { Record = b, Score = 0.0 };

                    return new
                    {
                        Record = b,
                        Score = ComputeNameSimilarity(incomingFullName, existingFullName)
                    };
                })
                .Where(x => x.Score >= 0.75)
                .Select(x => new SoftDuplicateCandidateDto
                {
                    ExistingId = x.Record.Id,
                    ExistingFullName = string.Join(", ",
                        new[] { x.Record.LastName?.Trim(), x.Record.FirstName?.Trim() }
                        .Where(s => !string.IsNullOrWhiteSpace(s))) +
                        (string.IsNullOrWhiteSpace(x.Record.MiddleName)
                            ? string.Empty
                            : $" {x.Record.MiddleName.Trim()}"),
                    ExistingMiddleName = x.Record.MiddleName?.Trim() ?? string.Empty,
                    ExistingBirthDate = x.Record.BirthDate,
                    ExistingOscaId = x.Record.OscaIdNumber ?? string.Empty,
                    ExistingProvince = x.Record.ProvinceName ?? string.Empty,
                    ExistingMunicipality = x.Record.MunicipalityName ?? string.Empty,
                    ExistingBarangay = x.Record.BarangayName ?? string.Empty,
                    MatchScore = x.Score
                })
                .OrderByDescending(x => x.MatchScore)
                .ToList();
        }

        public async Task<bool> ExistsDuplicateAsync(string? lastName, string? firstName, string? middleName, DateTime birthDate, Guid? excludeId = null)
        {
            var normalizedLastName = (lastName ?? string.Empty).Trim().ToLower();
            var normalizedFirstName = (firstName ?? string.Empty).Trim().ToLower();
            var normalizedMiddleName = (middleName ?? string.Empty).Trim().ToLower();
            var normalizedBirthDate = birthDate.Date;

            var query = _context.BeneficiaryInformations
                .AsNoTracking()
                .Where(x => !x.IsDeleted);

            if (excludeId.HasValue)
                query = query.Where(x => x.Id != excludeId.Value);

            return await query.AnyAsync(x =>
                (x.LastName ?? string.Empty).Trim().ToLower() == normalizedLastName &&
                (x.FirstName ?? string.Empty).Trim().ToLower() == normalizedFirstName &&
                (x.MiddleName ?? string.Empty).Trim().ToLower() == normalizedMiddleName &&
                x.BirthDate.Date == normalizedBirthDate);
        }

        public async Task<BeneficiaryInformation?> GetEntityByIdAsync(Guid id)
        {
            return await _context.BeneficiaryInformations
                .FirstOrDefaultAsync(x => x.Id == id);
        }
        public async Task<BeneficiaryInformationDto?> GetByIdAsync(Guid id)
        {
            var result = await (
                from b in _context.BeneficiaryInformations

                join region in _context.Regions
                    on b.Region equals region.PsgcCodeRegion into regionJoin
                from region in regionJoin.DefaultIfEmpty()

                join province in _context.Provinces
                    on b.Province equals province.PsgcCodeProvince into provinceJoin
                from province in provinceJoin.DefaultIfEmpty()

                join municipality in _context.Municipalities
                    on b.Municipality equals municipality.PsgcCodeMunicipality into municipalityJoin
                from municipality in municipalityJoin.DefaultIfEmpty()

                join barangay in _context.Barangays
                    on b.Barangay equals barangay.PsgcCodeBarangay into barangayJoin
                from barangay in barangayJoin.DefaultIfEmpty()

                    // ✅ NEW — same 1:1 join pattern already used in GetPagedListAsync
                join finding in _context.BeneficiaryFindings
                    on b.Id equals finding.BeneficiaryInformationId into findingJoin
                from finding in findingJoin.DefaultIfEmpty()

                where b.Id == id && !b.IsDeleted

                select new BeneficiaryInformationDto
                {
                    Id = b.Id,
                    Quarter = b.Quarter,
                    Batch = b.Batch,
                    RefYear = b.RefYear,
                    RefCode = b.RefCode,
                    DateApplied = b.DateApplied,
                    DateEndorsed = b.DateEndorsed,
                    BatchCode = b.BatchCode,
                    OscaIdNumber = b.OscaIdNumber,
                    OscaIdDateIssued = b.OscaIdDateIssued,
                    NcscRrn = b.NcscRrn,
                    LastName = b.LastName,
                    FirstName = b.FirstName,
                    MiddleName = b.MiddleName,
                    Extension = b.Extension,
                    BirthDate = b.BirthDate,
                    PhoneNumber = b.PhoneNumber,
                    Age = DateTime.Today.Year - b.BirthDate.Year -
                                           (b.BirthDate.Date > DateTime.Today.AddYears(
                                               -(DateTime.Today.Year - b.BirthDate.Year)) ? 1 : 0),
                    Sex = b.Sex,
                    IsIndigenousPeople = b.IsIndigenousPeople,
                    IsPersonWithDisability = b.IsPersonWithDisability,
                    CivilStatus = b.CivilStatus,
                    Citizenship = b.Citizenship,

                    // ✅ Integer PSGC codes — needed for dropdown pre-selection in the edit form
                    PsgcCodeRegion = b.Region,
                    PsgcCodeProvince = b.Province,
                    PsgcCodeMunicipality = b.Municipality,
                    PsgcCodeBarangay = b.Barangay,

                    // ✅ Name joins — needed for display labels in the form
                    Region = region != null ? JsonSerializer.SerializeToElement(region.Name) : null,
                    Province = province != null ? JsonSerializer.SerializeToElement(province.Name) : null,
                    Municipality = municipality != null ? JsonSerializer.SerializeToElement(municipality.Name) : null,
                    Barangay = barangay != null ? JsonSerializer.SerializeToElement(barangay.Name) : null,

                    IsCompliant = b.IsCompliant,
                    Validator = b.Validator,
                    ValidationDate = b.ValidationDate,
                    PayrollQuarter = b.PayrollQuarter,
                    FiscalYear = b.FiscalYear,
                    PaymentStatus = b.PaymentStatus,
                    ModeOfPayment = b.ModeOfPayment,
                    PaymentDate = b.PaymentDate,
                    IsDeceased = b.IsDeceased,       // ✅ fixes the checkbox bug
                    DateOfDeath = b.DateOfDeath,       // ✅ fixes date of death bug
                    IsEligible = b.IsEligible,
                    AssessmentRemarks = b.AssessmentRemarks,
                    EligibilityRemarks = b.EligibilityRemarks,  // ✅
                    RemarkCategory = b.RemarkCategory,
                    Remarks = b.Remarks,
                    DateAdded = b.DateAdded,
                    CoStatus = b.CoStatus,
                    CoDateEndorsed = b.CoDateEndorsed,
                    CoDateApproved = b.CoDateApproved,
                    IsDeleted = b.IsDeleted,
                    RowVersion = b.RowVersion,

                    // ✅ NEW — now sourced from the finding join
                    FindingStatus = finding != null ? finding.FindingStatus : (int?)null,
                    FindingRemarks = finding != null ? finding.FindingRemarks : null
                }
            ).AsNoTracking().FirstOrDefaultAsync();

            return result;
        }
        public async Task<int?> GetRegionCodeByNameAsync(string regionName)
        {
            if (string.IsNullOrWhiteSpace(regionName))
                return null;

            var normalized = regionName.Trim().ToLower();

            var region = await _context.Regions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name!.ToLower() == normalized);

            return region?.PsgcCodeRegion;
        }

        public async Task<int?> GetProvinceCodeByNameAsync(string provinceName)
        {
            if (string.IsNullOrWhiteSpace(provinceName))
                return null;

            var normalized = provinceName.Trim().ToLower();

            var province = await _context.Provinces
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name!.ToLower() == normalized);

            return province?.PsgcCodeProvince;
        }

        public async Task<int?> GetMunicipalityCodeByNameAsync(string municipalityName)
        {
            if (string.IsNullOrWhiteSpace(municipalityName))
                return null;

            var normalized = municipalityName.Trim().ToLower();

            var municipality = await _context.Municipalities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name!.ToLower() == normalized);

            return municipality?.PsgcCodeMunicipality;
        }

        public async Task<int?> GetBarangayCodeByNameAsync(string barangayName)
        {
            if (string.IsNullOrWhiteSpace(barangayName))
                return null;

            var normalized = barangayName.Trim().ToLower();

            var barangay = await _context.Barangays
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name!.ToLower() == normalized);

            return barangay?.PsgcCodeBarangay;
        }
        public async Task<IEnumerable<BeneficiaryInformationDto>> FilterAsync(BeneficiaryFilterDto filter)
        {

            // FilterAsync — AFTER:
            var raw = await BuildBeneficiaryRawQuery(filter)
                .OrderBy(x => x.LastName)
                  .ThenBy(x => x.FirstName)
                  .ThenBy(x => x.MiddleName)
                  .ToListAsync();
            return raw.Select(MapToDto).ToList();
        }

        public async Task<IEnumerable<BeneficiaryInformationDto>> GetAllAsync()
        {
            //This is where our joining of tables will be done, we will use the Include method to include the related tables
            var result = await (
             from b in _context.BeneficiaryInformations

             join region in _context.Regions on b.Region equals region.PsgcCodeRegion into regionJoin
             from region in regionJoin.DefaultIfEmpty()

             join province in _context.Provinces on b.Province equals province.PsgcCodeProvince into provinceJoin
             from province in provinceJoin.DefaultIfEmpty()

             join municipality in _context.Municipalities on b.Municipality equals municipality.PsgcCodeMunicipality into municipalityJoin
             from municipality in municipalityJoin.DefaultIfEmpty()

             join barangay in _context.Barangays on b.Barangay equals barangay.PsgcCodeBarangay into barangayJoin
             from barangay in barangayJoin.DefaultIfEmpty()

             where !b.IsDeleted
             select new BeneficiaryInformationDto
             {
                 Id = b.Id,
                 Quarter = b.Quarter,
                 Batch = b.Batch,
                 RefYear = b.RefYear,
                 RefCode = b.RefCode,
                 DateApplied = b.DateApplied,
                 DateEndorsed = b.DateEndorsed,
                 BatchCode = b.BatchCode,
                 OscaIdNumber = b.OscaIdNumber,
                 OscaIdDateIssued = b.OscaIdDateIssued,
                 NcscRrn = b.NcscRrn,
                 LastName = b.LastName,
                 FirstName = b.FirstName,
                 MiddleName = b.MiddleName,
                 Extension = b.Extension,
                 BirthDate = b.BirthDate,
                 PhoneNumber = b.PhoneNumber,
                 Age = DateTime.Today.Year - b.BirthDate.Year -
                 (b.BirthDate.Date > DateTime.Today.AddYears(-(DateTime.Today.Year - b.BirthDate.Year)) ? 1 : 0),

                 MilestoneYear =
                   (b.BirthDate.Year + 100) < DateTime.Today.Year && (b.BirthDate.Year + 100) >= 2024 ? b.BirthDate.Year + 100 :
                   (b.BirthDate.Year + 100) == DateTime.Today.Year && b.BirthDate.DayOfYear <= DateTime.Today.DayOfYear && (b.BirthDate.Year + 100) >= 2024 ? b.BirthDate.Year + 100 :
                   (b.BirthDate.Year + 95) < DateTime.Today.Year && (b.BirthDate.Year + 95) >= 2024 ? b.BirthDate.Year + 95 :
                   (b.BirthDate.Year + 95) == DateTime.Today.Year && b.BirthDate.DayOfYear <= DateTime.Today.DayOfYear && (b.BirthDate.Year + 95) >= 2024 ? b.BirthDate.Year + 95 :
                   (b.BirthDate.Year + 90) < DateTime.Today.Year && (b.BirthDate.Year + 90) >= 2024 ? b.BirthDate.Year + 90 :
                   (b.BirthDate.Year + 90) == DateTime.Today.Year && b.BirthDate.DayOfYear <= DateTime.Today.DayOfYear && (b.BirthDate.Year + 90) >= 2024 ? b.BirthDate.Year + 90 :
                   (b.BirthDate.Year + 85) < DateTime.Today.Year && (b.BirthDate.Year + 85) >= 2024 ? b.BirthDate.Year + 85 :
                   (b.BirthDate.Year + 85) == DateTime.Today.Year && b.BirthDate.DayOfYear <= DateTime.Today.DayOfYear && (b.BirthDate.Year + 85) >= 2024 ? b.BirthDate.Year + 85 :
                   (b.BirthDate.Year + 80) < DateTime.Today.Year && (b.BirthDate.Year + 80) >= 2024 ? b.BirthDate.Year + 80 :
                   (b.BirthDate.Year + 80) == DateTime.Today.Year && b.BirthDate.DayOfYear <= DateTime.Today.DayOfYear && (b.BirthDate.Year + 80) >= 2024 ? b.BirthDate.Year + 80 :
                   0,

                 IsIndigenousPeople = b.IsIndigenousPeople,
                 IsPersonWithDisability = b.IsPersonWithDisability,
                 CivilStatus = b.CivilStatus != null ? b.CivilStatus : null,
                 Citizenship = b.Citizenship != null ? b.Citizenship : null,
                 Sex = b.Sex,
                 PsgcCodeRegion = b.Region,
                 Region = region != null ? JsonSerializer.SerializeToElement(region.Name) : null,
                 PsgcCodeProvince = b.Province,
                 Province = province != null ? JsonSerializer.SerializeToElement(province.Name) : null,
                 PsgcCodeMunicipality = b.Municipality,
                 Municipality = municipality != null ? JsonSerializer.SerializeToElement(municipality.Name) : null,
                 PsgcCodeBarangay = b.Barangay,
                 Barangay = barangay != null ? JsonSerializer.SerializeToElement(barangay.Name) : null,
                 IsCompliant = b.IsCompliant,
                 Validator = b.Validator,
                 ValidationDate = b.ValidationDate,
                 PayrollQuarter = b.PayrollQuarter,
                 PaymentStatus = b.PaymentStatus,
                 ModeOfPayment = b.ModeOfPayment,
                 PaymentDate = b.PaymentDate,
                 IsDeceased = b.IsDeceased,
                 DateOfDeath = b.DateOfDeath,
                 IsEligible = b.IsEligible,
                 AssessmentRemarks = b.AssessmentRemarks,
                 EligibilityRemarks = b.EligibilityRemarks,  // ✅
                 RemarkCategory = b.RemarkCategory != null ? b.RemarkCategory : null,
                 DateAdded = b.DateAdded,
                 CoStatus = b.CoStatus,
                 CoDateEndorsed = b.CoDateEndorsed,
                 CoDateApproved = b.CoDateApproved,
                 Remarks = b.Remarks,
                 IsDeleted = b.IsDeleted
             })
             .AsNoTracking()
             .ToListAsync();

            return result;
        }

        public async Task<BeneficiarySummaryResultDto> GetSummaryAsync(BeneficiaryFilterDto filter)
        {
            //var beneficiaries = await BuildBeneficiaryDtoQuery(filter).ToListAsync();

            // Materialize first, then deduplicate in memory

            var allItems = await BuildBeneficiaryRawQuery(filter).OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.MiddleName)
                .ToListAsync();
            // Deduplicate in memory — safe here since it's already a List
            var deduplicated = allItems.DistinctBy(x => x.Id).Select(MapToDto).ToList();

            var result = new BeneficiarySummaryResultDto
            {
                Beneficiaries = deduplicated,
                TotalBeneficiaries = deduplicated.Count,
                TotalMale = deduplicated.Count(x => x.Sex == 1),
                TotalFemale = deduplicated.Count(x => x.Sex == 2),

                ProvinceCounts = deduplicated
                    .Where(x => !string.IsNullOrWhiteSpace(x.Province.ToString()))
                    .GroupBy(x => x.Province!)
                    .Select(g => new ProvinceCountDto
                    {
                        Province = g.Key.ToString()!,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Province)
                    .ToList(),

                MunicipalityCounts = deduplicated
                    .Where(x => !string.IsNullOrWhiteSpace(x.Municipality.ToString()))
                    .GroupBy(x => x.Municipality!)
                    .Select(g => new MunicipalityCountDto
                    {
                        Municipality = g.Key.ToString()!,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Municipality)
                    .ToList()
            };

            return result;
        }


        public async Task SaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // ✅ Translate EF Core exception → Domain exception
                // Application layer catches ConcurrencyException, never DbUpdateConcurrencyException
                throw new ConcurrencyException(
                    "This record was modified by another user while you were editing it. " +
                    "Please reload the record and apply your changes again.",
                    ex);
            }
        }

        public Task UpdateAsync(BeneficiaryInformation beneficiaryInformation)
        {
            _context.BeneficiaryInformations.Update(beneficiaryInformation);
            return Task.CompletedTask;
        }

        public async Task BulkUpdateEligibilityAndBatchCodeAsync(
     List<Guid> ids,
     bool? isEligible,
     string? batchCode,
     Dictionary<Guid, byte[]>? rowVersions = null)
        {
            var beneficiaries = await _context.BeneficiaryInformations
                .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync();

            foreach (var b in beneficiaries)
            {
                if (rowVersions != null && rowVersions.TryGetValue(b.Id, out var rv))
                {
                    _context.Entry(b)
                            .Property(x => x.RowVersion)
                            .OriginalValue = rv;
                }

                if (isEligible.HasValue)
                    b.IsEligible = isEligible.Value;

                if (batchCode != null)
                    b.BatchCode = string.IsNullOrWhiteSpace(batchCode)
                        ? null
                        : batchCode.Trim();
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var conflictedNames = GetConflictedRecordNames(ex);

                throw new ConcurrencyException(
                    $"The following record(s) were modified by another user: " +
                    $"{conflictedNames}. Please refresh and try again.", ex);
            }
        }
        public async Task BulkUpdateCoStatusAsync(
            List<Guid> ids,
            int? coStatus,
            DateTime? coDateEndorsed,
            DateTime? coDateApproved,
            Dictionary<Guid, byte[]>? rowVersions = null)
        {
            var beneficiaries = await _context.BeneficiaryInformations
                .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync();

            foreach (var b in beneficiaries)
            {
                if (rowVersions != null && rowVersions.TryGetValue(b.Id, out var rv))
                {
                    _context.Entry(b)
                            .Property(x => x.RowVersion)
                            .OriginalValue = rv;
                }

                if (coStatus.HasValue)
                    b.CoStatus = coStatus.Value;

                if (coStatus == 1)
                    b.CoDateEndorsed = coDateEndorsed;
                else if (coStatus == 2)
                    b.CoDateApproved = coDateApproved;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var conflictedNames = GetConflictedRecordNames(ex);

                throw new ConcurrencyException(
                    $"The following record(s) were modified by another user: " +
                    $"{conflictedNames}. Please refresh and try again.", ex);
            }
        }
        public async Task BulkUpdatePaymentStatusAsync(
        List<Guid> ids,
        int paymentStatus,
        int? modeOfPayment,
        DateTime? paymentDate,
        Dictionary<Guid, byte[]>? rowVersions = null)
        {
            var beneficiaries = await _context.BeneficiaryInformations
                .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync();

            foreach (var b in beneficiaries)
            {
                // ✅ Set the original RowVersion the client saw
                // EF Core will now check: WHERE Id = X AND RowVersion = [client version]
                // If DB has a newer version → DbUpdateConcurrencyException
                if (rowVersions != null && rowVersions.TryGetValue(b.Id, out var rv))
                {
                    _context.Entry(b)
                            .Property(x => x.RowVersion)
                            .OriginalValue = rv;
                }

                b.PaymentStatus = paymentStatus;

                if (paymentStatus == 2)
                {
                    b.PaymentDate = paymentDate;
                    b.ModeOfPayment = modeOfPayment ?? 0;
                    // ✅ PayrollQuarter is no longer touched here — it's independent
                    // of Payment Status now. See BulkUpdatePayrollQuarterAsync.
                }
                else
                {
                    b.PaymentDate = null;
                    b.ModeOfPayment = 0;
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // ✅ Find which records caused the conflict for a better error message
                var conflictedNames = GetConflictedRecordNames(ex);

                throw new ConcurrencyException(
                    $"The following record(s) were modified by another user: " +
                    $"{conflictedNames}. Please refresh and try again.", ex);
            }
        }
        // WHY THIS QUERY IS FASTER THAN GetPagedAsync:
        //
        // 1. NO JOINS to Region/Province/Municipality/Barangay. Names resolved
        //    in-memory via _psgcCache AFTER the query returns. This removes the
        //    join-fanout that previously forced an in-memory DistinctBy() — at the
        //    SQL level there is exactly one row per beneficiary now, because there
        //    is nothing left in the query that can multiply rows per beneficiary
        //    (the Finding join is 1:1, enforced by your unique index on
        //    BeneficiaryInformationId in BeneficiaryFinding).
        //
        // 2. NARROW SELECT — no full Remarks text, only SQL-side truncated previews
        //    via Substring(), which EF translates to SQL Server's SUBSTRING().
        //
        // 3. COUNT runs against the SAME narrow, join-free filtered query, so the
        //    count itself is cheap too — not just the page fetch.
        //
        // 4. Combined with the Step 1 composite indexes, a filtered+sorted page
        //    fetch becomes a single ordered index seek with Skip/Take, no separate
        //    Sort operator, no join cost.
        public async Task<int> CountMatchingAsync(BeneficiaryFilterDto filter)
        {
            var query = await BuildNarrowFilterQuery(filter);
            return await query.CountAsync();
        }
        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(BeneficiaryFilterDto filter)
        {
            var query = await BuildNarrowFilterQuery(filter);

            // ── Region-wide payment counts ─────────────────────────────────────
            var paymentCounts = await query
                .GroupBy(b => b.PaymentStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var total = paymentCounts.Sum(x => x.Count);

            // ── Region-wide disbursement ───────────────────────────────────────
            var paidBirthDates = await query
                .Where(b => b.PaymentStatus == 2)
                .Select(b => b.BirthDate)
                .ToListAsync();

            var totalDisbursement = paidBirthDates
                .Sum(bd => PayrollSettingsDto.CalculateCashGiftAmount(ComputeAge(bd)));

            // ── Per-province breakdown ─────────────────────────────────────────
            // Group by Province + PaymentStatus in one SQL query, resolve names
            // in memory via the cache — no joins, no extra round trips.
            var provinceRaw = await query
                .GroupBy(b => new { b.Province, b.PaymentStatus })
                .Select(g => new
                {
                    ProvinceCode = g.Key.Province,
                    Status = g.Key.PaymentStatus,
                    Count = g.Count()
                })
                .ToListAsync();

            // Need BirthDates per province for paid records to compute disbursement
            var paidByProvince = await query
                .Where(b => b.PaymentStatus == 2)
                .Select(b => new { b.Province, b.BirthDate })
                .ToListAsync();

            var provinceBreakdowns = provinceRaw
                .GroupBy(x => x.ProvinceCode)
                .Select(g =>
                {
                    var provinceName = _psgcNameCache.GetProvinceName(g.Key) ?? g.Key.ToString();
                    var paidBds = paidByProvince
                        .Where(p => p.Province == g.Key)
                        .Select(p => p.BirthDate)
                        .ToList();

                    return new ProvinceBreakdownDto
                    {
                        ProvinceName = provinceName,
                        PaidCount = g.FirstOrDefault(x => x.Status == 2)?.Count ?? 0,
                        UnpaidCount = g.FirstOrDefault(x => x.Status == 1)?.Count ?? 0,
                        PendingCount = g.FirstOrDefault(x => x.Status == 3)?.Count ?? 0,
                        NotApplicableCount = g.FirstOrDefault(x => x.Status == 0)?.Count ?? 0,
                        TotalCount = g.Sum(x => x.Count),
                        TotalDisbursement = paidBds
                            .Sum(bd => PayrollSettingsDto.CalculateCashGiftAmount(ComputeAge(bd)))
                    };
                })
                .OrderBy(p => p.ProvinceName)
                .ToList();

            return new DashboardSummaryDto
            {
                TotalBeneficiaries = total,
                PaidCount = paymentCounts.FirstOrDefault(x => x.Status == 2)?.Count ?? 0,
                UnpaidCount = paymentCounts.FirstOrDefault(x => x.Status == 1)?.Count ?? 0,
                PendingCount = paymentCounts.FirstOrDefault(x => x.Status == 3)?.Count ?? 0,
                NotApplicableCount = paymentCounts.FirstOrDefault(x => x.Status == 0)?.Count ?? 0,
                TotalDisbursement = totalDisbursement,
                ProvinceBreakdowns = provinceBreakdowns
            };
        }
        public async Task<PagedResultDto<BeneficiaryListItemDto>> GetPagedListAsync(BeneficiaryFilterDto filter)
        {
            var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

            var baseQuery = await BuildNarrowFilterQuery(filter);
            var totalCount = await baseQuery.CountAsync();

            string sortColumn = filter.SortColumn?.ToLower() ?? "default";
            bool isAscending = filter.SortAscending;

            IQueryable<BeneficiaryInformation> sorted = sortColumn switch
            {
                "birthdate" => isAscending
                    ? baseQuery.OrderBy(b => b.BirthDate).ThenBy(b => b.LastName).ThenBy(b => b.FirstName)
                    : baseQuery.OrderByDescending(b => b.BirthDate).ThenBy(b => b.LastName).ThenBy(b => b.FirstName),
                "batchcode" => isAscending
                    ? baseQuery.OrderBy(b => b.BatchCode).ThenBy(b => b.LastName).ThenBy(b => b.FirstName)
                    : baseQuery.OrderByDescending(b => b.BatchCode).ThenBy(b => b.LastName).ThenBy(b => b.FirstName),
                _ => baseQuery.OrderBy(b => b.LastName).ThenBy(b => b.FirstName).ThenBy(b => b.MiddleName)
            };

            // ── Step 1: page the beneficiaries + Findings join only (single GroupJoin, known-good shape) ──
            var pageRaw = await sorted
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .GroupJoin(
                    _context.BeneficiaryFindings,
                    b => b.Id,
                    f => f.BeneficiaryInformationId,
                    (b, findings) => new { b, finding = findings.FirstOrDefault() })
                .Select(x => new
                {
                    x.b.Id,
                    x.b.Quarter,
                    x.b.Batch,
                    x.b.RefYear,
                    x.b.RefCode,
                    x.b.BatchCode,
                    x.b.PhoneNumber,
                    x.b.LastName,
                    x.b.FirstName,
                    x.b.MiddleName,
                    x.b.Extension,
                    x.b.BirthDate,
                    x.b.Sex,
                    x.b.Region,
                    x.b.Province,
                    x.b.Municipality,
                    x.b.Barangay,
                    x.b.Validator,
                    x.b.IsEligible,
                    x.b.IsCompliant,
                    x.b.CoStatus,
                    x.b.CoDateEndorsed,
                    x.b.CoDateApproved,
                    x.b.RowVersion,
                    x.b.DateEndorsed,
                    x.b.DateApplied,
                    x.b.NcscRrn,
                    x.b.CurrentPaymentHistoryId,
                    HasDocuments = _context.BeneficiaryDocuments
                        .Any(d => d.BeneficiaryInformationId == x.b.Id && !d.IsDeleted),
                    FindingStatus = x.finding != null ? x.finding.FindingStatus : (int?)null,
                    EligibilityRemarksPreview = x.b.EligibilityRemarks != null && x.b.EligibilityRemarks.Length > 80
                        ? x.b.EligibilityRemarks.Substring(0, 80) : x.b.EligibilityRemarks,
                    AssessmentRemarksPreview = x.b.AssessmentRemarks != null && x.b.AssessmentRemarks.Length > 80
                        ? x.b.AssessmentRemarks.Substring(0, 80) : x.b.AssessmentRemarks,
                    FindingRemarksPreview = x.finding != null && x.finding.FindingRemarks != null
                                             && x.finding.FindingRemarks.Length > 80
                        ? x.finding.FindingRemarks.Substring(0, 80)
                        : (x.finding != null ? x.finding.FindingRemarks : null)
                })
                .ToListAsync();

            var pageIds = pageRaw.Select(x => x.Id).ToList();

            var historyCounts = await _context.BeneficiaryPaymentHistories
                .Where(h => pageIds.Contains(h.BeneficiaryInformationId))
                .GroupBy(h => h.BeneficiaryInformationId)
                .Select(g => new { BeneficiaryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BeneficiaryId, x => x.Count);

            var recentHistory = await _context.BeneficiaryPaymentHistories
                .Where(h => pageIds.Contains(h.BeneficiaryInformationId))
                .OrderByDescending(h => h.PaymentDate ?? h.DateCreated)
                .ThenByDescending(h => h.DateCreated)
                .ToListAsync();

            var summaryByBeneficiary = recentHistory
                .GroupBy(h => h.BeneficiaryInformationId)
                .ToDictionary(g => g.Key, g => string.Join(" • ", g.Take(3).Select(h =>
                 $"Q{(h.PayrollQuarter?.ToString() ?? "-")} {h.FiscalYear?.ToString() ?? ""}: {PaymentStatusLabelFor(h.PaymentStatus)}"
                .Trim())));

            // ── Step 2: fetch current payment history rows for just this page, in one small query ──
            var historyIds = pageRaw
                .Where(x => x.CurrentPaymentHistoryId.HasValue)
                .Select(x => x.CurrentPaymentHistoryId!.Value)
                .Distinct()
                .ToList();

            var historyLookup = historyIds.Any()
                ? await _context.BeneficiaryPaymentHistories
                    .AsNoTracking()
                    .Where(h => historyIds.Contains(h.Id))
                    .ToDictionaryAsync(h => h.Id)
                : new Dictionary<Guid, BeneficiaryPaymentHistory>();

            // ── Step 3: merge in memory ──
            var items = pageRaw.Select(x =>
            {
                historyLookup.TryGetValue(x.CurrentPaymentHistoryId ?? Guid.Empty, out var current);

                return new BeneficiaryListItemDto
                {
                    Id = x.Id,
                    Quarter = x.Quarter,
                    Batch = x.Batch,
                    RefYear = x.RefYear,
                    RefCode = x.RefCode,
                    BatchCode = x.BatchCode,
                    PhoneNumber = x.PhoneNumber,
                    LastName = x.LastName,
                    FirstName = x.FirstName ?? string.Empty,
                    MiddleName = x.MiddleName,
                    Extension = x.Extension,
                    BirthDate = x.BirthDate,
                    Age = ComputeAge(x.BirthDate),
                    MilestoneYear = ComputeMilestoneYear(x.BirthDate),
                    Sex = x.Sex,
                    PsgcCodeRegion = x.Region,
                    PsgcCodeProvince = x.Province,
                    PsgcCodeMunicipality = x.Municipality,
                    PsgcCodeBarangay = x.Barangay,
                    ProvinceName = _psgcNameCache.GetProvinceName(x.Province),
                    MunicipalityName = _psgcNameCache.GetMunicipalityName(x.Municipality),
                    BarangayName = _psgcNameCache.GetBarangayName(x.Barangay),
                    Validator = x.Validator,
                    PayrollQuarter = current?.PayrollQuarter,
                    FiscalYear = current?.FiscalYear,
                    PaymentStatus = current?.PaymentStatus ?? 0,
                    ModeOfPayment = current?.ModeOfPayment ?? 0,
                    PaymentHistoryCount = historyCounts.TryGetValue(x.Id, out var c) ? c : 0,
                    PaymentHistorySummary = summaryByBeneficiary.TryGetValue(x.Id, out var s) ? s : null,
                    PaymentDate = current?.PaymentDate,
                    IsEligible = x.IsEligible,
                    IsCompliant = x.IsCompliant,
                    FindingStatus = x.FindingStatus,
                    CoStatus = x.CoStatus,
                    CoDateEndorsed = x.CoDateEndorsed,
                    CoDateApproved = x.CoDateApproved,
                    HasDocuments = x.HasDocuments,
                    EligibilityRemarksPreview = x.EligibilityRemarksPreview,
                    AssessmentRemarksPreview = x.AssessmentRemarksPreview,
                    FindingRemarksPreview = x.FindingRemarksPreview,
                    RowVersion = x.RowVersion,
                    DateEndorsed = x.DateEndorsed,
                    DateApplied = x.DateApplied,
                    NcscRrn = x.NcscRrn
                };
            }).ToList();

            return new PagedResultDto<BeneficiaryListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PagedResultDto<BeneficiaryInformationDto>> GetPagedAsync(BeneficiaryFilterDto filter)
        {
            var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

            var query = BuildBeneficiaryRawQuery(filter);

            // ✅ Count distinct IDs only
            var totalCount = await query
                .Select(x => x.Id)
                .Distinct()
                .CountAsync();

            // ✅ Dynamic sort
            string sortColumn = filter.SortColumn?.ToLower() ?? "default";
            bool isAscending = filter.SortAscending;

            IQueryable<BeneficiaryRawDto> sorted;

            switch (sortColumn)
            {
                case "birthdate":
                    sorted = isAscending
                        ? query.OrderBy(x => x.BirthDate)
                               .ThenBy(x => x.LastName)
                               .ThenBy(x => x.FirstName)
                        : query.OrderByDescending(x => x.BirthDate)
                               .ThenBy(x => x.LastName)
                               .ThenBy(x => x.FirstName);
                    break;
                case "batchcode":
                    sorted = isAscending
                        ? query.OrderBy(x => x.BatchCode)
                               .ThenBy(x => x.LastName)
                               .ThenBy(x => x.FirstName)
                        : query.OrderByDescending(x => x.BatchCode)
                               .ThenBy(x => x.LastName)
                               .ThenBy(x => x.FirstName);
                    break;
                default:
                    sorted = query.OrderBy(x => x.LastName)
                                  .ThenBy(x => x.FirstName)
                                  .ThenBy(x => x.MiddleName);
                    break;
            }

            // ✅ Use sorted — not query
            var items = await sorted
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var deduped = items
                .DistinctBy(x => x.Id)
                .Select(MapToDto)
                .ToList();

            return new PagedResultDto<BeneficiaryInformationDto>
            {
                Items = deduped,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
        private IQueryable<BeneficiaryQueryModel> BuildBeneficiaryFilteredQuery(BeneficiaryFilterDto filter)
        {
            var query =
                from b in _context.BeneficiaryInformations

                join region in _context.Regions
                    on b.Region equals region.PsgcCodeRegion into regionJoin
                from region in regionJoin.DefaultIfEmpty()

                join province in _context.Provinces
                    on b.Province equals province.PsgcCodeProvince into provinceJoin
                from province in provinceJoin.DefaultIfEmpty()

                join municipality in _context.Municipalities
                    on b.Municipality equals municipality.PsgcCodeMunicipality into municipalityJoin
                from municipality in municipalityJoin.DefaultIfEmpty()

                join barangay in _context.Barangays
                    on b.Barangay equals barangay.PsgcCodeBarangay into barangayJoin
                from barangay in barangayJoin.DefaultIfEmpty()

                join finding in _context.BeneficiaryFindings
                    on b.Id equals finding.BeneficiaryInformationId into findingJoin
                from finding in findingJoin.DefaultIfEmpty()

                where !b.IsDeleted
                select new BeneficiaryQueryModel
                {
                    Beneficiary = b,
                    Region = region != null ? region.Name : null,
                    Province = province != null ? province.Name : null,
                    Municipality = municipality != null ? municipality.Name : null,
                    Barangay = barangay != null ? barangay.Name : null,
                    FindingStatus = finding != null ? finding.FindingStatus : (int?)null,
                    FindingRemarks = finding != null ? finding.FindingRemarks : null,
                };
            // ── General Search ────────────────────────────────────────────────────────
            // Scans all relevant columns with OR logic.
            // Example: "USA" matches citizenship, remarks, assessment remarks, validator, etc.
            // Example: "JUAN" matches first name, last name, full name combo
            // Example: "12345" matches NCSC RRN, OSCA ID, phone number
            if (!string.IsNullOrWhiteSpace(filter.GeneralSearch))
            {
                var term = filter.GeneralSearch.Trim().ToLower();

                // ── Pre-resolve mapped integer values from the search term ────────────
                // So "male" matches Sex == 1, "paid" matches PaymentStatus == 2, etc.
                int? sexMatch = term switch
                {
                    "male" => 1,
                    "female" => 2,
                    _ => null
                };

                int? paymentMatch = term switch
                {
                    "paid" => 2,
                    "unpaid" => 1,
                    "pending" => 3,
                    "n/a" => 0,
                    _ => null
                };

                int? citizenshipMatch = term switch
                {
                    "filipino" => 1,
                    "dual citizenship" => 2,
                    "dual" => 2,
                    _ => null
                };
                // ✅ CO Status — "not set" needs special null/0 handling below
                int? coStatusMatch = term switch
                {
                    "endorsed" => 1,
                    "approved" => 2,
                    _ => null
                };
                int? modeOfPaymentMatch = term switch
                {
                    "cash advance" => 1,
                    "cash advance by sdo" => 1,
                    "bank transfer" => 2,
                    _ => null
                };
                // ✅ Flag for "not set" search — matches null or 0 CoStatus
                bool searchCoNotSet = term == "not set";

                // ── Parse as year for milestone matching ──────────────────────────────
                int.TryParse(term, out var yearTerm);

                query = query.Where(x =>
                    // ── Name fields ───────────────────────────────────────────────────
                    (x.Beneficiary.LastName != null && x.Beneficiary.LastName.ToLower().Contains(term)) ||
                    (x.Beneficiary.FirstName.ToLower().Contains(term)) ||
                    (x.Beneficiary.MiddleName != null && x.Beneficiary.MiddleName.ToLower().Contains(term)) ||
                    (x.Beneficiary.Extension != null && x.Beneficiary.Extension.ToLower().Contains(term)) ||

                    // ── ID / code fields ──────────────────────────────────────────────
                    (x.Beneficiary.OscaIdNumber != null && x.Beneficiary.OscaIdNumber.ToLower().Contains(term)) ||
                    (x.Beneficiary.BatchCode != null && x.Beneficiary.BatchCode.ToLower().Contains(term)) ||
                    (x.Beneficiary.PhoneNumber != null && x.Beneficiary.PhoneNumber.ToLower().Contains(term)) ||
                    (x.Beneficiary.NcscRrn != null && x.Beneficiary.NcscRrn.ToString()!.Contains(term)) ||

                    // ── Location name fields (joined) ─────────────────────────────────
                    (x.Province != null && x.Province.ToLower().Contains(term)) ||
                    (x.Municipality != null && x.Municipality.ToLower().Contains(term)) ||
                    (x.Barangay != null && x.Barangay.ToLower().Contains(term)) ||
                    (x.Region != null && x.Region.ToLower().Contains(term)) ||

                    // ── Validation fields ─────────────────────────────────────────────
                    (x.Beneficiary.Validator != null && x.Beneficiary.Validator.ToLower().Contains(term)) ||

                    // ── Remarks fields ────────────────────────────────────────────────
                    (x.Beneficiary.Remarks != null && x.Beneficiary.Remarks.ToLower().Contains(term)) ||
                    (x.Beneficiary.AssessmentRemarks != null && x.Beneficiary.AssessmentRemarks.ToLower().Contains(term)) ||
                    (x.Beneficiary.EligibilityRemarks != null && x.Beneficiary.EligibilityRemarks.ToLower().Contains(term)) ||

                    // ── Mapped integer fields ─────────────────────────────────────────
                    (sexMatch.HasValue && x.Beneficiary.Sex == sexMatch.Value) ||
                    (paymentMatch.HasValue && x.Beneficiary.PaymentStatus == paymentMatch.Value) ||
                    (citizenshipMatch.HasValue && x.Beneficiary.Citizenship == citizenshipMatch.Value) ||
                    (modeOfPaymentMatch.HasValue && x.Beneficiary.ModeOfPayment == modeOfPaymentMatch.Value) || // ✅ new

                    // ── CO Status — named values ──────────────────────────────────────
                    // "endorsed" → CoStatus == 1
                    // "approved" → CoStatus == 2
                    (coStatusMatch.HasValue && x.Beneficiary.CoStatus == coStatusMatch.Value) ||
                    // ── CO Status — "not set" → CoStatus is null or 0 ────────────────
                    (searchCoNotSet && (x.Beneficiary.CoStatus == null || x.Beneficiary.CoStatus == 0)) ||

                      // ── CO date fields — match year or full date string ───────────────
                      (yearTerm > 0 && x.Beneficiary.CoDateEndorsed.HasValue &&
                      x.Beneficiary.CoDateEndorsed.Value.Year == yearTerm) ||
                     (yearTerm > 0 && x.Beneficiary.CoDateApproved.HasValue &&
                     x.Beneficiary.CoDateApproved.Value.Year == yearTerm) ||


                    // ── Birth year ────────────────────────────────────────────────────
                    (yearTerm > 0 && x.Beneficiary.BirthDate.Year == yearTerm) ||

                    // ── Reference number components ───────────────────────────────────────────
                    (x.Beneficiary.Batch != null && x.Beneficiary.Batch.ToLower().Contains(term)) ||
                    (yearTerm > 0 && x.Beneficiary.RefYear == yearTerm) ||
                    // ── Ref Code ──────────────────────────────────────────────────────────────
                    (x.Beneficiary.RefCode != null && x.Beneficiary.RefCode.ToLower().Contains(term)) ||

                    // ── Finding remarks ───────────────────────────────────────────────
                    (x.FindingRemarks != null && x.FindingRemarks.ToLower().Contains(term))
                );
            }
            // ── Location ─────────────────────────────────────────────────────────
            if (filter.PsgcCodeRegion.HasValue && filter.PsgcCodeRegion.Value > 0)
            {
                var allRegionCodesForName = _context.Regions
                    .Where(r => _context.Regions
                        .Where(r2 => r2.PsgcCodeRegion == filter.PsgcCodeRegion.Value)
                        .Select(r2 => r2.Name)
                        .Contains(r.Name))
                    .Select(r => r.PsgcCodeRegion);

                query = query.Where(x => allRegionCodesForName.Contains(x.Beneficiary.Region));
            }

            if (filter.PsgcCodeProvince != null)
            {
                var allProvinceCodesForName = _context.Provinces
                    .Where(p => _context.Provinces
                        .Where(p2 => p2.PsgcCodeProvince == filter.PsgcCodeProvince.Value)
                        .Select(p2 => p2.Name)
                        .Contains(p.Name))
                    .Select(p => p.PsgcCodeProvince);

                query = query.Where(x => allProvinceCodesForName.Contains(x.Beneficiary.Province));
            }

            if (filter.PsgcCodeMunicipality != null)
                query = query.Where(x => x.Beneficiary.Municipality == filter.PsgcCodeMunicipality);

            if (filter.PsgcCodeBarangay != null)
                query = query.Where(x => x.Beneficiary.Barangay == filter.PsgcCodeBarangay);

            // ── Finding Status ────────────────────────────────────────────────────
            if (filter.FindingStatus.HasValue && filter.FindingStatus.Value != 3)
            {
                var status = filter.FindingStatus.Value;

                if (status == 0)
                {
                    query = query.Where(x =>
                        x.FindingStatus == null ||
                        x.FindingStatus == 0);
                }
                else
                {
                    query = query.Where(x => x.FindingStatus == status);
                }
            }
            // ── Compliance Filter ─────────────────────────────────────────────────────
            // ComplianceMode replaces IsCompliant for richer filtering
            // Keep IsCompliant as fallback for backward compatibility
            if (!string.IsNullOrWhiteSpace(filter.ComplianceMode))
            {
                switch (filter.ComplianceMode)
                {
                    case "compliant":
                        query = query.Where(x => x.Beneficiary.IsCompliant == true);
                        break;
                    case "noncompliant":
                        query = query.Where(x => x.Beneficiary.IsCompliant == false);
                        break;
                    case "withfindings":
                        // ✅ Compliant but has assessment remarks
                        query = query.Where(x =>
                            x.Beneficiary.IsCompliant == true &&
                            x.Beneficiary.AssessmentRemarks != null &&
                            x.Beneficiary.AssessmentRemarks != string.Empty);
                        break;
                }
            }
            else if (filter.IsCompliant.HasValue)
            {
                // ✅ Fallback for old callers that still use bool?
                query = query.Where(x => x.Beneficiary.IsCompliant == filter.IsCompliant.Value);
            }

            // ── Eligibility Filter ────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.EligibilityMode))
            {
                switch (filter.EligibilityMode)
                {
                    case "eligible":
                        query = query.Where(x => x.Beneficiary.IsEligible == true);
                        break;
                    case "ineligible":
                        query = query.Where(x => x.Beneficiary.IsEligible == false);
                        break;
                    case "withfindings":
                        query = query.Where(x =>
                            x.Beneficiary.IsEligible == true &&
                            x.Beneficiary.EligibilityRemarks != null &&
                            x.Beneficiary.EligibilityRemarks != string.Empty);
                        break;
                }
            }
            else if (filter.IsEligible.HasValue)
            {
                query = query.Where(x => x.Beneficiary.IsEligible == filter.IsEligible.Value);
            }

            // ✅ NEW — IsEligible Filter
            if (filter.IsEligible.HasValue)
            {
                query = query.Where(x => x.Beneficiary.IsEligible == filter.IsEligible.Value);
            }

            // ── Name ──────────────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.LastName))
                query = query.Where(x =>
                    x.Beneficiary.LastName != null &&
                    x.Beneficiary.LastName.Contains(filter.LastName));

            if (!string.IsNullOrWhiteSpace(filter.FirstName))
                query = query.Where(x =>
                    x.Beneficiary.FirstName.Contains(filter.FirstName));

            //Full Name
            if (!string.IsNullOrWhiteSpace(filter.FullName))
            {
                var name = NormalizeSearchTerm(filter.FullName);
                query = query.Where(b =>
                    (b.Beneficiary.LastName + " " + b.Beneficiary.FirstName + " " + b.Beneficiary.MiddleName)
                    .ToLower().Contains(name) ||
                    (b.Beneficiary.FirstName + " " + b.Beneficiary.MiddleName + " " + b.Beneficiary.LastName)
                    .ToLower().Contains(name));
            }

            // ── Age ───────────────────────────────────────────────────────────────
            if (filter.SpecificAge.HasValue)
            {
                var cutoffEnd = DateTime.Today.AddYears(-filter.SpecificAge.Value).Date;
                var cutoffStart = DateTime.Today.AddYears(-filter.SpecificAge.Value - 1).Date;
                query = query.Where(x =>
                    x.Beneficiary.BirthDate > cutoffStart &&
                    x.Beneficiary.BirthDate <= cutoffEnd);
            }

            // ── Birthday (month + day only, year ignored) ─────────────────────────
            if (filter.SpecificBirthday.HasValue)
            {
                var month = filter.SpecificBirthday.Value.Month;
                var day = filter.SpecificBirthday.Value.Day;
                query = query.Where(x =>
                    x.Beneficiary.BirthDate.Month == month &&
                    x.Beneficiary.BirthDate.Day == day);
            }
            else
            {
                bool hasFrom = filter.BirthdayFrom.HasValue;
                bool hasTo = filter.BirthdayTo.HasValue;

                if (hasFrom && hasTo)
                {
                    var fromMonth = filter.BirthdayFrom!.Value.Month;
                    var fromDay = filter.BirthdayFrom!.Value.Day;
                    var toMonth = filter.BirthdayTo!.Value.Month;
                    var toDay = filter.BirthdayTo!.Value.Day;

                    // MMDD integer for easy comparison e.g. March 5 = 305
                    int fromMD = fromMonth * 100 + fromDay;
                    int toMD = toMonth * 100 + toDay;

                    bool isWrap = fromMD > toMD; // e.g. Nov(1101) → Feb(228)

                    if (!isWrap)
                    {
                        // Normal range e.g. March 1 → August 31
                        query = query.Where(x =>
                            (x.Beneficiary.BirthDate.Month * 100 + x.Beneficiary.BirthDate.Day) >= fromMD &&
                            (x.Beneficiary.BirthDate.Month * 100 + x.Beneficiary.BirthDate.Day) <= toMD);
                    }
                    else
                    {
                        // Wrap range e.g. Nov 1 → Feb 28
                        query = query.Where(x =>
                            (x.Beneficiary.BirthDate.Month * 100 + x.Beneficiary.BirthDate.Day) >= fromMD ||
                            (x.Beneficiary.BirthDate.Month * 100 + x.Beneficiary.BirthDate.Day) <= toMD);
                    }
                }
                else if (hasFrom)
                {
                    var fromMonth = filter.BirthdayFrom!.Value.Month;
                    var fromDay = filter.BirthdayFrom!.Value.Day;
                    int fromMD = fromMonth * 100 + fromDay;

                    query = query.Where(x =>
                        (x.Beneficiary.BirthDate.Month * 100 + x.Beneficiary.BirthDate.Day) >= fromMD);
                }
                else if (hasTo)
                {
                    var toMonth = filter.BirthdayTo!.Value.Month;
                    var toDay = filter.BirthdayTo!.Value.Day;
                    int toMD = toMonth * 100 + toDay;

                    query = query.Where(x =>
                        (x.Beneficiary.BirthDate.Month * 100 + x.Beneficiary.BirthDate.Day) <= toMD);
                }
            }

            // ── Milestone Year ────────────────────────────────────────────────────
            if (filter.MilestoneYear.HasValue)
            {
                var milestoneYear = filter.MilestoneYear.Value;
                var milestones = new[] { 80, 85, 90, 95, 100 };
                var today = DateTime.Today;

                if (milestoneYear == 0)
                {
                    // ✅ Mirror exact same logic as ComputeMilestoneYear returning 0:
                    // No milestone satisfies: >= 2024 AND
                    // (year < today.Year OR (year == today.Year AND dayOfYear <= today.DayOfYear))
                    query = query.Where(x =>
                        !milestones.Any(m =>
                            (x.Beneficiary.BirthDate.Year + m) >= 2024 &&
                            (
                                (x.Beneficiary.BirthDate.Year + m) < today.Year ||
                                (
                                    (x.Beneficiary.BirthDate.Year + m) == today.Year &&
                                    x.Beneficiary.BirthDate.DayOfYear <= today.DayOfYear
                                )
                            )
                        )
                    );
                }
                else
                {
                    // ✅ Specific milestone year — must match AND be >= 2024
                    query = query.Where(x =>
                        milestones.Any(m =>
                            x.Beneficiary.BirthDate.Year + m == milestoneYear &&
                            x.Beneficiary.BirthDate.Year + m >= 2024));
                }
            }

            // ── Sex ───────────────────────────────────────────────────────────────
            if (filter.Sex.HasValue && filter.Sex.Value > 0)
                query = query.Where(x => x.Beneficiary.Sex == filter.Sex.Value);

            // ── Payment Status ────────────────────────────────────────────────────
            if (filter.PaymentStatus.HasValue && filter.PaymentStatus.Value >= 0)
                query = query.Where(x => x.Beneficiary.PaymentStatus == filter.PaymentStatus.Value);

            // ── Mode of Payment Filter ────────────────────────────────────────────────
            if (filter.FilterModeOfPayment.HasValue && filter.FilterModeOfPayment.Value > 0)
                query = query.Where(x =>
                    x.Beneficiary.ModeOfPayment == filter.FilterModeOfPayment.Value);

            // ── Payment Date (exact) ──────────────────────────────────────────────
            if (filter.PaymentDate.HasValue)
            {
                var paymentStart = filter.PaymentDate.Value.Date;
                var paymentEnd = paymentStart.AddDays(1);
                query = query.Where(x =>
                    x.Beneficiary.PaymentDate >= paymentStart &&
                    x.Beneficiary.PaymentDate < paymentEnd);
            }

            // ── Payment Date Range ────────────────────────────────────────────────
            if (filter.PaymentDateFrom.HasValue)
            {
                var from = filter.PaymentDateFrom.Value.Date;
                query = query.Where(x => x.Beneficiary.PaymentDate >= from);
            }

            if (filter.PaymentDateTo.HasValue)
            {
                var to = filter.PaymentDateTo.Value.Date.AddDays(1);
                query = query.Where(x => x.Beneficiary.PaymentDate < to);
            }
            // ── Date Added Range ──────────────────────────────────────────────────────
            if (filter.DateAddedFrom.HasValue)
            {
                var from = filter.DateAddedFrom.Value.Date;
                query = query.Where(x => x.Beneficiary.DateAdded >= from);
            }

            if (filter.DateAddedTo.HasValue)
            {
                // ✅ Add one day so "To = June 5" includes all records added on June 5
                var to = filter.DateAddedTo.Value.Date.AddDays(1);
                query = query.Where(x => x.Beneficiary.DateAdded < to);
            }

            // ── Other ─────────────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.Validator))
                query = query.Where(x =>
                    x.Beneficiary.Validator != null &&
                    x.Beneficiary.Validator.Contains(filter.Validator));

            // ── New ──
            if (!string.IsNullOrWhiteSpace(filter.BatchCode))
            {
                // Split by comma, trim each entry, remove empties
                var batchCodes = filter.BatchCode
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(b => b.Trim())
                    .Where(b => !string.IsNullOrWhiteSpace(b))
                    .ToList();

                if (batchCodes.Count == 1)
                {
                    // Single entry — use Contains for partial match
                    // e.g. "123" matches "123-45" and "123-99"
                    var single = batchCodes[0];
                    query = query.Where(x =>
                        x.Beneficiary.BatchCode != null &&
                        x.Beneficiary.BatchCode.Contains(single));
                }
                else
                {
                    // Multiple entries — exact match against the list
                    // e.g. "123-45, 678-90" matches only those exact codes
                    query = query.Where(x =>
                        x.Beneficiary.BatchCode != null &&
                        batchCodes.Contains(x.Beneficiary.BatchCode));
                }
            }
            // ── CO Status Filter ──────────────────────────────────────────────────────
            // -1  = no filter (user hasn't selected anything)
            //  0  = filter for Not Set (null or 0 in DB)
            //  1  = Endorsed
            //  2  = Approved
            if (filter.CoStatus.HasValue && filter.CoStatus.Value >= 0)
            {
                if (filter.CoStatus.Value == 0)
                {
                    // ✅ "Not Set" = CoStatus is null OR CoStatus is 0
                    query = query.Where(x =>
                        x.Beneficiary.CoStatus == null ||
                        x.Beneficiary.CoStatus == 0);
                }
                else
                {
                    query = query.Where(x => x.Beneficiary.CoStatus == filter.CoStatus.Value);
                }
            }
            // ── Quarter Filter ────────────────────────────────────────────────────────
            if (filter.FilterQuarter.HasValue)
                query = query.Where(x => x.Beneficiary.Quarter == filter.FilterQuarter.Value);

            if (filter.FilterFiscalYear.HasValue)
                query = query.Where(b => b.Beneficiary.FiscalYear == filter.FilterFiscalYear);

            // ── Payroll Quarter Filter ────────────────────────────────────────────────
            if (filter.FilterPayrollQuarter.HasValue)
                query = query.Where(x => x.Beneficiary.PayrollQuarter == filter.FilterPayrollQuarter.Value);

            if (filter.FilterPayrollQuarters != null && filter.FilterPayrollQuarters.Any())
                query = query.Where(x => filter.FilterPayrollQuarters.Contains(x.Beneficiary.PayrollQuarter ?? 0));

            // ── Batch Filter ──────────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.FilterBatch))
                query = query.Where(x =>
                    x.Beneficiary.Batch != null &&
                    x.Beneficiary.Batch.Contains(filter.FilterBatch.Trim()));

            // ── RefYear Filter ────────────────────────────────────────────────────────
            if (filter.FilterRefYear.HasValue)
                query = query.Where(x => x.Beneficiary.RefYear == filter.FilterRefYear.Value);

            // ── Region Roman Filter ───────────────────────────────────────────────────
            // Translate roman back to region codes and filter
            if (!string.IsNullOrWhiteSpace(filter.FilterRegionRoman))
            {
                var regionCodes = RegionRomanNumeralHelper.GetRegionCodesForRoman(filter.FilterRegionRoman);
                if (regionCodes.Any())
                    query = query.Where(x => regionCodes.Contains(x.Beneficiary.Region));
            }

            Console.WriteLine(query);
            return query.AsNoTracking();
        }


        // ── Keep this method returning the raw anonymous IQueryable ──────────────────
        private IQueryable<BeneficiaryRawDto> BuildBeneficiaryRawQuery(BeneficiaryFilterDto filter)
        {
            return BuildBeneficiaryFilteredQuery(filter)
                .Select(x => new BeneficiaryRawDto
                {
                    Id = x.Beneficiary.Id,
                    Quarter = x.Beneficiary.Quarter,
                    Batch = x.Beneficiary.Batch,
                    RefYear = x.Beneficiary.RefYear,
                    RefCode = x.Beneficiary.RefCode,
                    DateApplied = x.Beneficiary.DateApplied,
                    DateEndorsed = x.Beneficiary.DateEndorsed,
                    BatchCode = x.Beneficiary.BatchCode,
                    OscaIdNumber = x.Beneficiary.OscaIdNumber,
                    OscaIdDateIssued = x.Beneficiary.OscaIdDateIssued,
                    NcscRrn = x.Beneficiary.NcscRrn,
                    LastName = x.Beneficiary.LastName,
                    FirstName = x.Beneficiary.FirstName,
                    MiddleName = x.Beneficiary.MiddleName,
                    Extension = x.Beneficiary.Extension,
                    BirthDate = x.Beneficiary.BirthDate,
                    PhoneNumber = x.Beneficiary.PhoneNumber,
                    IsIndigenousPeople = x.Beneficiary.IsIndigenousPeople,
                    IsPersonWithDisability = x.Beneficiary.IsPersonWithDisability,
                    CivilStatus = x.Beneficiary.CivilStatus,
                    Citizenship = x.Beneficiary.Citizenship,
                    Sex = x.Beneficiary.Sex,
                    IsCompliant = x.Beneficiary.IsCompliant,
                    Validator = x.Beneficiary.Validator,
                    ValidationDate = x.Beneficiary.ValidationDate,
                    PayrollQuarter = x.Beneficiary.PayrollQuarter,
                    FiscalYear = x.Beneficiary.FiscalYear,
                    PaymentStatus = x.Beneficiary.PaymentStatus,
                    ModeOfPayment = x.Beneficiary.ModeOfPayment,
                    PaymentDate = x.Beneficiary.PaymentDate,
                    IsDeceased = x.Beneficiary.IsDeceased,
                    DateOfDeath = x.Beneficiary.DateOfDeath,
                    IsEligible = x.Beneficiary.IsEligible,
                    AssessmentRemarks = x.Beneficiary.AssessmentRemarks,
                    EligibilityRemarks = x.Beneficiary.EligibilityRemarks,
                    RemarkCategory = x.Beneficiary.RemarkCategory,
                    DateAdded = x.Beneficiary.DateAdded,
                    Remarks = x.Beneficiary.Remarks,
                    IsDeleted = x.Beneficiary.IsDeleted,
                    PsgcCodeRegion = x.Beneficiary.Region,
                    PsgcCodeProvince = x.Beneficiary.Province,
                    PsgcCodeMunicipality = x.Beneficiary.Municipality,
                    PsgcCodeBarangay = x.Beneficiary.Barangay,
                    RegionName = x.Region,
                    ProvinceName = x.Province,
                    MunicipalityName = x.Municipality,
                    BarangayName = x.Barangay,
                    FindingStatus = x.FindingStatus,
                    FindingRemarks = x.FindingRemarks,
                    CoStatus = x.Beneficiary.CoStatus,
                    CoDateEndorsed = x.Beneficiary.CoDateEndorsed,
                    CoDateApproved = x.Beneficiary.CoDateApproved,
                    RowVersion = x.Beneficiary.RowVersion,
                    HasDocuments = _context.BeneficiaryDocuments
                    .Any(d => d.BeneficiaryInformationId == x.Beneficiary.Id && !d.IsDeleted),
                    CgpPageNumber = x.Beneficiary.CgpPageNumber,
                    CgpGenerationId = x.Beneficiary.CgpGenerationId,
                    CgpPrefix = x.Beneficiary.CgpPrefix
                });
        }

        // ── Mapper: call this AFTER .ToListAsync() ───────────────────────────────────
        private static BeneficiaryInformationDto MapToDto(BeneficiaryRawDto x) => new()
        {
            Id = x.Id,
            Quarter = x.Quarter,
            Batch = x.Batch,
            RefYear = x.RefYear,
            RefCode = x.RefCode,
            DateApplied = x.DateApplied,
            DateEndorsed = x.DateEndorsed,
            BatchCode = x.BatchCode,
            OscaIdNumber = x.OscaIdNumber,
            OscaIdDateIssued = x.OscaIdDateIssued,
            NcscRrn = x.NcscRrn,
            LastName = x.LastName,
            FirstName = x.FirstName ?? string.Empty,
            MiddleName = x.MiddleName,
            Extension = x.Extension,
            BirthDate = x.BirthDate,
            PhoneNumber = x.PhoneNumber,
            Age = DateTime.Today.Year - x.BirthDate.Year -
                                     (x.BirthDate.Date > DateTime.Today.AddYears(
                                         -(DateTime.Today.Year - x.BirthDate.Year)) ? 1 : 0),
            MilestoneYear = ComputeMilestoneYear(x.BirthDate),
            IsIndigenousPeople = x.IsIndigenousPeople,
            IsPersonWithDisability = x.IsPersonWithDisability,
            CivilStatus = x.CivilStatus,
            Citizenship = x.Citizenship,
            Sex = x.Sex,
            PsgcCodeRegion = x.PsgcCodeRegion,
            PsgcCodeProvince = x.PsgcCodeProvince,
            PsgcCodeMunicipality = x.PsgcCodeMunicipality,
            PsgcCodeBarangay = x.PsgcCodeBarangay,

            // ✅ Safe: JsonSerializer runs in-memory, not in SQL
            Region = x.RegionName != null ? JsonSerializer.SerializeToElement(x.RegionName) : null,
            Province = x.ProvinceName != null ? JsonSerializer.SerializeToElement(x.ProvinceName) : null,
            Municipality = x.MunicipalityName != null ? JsonSerializer.SerializeToElement(x.MunicipalityName) : null,
            Barangay = x.BarangayName != null ? JsonSerializer.SerializeToElement(x.BarangayName) : null,

            IsCompliant = x.IsCompliant,
            Validator = x.Validator ?? string.Empty,
            ValidationDate = x.ValidationDate,
            PayrollQuarter = x.PayrollQuarter,
            FiscalYear = x.FiscalYear,
            PaymentStatus = x.PaymentStatus,
            ModeOfPayment = x.ModeOfPayment,
            PaymentDate = x.PaymentDate,
            IsDeceased = x.IsDeceased,
            DateOfDeath = x.DateOfDeath,
            IsEligible = x.IsEligible,
            AssessmentRemarks = x.AssessmentRemarks,
            EligibilityRemarks = x.EligibilityRemarks,
            RemarkCategory = x.RemarkCategory,
            DateAdded = x.DateAdded,
            Remarks = x.Remarks,
            IsDeleted = x.IsDeleted,
            FindingStatus = x.FindingStatus,
            FindingRemarks = x.FindingRemarks,
            CoStatus = x.CoStatus,
            CoDateEndorsed = x.CoDateEndorsed,
            CoDateApproved = x.CoDateApproved,
            RowVersion = x.RowVersion,
            HasDocuments = x.HasDocuments,
            CgpPageNumber = x.CgpPageNumber,
            CgpGenerationId = x.CgpGenerationId,
            CgpPrefix = x.CgpPrefix
        };

        private static int ComputeMilestoneYear(DateTime birthDate)
        {
            var today = DateTime.Today;
            foreach (var m in new[] { 100, 95, 90, 85, 80 })
            {
                int y = birthDate.Year + m;
                if (y >= 2024 &&
                    (y < today.Year ||
                    (y == today.Year && birthDate.DayOfYear <= today.DayOfYear)))
                    return y;
            }
            // ✅ Returns 0 when:
            // - No milestone year >= 2024 has been reached yet (birthday hasn't come)
            // - e.g. age 84 born May 1941 → 85th milestone is 2026, birthday not yet passed → 0
            // - e.g. age 104 born 1922 → 100th was 2022, before 2024 program window → 0
            // - e.g. age 81 born 1944 → 85th is 2029, not reached → 0
            return 0;
        }
        private static int ComputeAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
        }

        public async Task<BeneficiaryInformation?> FindExistingAsync(string? lastName, string? firstName, string? middleName, DateTime birthDate)
        {
            var normalizedLastName = (lastName ?? string.Empty).Trim().ToLower();
            var normalizedFirstName = (firstName ?? string.Empty).Trim().ToLower();
            var normalizedMiddleName = (middleName ?? string.Empty).Trim().ToLower();
            var normalizedBirthDate = birthDate.Date;

            return await _context.BeneficiaryInformations
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    (x.LastName ?? string.Empty).Trim().ToLower() == normalizedLastName &&
                    (x.FirstName ?? string.Empty).Trim().ToLower() == normalizedFirstName &&
                    (x.MiddleName ?? string.Empty).Trim().ToLower() == normalizedMiddleName &&
                    x.BirthDate.Date == normalizedBirthDate);
        }
        // Infrastructure/Repositories/BeneficiaryInformationRepository.cs
        public void SetOriginalRowVersion(BeneficiaryInformation entity, byte[] rowVersion)
        {
            // ✅ EF Core only used here in Infrastructure — not in Application
            _context.Entry(entity)
                    .Property(x => x.RowVersion)
                    .OriginalValue = rowVersion;
        }
        public async Task<List<BeneficiaryInformationDto>> GetByIdsAsync(List<Guid> ids)
        {
            if (ids == null || !ids.Any())
                return new List<BeneficiaryInformationDto>();

            // ✅ Batch the ID list. A WHERE IN clause with thousands of GUID
            // parameters (one per ID) blows past SQL Server's parameter ceiling
            // and causes severe query-plan/timeout degradation — this is what
            // was producing the 503 at 2000-3000 records while 1000 worked fine.
            // 500 per batch keeps each query's parameter count comfortably low
            // regardless of how large the overall selection gets.
            const int batchSize = 500;
            var distinctIds = ids.Distinct().ToList();
            var allRaw = new List<GetByIdsRawDto>();

            for (int i = 0; i < distinctIds.Count; i += batchSize)
            {
                var batch = distinctIds.Skip(i).Take(batchSize).ToList();

                var raw = await (
                    from b in _context.BeneficiaryInformations

                    join region in _context.Regions
                        on b.Region equals region.PsgcCodeRegion into regionJoin
                    from region in regionJoin.DefaultIfEmpty()

                    join province in _context.Provinces
                        on b.Province equals province.PsgcCodeProvince into provinceJoin
                    from province in provinceJoin.DefaultIfEmpty()

                    join municipality in _context.Municipalities
                        on b.Municipality equals municipality.PsgcCodeMunicipality into municipalityJoin
                    from municipality in municipalityJoin.DefaultIfEmpty()

                    join barangay in _context.Barangays
                        on b.Barangay equals barangay.PsgcCodeBarangay into barangayJoin
                    from barangay in barangayJoin.DefaultIfEmpty()

                    where batch.Contains(b.Id) && !b.IsDeleted

                    select new GetByIdsRawDto
                    {
                        Id = b.Id,
                        Quarter = b.Quarter,
                        Batch = b.Batch,
                        RefYear = b.RefYear,
                        RefCode = b.RefCode,
                        BatchCode = b.BatchCode,
                        OscaIdNumber = b.OscaIdNumber,
                        OscaIdDateIssued = b.OscaIdDateIssued,
                        NcscRrn = b.NcscRrn,
                        LastName = b.LastName,
                        FirstName = b.FirstName,
                        MiddleName = b.MiddleName,
                        Extension = b.Extension,
                        BirthDate = b.BirthDate,
                        Sex = b.Sex,
                        IsIndigenousPeople = b.IsIndigenousPeople,
                        IsPersonWithDisability = b.IsPersonWithDisability,
                        CivilStatus = b.CivilStatus,
                        Citizenship = b.Citizenship,
                        IsCompliant = b.IsCompliant,
                        Validator = b.Validator,
                        ValidationDate = b.ValidationDate,
                        PayrollQuarter = b.PayrollQuarter,
                        FiscalYear = b.FiscalYear,
                        PaymentStatus = b.PaymentStatus,
                        ModeOfPayment = b.ModeOfPayment,
                        PaymentDate = b.PaymentDate,
                        IsDeceased = b.IsDeceased,
                        DateOfDeath = b.DateOfDeath,
                        IsEligible = b.IsEligible,
                        AssessmentRemarks = b.AssessmentRemarks,
                        EligibilityRemarks = b.EligibilityRemarks,
                        RemarkCategory = b.RemarkCategory,
                        Remarks = b.Remarks,
                        DateAdded = b.DateAdded,
                        CoStatus = b.CoStatus,
                        CoDateEndorsed = b.CoDateEndorsed,
                        CoDateApproved = b.CoDateApproved,
                        IsDeleted = b.IsDeleted,
                        DateApplied = b.DateApplied,
                        DateEndorsed = b.DateEndorsed,
                        PhoneNumber = b.PhoneNumber,
                        PsgcCodeRegion = b.Region,
                        PsgcCodeProvince = b.Province,
                        PsgcCodeMunicipality = b.Municipality,
                        PsgcCodeBarangay = b.Barangay,
                        RegionName = region != null ? region.Name : null,
                        ProvinceName = province != null ? province.Name : null,
                        MunicipalityName = municipality != null ? municipality.Name : null,
                        BarangayName = barangay != null ? barangay.Name : null,
                        RowVersion = b.RowVersion
                    }
                ).AsNoTracking().ToListAsync();

                allRaw.AddRange(raw);
            }

            // Deduplicate across all batches by beneficiary Id — same reasoning as
            // before: LEFT JOINs can produce duplicate rows per beneficiary when
            // lookup tables contain duplicate codes for the same name.
            var deduped = allRaw
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();

            // Map to DTO in-memory — unchanged logic from your existing version
            var result = deduped.Select(x => new BeneficiaryInformationDto
            {
                Id = x.Id,
                Quarter = x.Quarter,
                Batch = x.Batch,
                RefYear = x.RefYear,
                RefCode = x.RefCode,
                DateApplied = x.DateApplied,
                DateEndorsed = x.DateEndorsed,
                BatchCode = x.BatchCode,
                OscaIdNumber = x.OscaIdNumber,
                OscaIdDateIssued = x.OscaIdDateIssued,
                NcscRrn = x.NcscRrn,
                LastName = x.LastName,
                FirstName = x.FirstName ?? string.Empty,
                MiddleName = x.MiddleName,
                Extension = x.Extension,
                BirthDate = x.BirthDate,
                PhoneNumber = x.PhoneNumber,
                Age = DateTime.Today.Year - x.BirthDate.Year -
                                       (x.BirthDate.Date > DateTime.Today.AddYears(
                                           -(DateTime.Today.Year - x.BirthDate.Year)) ? 1 : 0),
                Sex = x.Sex,
                IsIndigenousPeople = x.IsIndigenousPeople,
                IsPersonWithDisability = x.IsPersonWithDisability,
                CivilStatus = x.CivilStatus,
                Citizenship = x.Citizenship,
                PsgcCodeRegion = x.PsgcCodeRegion,
                PsgcCodeProvince = x.PsgcCodeProvince,
                PsgcCodeMunicipality = x.PsgcCodeMunicipality,
                PsgcCodeBarangay = x.PsgcCodeBarangay,
                Region = x.RegionName != null ? JsonSerializer.SerializeToElement(x.RegionName) : null,
                Province = x.ProvinceName != null ? JsonSerializer.SerializeToElement(x.ProvinceName) : null,
                Municipality = x.MunicipalityName != null ? JsonSerializer.SerializeToElement(x.MunicipalityName) : null,
                Barangay = x.BarangayName != null ? JsonSerializer.SerializeToElement(x.BarangayName) : null,
                IsCompliant = x.IsCompliant,
                Validator = x.Validator ?? string.Empty,
                ValidationDate = x.ValidationDate,
                PayrollQuarter = x.PayrollQuarter,
                FiscalYear = x.FiscalYear,
                PaymentStatus = x.PaymentStatus,
                ModeOfPayment = x.ModeOfPayment,
                PaymentDate = x.PaymentDate,
                IsDeceased = x.IsDeceased,
                DateOfDeath = x.DateOfDeath,
                IsEligible = x.IsEligible,
                AssessmentRemarks = x.AssessmentRemarks,
                EligibilityRemarks = x.EligibilityRemarks,
                RemarkCategory = x.RemarkCategory,
                Remarks = x.Remarks,
                DateAdded = x.DateAdded,
                CoStatus = x.CoStatus,
                CoDateEndorsed = x.CoDateEndorsed,
                CoDateApproved = x.CoDateApproved,
                IsDeleted = x.IsDeleted,
                RowVersion = x.RowVersion,
                MilestoneYear =
                    (x.BirthDate.Year + 100) <= DateTime.Today.Year && (x.BirthDate.Year + 100) >= 2024 ? x.BirthDate.Year + 100 :
                    (x.BirthDate.Year + 95) <= DateTime.Today.Year && (x.BirthDate.Year + 95) >= 2024 ? x.BirthDate.Year + 95 :
                    (x.BirthDate.Year + 90) <= DateTime.Today.Year && (x.BirthDate.Year + 90) >= 2024 ? x.BirthDate.Year + 90 :
                    (x.BirthDate.Year + 85) <= DateTime.Today.Year && (x.BirthDate.Year + 85) >= 2024 ? x.BirthDate.Year + 85 :
                    (x.BirthDate.Year + 80) <= DateTime.Today.Year && (x.BirthDate.Year + 80) >= 2024 ? x.BirthDate.Year + 80 :
                    0,
            }).ToList();

            return result;
        }

        #region Private functions
        // Strips commas/periods and collapses whitespace so "URIARTE, ROSITA" and
        // "Uriarte Rosita" both normalize to the same searchable form as the
        // space-joined LastName+FirstName+MiddleName concatenation used in queries.
        private static string NormalizeSearchTerm(string input)
        {
            var normalized = input.Trim().ToLower()
                .Replace(",", " ")
                .Replace(".", " ");

            while (normalized.Contains("  "))
                normalized = normalized.Replace("  ", " ");

            return normalized.Trim();
        }
        // ── Shared filter-building, used by GetPagedListAsync, CountMatchingAsync,
        // and the bulk-by-filter methods in Step 8. One source of truth for "what
        // matches this filter" — no joins, operates directly on
        // IQueryable<BeneficiaryInformation>.
        private async Task<IQueryable<BeneficiaryInformation>> BuildNarrowFilterQuery(BeneficiaryFilterDto filter)
        {
            var query = _context.BeneficiaryInformations.AsNoTracking().Where(b => !b.IsDeleted);

            if (filter.PsgcCodeRegion.HasValue && filter.PsgcCodeRegion.Value > 0)
                query = query.Where(b => b.Region == filter.PsgcCodeRegion.Value);

            // ── Province (multi-select OR single-select) ─────────────────────────
            if (filter.PsgcCodeProvinces != null && filter.PsgcCodeProvinces.Any())
                query = query.Where(b => filter.PsgcCodeProvinces.Contains(b.Province));
            else if (filter.PsgcCodeProvince.HasValue && filter.PsgcCodeProvince.Value > 0)
                query = query.Where(b => b.Province == filter.PsgcCodeProvince.Value);

            // ── Municipality (multi-select) ──────────────────────────────────────
            if (filter.PsgcCodeMunicipalities != null && filter.PsgcCodeMunicipalities.Any())
                query = query.Where(b => filter.PsgcCodeMunicipalities.Contains(b.Municipality));

            if (filter.PsgcCodeBarangay.HasValue)
                query = query.Where(b => b.Barangay == filter.PsgcCodeBarangay.Value);

            if (!string.IsNullOrWhiteSpace(filter.DataQualityIssue))
            {
                switch (filter.DataQualityIssue)
                {
                    case "location":
                        query = query.Where(b => !_context.Barangays.Any(br => br.PsgcCodeBarangay == b.Barangay)
                            || !_context.Municipalities.Any(m => m.PsgcCodeMunicipality == b.Municipality));
                        break;
                    case "headsup":
                        query = query.Where(b => b.PaymentStatus == 2 && (b.PaymentDate == null || b.ModeOfPayment == 0));
                        break;
                    case "incomplete":
                        query = query.Where(b => b.DateEndorsed == null || b.DateApplied == null
                            || string.IsNullOrWhiteSpace(b.PhoneNumber) || b.NcscRrn == null);
                        break;
                }
            }
            if (filter.Sex.HasValue && filter.Sex.Value > 0)
                query = query.Where(b => b.Sex == filter.Sex.Value);

            if (filter.FilterModeOfPayment.HasValue && filter.FilterModeOfPayment.Value > 0)
                query = query.Where(b => b.ModeOfPayment == filter.FilterModeOfPayment.Value);

            if (filter.PaymentDate.HasValue)
            {
                var start = filter.PaymentDate.Value.Date;
                var end = start.AddDays(1);
                query = query.Where(b => b.PaymentDate >= start && b.PaymentDate < end);
            }

            if (filter.PaymentDateFrom.HasValue)
                query = query.Where(b => b.PaymentDate >= filter.PaymentDateFrom.Value.Date);

            if (filter.PaymentDateTo.HasValue)
                query = query.Where(b => b.PaymentDate < filter.PaymentDateTo.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(filter.ComplianceMode))
            {
                query = filter.ComplianceMode switch
                {
                    "compliant" => query.Where(b => b.IsCompliant == true),
                    "noncompliant" => query.Where(b => b.IsCompliant == false),
                    "withfindings" => query.Where(b => b.IsCompliant == true
                        && b.AssessmentRemarks != null && b.AssessmentRemarks != string.Empty),
                    _ => query
                };
            }
            else if (filter.IsCompliant.HasValue)
                query = query.Where(b => b.IsCompliant == filter.IsCompliant.Value);

            if (!string.IsNullOrWhiteSpace(filter.EligibilityMode))
            {
                query = filter.EligibilityMode switch
                {
                    "eligible" => query.Where(b => b.IsEligible == true),
                    "ineligible" => query.Where(b => b.IsEligible == false),
                    "withfindings" => query.Where(b => b.IsEligible == true
                        && b.EligibilityRemarks != null && b.EligibilityRemarks != string.Empty),
                    _ => query
                };
            }
            else if (filter.IsEligible.HasValue)
                query = query.Where(b => b.IsEligible == filter.IsEligible.Value);

            if (filter.CoStatus.HasValue && filter.CoStatus.Value >= 0)
            {
                query = filter.CoStatus.Value == 0
                    ? query.Where(b => b.CoStatus == null || b.CoStatus == 0)
                    : query.Where(b => b.CoStatus == filter.CoStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.LastName))
                query = query.Where(b => b.LastName != null && b.LastName.Contains(filter.LastName));

            if (!string.IsNullOrWhiteSpace(filter.FirstName))
                query = query.Where(b => b.FirstName.Contains(filter.FirstName));

            if (!string.IsNullOrWhiteSpace(filter.FullName))
            {
                var name = NormalizeSearchTerm(filter.FullName);
                query = query.Where(b =>
                    (b.LastName + " " + b.FirstName + " " + b.MiddleName).ToLower().Contains(name) ||
                    (b.FirstName + " " + b.MiddleName + " " + b.LastName).ToLower().Contains(name));
            }

            if (!string.IsNullOrWhiteSpace(filter.BatchCode))
            {
                var codes = filter.BatchCode.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim()).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

                query = codes.Count == 1
                    ? query.Where(b => b.BatchCode != null && b.BatchCode.Contains(codes[0]))
                    : query.Where(b => b.BatchCode != null && codes.Contains(b.BatchCode));
            }

            if (!string.IsNullOrWhiteSpace(filter.Validator))
                query = query.Where(b => b.Validator != null && b.Validator.Contains(filter.Validator));

            if (filter.FilterQuarter.HasValue)
                query = query.Where(b => b.Quarter == filter.FilterQuarter.Value);

            if (!string.IsNullOrWhiteSpace(filter.FilterBatch))
                query = query.Where(b => b.Batch != null && b.Batch.Contains(filter.FilterBatch.Trim()));

            if (filter.FilterRefYear.HasValue)
                query = query.Where(b => b.RefYear == filter.FilterRefYear.Value);

            if (filter.DateAddedFrom.HasValue)
                query = query.Where(b => b.DateAdded >= filter.DateAddedFrom.Value.Date);

            if (filter.DateAddedTo.HasValue)
                query = query.Where(b => b.DateAdded < filter.DateAddedTo.Value.Date.AddDays(1));

            if (filter.SpecificAge.HasValue)
            {
                var end = DateTime.Today.AddYears(-filter.SpecificAge.Value).Date;
                var start = DateTime.Today.AddYears(-filter.SpecificAge.Value - 1).Date;
                query = query.Where(b => b.BirthDate > start && b.BirthDate <= end);
            }

            if (filter.SpecificBirthday.HasValue)
            {
                var month = filter.SpecificBirthday.Value.Month;
                var day = filter.SpecificBirthday.Value.Day;
                query = query.Where(b => b.BirthDate.Month == month && b.BirthDate.Day == day);
            }

            if (filter.MilestoneYear.HasValue)
            {
                var milestoneYear = filter.MilestoneYear.Value;
                var milestones = new[] { 80, 85, 90, 95, 100 };
                var today = DateTime.Today;

                if (milestoneYear == 0)
                {
                    query = query.Where(b => !milestones.Any(m =>
                        (b.BirthDate.Year + m) >= 2024 &&
                        ((b.BirthDate.Year + m) < today.Year ||
                         ((b.BirthDate.Year + m) == today.Year && b.BirthDate.DayOfYear <= today.DayOfYear))));
                }
                else
                {
                    query = query.Where(b => milestones.Any(m =>
                        b.BirthDate.Year + m == milestoneYear && b.BirthDate.Year + m >= 2024));
                }
            }
            // ── General Search ──────────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.GeneralSearch))
            {
                var term = filter.GeneralSearch.Trim().ToLower();

                // Resolve mapped keyword values
                int? sexMatch = term switch { "male" => 1, "female" => 2, _ => null };
                int? paymentMatch = term switch
                {
                    "paid" => 2,
                    "unpaid" => 1,
                    "pending" => 3,
                    "n/a" => 0,
                    _ => null
                };
                int? citizenshipMatch = term switch
                {
                    "filipino" => 1,
                    "dual citizenship" => 2,
                    "dual" => 2,
                    _ => null
                };
                int? coStatusMatch = term switch { "endorsed" => 1, "approved" => 2, _ => null };
                int? modeOfPaymentMatch = term switch
                {
                    "cash advance" => 1,
                    "cash advance by sdo" => 1,
                    "bank transfer" => 2,
                    _ => null
                };
                bool searchCoNotSet = term == "not set";
                int.TryParse(term, out var yearTerm);

                // Resolve location name matches via the in-memory cache
                var matchingProvinceCodes = _psgcNameCache.GetProvinceCodesByNameContains(term);
                var matchingMunicipalityCodes = _psgcNameCache.GetMunicipalityCodesByNameContains(term);
                var matchingBarangayCodes = _psgcNameCache.GetBarangayCodesByNameContains(term);
                var matchingRegionCodes = _psgcNameCache.GetRegionCodesByNameContains(term);

                query = query.Where(b =>
                    // ── Name fields ──────────────────────────────────────────────
                    (b.LastName != null && b.LastName.ToLower().Contains(term)) ||
                    b.FirstName.ToLower().Contains(term) ||
                    (b.MiddleName != null && b.MiddleName.ToLower().Contains(term)) ||
                    (b.Extension != null && b.Extension.ToLower().Contains(term)) ||

                    // ── ID / code fields ─────────────────────────────────────────
                    (b.OscaIdNumber != null && b.OscaIdNumber.ToLower().Contains(term)) ||
                    (b.BatchCode != null && b.BatchCode.ToLower().Contains(term)) ||
                    (b.PhoneNumber != null && b.PhoneNumber.ToLower().Contains(term)) ||
                    (b.NcscRrn != null && b.NcscRrn.ToString()!.Contains(term)) ||

                    // ── Location — matched via cache-resolved codes ─────────────
                    matchingProvinceCodes.Contains(b.Province) ||
                    matchingMunicipalityCodes.Contains(b.Municipality) ||
                    matchingBarangayCodes.Contains(b.Barangay) ||
                    matchingRegionCodes.Contains(b.Region) ||

                    // ── Validator / remarks ──────────────────────────────────────
                    (b.Validator != null && b.Validator.ToLower().Contains(term)) ||
                    (b.Remarks != null && b.Remarks.ToLower().Contains(term)) ||
                    (b.AssessmentRemarks != null && b.AssessmentRemarks.ToLower().Contains(term)) ||
                    (b.EligibilityRemarks != null && b.EligibilityRemarks.ToLower().Contains(term)) ||

                    // ── Mapped integer fields ──────────────────────────────────────
                    (sexMatch.HasValue && b.Sex == sexMatch.Value) ||
                    (paymentMatch.HasValue && b.PaymentStatus == paymentMatch.Value) ||
                    (citizenshipMatch.HasValue && b.Citizenship == citizenshipMatch.Value) ||
                    (modeOfPaymentMatch.HasValue && b.ModeOfPayment == modeOfPaymentMatch.Value) ||
                    (coStatusMatch.HasValue && b.CoStatus == coStatusMatch.Value) ||
                    (searchCoNotSet && (b.CoStatus == null || b.CoStatus == 0)) ||

                    (yearTerm > 0 && b.CoDateEndorsed.HasValue && b.CoDateEndorsed.Value.Year == yearTerm) ||
                    (yearTerm > 0 && b.CoDateApproved.HasValue && b.CoDateApproved.Value.Year == yearTerm) ||
                    (yearTerm > 0 && b.BirthDate.Year == yearTerm) ||

                    (b.Batch != null && b.Batch.ToLower().Contains(term)) ||
                    (yearTerm > 0 && b.RefYear == yearTerm) ||
                    (b.RefCode != null && b.RefCode.ToLower().Contains(term))
                );

                // ── FindingRemarks — separate query ─────────────────────────────
                var matchingFindingIds = await _context.BeneficiaryFindings
                    .Where(f => f.FindingRemarks != null && f.FindingRemarks.ToLower().Contains(term))
                    .Select(f => f.BeneficiaryInformationId)
                    .ToListAsync();

                if (matchingFindingIds.Any())
                {
                    var idSet = matchingFindingIds.ToHashSet();
                    query = query.Union(
                        _context.BeneficiaryInformations.AsNoTracking()
                            .Where(b => !b.IsDeleted && idSet.Contains(b.Id)));
                }
            }

            // ── FindingStatus filter ────────────────────────────────────────────────
            if (filter.FindingStatus.HasValue && filter.FindingStatus.Value != 3)
            {
                var matchingIds = await BuildFindingStatusIdQueryAsync(filter.FindingStatus.Value);
                query = query.Where(b => matchingIds.Contains(b.Id));
            }
            // ✅ NEW — match against the FULL payment history, not just the current
            // mirror. A single history row must satisfy every specified criterion
            // together (e.g. "Q1 2026 Paid" means one entry that IS Q1, FY2026, AND
            // Paid — not three different entries each satisfying one piece). This is
            // what lets "go back" filtering (Q1 2026) still find someone whose current
            // status has since moved on to Q2 2026 — essential for accurate historical
            // reporting/statistics.
            bool hasStatuses = filter.PaymentStatuses != null && filter.PaymentStatuses.Any();
            bool hasQuarter = filter.FilterPayrollQuarter.HasValue;
            bool hasQuarters = filter.FilterPayrollQuarters != null && filter.FilterPayrollQuarters.Any();
            bool hasFiscalYear = filter.FilterFiscalYear.HasValue;

            // ✅ CHANGED — Grid search filters against the beneficiary's CURRENT payment
            // status only (the flat PaymentStatus/PayrollQuarter/FiscalYear columns,
            // which every payment-history write path keeps mirrored to whichever entry
            // is marked current). This is intentionally narrower than Statistics, which
            // still needs to search the FULL history (e.g. "who was Paid in Q1 2026 at
            // any point, even if since corrected/moved to Q2") — that logic stays
            // untouched wherever Statistics builds its own query.
            if (hasStatuses)
                query = query.Where(b => filter.PaymentStatuses!.Contains(b.PaymentStatus));

            if (hasQuarter)
                query = query.Where(b => b.PayrollQuarter == filter.FilterPayrollQuarter!.Value);

            if (hasQuarters)
                query = query.Where(b => filter.FilterPayrollQuarters!.Contains(b.PayrollQuarter ?? 0));

            if (hasFiscalYear)
                query = query.Where(b => b.FiscalYear == filter.FilterFiscalYear!.Value);

            return query;
        }
        // WHY A SEPARATE QUERY INSTEAD OF A JOIN:
        // FindingStatus = 0 needs special handling (matches NULL finding OR an
        // explicit FindingStatus=0 row) which is awkward to express cleanly across
        // a LEFT JOIN's null-coalescing in LINQ-to-SQL. A small standalone query
        // against BeneficiaryFindings (which itself has its own FindingStatus
        // index from your AppDbContext) resolving to a HashSet<Guid> of matching
        // IDs, then filtered via .Contains() against the base query, is simpler
        // to read, easier to verify correctness on, and still avoids the 4-table
        // join entirely — only ever touches BeneficiaryFindings, a small table.

        private async Task<HashSet<Guid>> BuildFindingStatusIdQueryAsync(int status)
        {
            if (status == 0)
            {
                // "N/A" = beneficiaries with NO finding row at all, OR an explicit
                // FindingStatus = 0 row. Two separate small queries, unioned in C#
                // — clearer than trying to express "LEFT JOIN ... WHERE x IS NULL
                // OR x = 0" in one LINQ expression.
                var explicitZero = await _context.BeneficiaryFindings
                    .Where(f => f.FindingStatus == 0)
                    .Select(f => f.BeneficiaryInformationId)
                    .ToListAsync();

                var hasNoFindingRow = await _context.BeneficiaryInformations
                    .Where(b => !b.IsDeleted && !_context.BeneficiaryFindings
                        .Any(f => f.BeneficiaryInformationId == b.Id))
                    .Select(b => b.Id)
                    .ToListAsync();

                return explicitZero.Concat(hasNoFindingRow).ToHashSet();
            }

            var matching = await _context.BeneficiaryFindings
                .Where(f => f.FindingStatus == status)
                .Select(f => f.BeneficiaryInformationId)
                .ToListAsync();

            return matching.ToHashSet();
        }
        // Add this private nested class near your other private helper classes
        // (e.g., next to BeneficiaryRawDto) — gives batches a stable, named type
        // instead of relying on anonymous-type identity across loop iterations.
        private sealed class GetByIdsRawDto
        {
            public Guid Id { get; set; }
            public int? Quarter { get; set; }
            public string? Batch { get; set; }
            public int? RefYear { get; set; }
            public string? RefCode { get; set; }
            public string? BatchCode { get; set; }
            public string? OscaIdNumber { get; set; }
            public DateTime? OscaIdDateIssued { get; set; }
            public int? NcscRrn { get; set; }
            public string? LastName { get; set; }
            public string? FirstName { get; set; }
            public string? MiddleName { get; set; }
            public string? Extension { get; set; }
            public DateTime BirthDate { get; set; }
            public int Sex { get; set; }
            public bool IsIndigenousPeople { get; set; }
            public bool IsPersonWithDisability { get; set; }
            public int? CivilStatus { get; set; }
            public int? Citizenship { get; set; }
            public bool IsCompliant { get; set; }
            public string? Validator { get; set; }
            public DateTime ValidationDate { get; set; }
            public int? PayrollQuarter { get; set; }
            public int? FiscalYear { get; set; }
            public int PaymentStatus { get; set; }
            public int ModeOfPayment { get; set; }
            public DateTime? PaymentDate { get; set; }
            public bool IsDeceased { get; set; }
            public DateTime? DateOfDeath { get; set; }
            public bool IsEligible { get; set; }
            public string? AssessmentRemarks { get; set; }
            public string? EligibilityRemarks { get; set; }
            public int? RemarkCategory { get; set; }
            public string? Remarks { get; set; }
            public DateTime DateAdded { get; set; }
            public int? CoStatus { get; set; }
            public DateTime? CoDateEndorsed { get; set; }
            public DateTime? CoDateApproved { get; set; }
            public bool IsDeleted { get; set; }
            public DateTime? DateApplied { get; set; }
            public DateTime? DateEndorsed { get; set; }
            public string? PhoneNumber { get; set; }
            public int PsgcCodeRegion { get; set; }
            public int PsgcCodeProvince { get; set; }
            public int PsgcCodeMunicipality { get; set; }
            public int PsgcCodeBarangay { get; set; }
            public string? RegionName { get; set; }
            public string? ProvinceName { get; set; }
            public string? MunicipalityName { get; set; }
            public string? BarangayName { get; set; }
            public byte[]? RowVersion { get; set; }
        }
        // Add this helper method if not already present
        private static string FormatDuplicateName(
            string? lastName, string? firstName, string? middleName)
        {
            var full = string.Join(", ",
                new[] { lastName?.Trim(), firstName?.Trim() }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            return string.IsNullOrWhiteSpace(middleName)
                ? full
                : $"{full} {middleName.Trim()}";
        }
        // ── Private enum — keeps the logic readable ───────────────────────────────────
        private enum MiddleNameStatus
        {
            BothBlank,
            OneBlank,
            Similar,
            PartiallyDifferent,
            Conflicting
        }
        private static string PaymentStatusLabelFor(int status) => status switch
        {
            1 => "Unpaid",
            2 => "Paid",
            3 => "Pending",
            _ => "N/A"
        };

        // ── Private helper — builds a clear reason label for the reviewer ─────────────
        private static string BuildDuplicateReason(
            bool firstLastExact,
            MiddleNameStatus middleStatus,
            double daysDiff,
            string? middleA,
            string? middleB)
        {
            var birthdatePart = daysDiff == 0
                ? "same birthdate"
                : $"birthdate ±{(int)daysDiff} day(s)";

            // ✅ Most informative label — tells the reviewer exactly what to check
            var namePart = (firstLastExact, middleStatus) switch
            {
                (true, MiddleNameStatus.BothBlank) => "Exact name",
                (true, MiddleNameStatus.OneBlank) => "Exact name (one missing middle)",
                (true, MiddleNameStatus.Similar) => "Exact name",
                (true, MiddleNameStatus.PartiallyDifferent) => "Exact first + last, similar middle",
                (true, MiddleNameStatus.Conflicting) => "⚠ Exact first + last, DIFFERENT middle",
                (false, MiddleNameStatus.BothBlank) => "Similar name",
                (false, MiddleNameStatus.OneBlank) => "Similar name (one missing middle)",
                (false, MiddleNameStatus.Similar) => "Similar name",
                (false, MiddleNameStatus.PartiallyDifferent) => "Similar name + partially different middle",
                (false, MiddleNameStatus.Conflicting) => "⚠ Similar first + last, DIFFERENT middle",
                _ => "Similar name"
            };

            return $"{namePart} + {birthdatePart}";
        }
        // ✅ Helper — extracts names from conflicted entries for a useful error message
        private static string GetConflictedRecordNames(DbUpdateConcurrencyException ex)
        {
            var names = ex.Entries
                .Where(e => e.Entity is BeneficiaryInformation)
                .Select(e =>
                {
                    var entity = (BeneficiaryInformation)e.Entity;
                    return $"{entity.LastName}, {entity.FirstName}";
                })
                .ToList();

            return names.Any()
                ? string.Join("; ", names)
                : "unknown record(s)";
        }
        private sealed class BeneficiaryQueryModel
        {
            public BeneficiaryInformation Beneficiary { get; set; } = default!;
            public string? Region { get; set; }
            public string? Province { get; set; }
            public string? Municipality { get; set; }
            public string? Barangay { get; set; }

            public int? FindingStatus { get; set; }
            public string? FindingRemarks { get; set; }
        }
        private sealed class BeneficiaryRawDto
        {
            public Guid Id { get; set; }
            public int? Quarter { get; set; }
            public string? Batch { get; set; }
            public int? RefYear { get; set; }
            public string? RefCode { get; set; }
            public DateTime? DateApplied { get; set; }
            public DateTime? DateEndorsed { get; set; }
            public string? BatchCode { get; set; }
            public string? OscaIdNumber { get; set; }
            public DateTime? OscaIdDateIssued { get; set; }
            public int? NcscRrn { get; set; }
            public string? LastName { get; set; }
            public string? FirstName { get; set; }
            public string? MiddleName { get; set; }
            public string? Extension { get; set; }
            public DateTime BirthDate { get; set; }
            public string? PhoneNumber { get; set; }
            public bool IsIndigenousPeople { get; set; }
            public bool IsPersonWithDisability { get; set; }
            public int? CivilStatus { get; set; }
            public int? Citizenship { get; set; }
            public int Sex { get; set; }
            public int PsgcCodeRegion { get; set; }
            public int PsgcCodeProvince { get; set; }
            public int PsgcCodeMunicipality { get; set; }
            public int PsgcCodeBarangay { get; set; }
            public string? RegionName { get; set; }
            public string? ProvinceName { get; set; }
            public string? MunicipalityName { get; set; }
            public string? BarangayName { get; set; }
            public bool IsCompliant { get; set; }
            public string? Validator { get; set; }
            public DateTime ValidationDate { get; set; }
            public int? PayrollQuarter { get; set; }
            public int? FiscalYear { get; set; }
            public int PaymentStatus { get; set; }
            public int ModeOfPayment { get; set; }
            public DateTime? PaymentDate { get; set; }
            public bool IsDeceased { get; set; }
            public DateTime? DateOfDeath { get; set; }
            public bool IsEligible { get; set; }
            public string? AssessmentRemarks { get; set; }
            public string? EligibilityRemarks { get; set; }
            public int? RemarkCategory { get; set; }
            public DateTime DateAdded { get; set; }
            public string? Remarks { get; set; }
            public bool IsDeleted { get; set; }
            public int? FindingStatus { get; set; }
            public string? FindingRemarks { get; set; }
            public int? CoStatus { get; set; }
            public DateTime? CoDateEndorsed { get; set; }
            public DateTime? CoDateApproved { get; set; }
            public byte[]? RowVersion { get; set; }
            public bool HasDocuments { get; set; }
            public int? CgpPageNumber { get; set; }
            public Guid? CgpGenerationId { get; set; }
            public string? CgpPrefix { get; set; }
        }

        //Normalizes Levenshtein (0.0 = no match, 1.0 = identical)
        private static double ComputeNameSimilarity(string? a, string? b)
        {
            // ✅ Sanitize fully before any length check or comparison
            a = (a ?? string.Empty).Trim().ToUpperInvariant();
            b = (b ?? string.Empty).Trim().ToUpperInvariant();

            // ✅ Remove any double spaces that came from null-interpolation like "John  Smith"
            while (a.Contains("  ")) a = a.Replace("  ", " ");
            while (b.Contains("  ")) b = b.Replace("  ", " ");

            if (a.Length == 0 || b.Length == 0) return 0.0;
            if (a == b) return 1.0;

            int dist = LevenshteinDistance(a, b);
            return 1.0 - (double)dist / Math.Max(a.Length, b.Length);
        }
        private static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            // ✅ Use a flat 1D array instead of 2D to avoid dimension miscalculation
            int aLen = a.Length;
            int bLen = b.Length;

            var prev = new int[bLen + 1];
            var curr = new int[bLen + 1];

            // Initialize first row: cost of deleting all chars from b
            for (int j = 0; j <= bLen; j++)
                prev[j] = j;

            for (int i = 1; i <= aLen; i++)
            {
                curr[0] = i; // cost of deleting i chars from a

                for (int j = 1; j <= bLen; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;

                    curr[j] = Math.Min(
                        Math.Min(
                            prev[j] + 1,      // deletion
                            curr[j - 1] + 1), // insertion
                            prev[j - 1] + cost // substitution
                    );
                }

                // Swap rows
                var temp = prev;
                prev = curr;
                curr = temp;
            }

            return prev[bLen];
        }
        #endregion Private functions - End


    }
}
