using ClosedXML.Excel;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace EcaInformationSystem.Application.Services
{
    public class StatisticsService : IStatisticsService
    {
        private readonly IBeneficiaryInformationRepository _repo;
        private readonly IMemoryCache _memoryCache;
        private const string StatisticsCacheVersionKey = "statistics_cache_version_v1";
        private const string StatisticsCachePrefix = "statistics_report_";

        public StatisticsService(
            IBeneficiaryInformationRepository repo,
            IMemoryCache memoryCache)
        {
            _repo = repo;
            _memoryCache = memoryCache;
        }

        public async Task<StatisticsReportDto> GetStatisticsReportAsync(StatisticsRequestDto request)
        {
            var cacheKey = BuildCacheKey(request);

            if (_memoryCache.TryGetValue(cacheKey, out StatisticsReportDto? cached) && cached is not null)
                return cached;

            var report = await _repo.GetStatisticsReportAsync(request);

            _memoryCache.Set(cacheKey, report, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(5)
            });

            return report;
        }

        // Not cached — an on-demand audit drill-down, not part of the main
        // report render path, so freshness matters more than round-trip cost.
        public async Task<StatisticsMembersPagedResultDto> GetStatisticsMembersAsync(StatisticsMembersRequestDto request)
            => await _repo.GetStatisticsMembersAsync(request.Filter, request.Bucket, request.PageNumber, request.PageSize);

        // "Download Excel" inside the Statistics audit modal — not cached,
        // same reasoning as GetStatisticsMembersAsync above (an on-demand
        // export, always the freshest data for whatever's currently shown).
        public async Task<byte[]> ExportGranteesAsync(StatisticsMembersRequestDto request)
        {
            var rows = await _repo.GetGranteeExportRowsAsync(request.Filter, request.Bucket);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Grantees");

            string[] headers =
            {
                "Batch Code", "OSCA ID No.", "NCSC RRN", "Last Name", "First Name", "Middle Name", "Extension",
                "Birth Date", "Age", "Sex", "Region", "Province", "Municipality", "Barangay", "Contact Number",
                "Compliant", "Validator", "Validation Date", "Remarks",
                "Payment Status", "Payroll Quarter", "Fiscal Year", "Mode of Payment", "CO Status",
                "Milestone Year", "Liveness Verified", "Ready for EFT",
                "Preferred Payout Channel", "Bank/Wallet Name", "Account Number", "Branch Name",
                "Bank Address", "GCash Name", "GCash/Mobile Number", "Joint Account", "Swift Code", "IBAN"
            };

            for (var col = 1; col <= headers.Length; col++)
                ws.Cell(1, col).Value = headers[col - 1];

            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var row = 2;
            foreach (var r in rows)
            {
                var col = 1;
                ws.Cell(row, col++).Value = r.BatchCode;
                ws.Cell(row, col++).Value = r.OscaIdNumber;
                ws.Cell(row, col++).Value = r.NcscRrn;
                ws.Cell(row, col++).Value = r.LastName;
                ws.Cell(row, col++).Value = r.FirstName;
                ws.Cell(row, col++).Value = r.MiddleName;
                ws.Cell(row, col++).Value = r.Extension;
                ws.Cell(row, col++).Value = r.BirthDate.ToString("MMM d, yyyy");
                ws.Cell(row, col++).Value = r.Age;
                ws.Cell(row, col++).Value = r.Sex;
                ws.Cell(row, col++).Value = r.RegionName;
                ws.Cell(row, col++).Value = r.ProvinceName;
                ws.Cell(row, col++).Value = r.MunicipalityName;
                ws.Cell(row, col++).Value = r.BarangayName;
                ws.Cell(row, col++).Value = r.ContactNumber;
                ws.Cell(row, col++).Value = r.IsCompliant ? "Yes" : "No";
                ws.Cell(row, col++).Value = r.Validator;
                ws.Cell(row, col++).Value = r.ValidationDate == default ? string.Empty : r.ValidationDate.ToString("MMM d, yyyy");
                ws.Cell(row, col++).Value = r.Remarks;
                ws.Cell(row, col++).Value = r.PaymentStatusLabel;
                ws.Cell(row, col++).Value = r.PayrollQuarter.HasValue ? $"Q{r.PayrollQuarter}" : string.Empty;
                ws.Cell(row, col++).Value = r.FiscalYear;
                ws.Cell(row, col++).Value = r.ModeOfPaymentLabel;
                ws.Cell(row, col++).Value = r.CoStatusLabel;
                ws.Cell(row, col++).Value = r.MilestoneYear > 0 ? r.MilestoneYear.ToString() : string.Empty;
                ws.Cell(row, col++).Value = r.IsLivenessVerified switch { true => "Verified", false => "Not Verified", null => "" };
                ws.Cell(row, col++).Value = r.IsReadyForEft switch { true => "Ready", false => "Not Ready", null => "" };
                ws.Cell(row, col++).Value = r.PreferredChannelLabel;
                ws.Cell(row, col++).Value = r.BankOrWalletName;
                ws.Cell(row, col++).Value = r.AccountNumber;
                ws.Cell(row, col++).Value = r.BranchName;
                ws.Cell(row, col++).Value = r.BankAddress;
                ws.Cell(row, col++).Value = r.GCashName;
                ws.Cell(row, col++).Value = r.GCashOrMobileNumber;
                ws.Cell(row, col++).Value = r.IsJointAccount switch { true => "Yes", false => "No", null => "" };
                ws.Cell(row, col++).Value = r.SwiftCode;
                ws.Cell(row, col++).Value = r.Iban;
                row++;
            }

            if (rows.Count > 0)
                ws.Range(1, 1, row - 1, headers.Length).SetAutoFilter();

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public Task InvalidateStatisticsCacheAsync()
        {
            // Bump the version token to invalidate all statistics cache entries
            var newVersion = Guid.NewGuid().ToString();
            _memoryCache.Set(StatisticsCacheVersionKey, newVersion);
            return Task.CompletedTask;
        }

        private string BuildCacheKey(StatisticsRequestDto request)
        {
            var version = GetCurrentCacheVersion();

            return string.Join("|",
                StatisticsCachePrefix,
                version,
                request.Region?.ToString() ?? "null",
                request.Province?.ToString() ?? "null",
                request.Municipality?.ToString() ?? "null",
                request.MilestoneYear.ToString(),
                request.MilestoneAge.ToString(),
                // ✅ NEW — separate, additive anticipation filter (2024/2025/2026
                // multi-select); order-independent join so [2024,2025] and
                // [2025,2024] hit the same cache entry (same reasoning as
                // PaymentStatuses below).
                request.AnticipatedMilestoneYears != null && request.AnticipatedMilestoneYears.Any()
                    ? string.Join(",", request.AnticipatedMilestoneYears.OrderBy(y => y))
                    : "null",
                // ✅ CHANGED — order-independent so [1,2] and [2,1] hit the same
                // cache entry instead of silently missing each other.
                request.PaymentStatuses != null && request.PaymentStatuses.Any()
                    ? string.Join(",", request.PaymentStatuses.OrderBy(s => s))
                    : "null",
                request.PayrollQuarter?.ToString() ?? "null",
                request.FiscalYear?.ToString() ?? "null", // ✅ new
                request.DateEndorsedFrom?.ToString("yyyyMMdd") ?? "null",
                request.DateEndorsedTo?.ToString("yyyyMMdd") ?? "null",
                request.DateAddedFrom?.ToString("yyyyMMdd") ?? "null",
                request.DateAddedTo?.ToString("yyyyMMdd") ?? "null",
                request.IsLivenessVerified?.ToString() ?? "null",
                request.IsReadyForEft?.ToString() ?? "null",
                request.CoDateEndorsedFrom?.ToString("yyyyMMdd") ?? "null",
                request.CoDateEndorsedTo?.ToString("yyyyMMdd") ?? "null",
                request.CoDateApprovedFrom?.ToString("yyyyMMdd") ?? "null",
                request.CoDateApprovedTo?.ToString("yyyyMMdd") ?? "null");
        }
        private string GetCurrentCacheVersion()
        {
            return _memoryCache.GetOrCreate(StatisticsCacheVersionKey, entry =>
            {
                entry.Priority = CacheItemPriority.NeverRemove;
                return "v1";
            })!;
        }
    }
}