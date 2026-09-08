using ClosedXML.Excel;
using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Common.Extensions;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.IO.Compression;

namespace EcaInformationSystem.Application.Services
{
    public class BeneficiaryInformationService : IBeneficiaryInformationService
    {
        private readonly IBeneficiaryInformationRepository _repo;
        private readonly IRegionRepository _regionRepository;
        private readonly IProvinceRepository _provinceRepository;
        private readonly IMunicipalityRepository _municipalityRepository;
        private readonly IBarangayRepository _barangayRepository;
        private readonly ILogRepository _logRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly IPayrollJobTracker _payrollJobTracker;
        private readonly IBackgroundTaskQueue _backgroundTaskQueue;
        private readonly IPsgcNameCache _psgcNameCache;
        private readonly IStatisticsService _statisticsService;
        private readonly IBeneficiaryVerificationChecklistRepository _checklistRepo;
        private readonly IResolvedDuplicatePairRepository _resolvedDuplicatePairRepo;
        private readonly IJurisdictionGuardService _jurisdictionGuardService;
        private const string GlobalDuplicateScanCacheKey = "global_duplicate_scan_v1";
        public BeneficiaryInformationService(IBeneficiaryInformationRepository repo, IRegionRepository regionRepository
    , IProvinceRepository provinceRepository, IMunicipalityRepository municipalityRepository, IBarangayRepository barangayRepository,
    ILogRepository logRepository, IMemoryCache memoryCache, IPayrollJobTracker payrollJobTracker,
    IBackgroundTaskQueue backgroundTaskQueue, IPsgcNameCache psgcNameCache, IStatisticsService statisticsService,
    IBeneficiaryVerificationChecklistRepository checklistRepo, IResolvedDuplicatePairRepository resolvedDuplicatePairRepo,
    IJurisdictionGuardService jurisdictionGuardService)
        {
            _repo = repo;
            _regionRepository = regionRepository;
            _provinceRepository = provinceRepository;
            _municipalityRepository = municipalityRepository;
            _barangayRepository = barangayRepository;
            _logRepository = logRepository;
            _memoryCache = memoryCache;
            _payrollJobTracker = payrollJobTracker;
            _backgroundTaskQueue = backgroundTaskQueue;
            _psgcNameCache = psgcNameCache;
            _statisticsService = statisticsService;
            _checklistRepo = checklistRepo;
            _resolvedDuplicatePairRepo = resolvedDuplicatePairRepo;
            _jurisdictionGuardService = jurisdictionGuardService;
        }
        public async Task SetCurrentPaymentHistoryAsync(Guid beneficiaryId, Guid historyId, string userName)
        {
            await _repo.SetCurrentPaymentHistoryAsync(beneficiaryId, historyId, userName);
            await AddLogAsync(beneficiaryId, "Marked a different payment history entry as current", userName);
            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }
        public async Task<List<CgpRangeMemberDto>> GetCgpRangeMembersAsync(Guid cgpGenerationId, int municipalityCode, int milestoneYear)
        {
            return await _repo.GetCgpRangeMembersAsync(cgpGenerationId, municipalityCode, milestoneYear);
        }
        public async Task DeletePaymentHistoryAsync(Guid historyId, string userName)
        {
            var info = await _repo.DeletePaymentHistoryAsync(historyId);

            var periodLabel = info.PayrollQuarter.HasValue || info.FiscalYear.HasValue
                ? $" (Q{info.PayrollQuarter} FY{info.FiscalYear})"
                : string.Empty;

            await AddLogAsync(
                info.BeneficiaryId,
                $"Payment record deleted → was {MapPaymentStatusLabel(info.PaymentStatus)}{periodLabel}",
                userName);

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }

        public async Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(Guid beneficiaryId)
        {
            return await _repo.GetPaymentHistoryAsync(beneficiaryId);
        }

        public async Task BulkAddPaymentHistoryAsync(
            List<Guid> beneficiaryIds, int? payrollQuarter, int? fiscalYear,
            int paymentStatus, int? modeOfPayment, DateTime? paymentDate,
            string? remarks, string userName)
        {
            if (beneficiaryIds == null || !beneficiaryIds.Any())
                throw new Exception(CommonConstants.NoRecordsSelected);

            // ── Business rule validation lives here, in the service layer, not the
            // repository — the repository should stay a thin data-access layer.
            if (paymentStatus != 0 && paymentStatus != 1 && paymentStatus != 2 && paymentStatus != 3)
                throw new Exception(CommonConstants.InvalidPaymentStatus);

            // ✅ CHANGED — Payment Date is no longer required at this shared layer.
            // Bulk office transactions are frequently recorded before the exact date
            // is known, so leaving it blank here now means "leave whatever date this
            // beneficiary already had" (see the repository, which carries the prior
            // date forward per-beneficiary) instead of forcing a value or wiping it.
            // The individual "Record Payment" UI flow still requires a date — that's
            // enforced client-side before this method is ever called for that flow.

            if (paymentStatus == 2 && !modeOfPayment.HasValue)
                throw new Exception("Mode of Payment is required when status is Paid.");

            if (paymentStatus == 2 && (!payrollQuarter.HasValue || !fiscalYear.HasValue))
                throw new Exception("Payroll Quarter and Fiscal Year are required when status is Paid.");

            await _repo.BulkAddPaymentHistoryAsync(
                beneficiaryIds, payrollQuarter, fiscalYear, paymentStatus,
                modeOfPayment, paymentDate, remarks, userName);

            var statusLabel = MapPaymentStatusLabel(paymentStatus);
            var periodLabel = payrollQuarter.HasValue || fiscalYear.HasValue
                ? $" (Q{payrollQuarter} FY{fiscalYear})"
                : string.Empty;
            var paymentDateLabel = paymentDate.HasValue
                ? $"Payment Date {paymentDate.Value.ToString("MMMM dd, yyyy")}"
                : string.Empty;

            foreach (var id in beneficiaryIds)
                await AddLogAsync(id, $"New payment record{periodLabel} {paymentDateLabel} → {statusLabel}", userName);

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }

        public async Task EditPaymentHistoryEntryAsync(
            Guid historyId, Guid beneficiaryId, int? payrollQuarter, int? fiscalYear,
            int paymentStatus, int? modeOfPayment, DateTime? paymentDate,
            string? remarks, string userName)
        {
            if ((paymentStatus == 1 || paymentStatus == 2) && !paymentDate.HasValue)
            {
                var label = paymentStatus == 2 ? "Payment" : "Unpaid";
                throw new Exception($"{label} Date is required when status is {(paymentStatus == 2 ? "Paid" : "Unpaid")}.");
            }

            if (paymentStatus == 2 && !modeOfPayment.HasValue)
                throw new Exception("Mode of Payment is required when status is Paid.");

            await _repo.EditPaymentHistoryEntryAsync(
                historyId, payrollQuarter, fiscalYear, paymentStatus,
                modeOfPayment, paymentDate, remarks, userName);

            var paymentDateLabel = paymentDate.HasValue
                ? $"Payment Date {paymentDate.Value.ToString("MMMM dd, yyyy")}"
                : string.Empty;

            await AddLogAsync(beneficiaryId,
                $"Payment record corrected → {MapPaymentStatusLabel(paymentStatus)} (Q{payrollQuarter} FY{fiscalYear}) {paymentDateLabel}",
                userName);

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }
        // ── "Search Similar Names" — explicit, user-triggered fuzzy fallback ──────
        // Only called when the person clicks "Search Similar Names" after a normal
        // search (Full Name, Last Name, First Name, or General Search) returns zero
        // results. This is a deliberate secondary action, not an automatic silent
        // substitution — results are always presented to the user as "similar,"
        // never mixed into or mistaken for an exact match.
        public async Task<PagedResultDto<BeneficiaryListItemDto>> SearchSimilarNamesAsync(BeneficiaryFilterDto filter)
        {
            // Prefer the most specific name field first, fall back through the
            // others, General Search last — matches the priority a person would
            // naturally expect ("if I typed a Last Name, match on that, not on
            // whatever else happens to be filled in").
            var nameTerm = !string.IsNullOrWhiteSpace(filter.FullName) ? filter.FullName
                : !string.IsNullOrWhiteSpace(filter.LastName) ? filter.LastName
                : !string.IsNullOrWhiteSpace(filter.FirstName) ? filter.FirstName
                : filter.GeneralSearch;

            if (string.IsNullOrWhiteSpace(nameTerm))
            {
                return new PagedResultDto<BeneficiaryListItemDto>
                {
                    Items = new List<BeneficiaryListItemDto>(),
                    TotalCount = 0,
                    PageNumber = 1,
                    PageSize = filter.PageSize,
                    IsFuzzyMatch = true
                };
            }

            // Stricter threshold (0.75) — favors precision over recall. This result
            // set is shown to the user as "these are probably who you meant," so
            // false positives are worse here than in the duplicate-detection scan,
            // which is reviewed by a human anyway and tolerates more noise.
            var similarIds = await _repo.FindSimilarNameIdsAsync(nameTerm, maxResults: 50, minScore: 0.75);

            if (!similarIds.Any())
            {
                return new PagedResultDto<BeneficiaryListItemDto>
                {
                    Items = new List<BeneficiaryListItemDto>(),
                    TotalCount = 0,
                    PageNumber = 1,
                    PageSize = filter.PageSize,
                    IsFuzzyMatch = true
                };
            }

            var fuzzyDtos = await _repo.GetByIdsAsync(similarIds);

            var items = fuzzyDtos
                .Select(MapInformationDtoToListItem)
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ToList();

            return new PagedResultDto<BeneficiaryListItemDto>
            {
                Items = items,
                TotalCount = items.Count,
                PageNumber = 1,
                PageSize = filter.PageSize,
                IsFuzzyMatch = true
            };
        }

        // Maps the richer BeneficiaryInformationDto (returned by GetByIdsAsync)
        // into the leaner BeneficiaryListItemDto shape the Records grid expects.
        // Needed specifically here because the fuzzy-match path goes through
        // GetByIdsAsync, whose DTO wraps location names as JsonElement rather than
        // plain strings — the grid's normal paged path never hits this conversion.
        private static BeneficiaryListItemDto MapInformationDtoToListItem(BeneficiaryInformationDto x) => new()
        {
            Id = x.Id,
            Quarter = x.Quarter,
            Batch = x.Batch,
            RefYear = x.RefYear,
            RefCode = x.RefCode,
            BatchCode = x.BatchCode,
            PhoneNumbers = x.PhoneNumbers,
            LastName = x.LastName,
            FirstName = x.FirstName,
            MiddleName = x.MiddleName,
            Extension = x.Extension,
            BirthDate = x.BirthDate,
            Age = x.Age,
            MilestoneYear = x.MilestoneYear,
            Sex = x.Sex,
            Citizenship = x.Citizenship,
            PsgcCodeRegion = x.PsgcCodeRegion,
            PsgcCodeProvince = x.PsgcCodeProvince,
            PsgcCodeMunicipality = x.PsgcCodeMunicipality,
            PsgcCodeBarangay = x.PsgcCodeBarangay,
            ProvinceName = x.Province?.GetString(),
            MunicipalityName = x.Municipality?.GetString(),
            BarangayName = x.Barangay?.GetString(),
            Validator = x.Validator,
            PayrollQuarter = x.PayrollQuarter,
            FiscalYear = x.FiscalYear,
            PaymentStatus = x.PaymentStatus,
            ModeOfPayment = x.ModeOfPayment,
            PaymentDate = x.PaymentDate,
            IsEligible = x.IsEligible,
            IsCompliant = x.IsCompliant,
            FindingStatus = x.FindingStatus,
            CoStatus = x.CoStatus,
            CoDateEndorsed = x.CoDateEndorsed,
            CoDateApproved = x.CoDateApproved,
            HasDocuments = false, // not tracked by GetByIdsAsync — acceptable for this secondary view
            EligibilityRemarksPreview = Truncate(x.EligibilityRemarks, 80),
            AssessmentRemarksPreview = Truncate(x.AssessmentRemarks, 80),
            FindingRemarksPreview = Truncate(x.FindingRemarks, 80),
            RowVersion = x.RowVersion
        };

        private static string? Truncate(string? value, int maxLength) =>
            string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value.Substring(0, maxLength);
        public async Task<PagedResultDto<LogEntryDto>> GetAllLogsAsync(LogFilterDto filter)
        {
            var (items, totalCount) = await _logRepository.GetAllLogsAsync(filter);
            return new PagedResultDto<LogEntryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber, // ✅ set instead of TotalPages
                PageSize = filter.PageSize      // ✅ TotalPages derives from this + TotalCount
            };
        }
        public async Task<byte[]> ExportFilteredAsTemplateAsync(BeneficiaryFilterDto filter, string userName)
        {
            var allData = (await _repo.GetByIdsAsync(filter.Ids))
                    .DistinctBy(x => x.Id)
                    .ToList();

            var data = allData
             .OrderBy(x => x.LastName)
             .ThenBy(x => x.FirstName)
             .ThenBy(x => x.MiddleName)
             .ToList();

            if (data == null || !data.Any())
                throw new InvalidOperationException(CommonConstants.NoDataAvailableToExport);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(CommonConstants.Grantees);
            BuildExportTemplateSheet(worksheet, data);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            await AddSystemLogAsync(
                $"Exported {data.Count} record(s) to Excel",
                userName,
                "Download");

            return stream.ToArray();
        }
        private void BuildExportTemplateSheet(IXLWorksheet ws, List<BeneficiaryInformationDto> records)
        {
            // =========================
            // ✅ TITLE HEADER (ROW 1–9)
            // =========================

            int colCount = 22; // total columns in your sheet

            void AddCenteredTitle(int row, string text)
            {
                var range = ws.Range(row, 1, row, colCount);
                range.Merge();
                range.Value = text;
                range.Style.Font.Bold = true;
                range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }


            // Row 1
            AddCenteredTitle(1, CommonConstants.NCSC);

            // Row 2
            AddCenteredTitle(2, CommonConstants.Act);

            // Row 3
            AddCenteredTitle(3, CommonConstants.RegionalOfficeCaraga);

            // Row 4
            AddCenteredTitle(4, $"({CommonConstants.ListOfValidatedPaid} {DateTime.UtcNow.ToYear()})");

            // Row 5–9 intentionally blank (no content)

            // =========================
            // ✅ HEADER (ROW 10)
            // =========================
            int headerRow = 10;

            string[] headers = new[]
             {
                CommonConstants.BatchCode, CommonConstants.Number, CommonConstants.OscaIdNumber,
                CommonConstants.NcscRrn, CommonConstants.LastName, CommonConstants.FirstName,
                CommonConstants.MiddleName, CommonConstants.Extension, CommonConstants.BirthMonth,
                CommonConstants.BirthDay, CommonConstants.BirthYear, CommonConstants.Age,
                CommonConstants.Sex, CommonConstants.Region, CommonConstants.Province,
                CommonConstants.Municipality, CommonConstants.Barangay, CommonConstants.ComplianceToDocumentaryRequirements,
                CommonConstants.NameOfValidator, CommonConstants.ValidationDate, CommonConstants.Remarks,
                CommonConstants.ContactNumber
            };

            for (int col = 1; col <= headers.Length; col++)
            {
                ws.Cell(headerRow, col).Value = headers[col - 1];
            }

            // STYLE HEADER
            var headerRange = ws.Range(headerRow, 1, headerRow, colCount);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // After AddCenteredTitle(4, ...)
            PayrollLogos.AddLogos(
                ws,
                anchorRow: 1,      // anchor to row 1 (title rows are 1-4)
                leftCol: 1,      // col A — left edge
                rightCol: colCount,     // right edge (last column)
                widthPx: 60,
                heightPx: 60,
                offsetLeft: 4,
                offsetRight: 270);

            // =========================
            // ✅ DATA (ROW 11+)
            // =========================
            int row = 11;
            int counter = 1;

            foreach (var item in records)
            {
                ws.Cell(row, 1).Value = item.BatchCode?.ToUpperInvariant();
                ws.Cell(row, 2).Value = counter++;

                ws.Cell(row, 3).Value = item.OscaIdNumber?.ToUpperInvariant();
                ws.Cell(row, 4).Value = item.NcscRrn;

                ws.Cell(row, 5).Value = item.LastName?.ToUpperInvariant();
                ws.Cell(row, 6).Value = item.FirstName?.ToUpperInvariant();
                ws.Cell(row, 7).Value = item.MiddleName?.ToUpperInvariant();
                ws.Cell(row, 8).Value = item.Extension?.ToUpperInvariant();

                // MONTH AS TEXT
                ws.Cell(row, 9).Value = GetMonthName(item.BirthDate.Month);
                ws.Cell(row, 10).Value = item.BirthDate.Day.ToPaddedDay();
                ws.Cell(row, 11).Value = item.BirthDate.Year;

                // AGE (NEW COLUMN)
                ws.Cell(row, 12).Value = GetAge(item.BirthDate);

                ws.Cell(row, 13).Value = item.Sex == 1 ? CommonConstants.Male : CommonConstants.Female;

                ws.Cell(row, 14).Value = item.Region?.ToString().ToUpperInvariant();
                ws.Cell(row, 15).Value = item.Province?.ToString().ToUpperInvariant();
                ws.Cell(row, 16).Value = item.Municipality?.ToString().ToUpperInvariant();
                ws.Cell(row, 17).Value = item.Barangay?.ToString().ToUpperInvariant();

                // COMPLIANCE MAPPING — now includes "Yes (w/ Minor Findings)" + remarks
                var complianceLabel = GetComplianceExportLabel(item.IsCompliant, item.AssessmentRemarks);
                var hasRemarksToShow = item.IsCompliant && !string.IsNullOrWhiteSpace(item.AssessmentRemarks);

                ws.Cell(row, 18).Value = hasRemarksToShow
                    ? $"{complianceLabel}\n{item.AssessmentRemarks!.ToUpperInvariant()}"
                    : complianceLabel;

                if (hasRemarksToShow)
                {
                    ws.Cell(row, 18).Style.Alignment.WrapText = true;
                    ws.Row(row).Height = Math.Max(ws.Row(row).Height, 30); // give the wrapped remarks room
                }

                ws.Cell(row, 19).Value = item.Validator?.ToUpperInvariant();
                ws.Cell(row, 20).Value = item.ValidationDate.ToDefaultFormat();
                ws.Cell(row, 21).Value = item.Remarks?.ToUpperInvariant();

                // Mobile number — "N/A" when the grantee has none on file, same
                // fallback convention the grid itself uses elsewhere.
                var mobileNumbers = item.PhoneNumbers?
                    .Where(n => !string.IsNullOrWhiteSpace(n.Number))
                    .Select(n => n.Number)
                    .ToList() ?? new List<string>();
                ws.Cell(row, 22).Value = mobileNumbers.Any() ? string.Join(", ", mobileNumbers) : "N/A";

                ws.Range(row, 1, row, colCount)
                    .Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                row++;
            }
            // ✅ ADD HERE — border entire used range
            ws.Range(11, 1, row - 1, colCount).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(11, 1, row - 1, colCount).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // =========================
            // ✅ AUTO FORMAT
            // =========================
            ws.Columns().AdjustToContents();

            // Freeze header
            ws.SheetView.FreezeRows(10);

            // Auto filter
            ws.Range(headerRow, 1, headerRow, colCount).SetAutoFilter();
            // =========================
            // ✅ SIGNATURE BLOCK
            // =========================
            int signatureStartRow = row + 2;
            string today = DateTime.Today.ToCompeleteDate();

            // Helper to build one signature block
            void AddSignatureBlock(int labelCol, int blockStartCol, int blockEndCol, int nameStartCol, int nameEndCol, string role)
            {
                // Role label — flush left
                ws.Cell(signatureStartRow, labelCol).Value = role;
                ws.Cell(signatureStartRow, labelCol).Style.Font.Bold = true;

                int nameRow = signatureStartRow + 3;

                // Name — italic placeholder, flush left, with underline
                var nameRange = ws.Range(nameRow, nameStartCol, nameRow, nameEndCol);
                nameRange.Merge();
                nameRange.Value = CommonConstants.EnterName;
                nameRange.Style.Font.Italic = true;
                nameRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                // Position — italic placeholder, flush left, with underline
                var posRange = ws.Range(nameRow + 1, nameStartCol, nameRow + 1, nameEndCol);
                posRange.Merge();
                posRange.Value = CommonConstants.EnterPosition;
                posRange.Style.Font.Italic = true;
                posRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                // "Signature over printed name" — flush left, no center
                var sigRange = ws.Range(nameRow + 2, blockStartCol, nameRow + 2, blockEndCol);
                sigRange.Merge();
                sigRange.Value = CommonConstants.SignatureOverPrintedName;
                sigRange.Style.Font.Italic = true;
                sigRange.Style.Font.FontSize = 8;
                sigRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                // Date — flush left, no center
                var dateRange = ws.Range(nameRow + 3, blockStartCol, nameRow + 3, blockEndCol);
                dateRange.Merge();
                dateRange.Value = today;
                dateRange.Style.Font.FontSize = 9;
                dateRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            }

            // PREPARED BY — left, label at col 1, underlines cols 1–5
            AddSignatureBlock(
                labelCol: 1,
                blockStartCol: 1, blockEndCol: 6,
                nameStartCol: 1, nameEndCol: 5,
                role: CommonConstants.PreparedBy);

            // NOTED BY — middle, label at col 8, underlines cols 8–13
            AddSignatureBlock(
                labelCol: 8,
                blockStartCol: 8, blockEndCol: 14,
                nameStartCol: 8, nameEndCol: 13,
                role: CommonConstants.NotedBy);

            // APPROVED BY — right, label at col 16, underlines cols 16–20
            AddSignatureBlock(
                labelCol: 16,
                blockStartCol: 16, blockEndCol: 21,
                nameStartCol: 16, nameEndCol: 20,
                role: CommonConstants.ApprovedBy);
            // =========================
            // ✅ PAGE SETUP
            // =========================

            // Legal paper size (5 = Legal in ClosedXML)
            ws.PageSetup.PaperSize = XLPaperSize.LegalPaper;

            // Landscape orientation
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;

            // Fit all columns on one page (scale to width), unlimited rows
            ws.PageSetup.FitToPages(1, 0);

            // Repeat ONLY the column header row (row 10) on every printed page
            // This excludes rows 1-9 (the main title header)
            ws.PageSetup.SetRowsToRepeatAtTop(10, 10);

            // Page numbering — "Page 1 of 12" format
            // Center footer
            ws.PageSetup.Footer.Center.AddText(CommonConstants.Page);
            ws.PageSetup.Footer.Center.AddText(XLHFPredefinedText.PageNumber);
            ws.PageSetup.Footer.Center.AddText(CommonConstants.Of);
            ws.PageSetup.Footer.Center.AddText(XLHFPredefinedText.NumberOfPages);
            // Push footer below content area
            ws.PageSetup.Margins.Bottom = 0.7; // inches — gives footer room
            ws.PageSetup.Margins.Footer = 0.5; // inches — footer distance from bottom edge

        }
        // In BeneficiaryInformationService.cs - Update GetPossibleDuplicatesAsync

        public async Task<PossibleDuplicateSummaryDto> GetPossibleDuplicatesAsync(BeneficiaryFilterDto filter)
        {
            var cacheKey = BuildDuplicateScanCacheKey(filter);

            if (_memoryCache.TryGetValue(cacheKey, out PossibleDuplicateSummaryDto? cached) && cached is not null)
                return cached;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            List<PossibleDuplicatePairDto> pairs;
            bool timedOut = false;

            try
            {
                pairs = await _repo.FindAllPossibleDuplicatesAsync(filter, maxPairs: 50, cancellationToken: cts.Token);
            }
            catch (OperationCanceledException)
            {
                pairs = new List<PossibleDuplicatePairDto>();
                timedOut = true;
            }

            var description = BuildFilterDescription(filter);

            var summary = new PossibleDuplicateSummaryDto
            {
                TotalPairs = pairs.Count,
                Pairs = pairs,
                TimedOut = timedOut,
                FilterDescription = description
            };

            if (!timedOut)
            {
                _memoryCache.Set(cacheKey, summary, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration = TimeSpan.FromMinutes(3)
                });
            }

            // ✅ Overlaid fresh on every call (even on a cache hit) so a pair
            // resolved/unresolved elsewhere shows up immediately, without
            // needing to invalidate the whole scan cache.
            await StampResolutionStatusAsync(summary.Pairs);

            return summary;
        }

        private async Task StampResolutionStatusAsync(List<PossibleDuplicatePairDto> pairs)
        {
            if (pairs.Count == 0) return;

            var keys = pairs.Select(p => (p.Record1Id, p.Record2Id));
            var resolved = await _resolvedDuplicatePairRepo.GetForPairsAsync(keys);
            if (resolved.Count == 0) return;

            foreach (var pair in pairs)
            {
                var lo = pair.Record1Id.CompareTo(pair.Record2Id) <= 0 ? pair.Record1Id : pair.Record2Id;
                var hi = pair.Record1Id.CompareTo(pair.Record2Id) <= 0 ? pair.Record2Id : pair.Record1Id;

                if (resolved.TryGetValue((lo, hi), out var entry))
                {
                    pair.IsResolved = entry.IsResolved;
                    pair.Remarks = entry.Remarks;
                    pair.ResolvedAt = entry.ResolvedAt;
                    pair.ResolvedBy = entry.ResolvedBy;
                }
            }
        }

        public async Task<PossibleDuplicatePairDto> ResolveDuplicatePairAsync(Guid record1Id, Guid record2Id, string? remarks, string resolvedBy)
        {
            var entry = await _resolvedDuplicatePairRepo.ResolveAsync(record1Id, record2Id, remarks, resolvedBy);
            return new PossibleDuplicatePairDto
            {
                Record1Id = record1Id,
                Record2Id = record2Id,
                IsResolved = entry.IsResolved,
                Remarks = entry.Remarks,
                ResolvedAt = entry.ResolvedAt,
                ResolvedBy = entry.ResolvedBy
            };
        }

        public async Task<PossibleDuplicatePairDto> UnresolveDuplicatePairAsync(Guid record1Id, Guid record2Id, string unresolvedBy)
        {
            var entry = await _resolvedDuplicatePairRepo.UnresolveAsync(record1Id, record2Id, unresolvedBy);
            return new PossibleDuplicatePairDto
            {
                Record1Id = record1Id,
                Record2Id = record2Id,
                IsResolved = entry.IsResolved,
                Remarks = entry.Remarks,
                ResolvedAt = entry.ResolvedAt,
                ResolvedBy = entry.ResolvedBy
            };
        }
        private string BuildDuplicateScanCacheKey(BeneficiaryFilterDto f)
        {
            var version = _memoryCache.GetOrCreate(
                CommonConstants.DuplicateScanCacheVersionKey,
                entry =>
                {
                    entry.Priority = CacheItemPriority.NeverRemove;
                    return CommonConstants.V1;
                })!;

            static string N(object? v) => v?.ToString() ?? "null";
            static string ListN<T>(List<T>? list) => list != null && list.Any()
                ? string.Join(",", list.OrderBy(x => x))
                : "null";

            return string.Join("|",
                "dup_scan",
                version,
                // ── Location ──────────────────────────────────────────────────
                N(f.PsgcCodeRegion),
                ListN(f.PsgcCodeProvinces),      // ✅ FIX: Use the list
                ListN(f.PsgcCodeMunicipalities), // ✅ FIX: Use the list
                N(f.PsgcCodeBarangay),
                // ── Name filters ─────────────────────────────────────────────
                N(f.LastName),                   // ✅ ADDED
                N(f.FirstName),                  // ✅ ADDED
                N(f.MiddleName),
                N(f.Suffix),
                N(f.FullName),                   // ✅ ADDED
                                                 // ── Status ────────────────────────────────────────────────────
                ListN(f.PaymentStatuses),        // ✅ FIX: Use the list
                N(f.PaymentDate),
                N(f.PaymentDateFrom),
                N(f.PaymentDateTo),
                N(f.IsEligible),
                N(f.EligibilityMode),
                N(f.IsCompliant),
                N(f.ComplianceMode),
                N(f.CoStatus),
                N(f.ReplacementStatus),
                N(f.FindingStatus),
                N(f.Sex),
                N(f.FilterModeOfPayment),
                N(f.IsLivenessVerified),
                N(f.IsReadyForEft),
                // ── Age / Birthday ────────────────────────────────────────────
                N(f.SpecificAge),
                N(f.MilestoneYear),
                N(f.SpecificBirthday),
                N(f.BirthdayFrom),
                N(f.BirthdayTo),
                // ── Reference number ──────────────────────────────────────────
                N(f.FilterQuarter),
                N(f.FilterFiscalYear),
                N(f.FilterBatch),
                N(f.FilterRefYear),
                N(f.FilterRegionRoman),
                N(f.DateAddedFrom),
                N(f.DateAddedTo),
                N(f.DateEndorsedFrom),
                N(f.DateEndorsedTo),
                // ── Other ─────────────────────────────────────────────────────
                N(f.Validator),
                N(f.BatchCode),
                N(f.GeneralSearch),
                N(f.DataQualityIssue),
                ListN(f.FilterPayrollQuarters)   // ✅ new
            );
        }
        public async Task<CreateBeneficiaryResultDto> CreateAsync(CreateBeneficiaryInformationDto dto, string userName)
        {

            if (dto.DataPrivacyConsent != true)
                throw new Exception("Data Privacy Consent must be given before this record can be saved.");
            if (!dto.IsSignedDeclaration)
                throw new Exception("The declaration must be confirmed as signed before this record can be saved.");
            ValidatePhoneNumbers(dto.PhoneNumbers);

            // Exact (100%) duplicate check — per office policy this no longer
            // hard-blocks Create outright. It still blocks until the user has
            // seen and acknowledged the match (AcknowledgeExactDuplicate),
            // exactly like the fuzzy soft-duplicate flow below — it just no
            // longer blocks PERMANENTLY. See BeneficiaryDuplicateHistory.
            SoftDuplicateCandidateDto? exactDuplicate = null;
            if (!dto.AcknowledgeExactDuplicate)
            {
                exactDuplicate = await _repo.FindExactDuplicateAsync(
                    dto.LastName,
                    dto.FirstName,
                    dto.MiddleName,
                    dto.BirthDate);

                if (exactDuplicate is not null)
                {
                    return new CreateBeneficiaryResultDto
                    {
                        RequiresConfirmation = true,
                        RequiresExactDuplicateConfirmation = true,
                        ExactDuplicate = exactDuplicate
                    };
                }
            }
            else
            {
                // Re-resolve which existing record this is a Known Duplicate
                // of — never trust an id the client might supply, and the
                // client's earlier confirmation payload only proves it SAW a
                // match, not which one (re-checking also protects against
                // that original match having been deleted/edited since).
                exactDuplicate = await _repo.FindExactDuplicateAsync(
                    dto.LastName,
                    dto.FirstName,
                    dto.MiddleName,
                    dto.BirthDate);
            }

            // Soft duplicate check (fuzzy match) — skipped when the user already
            // confirmed it directly, AND when they just confirmed the EXACT
            // (100%) Known Duplicate match above: that confirmation already
            // covers this exact record, so a second "Possible Duplicate" modal
            // for the same person right after would be redundant. A record
            // that ISN'T a 100% exact match still goes through this fuzzy
            // check normally.
            if (!dto.BypassSoftDuplicateCheck && !dto.AcknowledgeExactDuplicate)
            {
                var softMatches = await _repo.FindSoftDuplicatesAsync(
                dto.FirstName,
                dto.LastName,
                dto.BirthDate);

                if (softMatches.Any())
                {
                    return new CreateBeneficiaryResultDto
                    {
                        RequiresConfirmation = true,
                        SoftDuplicates = softMatches
                    };
                }
            }

            var beneficiary = new BeneficiaryInformation
            {
                Id = Guid.NewGuid(),
                Quarter = dto.Quarter,
                Batch = dto.Batch,
                RefYear = dto.RefYear,
                RefCode = (dto.Quarter.HasValue &&
                 !string.IsNullOrWhiteSpace(dto.Batch) &&
                 dto.RefYear.HasValue)
                 ? RegionRomanNumeralHelper.GenerateRefCode()
                 : null,
                DateApplied = dto.DateApplied,
                DateEndorsed = dto.DateEndorsed,
                BatchCode = dto.BatchCode,
                OscaIdNumber = dto.OscaIdNumber,
                OscaIdDateIssued = dto.OscaIdDateIssued,
                NcscRrn = dto.NcscRrn,
                LastName = dto.LastName,
                FirstName = dto.FirstName,
                MiddleName = dto.MiddleName,
                Extension = dto.Extension,
                BirthDate = dto.BirthDate,
                IsIndigenousPeople = dto.IsIndigenousPeople,
                IsPersonWithDisability = dto.IsPersonWithDisability,
                CivilStatus = dto.CivilStatus,
                Citizenship = dto.Citizenship,
                Sex = dto.Sex,
                Region = dto.PsgcCodeRegion,
                Province = dto.PsgcCodeProvince,
                Municipality = dto.PsgcCodeMunicipality,
                Barangay = dto.PsgcCodeBarangay,
                IsCompliant = dto.IsCompliant,
                Validator = dto.Validator,
                ValidationDate = dto.ValidationDate,
                IsDeceased = dto.IsDeceased,
                DateOfDeath = dto.DateOfDeath,
                // ✅ Guard — never trust the client for this: a grantee whose 80th
                // birthday fell before the ECA program's actual start date
                // (March 17, 2024) has no path into the program, regardless of
                // what IsEligible the form computed.
                IsEligible = EcaEligibilityHelper.MissedProgramStartCutoff(dto.BirthDate) ? false : dto.IsEligible,
                AssessmentRemarks = dto.AssessmentRemarks,
                EligibilityRemarks = dto.EligibilityRemarks,
                RemarkCategory = dto.RemarkCategory,
                Remarks = dto.Remarks,
                DateAdded = DateTime.UtcNow,
                IsDeleted = false
                // ✅ NOTE — PayrollQuarter/FiscalYear/PaymentStatus/ModeOfPayment/PaymentDate
                // are intentionally NOT set from dto here anymore. They're now seeded
                // below from a real Payment History entry instead, since the Create form
                // no longer collects payment info at all (that's Payment History's job).
            };

            // ✅ NEW — every new grantee starts with an explicit Pending payment
            // history entry, rather than an ambiguous PaymentStatus = 0 (N/A). This
            // gives the grid, dashboard, and Payment History timeline a real, visible
            // starting state from day one, and matches how every other status change
            // already flows through Payment History rather than flat columns.
            //
            // Exception: a grantee who missed the program's start-date cutoff (see
            // above) can never be paid — they start at N/A instead of Pending.
            var missedCutoff = EcaEligibilityHelper.MissedProgramStartCutoff(dto.BirthDate);
            var initialHistory = new BeneficiaryPaymentHistory
            {
                Id = Guid.NewGuid(),
                BeneficiaryInformationId = beneficiary.Id,
                PayrollQuarter = null,
                FiscalYear = null,
                PaymentStatus = missedCutoff ? 0 : 3, // N/A if ineligible due to cutoff, else Pending
                ModeOfPayment = 0,
                PaymentDate = null,
                Remarks = missedCutoff
                    ? $"Automatically set to N/A — grantee turned 80 before the ECA program started ({EcaEligibilityHelper.ProgramStartDate:MMMM d, yyyy})."
                    : "Automatically set to Pending upon grantee registration.",
                DateCreated = DateTime.UtcNow,
                CreatedBy = userName
            };

            beneficiary.CurrentPaymentHistoryId = initialHistory.Id;
            beneficiary.PayrollQuarter = initialHistory.PayrollQuarter;
            beneficiary.FiscalYear = initialHistory.FiscalYear;
            beneficiary.PaymentStatus = initialHistory.PaymentStatus;
            beneficiary.ModeOfPayment = initialHistory.ModeOfPayment;
            beneficiary.PaymentDate = initialHistory.PaymentDate;
            // ✅ Server is the authoritative source for this mirror — never trust the
            // client's TrackingNumber value, always derive it from NcscRrn directly.
            beneficiary.TrackingNumber = dto.NcscRrn?.ToString();
            beneficiary.DataPrivacyConsent = dto.DataPrivacyConsent;
            beneficiary.PlaceOfSubmission = dto.PlaceOfSubmission;
            beneficiary.HouseNumber = dto.HouseNumber;
            beneficiary.StreetName = dto.StreetName;
            beneficiary.ZipCode = dto.ZipCode;
            beneficiary.DisabilityType = dto.DisabilityType;
            beneficiary.EthnicityName = dto.EthnicityName;
            beneficiary.DualCitizenshipDetails = dto.DualCitizenshipDetails;
            beneficiary.CivilStatusOtherDetail = dto.CivilStatusOtherDetail;
            beneficiary.IsSignedDeclaration = dto.IsSignedDeclaration;
            beneficiary.DateSigned = dto.DateSigned;
            beneficiary.IsLivenessVerified = dto.IsLivenessVerified;
            beneficiary.DateOfLiveness = dto.DateOfLiveness;
            beneficiary.IsReadyForEft = dto.IsReadyForEft;

            await _repo.AddAsync(beneficiary);
            await _repo.AddPaymentHistoryEntryAsync(initialHistory);


            //Logging
            await AddLogAsync(
                beneficiary.Id,
                $"{CommonConstants.CreatedBeneficiary} {beneficiary.LastName}, {beneficiary.FirstName}",
                userName);

            // Known Duplicate — recorded in BeneficiaryDuplicateHistory (never
            // as a flag on the beneficiary itself, so it can't leak into the
            // grid/list/statistics), plus a log entry on each side so both
            // records' own Logs tab mentions it too.
            if (exactDuplicate is not null)
            {
                await _repo.AddDuplicateHistoryAsync(new BeneficiaryDuplicateHistory
                {
                    Id = Guid.NewGuid(),
                    BeneficiaryInformationId = beneficiary.Id,
                    DuplicateOfId = exactDuplicate.ExistingId,
                    Source = "Create",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName
                });

                await AddLogAsync(
                    beneficiary.Id,
                    $"Saved as a Known Duplicate of {exactDuplicate.ExistingFullName} (exact match on name and birth date) — acknowledged by {userName}",
                    userName);

                await AddLogAsync(
                    exactDuplicate.ExistingId,
                    $"Flagged as having a Known Duplicate — a new grantee record for {beneficiary.LastName}, {beneficiary.FirstName} was saved as an exact match, acknowledged by {userName}",
                    userName);
            }

            // ✅ NEW — separate log entry specifically for the auto-pending seed, so
            // it shows up distinctly in the beneficiary's Logs modal, not folded into
            // the generic "Created" entry.
            await AddLogAsync(
                beneficiary.Id,
                "Payment status automatically set to Pending upon registration",
                userName);

            // ✅ NEW — Annex A sub-entities, all committed in the same transaction as the
            // core record and initial payment history below.
            if (dto.FamilyMembers?.Any() == true)
            {
                var familyEntities = dto.FamilyMembers.Select(f => new BeneficiaryFamilyMember
                {
                    RelationType = f.RelationType,
                    LastName = f.LastName,
                    FirstName = f.FirstName,
                    MiddleName = f.MiddleName,
                    Extension = f.Extension,
                    ContactNumber = f.ContactNumber,
                    Sex = f.Sex,
                    Age = f.Age,
                    IsLivingWithGrantee = f.IsLivingWithGrantee,
                    SortOrder = f.SortOrder
                }).ToList();

                await _repo.ReplaceFamilyMembersAsync(beneficiary.Id, familyEntities);
            }

            // ✅ NEW — replaces the old single PhoneNumber scalar; already validated
            // (required + PH format) above via ValidatePhoneNumbers.
            {
                var phoneEntities = dto.PhoneNumbers
                    .Where(n => !string.IsNullOrWhiteSpace(n.Number))
                    .Select((n, i) => new BeneficiaryPhoneNumber { Number = n.Number.Trim(), SortOrder = i })
                    .ToList();
                await _repo.ReplacePhoneNumbersAsync(beneficiary.Id, phoneEntities);
            }

            if (dto.BankAccount != null)
            {
                await _repo.UpsertBankAccountAsync(beneficiary.Id, new BeneficiaryBankAccount
                {
                    PreferredChannel = dto.BankAccount.PreferredChannel,
                    AccountNumber = dto.BankAccount.AccountNumber,
                    MobileNumber = dto.BankAccount.MobileNumber,
                    BankOrWalletName = dto.BankAccount.BankOrWalletName,
                    GCashName = dto.BankAccount.GCashName,
                    BranchName = dto.BankAccount.BranchName,
                    BankAddress = dto.BankAccount.BankAddress,
                    IsJointAccount = dto.BankAccount.IsJointAccount,
                    SwiftCode = dto.BankAccount.SwiftCode,
                    Iban = dto.BankAccount.Iban,
                    DateModified = DateTime.UtcNow,
                    ModifiedBy = userName
                });
            }

            // ✅ Abroad address only persisted when PlaceOfSubmission is actually Abroad —
            // guards against a stray abroad block being saved if the frontend somehow
            // submits one alongside PlaceOfSubmission = Local.
            if (dto.PlaceOfSubmission == 2 && dto.AbroadAddress != null)
            {
                await _repo.UpsertAbroadAddressAsync(beneficiary.Id, new BeneficiaryAbroadAddress
                {
                    HouseNumber = dto.AbroadAddress.HouseNumber,
                    StreetName = dto.AbroadAddress.StreetName,
                    City = dto.AbroadAddress.City,
                    State = dto.AbroadAddress.State,
                    Country = dto.AbroadAddress.Country,
                    ZipCode = dto.AbroadAddress.ZipCode
                });
            }

            // ✅ Claimant only persisted when actually deceased — same guard reasoning.
            if (dto.IsDeceased && dto.Claimant != null)
            {
                await _repo.UpsertClaimantAsync(beneficiary.Id, new BeneficiaryClaimant
                {
                    LastName = dto.Claimant.LastName,
                    FirstName = dto.Claimant.FirstName,
                    MiddleName = dto.Claimant.MiddleName,
                    Extension = dto.Claimant.Extension,
                    ContactNumber = dto.Claimant.ContactNumber,
                    RelationshipToDeceased = dto.Claimant.RelationshipToDeceased,
                    HouseNumber = dto.Claimant.HouseNumber,
                    StreetName = dto.Claimant.StreetName,
                    Barangay = dto.Claimant.Barangay,
                    CityMunicipality = dto.Claimant.CityMunicipality,
                    Province = dto.Claimant.Province,
                    ZipCode = dto.Claimant.ZipCode
                });
            }
            // ✅ NEW — claimant's own bank account, independent from the grantee's
            if (dto.IsDeceased && dto.ClaimantBankAccount != null)
            {
                await _repo.UpsertClaimantBankAccountAsync(beneficiary.Id, new BeneficiaryClaimantBankAccount
                {
                    PreferredChannel = dto.ClaimantBankAccount.PreferredChannel,
                    AccountNumber = dto.ClaimantBankAccount.AccountNumber,
                    MobileNumber = dto.ClaimantBankAccount.MobileNumber,
                    BankOrWalletName = dto.ClaimantBankAccount.BankOrWalletName,
                    GCashName = dto.ClaimantBankAccount.GCashName,
                    BranchName = dto.ClaimantBankAccount.BranchName,
                    BankAddress = dto.ClaimantBankAccount.BankAddress,
                    IsJointAccount = dto.ClaimantBankAccount.IsJointAccount,
                    SwiftCode = dto.ClaimantBankAccount.SwiftCode,
                    Iban = dto.ClaimantBankAccount.Iban,
                    DateModified = DateTime.UtcNow,
                    ModifiedBy = userName
                });
            }
            // ✅ NEW — checklist rides the same transaction as everything else. Admin
            // gating for WHO can edit these fields is enforced client-side (only admins
            // see the inputs); the server still accepts whatever arrives on the DTO,
            // consistent with how CO Status/Findings are already gated only in the UI.
            if (dto.VerificationChecklist != null)
            {
                await _checklistRepo.UpsertAsync(beneficiary.Id, new BeneficiaryVerificationChecklist
                {
                    HasAnnexAForm = dto.VerificationChecklist.HasAnnexAForm,
                    AnnexARemarks = dto.VerificationChecklist.AnnexARemarks,
                    HasPrimaryIdLocal = dto.VerificationChecklist.HasPrimaryIdLocal,
                    PrimaryIdLocalRemarks = dto.VerificationChecklist.PrimaryIdLocalRemarks,
                    HasPrimaryIdAbroad = dto.VerificationChecklist.HasPrimaryIdAbroad,
                    PrimaryIdAbroadRemarks = dto.VerificationChecklist.PrimaryIdAbroadRemarks,
                    HasSecondaryIds = dto.VerificationChecklist.HasSecondaryIds,
                    SecondaryIdsRemarks = dto.VerificationChecklist.SecondaryIdsRemarks,
                    HasPhoto = dto.VerificationChecklist.HasPhoto,
                    PhotoRemarks = dto.VerificationChecklist.PhotoRemarks,
                    HasBankDepositSlip = dto.VerificationChecklist.HasBankDepositSlip,
                    BankDepositSlipRemarks = dto.VerificationChecklist.BankDepositSlipRemarks,
                    HasDeathCertificate = dto.VerificationChecklist.HasDeathCertificate,
                    DeathCertificateRemarks = dto.VerificationChecklist.DeathCertificateRemarks,
                    HasProofOfRelationship = dto.VerificationChecklist.HasProofOfRelationship,
                    ProofOfRelationshipRemarks = dto.VerificationChecklist.ProofOfRelationshipRemarks,
                    HasClaimantBankSlip = dto.VerificationChecklist.HasClaimantBankSlip,
                    ClaimantBankSlipRemarks = dto.VerificationChecklist.ClaimantBankSlipRemarks,
                    HasWarrantyReleaseForm = dto.VerificationChecklist.HasWarrantyReleaseForm,
                    WarrantyReleaseFormRemarks = dto.VerificationChecklist.WarrantyReleaseFormRemarks,
                    HasLguRcfCertification = dto.VerificationChecklist.HasLguRcfCertification,
                    LguRcfCertificationRemarks = dto.VerificationChecklist.LguRcfCertificationRemarks,
                    VerifierOffice = dto.VerificationChecklist.VerifierOffice
                });
            }

            await _repo.SaveChangesAsync(); // single commit — beneficiary + payment history + all sub-entities

            InvalidateSummaryCache();

            return new CreateBeneficiaryResultDto
            {
                RequiresConfirmation = false,
                CreatedBeneficiary = new BeneficiaryInformationDto
                {
                    Id = beneficiary.Id,
                    Quarter = beneficiary.Quarter,
                    Batch = beneficiary.Batch,
                    RefYear = beneficiary.RefYear,
                    RefCode = beneficiary.RefCode,
                    DateApplied = beneficiary.DateApplied,
                    DateEndorsed = beneficiary.DateEndorsed,
                    BatchCode = beneficiary.BatchCode,
                    OscaIdNumber = beneficiary.OscaIdNumber,
                    OscaIdDateIssued = beneficiary.OscaIdDateIssued,
                    NcscRrn = beneficiary.NcscRrn,
                    LastName = beneficiary.LastName,
                    FirstName = beneficiary.FirstName,
                    MiddleName = beneficiary.MiddleName,
                    Extension = beneficiary.Extension,
                    BirthDate = beneficiary.BirthDate,
                    Sex = beneficiary.Sex,
                    IsIndigenousPeople = beneficiary.IsIndigenousPeople,
                    IsPersonWithDisability = beneficiary.IsPersonWithDisability,
                    CivilStatus = beneficiary.CivilStatus,
                    Citizenship = beneficiary.Citizenship,
                    PsgcCodeRegion = beneficiary.Region,
                    PsgcCodeProvince = beneficiary.Province,
                    PsgcCodeMunicipality = beneficiary.Municipality,
                    PsgcCodeBarangay = beneficiary.Barangay,
                    IsCompliant = beneficiary.IsCompliant,
                    Validator = beneficiary.Validator,
                    ValidationDate = beneficiary.ValidationDate,
                    PayrollQuarter = beneficiary.PayrollQuarter,
                    FiscalYear = beneficiary.FiscalYear,
                    PaymentStatus = beneficiary.PaymentStatus,
                    ModeOfPayment = beneficiary.ModeOfPayment,
                    PaymentDate = beneficiary.PaymentDate,
                    IsDeceased = beneficiary.IsDeceased,
                    DateOfDeath = beneficiary.DateOfDeath,
                    IsEligible = beneficiary.IsEligible,
                    AssessmentRemarks = beneficiary.AssessmentRemarks,
                    EligibilityRemarks = beneficiary.EligibilityRemarks,
                    RemarkCategory = beneficiary.RemarkCategory,
                    Remarks = beneficiary.Remarks,
                    DateAdded = beneficiary.DateAdded,
                    IsDeleted = beneficiary.IsDeleted,

                    // ✅ NEW — Annex A flat fields, previously missing from this return DTO
                    TrackingNumber = beneficiary.TrackingNumber,
                    DataPrivacyConsent = beneficiary.DataPrivacyConsent,
                    PlaceOfSubmission = beneficiary.PlaceOfSubmission,
                    HouseNumber = beneficiary.HouseNumber,
                    StreetName = beneficiary.StreetName,
                    ZipCode = beneficiary.ZipCode,
                    DisabilityType = beneficiary.DisabilityType,
                    EthnicityName = beneficiary.EthnicityName,
                    DualCitizenshipDetails = beneficiary.DualCitizenshipDetails,
                    CivilStatusOtherDetail = beneficiary.CivilStatusOtherDetail,
                    IsSignedDeclaration = beneficiary.IsSignedDeclaration,
                    DateSigned = beneficiary.DateSigned,

                    // ✅ NEW — sub-entities, mapped from the same dto that was just persisted.
                    // Reusing dto.* here instead of re-querying the repo — the values are
                    // identical to what was just saved, and this avoids extra round-trips
                    // right after SaveChangesAsync for a response object whose contents
                    // aren't currently consumed by the Razor form's success path, but should
                    // still be complete and correct for any future caller that does read it.
                    FamilyMembers = dto.FamilyMembers ?? new List<BeneficiaryFamilyMemberDto>(),
                    PhoneNumbers = dto.PhoneNumbers,
                    BankAccount = dto.BankAccount,
                    AbroadAddress = dto.PlaceOfSubmission == 2 ? dto.AbroadAddress : null,
                    Claimant = dto.IsDeceased ? dto.Claimant : null,
                    ClaimantBankAccount = dto.IsDeceased ? dto.ClaimantBankAccount : null,  // ✅ NEW
                    VerificationChecklist = dto.VerificationChecklist
                }
            };
        }

        public async Task<IEnumerable<BeneficiaryInformationDto>> FilterAsync(BeneficiaryFilterDto filter)
        {
            return await _repo.FilterAsync(filter);
        }

        public async Task<IEnumerable<BeneficiaryInformationDto>> GetAllAsync()
        {
            var list = await _repo.GetAllAsync();
            return list;
        }

        public async Task<BeneficiaryInformationDto?> GetByIdAsync(Guid id)
        {
            var getById = await _repo.GetByIdAsync(id);
            if (getById == null) return null;

            var familyMembers = await _repo.GetFamilyMembersAsync(id);
            getById.FamilyMembers = familyMembers.Select(f => new BeneficiaryFamilyMemberDto
            {
                Id = f.Id,
                RelationType = f.RelationType,
                LastName = f.LastName,
                FirstName = f.FirstName,
                MiddleName = f.MiddleName,
                Extension = f.Extension,
                ContactNumber = f.ContactNumber,
                Sex = f.Sex,
                Age = f.Age,
                IsLivingWithGrantee = f.IsLivingWithGrantee,
                SortOrder = f.SortOrder
            }).ToList();

            var phoneNumbers = await _repo.GetPhoneNumbersAsync(id);
            getById.PhoneNumbers = phoneNumbers.Select(n => new BeneficiaryPhoneNumberDto
            {
                Id = n.Id,
                Number = n.Number,
                SortOrder = n.SortOrder
            }).ToList();

            var bankAccount = await _repo.GetBankAccountAsync(id);
            if (bankAccount != null)
                getById.BankAccount = new BeneficiaryBankAccountDto
                {
                    Id = bankAccount.Id,
                    PreferredChannel = bankAccount.PreferredChannel,
                    AccountNumber = bankAccount.AccountNumber,
                    MobileNumber = bankAccount.MobileNumber,
                    BankOrWalletName = bankAccount.BankOrWalletName,
                    GCashName = bankAccount.GCashName,
                    BranchName = bankAccount.BranchName,
                    BankAddress = bankAccount.BankAddress,
                    IsJointAccount = bankAccount.IsJointAccount,
                    SwiftCode = bankAccount.SwiftCode,
                    Iban = bankAccount.Iban,
                    DateModified = bankAccount.DateModified,
                    ModifiedBy = bankAccount.ModifiedBy
                };
            var checklist = await _checklistRepo.GetByBeneficiaryIdAsync(id); // inject IBeneficiaryVerificationChecklistRepository
            if (checklist != null)
                getById.VerificationChecklist = new BeneficiaryVerificationChecklistDto
                {
                    HasAnnexAForm = checklist.HasAnnexAForm,
                    AnnexARemarks = checklist.AnnexARemarks,
                    HasPrimaryIdLocal = checklist.HasPrimaryIdLocal,
                    PrimaryIdLocalRemarks = checklist.PrimaryIdLocalRemarks,
                    HasPrimaryIdAbroad = checklist.HasPrimaryIdAbroad,
                    PrimaryIdAbroadRemarks = checklist.PrimaryIdAbroadRemarks,
                    HasSecondaryIds = checklist.HasSecondaryIds,
                    SecondaryIdsRemarks = checklist.SecondaryIdsRemarks,
                    HasPhoto = checklist.HasPhoto,
                    PhotoRemarks = checklist.PhotoRemarks,
                    HasBankDepositSlip = checklist.HasBankDepositSlip,
                    BankDepositSlipRemarks = checklist.BankDepositSlipRemarks,
                    HasDeathCertificate = checklist.HasDeathCertificate,
                    DeathCertificateRemarks = checklist.DeathCertificateRemarks,
                    HasProofOfRelationship = checklist.HasProofOfRelationship,
                    ProofOfRelationshipRemarks = checklist.ProofOfRelationshipRemarks,
                    HasClaimantBankSlip = checklist.HasClaimantBankSlip,
                    ClaimantBankSlipRemarks = checklist.ClaimantBankSlipRemarks,
                    HasWarrantyReleaseForm = checklist.HasWarrantyReleaseForm,
                    WarrantyReleaseFormRemarks = checklist.WarrantyReleaseFormRemarks,
                    HasLguRcfCertification = checklist.HasLguRcfCertification,
                    LguRcfCertificationRemarks = checklist.LguRcfCertificationRemarks,
                    VerifierOffice = checklist.VerifierOffice
                };

            var abroadAddress = await _repo.GetAbroadAddressAsync(id);
            if (abroadAddress != null)
                getById.AbroadAddress = new BeneficiaryAbroadAddressDto
                {
                    Id = abroadAddress.Id,
                    HouseNumber = abroadAddress.HouseNumber,
                    StreetName = abroadAddress.StreetName,
                    City = abroadAddress.City,
                    State = abroadAddress.State,
                    Country = abroadAddress.Country,
                    ZipCode = abroadAddress.ZipCode
                };

            if (getById.IsDeceased)
            {
                var claimant = await _repo.GetClaimantAsync(id);
                if (claimant != null)
                    getById.Claimant = new BeneficiaryClaimantDto
                    {
                        Id = claimant.Id,
                        LastName = claimant.LastName,
                        FirstName = claimant.FirstName,
                        MiddleName = claimant.MiddleName,
                        Extension = claimant.Extension,
                        ContactNumber = claimant.ContactNumber,
                        RelationshipToDeceased = claimant.RelationshipToDeceased,
                        HouseNumber = claimant.HouseNumber,
                        StreetName = claimant.StreetName,
                        Barangay = claimant.Barangay,
                        CityMunicipality = claimant.CityMunicipality,
                        Province = claimant.Province,
                        ZipCode = claimant.ZipCode
                    };
            }
            // ✅ NEW
            var claimantBankAccount = await _repo.GetClaimantBankAccountAsync(id);
            if (claimantBankAccount != null)
                getById.ClaimantBankAccount = new BeneficiaryClaimantBankAccountDto
                {
                    Id = claimantBankAccount.Id,
                    PreferredChannel = claimantBankAccount.PreferredChannel,
                    AccountNumber = claimantBankAccount.AccountNumber,
                    MobileNumber = claimantBankAccount.MobileNumber,
                    BankOrWalletName = claimantBankAccount.BankOrWalletName,
                    GCashName = claimantBankAccount.GCashName,
                    BranchName = claimantBankAccount.BranchName,
                    BankAddress = claimantBankAccount.BankAddress,
                    IsJointAccount = claimantBankAccount.IsJointAccount,
                    SwiftCode = claimantBankAccount.SwiftCode,
                    Iban = claimantBankAccount.Iban,
                    DateModified = claimantBankAccount.DateModified,
                    ModifiedBy = claimantBankAccount.ModifiedBy
                };

            return getById;
        }

        public async Task<List<BeneficiaryInformationDto>> GetByIdsAsync(List<Guid> ids)
            => await _repo.GetByIdsAsync(ids);


        public async Task<BeneficiarySummaryResultDto> GetSummaryAsync(BeneficiaryFilterDto filter)
        {
            var cacheKey = BuildSummaryCacheKey(filter);
            if (_memoryCache.TryGetValue(cacheKey, out BeneficiarySummaryResultDto? cachedSummary)
                && cachedSummary is not null)
            {
                return cachedSummary;
            }

            var summary = await _repo.GetSummaryAsync(filter);

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(5)
            };
            _memoryCache.Set(cacheKey, summary, cacheOptions);

            return summary;
        }
        public async Task SoftDeleteAsync(Guid Id, string userName)
        {
            var selectedBeneficiary = await _repo.GetEntityByIdAsync(Id);
            if (selectedBeneficiary == null)
                throw new Exception(CommonConstants.GranteeNotFound);

            selectedBeneficiary.IsDeleted = true;

            await _repo.UpdateAsync(selectedBeneficiary);

            //Logging
            await AddLogAsync(
             selectedBeneficiary.Id,
             CommonConstants.LogSoftDelete,
             userName);


            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }
        // Service
        public async Task BulkAssignRefNumberAsync(
     List<Guid> ids, int quarter, string batch, int refYear, string userName)
        {
            if (ids == null || !ids.Any())
                throw new Exception("No records to assign.");

            var beneficiaries = await _repo.GetEntitiesByIdsAsync(ids);

            foreach (var b in beneficiaries)
            {
                b.Quarter = quarter;
                b.Batch = batch.Trim();
                b.RefYear = refYear;

                // ✅ Only generate a new RefCode if one doesn't already exist
                // — preserves the unique code on re-assignment (e.g. quarter change)
                if (string.IsNullOrWhiteSpace(b.RefCode))
                    b.RefCode = RegionRomanNumeralHelper.GenerateRefCode();
            }

            await _repo.SaveChangesAsync();

            foreach (var id in ids)
                await AddLogAsync(
                    id,
                    $"Reference number assigned: Q{quarter}B{batch}-{refYear:D2}",
                    userName);

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }

        public async Task BulkUpdateEligibilityAndBatchCodeAsync(List<Guid> ids, bool? isEligible, string? batchCode,
            string userName, Dictionary<Guid, byte[]>? rowVersions = null)  // ✅ added
        {
            if (ids == null || !ids.Any())
                throw new Exception(CommonConstants.NoRecordsSelected);

            if (!isEligible.HasValue && batchCode == null)
                throw new Exception("Nothing to update. Select at least one field to change.");

            await _repo.BulkUpdateEligibilityAndBatchCodeAsync(
                ids, isEligible, batchCode, rowVersions);  // ✅

            var parts = new List<string>();
            if (isEligible.HasValue)
                parts.Add($"Eligibility -> {(isEligible.Value ? "Eligible" : "Ineligible")}");
            if (batchCode != null)
                parts.Add($"Batch Code -> '{batchCode}'");

            foreach (var id in ids)
                await AddLogAsync(id, $"Bulk update: {string.Join(", ", parts)}", userName);

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }

        // PDO-confirmed auto-eligibility flip: turns 80 + Filipino -> Eligible.
        // Never trusts the client-submitted id list — re-checks the flip
        // criteria AND the caller's jurisdiction (for PDO) against fresh DB
        // data before touching anything, so a stale/tampered request can't
        // flip records outside what the caller is actually allowed to see.
        //
        // Only Admin/SuperAdmin/PDO/Encoder ever reach this method — the
        // controller action is gated by the "GranteeEncodeAccess" policy.
        // Encoder is unrestricted here, same as Admin/SuperAdmin — it's an
        // office-wide encoding role with no per-municipality jurisdiction
        // rows (see JurisdictionGuardService.CheckAsync, which treats it
        // identically). Only PDO is jurisdiction-restricted.
        public async Task<ConfirmAutoEligibilityResultDto> ConfirmAutoEligibilityAsync(List<Guid> ids, string userName, string role)
        {
            var result = new ConfirmAutoEligibilityResultDto();

            if (ids == null || !ids.Any())
                return result;

            var candidates = await _repo.GetByIdsAsync(ids);

            HashSet<int>? allowedMunicipalities = null;
            if (role == "PDO")
                allowedMunicipalities = (await _jurisdictionGuardService.GetAllowedMunicipalityCodesAsync(userName)).ToHashSet();
            else if (role != "Admin" && role != "SuperAdmin" && role != "Encoder")
                return result; // shouldn't happen given the controller policy, but fail closed regardless

            var toUpdate = new List<Guid>();

            foreach (var c in candidates)
            {
                // Must have ACTUALLY reached age 80 already (not merely "80
                // falls this year" — a grantee born in October is still 79
                // for most of the year). Mirrors
                // BeneficiaryListItemDto.PendingAutoEligibility exactly.
                var isValidCandidate = !c.IsEligible && c.Citizenship == 1 &&
                    EcaEligibilityHelper.ComputeAge(c.BirthDate) == 80 &&
                    !EcaEligibilityHelper.MissedProgramStartCutoff(c.BirthDate);

                var inJurisdiction = allowedMunicipalities is null || allowedMunicipalities.Contains(c.PsgcCodeMunicipality);

                if (isValidCandidate && inJurisdiction)
                    toUpdate.Add(c.Id);
                else if (!inJurisdiction)
                    result.SkippedOutsideJurisdictionIds.Add(c.Id);
                else
                    result.SkippedNoLongerValidIds.Add(c.Id);
            }

            if (toUpdate.Any())
            {
                await _repo.BulkUpdateEligibilityAndBatchCodeAsync(toUpdate, true, null, null);

                foreach (var id in toUpdate)
                    await AddLogAsync(id, $"Automatically marked Eligible — turned 80 and Filipino citizenship, confirmed by {userName}", userName);

                await _repo.SaveChangesAsync();
                InvalidateSummaryCache();
            }

            result.UpdatedIds = toUpdate;
            return result;
        }

        public async Task<List<BeneficiaryDuplicateHistoryDto>> GetDuplicateHistoryAsync(Guid beneficiaryId)
            => await _repo.GetDuplicateHistoryAsync(beneficiaryId);

        public async Task BulkUpdateCoStatusAsync(List<Guid> ids, int? coStatus, DateTime? coDateEndorsed, DateTime? coDateApproved,
        string userName, Dictionary<Guid, byte[]>? rowVersions = null)  // ✅ added
        {
            if (ids == null || !ids.Any())
                throw new Exception(CommonConstants.NoRecordsSelected);

            if (!coStatus.HasValue)
                throw new Exception("Co Status is required");

            if (coStatus == 1 && !coDateEndorsed.HasValue)
                throw new Exception("CO Date Endorsed is required when status is Endorsed.");

            if (coStatus == 2 && !coDateApproved.HasValue)
                throw new Exception("CO Date Approved is required when status is Approved");

            await _repo.BulkUpdateCoStatusAsync(
                ids, coStatus, coDateEndorsed, coDateApproved, rowVersions);  // ✅

            var statusLabel = CoStatusLabel(coStatus.Value);

            foreach (var id in ids)
                await AddLogAsync(id, $"Bulk CO Status update -> {statusLabel}", userName);

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }

        public async Task ReplaceBeneficiaryAsync(Guid outgoingHistoryId, Guid incomingHistoryId, DateTime? replacementDate, string? remarks, string userName)
        {
            if (outgoingHistoryId == Guid.Empty || incomingHistoryId == Guid.Empty)
                throw new Exception("Both the outgoing and replacement payment records are required.");

            if (outgoingHistoryId == incomingHistoryId)
                throw new Exception("A payment record cannot replace itself.");

            var (outgoing, incoming) = await _repo.ReplaceBeneficiaryAsync(outgoingHistoryId, incomingHistoryId, replacementDate, remarks);

            await AddLogAsync(outgoing.BeneficiaryInformationId,
                $"Payment record ({PeriodLabel(outgoing.PayrollQuarter, outgoing.FiscalYear)}) replacement status → Replaced (slot handed over)", userName);
            await AddLogAsync(incoming.BeneficiaryInformationId,
                $"Payment record ({PeriodLabel(incoming.PayrollQuarter, incoming.FiscalYear)}) replacement status → Is Replacement (took over a slot)", userName);

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }

        public async Task UndoReplacementAsync(Guid historyId, string userName)
        {
            if (historyId == Guid.Empty)
                throw new Exception(CommonConstants.NoRecordsSelected);

            var entry = await _repo.UndoReplacementAsync(historyId);

            await AddLogAsync(entry.BeneficiaryInformationId,
                $"Payment record ({PeriodLabel(entry.PayrollQuarter, entry.FiscalYear)}) replacement status cleared", userName);

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }

        private static string PeriodLabel(int? payrollQuarter, int? fiscalYear) =>
            $"Q{payrollQuarter?.ToString() ?? "-"} {fiscalYear?.ToString() ?? ""}".Trim();

        public async Task<List<BeneficiaryLookupDto>> SearchBeneficiaryLookupAsync(string? search, Guid excludeId)
            => await _repo.SearchBeneficiaryLookupAsync(search, excludeId);

        public async Task UpdateAsync(Guid Id, BeneficiaryInformationDto dto, string userName)
        {
            var beneficiary = await _repo.GetEntityByIdAsync(Id);
            if (beneficiary == null)
                throw new Exception(CommonConstants.GranteeNotFound);

            // ✅ Clean — no EF Core in Application layer
            if (dto.RowVersion != null)
                _repo.SetOriginalRowVersion(beneficiary, dto.RowVersion);

            if (dto.DataPrivacyConsent != true)
                throw new Exception("Data Privacy Consent must be given before this record can be saved.");
            if (!dto.IsSignedDeclaration)
                throw new Exception("The declaration must be confirmed as signed before this record can be saved.");
            // ✅ NEW — mandatory going forward. Old imported records may still have
            // NcscRrn = null (that's fine, untouched by this check), but every NEW
            // record created through this form must have it.
            if (dto.NcscRrn == null)
                throw new Exception("NCSC Registration Reference Number is required.");
            ValidatePhoneNumbers(dto.PhoneNumbers);


            var isDuplicate = await _repo.ExistsDuplicateAsync(
                dto.LastName, dto.FirstName, dto.MiddleName, dto.BirthDate, Id);

            if (isDuplicate)
                throw new Exception(CommonConstants.DuplicateFound);

            var changes = await GetChangedFields(beneficiary, dto);

            var refCodeToSave = beneficiary.RefCode;
            if (string.IsNullOrWhiteSpace(refCodeToSave) &&
                dto.Quarter.HasValue &&
                !string.IsNullOrWhiteSpace(dto.Batch) &&
                dto.RefYear.HasValue)
            {
                refCodeToSave = RegionRomanNumeralHelper.GenerateRefCode();
            }

            // ✅ Same guard as CreateAsync — never trust the client here either.
            // A grantee whose 80th birthday fell before the ECA program's actual
            // start date (March 17, 2024) is permanently ineligible and stays at
            // Payment Status N/A, regardless of what the form submitted (e.g. the
            // birthdate was just corrected on this edit to reveal the cutoff miss).
            var missedCutoffOnEdit = EcaEligibilityHelper.MissedProgramStartCutoff(dto.BirthDate);
            var isEligibleToSave = missedCutoffOnEdit ? false : dto.IsEligible;
            var paymentStatusToSave = missedCutoffOnEdit ? 0 : dto.PaymentStatus;

            beneficiary.Update(
                     dto.Quarter, dto.Batch, dto.RefYear, refCodeToSave,
                     dto.DateApplied, dto.DateEndorsed, dto.BatchCode,
                     dto.OscaIdNumber, dto.OscaIdDateIssued, dto.NcscRrn,
                     dto.LastName, dto.FirstName, dto.MiddleName,
                     dto.Extension, dto.BirthDate,
                     dto.Sex, dto.IsIndigenousPeople, dto.IsPersonWithDisability,
                     dto.CivilStatus, dto.Citizenship, dto.PsgcCodeRegion,
                     dto.PsgcCodeProvince, dto.PsgcCodeMunicipality, dto.PsgcCodeBarangay,
                     dto.IsCompliant, dto.Validator, dto.ValidationDate,
                     dto.PayrollQuarter, dto.FiscalYear, paymentStatusToSave, dto.ModeOfPayment, dto.PaymentDate,  // ✅ FiscalYear inserted
                     dto.IsDeceased, dto.DateOfDeath, isEligibleToSave,
                     dto.AssessmentRemarks, dto.EligibilityRemarks, dto.RemarkCategory, dto.Remarks,
                     dto.CoStatus, dto.CoDateEndorsed, dto.CoDateApproved);

            beneficiary.UpdateAnnexADetails(
                     dto.NcscRrn?.ToString(), dto.DataPrivacyConsent, dto.PlaceOfSubmission,
                     dto.HouseNumber, dto.StreetName, dto.ZipCode,
                     dto.DisabilityType, dto.EthnicityName, dto.DualCitizenshipDetails,
                     dto.CivilStatusOtherDetail, dto.IsSignedDeclaration, dto.DateSigned, dto.IsLivenessVerified, dto.DateOfLiveness,
                     dto.IsReadyForEft);

            // ✅ Family members — always replace-all on edit, matches create behavior
            if (dto.FamilyMembers != null)
            {
                var familyEntities = dto.FamilyMembers.Select(f => new BeneficiaryFamilyMember
                {
                    RelationType = f.RelationType,
                    LastName = f.LastName,
                    FirstName = f.FirstName,
                    MiddleName = f.MiddleName,
                    Extension = f.Extension,
                    ContactNumber = f.ContactNumber,
                    Sex = f.Sex,
                    Age = f.Age,
                    IsLivingWithGrantee = f.IsLivingWithGrantee,
                    SortOrder = f.SortOrder
                }).ToList();

                await _repo.ReplaceFamilyMembersAsync(beneficiary.Id, familyEntities);
            }

            // ✅ Phone numbers — always replace-all on edit, matches create behavior.
            // Already validated (required + PH format) above via ValidatePhoneNumbers.
            {
                var phoneEntities = dto.PhoneNumbers
                    .Where(n => !string.IsNullOrWhiteSpace(n.Number))
                    .Select((n, i) => new BeneficiaryPhoneNumber { Number = n.Number.Trim(), SortOrder = i })
                    .ToList();
                await _repo.ReplacePhoneNumbersAsync(beneficiary.Id, phoneEntities);
            }

            if (dto.BankAccount != null)
            {
                await _repo.UpsertBankAccountAsync(Id, new BeneficiaryBankAccount
                {
                    PreferredChannel = dto.BankAccount.PreferredChannel,
                    AccountNumber = dto.BankAccount.AccountNumber,
                    MobileNumber = dto.BankAccount.MobileNumber,
                    BankOrWalletName = dto.BankAccount.BankOrWalletName,
                    GCashName = dto.BankAccount.GCashName,
                    BranchName = dto.BankAccount.BranchName,
                    BankAddress = dto.BankAccount.BankAddress,
                    IsJointAccount = dto.BankAccount.IsJointAccount,
                    SwiftCode = dto.BankAccount.SwiftCode,
                    Iban = dto.BankAccount.Iban,
                    DateModified = DateTime.UtcNow,
                    ModifiedBy = userName
                });
            }

            // ✅ Handles the flip in both directions — Abroad→Local cleans up the stale
            // address instead of leaving an orphaned record nobody sees again.
            if (dto.PlaceOfSubmission == 2 && dto.AbroadAddress != null)
            {
                await _repo.UpsertAbroadAddressAsync(Id, new BeneficiaryAbroadAddress
                {
                    HouseNumber = dto.AbroadAddress.HouseNumber,
                    StreetName = dto.AbroadAddress.StreetName,
                    City = dto.AbroadAddress.City,
                    State = dto.AbroadAddress.State,
                    Country = dto.AbroadAddress.Country,
                    ZipCode = dto.AbroadAddress.ZipCode
                });
            }
            else if (dto.PlaceOfSubmission != 2)
            {
                await _repo.DeleteAbroadAddressAsync(Id);
            }

            // ✅ Same flip-cleanup logic for Deceased→Alive
            if (dto.IsDeceased && dto.Claimant != null)
            {
                await _repo.UpsertClaimantAsync(Id, new BeneficiaryClaimant
                {
                    LastName = dto.Claimant.LastName,
                    FirstName = dto.Claimant.FirstName,
                    MiddleName = dto.Claimant.MiddleName,
                    Extension = dto.Claimant.Extension,
                    ContactNumber = dto.Claimant.ContactNumber,
                    RelationshipToDeceased = dto.Claimant.RelationshipToDeceased,
                    HouseNumber = dto.Claimant.HouseNumber,
                    StreetName = dto.Claimant.StreetName,
                    Barangay = dto.Claimant.Barangay,
                    CityMunicipality = dto.Claimant.CityMunicipality,
                    Province = dto.Claimant.Province,
                    ZipCode = dto.Claimant.ZipCode
                });
            }
            else if (!dto.IsDeceased)
            {
                await _repo.DeleteClaimantAsync(Id);
            }
            // ✅ NEW — same flip-cleanup pattern for the claimant's own bank account
            if (dto.IsDeceased && dto.ClaimantBankAccount != null)
            {
                await _repo.UpsertClaimantBankAccountAsync(Id, new BeneficiaryClaimantBankAccount
                {
                    PreferredChannel = dto.ClaimantBankAccount.PreferredChannel,
                    AccountNumber = dto.ClaimantBankAccount.AccountNumber,
                    MobileNumber = dto.ClaimantBankAccount.MobileNumber,
                    BankOrWalletName = dto.ClaimantBankAccount.BankOrWalletName,
                    GCashName = dto.ClaimantBankAccount.GCashName,
                    BranchName = dto.ClaimantBankAccount.BranchName,
                    BankAddress = dto.ClaimantBankAccount.BankAddress,
                    IsJointAccount = dto.ClaimantBankAccount.IsJointAccount,
                    SwiftCode = dto.ClaimantBankAccount.SwiftCode,
                    Iban = dto.ClaimantBankAccount.Iban,
                    DateModified = DateTime.UtcNow,
                    ModifiedBy = userName
                });
            }
            else if (!dto.IsDeceased)
            {
                await _repo.DeleteClaimantBankAccountAsync(Id);
            }
            // ✅ NEW — checklist rides the same transaction as everything else. Admin
            // gating for WHO can edit these fields is enforced client-side (only admins
            // see the inputs); the server still accepts whatever arrives on the DTO,
            // consistent with how CO Status/Findings are already gated only in the UI.
            if (dto.VerificationChecklist != null)
            {
                await _checklistRepo.UpsertAsync(beneficiary.Id, new BeneficiaryVerificationChecklist
                {
                    HasAnnexAForm = dto.VerificationChecklist.HasAnnexAForm,
                    AnnexARemarks = dto.VerificationChecklist.AnnexARemarks,
                    HasPrimaryIdLocal = dto.VerificationChecklist.HasPrimaryIdLocal,
                    PrimaryIdLocalRemarks = dto.VerificationChecklist.PrimaryIdLocalRemarks,
                    HasPrimaryIdAbroad = dto.VerificationChecklist.HasPrimaryIdAbroad,
                    PrimaryIdAbroadRemarks = dto.VerificationChecklist.PrimaryIdAbroadRemarks,
                    HasSecondaryIds = dto.VerificationChecklist.HasSecondaryIds,
                    SecondaryIdsRemarks = dto.VerificationChecklist.SecondaryIdsRemarks,
                    HasPhoto = dto.VerificationChecklist.HasPhoto,
                    PhotoRemarks = dto.VerificationChecklist.PhotoRemarks,
                    HasBankDepositSlip = dto.VerificationChecklist.HasBankDepositSlip,
                    BankDepositSlipRemarks = dto.VerificationChecklist.BankDepositSlipRemarks,
                    HasDeathCertificate = dto.VerificationChecklist.HasDeathCertificate,
                    DeathCertificateRemarks = dto.VerificationChecklist.DeathCertificateRemarks,
                    HasProofOfRelationship = dto.VerificationChecklist.HasProofOfRelationship,
                    ProofOfRelationshipRemarks = dto.VerificationChecklist.ProofOfRelationshipRemarks,
                    HasClaimantBankSlip = dto.VerificationChecklist.HasClaimantBankSlip,
                    ClaimantBankSlipRemarks = dto.VerificationChecklist.ClaimantBankSlipRemarks,
                    HasWarrantyReleaseForm = dto.VerificationChecklist.HasWarrantyReleaseForm,
                    WarrantyReleaseFormRemarks = dto.VerificationChecklist.WarrantyReleaseFormRemarks,
                    HasLguRcfCertification = dto.VerificationChecklist.HasLguRcfCertification,
                    LguRcfCertificationRemarks = dto.VerificationChecklist.LguRcfCertificationRemarks,
                    VerifierOffice = dto.VerificationChecklist.VerifierOffice
                });
            }

            await _repo.UpdateAsync(beneficiary);

            if (changes.Any())
            {
                await AddLogAsync(
                    beneficiary.Id,
                    $"{CommonConstants.UpdatedBeneficiaryChanges} {string.Join("; ", changes)}",
                    userName);
            }
            // ✅ ConcurrencyException thrown from repository's SaveChangesAsync
            // No try/catch needed here — let it bubble up to the controller
            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }
        public async Task<IEnumerable<LogSummaryResultDto>> GetLogSummaryAsync(Guid beneficiaryId)
        {
            var result = await _logRepository.GetLogSummaryAsync(beneficiaryId);
            return result;
        }
        #region Excel Updating/Importing and creating Payroll - START
        public async Task<Guid> QueuePayrollGenerationAsync(PayrollSettingsDto settings, string userName)
        {
            if (settings?.Ids == null || !settings.Ids.Any())
                throw new InvalidOperationException(CommonConstants.NoRecordsSelected);

            var jobId = _payrollJobTracker.CreateJob(settings.Ids.Count);

            _backgroundTaskQueue.QueueBackgroundWorkItem(async (serviceProvider, cancellationToken) =>
            {
                _payrollJobTracker.MarkProcessing(jobId);

                try
                {
                    // ✅ Resolve a FRESH instance from the job's own DI scope.
                    // The original `this` instance belongs to the HTTP request's scope,
                    // which is disposed (along with its DbContext) by the time this runs.
                    var scopedService = serviceProvider.GetRequiredService<IBeneficiaryInformationService>();
                    var fileBytes = await scopedService.GeneratePayrollAsync(settings, userName);

                    // ✅ Application layer asks Infrastructure to persist the result —
                    // it doesn't touch System.IO directly, keeping file-system specifics
                    // out of the Application layer per your DDD boundary.
                    var fileStorage = serviceProvider.GetRequiredService<IPayrollFileStorageService>();
                    var dateStamp = DateTime.Now.ToString("yyyy-MM-dd");
                    var displayFileName = $"CashGiftPayroll_{dateStamp}.zip";
                    var storedFilePath = await fileStorage.SaveAsync(jobId, fileBytes, cancellationToken);

                    _payrollJobTracker.MarkCompleted(jobId, storedFilePath, displayFileName);
                }
                catch (Exception ex)
                {
                    _payrollJobTracker.MarkFailed(jobId, ex.Message);
                }
            });

            return jobId;
        }

        // ============================================================
        // Drop-in replacement for BuildPayrollSheet + GeneratePayrollAsync
        // Fixes:
        //   1. continousNo  — already global across the ZIP; no change needed there.
        //   2. CGP page number — now passed in by ref so it continues across
        //      every municipality sheet inside the same province workbook.
        //   3. Empty-last-page — when total records == PAGE1_RECORDS (6),
        //      page 1 is reduced to PAGE1_RECORDS-1 (5) so page 2 gets at
        //      least one data row instead of just a footer.
        // ============================================================

        public async Task<byte[]> GeneratePayrollAsync(PayrollSettingsDto settings, string userName)
        {
            if (settings.Ids == null || !settings.Ids.Any())
                throw new InvalidOperationException(CommonConstants.NoRecordsSelected);

            var allData = (await _repo.GetByIdsAsync(settings.Ids))
                    .DistinctBy(x => x.Id)
                    .ToList();

            if (!allData.Any())
                throw new InvalidOperationException(CommonConstants.NoneOfTheRecordsFound);

            int continousNo = 1;
            int cgpPageNumber = 1;
            var cgpAssignments = new List<CgpAssignmentDto>(); // ✅ NEW — collects every assignment across the whole ZIP
            var generationId = Guid.NewGuid(); // ✅ NEW — one ID for this entire run

            byte[] finalizedResult;

            using (var zipStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    var provinceGroups = allData
                        .GroupBy(x => x.ProvinceName)
                        .OrderBy(g => g.Key);

                    foreach (var provinceGroup in provinceGroups)
                    {
                        string province = provinceGroup.Key ?? "Unknown Province";

                        using (var workbookNew = new XLWorkbook())
                        {
                            var muniGroups = provinceGroup
                                .GroupBy(x => (x.MunicipalityName ?? "").ToUpperInvariant())
                                .OrderBy(g => g.Key);

                            foreach (var muniGroup in muniGroups)
                            {
                                string municipality = muniGroup.Key;

                                var records = muniGroup
                                    .OrderBy(x => x.LastName)
                                    .ThenBy(x => x.FirstName)
                                    .ThenBy(x => x.MiddleName)
                                    .ToList();

                                var sheetName = SanitizeSheetName(municipality);
                                var ws = workbookNew.Worksheets.Add(sheetName);

                                BuildPayrollSheet(
                                    ws,
                                    records,
                                    settings,
                                    ref continousNo,
                                    ref cgpPageNumber,
                                    cgpAssignments); // ✅ NEW
                            }

                            string safeProvince = SanitizeSheetName(province);
                            var entry = archive.CreateEntry($"{safeProvince}_Payroll.xlsx");
                            using (var entryStream = entry.Open())
                            {
                                workbookNew.SaveAs(entryStream);
                            }
                        }
                    }
                }

                zipStream.Position = 0;
                finalizedResult = zipStream.ToArray();
            }

            // ✅ NEW — persist exactly what got written into the Excel, and log it.
            // The most recent generation always wins, overwriting any prior CGP
            // assignment for these beneficiaries — matches your confirmed rule.
            if (cgpAssignments.Any())
            {
                await _repo.BulkSetCgpAssignmentsAsync(cgpAssignments);

                foreach (var a in cgpAssignments)
                {
                    await AddLogAsync(
                        a.BeneficiaryId,
                        $"CGP Number assigned: {CommonConstants.CgpNo} {a.CgpPrefix}-{a.CgpPageNumber.ToPaddedPage()}",
                        "System (Payroll Generation)");
                }

                await _repo.SaveChangesAsync();
            }

            await AddSystemLogAsync(
                $"Downloaded Payroll for {allData.Count} record(s)",
                userName,
                "Download");

            return finalizedResult;
        }
        private static void BuildPayrollSheet(
             IXLWorksheet ws,
             List<BeneficiaryInformationDto> records,
             PayrollSettingsDto s,
             ref int continousNo,
             ref int cgpPageNumber,
             List<CgpAssignmentDto> cgpAssignments)
        {
            const int COLS = 18;
            const int FONT_SIZE = 14;

            // Legal paper landscape = 14" x 8.5"
            // Margins: Top 0.5" + Bottom 0.5" = 1.0" total used
            // Calibrated empirically against actual print output.
            const double TOTAL_PRINTABLE_HEIGHT = 1097.0;

            // Fixed heights for non-data rows
            const double CGP_ROW_HEIGHT = 20.0;
            const double SUBTOTAL_HEIGHT = 18.0;

            const int PAGE1_MAX = 6;
            const int PAGE2_MAX = 7;

            // ✅ Single shared data-row height, derived from page 2's budget
            // (CGP row is the only fixed content there, so it gives the
            // truest "pure data row" height). Page 1 reuses this SAME
            // height instead of computing its own shorter value — page 1
            // naturally has less room for data because it carries the
            // header block, CGP number block, subtotal, and signatory
            // block on the same physical page. That's expected: rows
            // should look consistent across pages, not artificially
            // squeezed on page 1 to "use up" space that's actually
            // occupied by the header/signatory content.
            double dataRowHeight = (TOTAL_PRINTABLE_HEIGHT - CGP_ROW_HEIGHT) / PAGE2_MAX;
            dataRowHeight = Math.Max(60.0, Math.Min(192.0, dataRowHeight));

            var first = records.FirstOrDefault();
            var municipality = first?.MunicipalityName ?? "";
            var province = first?.ProvinceName ?? "";
            var milestoneYear = first?.MilestoneYear ?? 0;
            string cgpPrefix = $"{s.RegionCode}-{milestoneYear}{s.Month}-{s.FixedSegment}-{s.ShortenYear}"; // ✅ NEW
            // =========================================================================
            // BUILD PAGE PLAN UPFRONT
            // =========================================================================
            var pagePlan = new List<int>();

            if (records.Count <= 1)
            {
                pagePlan.Add(records.Count);
            }
            else
            {
                int remaining = records.Count;
                int p1 = Math.Min(remaining - 1, PAGE1_MAX);
                pagePlan.Add(p1);
                remaining -= p1;

                while (remaining > 1)
                {
                    int take = Math.Min(remaining - 1, PAGE2_MAX);
                    pagePlan.Add(take);
                    remaining -= take;
                }

                pagePlan.Add(remaining);
            }

            // ── Helpers ───────────────────────────────────────────────────────────────
            void NavyHeader(IXLRange r, string text)
            {
                r.Merge();
                r.Value = text;
                r.Style.Font.Bold = true;
                r.Style.Font.FontSize = FONT_SIZE;
                r.Style.Font.FontName = CommonConstants.Arial;
                r.Style.Font.FontColor = XLColor.White;
                r.Style.Fill.BackgroundColor = XLColor.FromHtml(CommonConstants.NavyColor);
                r.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                r.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                r.Style.Alignment.WrapText = true;
                r.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                r.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            void MergeCenter(int row, int c1, int c2, string text, int fs = FONT_SIZE, bool bold = false)
            {
                if (c1 != c2) ws.Range(row, c1, row, c2).Merge();
                var cell = ws.Cell(row, c1);
                cell.Value = text;
                cell.Style.Font.Bold = bold;
                cell.Style.Font.FontSize = fs;
                cell.Style.Font.FontName = CommonConstants.Arial;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            void DataCell(int row, int col, object? val,
                          XLAlignmentHorizontalValues align = XLAlignmentHorizontalValues.Left,
                          int fontSize = 16,
                          string? numberFormat = null,
                          bool shrinkToFit = false)
            {
                if (val != null) ws.Cell(row, col).Value = XLCellValue.FromObject(val);
                ws.Cell(row, col).Style.Font.FontSize = fontSize;
                ws.Cell(row, col).Style.Font.FontName = CommonConstants.Arial;
                ws.Cell(row, col).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Cell(row, col).Style.Alignment.Horizontal = align;
                ws.Cell(row, col).Style.Alignment.WrapText = !shrinkToFit;
                ws.Cell(row, col).Style.Alignment.ShrinkToFit = shrinkToFit;

                if (!string.IsNullOrEmpty(numberFormat))
                    ws.Cell(row, col).Style.NumberFormat.Format = numberFormat;
            }

            void CgpCell(int row, string text)
            {
                ws.Cell(row, 18).Value = text;
                ws.Cell(row, 18).Style.Font.Bold = false;
                ws.Cell(row, 18).Style.Font.FontSize = FONT_SIZE;
                ws.Cell(row, 18).Style.Font.FontName = CommonConstants.Arial;
                ws.Cell(row, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Cell(row, 18).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Cell(row, 18).Style.Border.OutsideBorder = XLBorderStyleValues.None;
            }

            // =========================================================================
            // SECTION 1: HEADER
            // =========================================================================
            ws.Row(3).Height = 24;
            MergeCenter(3, 10, 10, CommonConstants.NCSC, bold: true);

            ws.Row(4).Height = 24;
            string municipalityDisplay = municipality.Contains("City", StringComparison.OrdinalIgnoreCase)
                ? municipality
                : $"{CommonConstants.MunicipalityOf} {municipality}";
            MergeCenter(4, 10, 10, $"{CommonConstants.RegionalOfficeProvinceOf} {province}, {municipalityDisplay}");

            ws.Row(5).Height = 21.75;
            MergeCenter(5, 10, 10, CommonConstants.Act);

            ws.Row(6).Height = 10.5;
            ws.Row(7).Height = 10.5;

            ws.Row(8).Height = 18.75;
            MergeCenter(8, 10, 10, CommonConstants.CashGiftPayroll, bold: true);

            ws.Row(9).Height = 14.25;

            // Row 10: A. PURPOSE + CGP page 1
            ws.Row(10).Height = 23.25;
            ws.Cell(10, 1).Value = CommonConstants.Apurpose;
            ws.Cell(10, 1).Style.Font.Bold = true;
            ws.Cell(10, 1).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(10, 1).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(10, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Cell(10, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Range(10, 3, 10, 14).Merge();
            ws.Cell(10, 3).Value = CommonConstants.PayrollPurpose;
            ws.Cell(10, 3).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(10, 3).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(10, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            ws.Cell(10, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Cell(10, 3).Style.Alignment.WrapText = true;
            int page1CgpNumber = cgpPageNumber;  // ✅ capture before increment
            CgpCell(10, $"{CommonConstants.CgpNo} {s.RegionCode}-{milestoneYear}{s.Month}-{s.FixedSegment}-{s.ShortenYear}-{cgpPageNumber.ToPaddedPage()}");
            cgpPageNumber++;

            ws.Row(11).Height = 11.25;

            // =========================================================================
            // SECTION 2: COLUMN HEADERS rows 12–14
            // =========================================================================
            ws.Row(12).Height = 14.25;
            ws.Row(13).Height = 24.75;
            ws.Row(14).Height = 60.0;

            NavyHeader(ws.Range(12, 1, 14, 1), CommonConstants.BatchCode.ToTitleCase());
            NavyHeader(ws.Range(12, 2, 14, 2), CommonConstants.Number);
            NavyHeader(ws.Range(12, 3, 13, 6), CommonConstants.FullNameOfBeneficiary);
            NavyHeader(ws.Range(14, 3, 14, 3), CommonConstants.LastName.ToTitleCase());
            NavyHeader(ws.Range(14, 4, 14, 4), CommonConstants.FirstName.ToTitleCase());
            NavyHeader(ws.Range(14, 5, 14, 5), CommonConstants.MiddleName.ToTitleCase());
            NavyHeader(ws.Range(14, 6, 14, 6), CommonConstants.Ext);
            NavyHeader(ws.Range(12, 7, 14, 7), CommonConstants.PayrollBirthdate);
            NavyHeader(ws.Range(12, 8, 14, 8), CommonConstants.Age.ToTitleCase());
            NavyHeader(ws.Range(12, 9, 14, 9), CommonConstants.Sex.ToTitleCase());
            NavyHeader(ws.Range(12, 10, 14, 10), CommonConstants.Barangay.ToTitleCase());
            NavyHeader(ws.Range(12, 11, 14, 11), CommonConstants.Amount);
            NavyHeader(ws.Range(12, 12, 14, 12), CommonConstants.AmountReceived);
            NavyHeader(ws.Range(12, 13, 13, 14), CommonConstants.BeneficiaryAuthRepresentative);
            NavyHeader(ws.Range(14, 13, 14, 13), CommonConstants.SignatureOverPrintedName);
            NavyHeader(ws.Range(14, 14, 14, 14), CommonConstants.Thumbmark);
            NavyHeader(ws.Range(12, 15, 14, 15), CommonConstants.ForAuthRep);
            NavyHeader(ws.Range(12, 16, 14, 16), CommonConstants.DateOfDeath);
            NavyHeader(ws.Range(12, 17, 14, 17), CommonConstants.DateReceived);
            NavyHeader(ws.Range(12, 18, 14, 18), CommonConstants.Remarks.ToTitleCase());

            // =========================================================================
            // SECTION 3: DATA ROWS
            // =========================================================================
            int currentRow = 15;
            int processed = 0;
            int thisPageCgpNumber;
            var generationId = Guid.NewGuid(); // ✅ NEW — one ID for this entire run

            for (int pageIndex = 0; pageIndex < pagePlan.Count; pageIndex++)
            {
                int pageSize = pagePlan[pageIndex];
                var pageRecs = records.Skip(processed).Take(pageSize).ToList();
                bool isFirstPage = pageIndex == 0;

                if (!isFirstPage)
                {
                    ws.PageSetup.AddHorizontalPageBreak(currentRow - 1);

                    thisPageCgpNumber = cgpPageNumber;   // ✅ capture before increment
                    CgpCell(currentRow, $"{CommonConstants.CgpNo} {s.RegionCode}-{milestoneYear}{s.Month}-{s.FixedSegment}-{s.ShortenYear}-{cgpPageNumber.ToPaddedPage()}");
                    cgpPageNumber++;
                    ws.Row(currentRow).Height = CGP_ROW_HEIGHT;
                    currentRow++;
                }
                else
                {
                    thisPageCgpNumber = page1CgpNumber;
                }

                // ✅ Same row height on every page — page 1's smaller data
                // budget is absorbed by its larger header/signatory
                // footprint, not by shrinking the rows themselves.
                foreach (var rec in pageRecs)
                {
                    int dr = currentRow;
                    decimal cashGiftAmount = PayrollSettingsDto.CalculateCashGiftAmount(rec.Age);

                    cgpAssignments.Add(new CgpAssignmentDto
                    {
                        BeneficiaryId = rec.Id,
                        CgpPageNumber = thisPageCgpNumber,
                        CgpPrefix = cgpPrefix,
                        CgpGenerationId = generationId // ✅ NEW
                    });

                    ws.Row(dr).Height = dataRowHeight;

                    DataCell(dr, 1, (rec.BatchCode ?? "").ToUpperInvariant());
                    DataCell(dr, 2, continousNo++, XLAlignmentHorizontalValues.Center, 16, null, true);
                    DataCell(dr, 3, (rec.LastName ?? "").ToUpperInvariant(), XLAlignmentHorizontalValues.Left, 14, null, true);
                    DataCell(dr, 4, (rec.FirstName ?? "").ToUpperInvariant(), XLAlignmentHorizontalValues.Left, 14, null, true);
                    DataCell(dr, 5, (rec.MiddleName ?? "").ToUpperInvariant(), XLAlignmentHorizontalValues.Left, 14, null, true);
                    DataCell(dr, 6, (rec.Extension ?? "").ToUpperInvariant());
                    DataCell(dr, 7, rec.BirthDate.ToStandardDate(), XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 8, rec.Age, XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 9, rec.Sex == 1 ? CommonConstants.Male : CommonConstants.Female,
                                     XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 10, rec.BarangayName.ToUpperInvariant(), XLAlignmentHorizontalValues.Center, 14, null, true);
                    DataCell(dr, 11, cashGiftAmount, XLAlignmentHorizontalValues.Center, 12, "₱#,##0.00");
                    DataCell(dr, 12, "", XLAlignmentHorizontalValues.Center);

                    if (rec.IsDeceased && rec.DateOfDeath.HasValue)
                        DataCell(dr, 16, rec.DateOfDeath.Value.ToStandardDate(),
                                         XLAlignmentHorizontalValues.Center);

                    ws.Range(dr, 1, dr, COLS).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Range(dr, 1, dr, COLS).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    currentRow++;
                }

                processed += pageSize;
            }

            // =========================================================================
            // SECTION 4: SUBTOTAL
            // =========================================================================
            int subtotalRow = currentRow + 1;
            int dataStartRow = 15;
            ws.Row(subtotalRow).Height = SUBTOTAL_HEIGHT;

            ws.Range(subtotalRow, 1, subtotalRow, 8).Merge();
            ws.Cell(subtotalRow, 1).Value = CommonConstants.SubTotal;
            ws.Cell(subtotalRow, 1).Style.Font.Bold = true;
            ws.Cell(subtotalRow, 1).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(subtotalRow, 1).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(subtotalRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(subtotalRow, 11).FormulaA1 = $"=SUM(K{dataStartRow}:K{subtotalRow - 2})";
            ws.Cell(subtotalRow, 11).Style.Font.Bold = true;
            ws.Cell(subtotalRow, 11).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(subtotalRow, 11).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(subtotalRow, 11).Style.NumberFormat.Format = "₱#,##0.00";
            ws.Cell(subtotalRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            // ✅ Force ClosedXML to recalculate the formula NOW (at generation time,
            // not just when Excel later opens the file), so we can measure the
            // actual resulting text length and widen the column if needed —
            // without this, the formula's displayed text doesn't exist yet to
            // measure against, since ClosedXML doesn't evaluate formulas by default.
            ws.Workbook.RecalculateAllFormulas();

            // ✅ Auto-widen ONLY column K (Amount), and only if the subtotal's
            // rendered text needs more room than your hand-tuned 16.0 width
            // already provides. This preserves your deliberate column balance
            // for every other column, while making column K resilient to
            // unpredictable subtotal magnitudes (more records summed = more
            // digits = wider text), instead of hardcoding a guess that could
            // break again later.
            double subtotalTextWidth = EstimateTextWidth(
                ws.Cell(subtotalRow, 11).GetFormattedString(), FONT_SIZE);

            if (subtotalTextWidth > ws.Column(11).Width)
                ws.Column(11).Width = subtotalTextWidth + 2.0; // small padding buffer

            ws.Range(subtotalRow, 12, subtotalRow, COLS).Merge();

            currentRow = subtotalRow + 2;

            // =========================================================================
            // SECTION 5: SIGNATORIES
            // =========================================================================

            // A)
            ws.Range(currentRow, 1, currentRow, 8).Merge();
            ws.Cell(currentRow, 1).Value = s.Signatory1Label;
            ws.Cell(currentRow, 1).Style.Font.Italic = true;
            ws.Cell(currentRow, 1).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 1).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(currentRow, 1).Style.Alignment.WrapText = true;
            ws.Cell(currentRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(currentRow).Height = 16;
            currentRow++;

            // B)
            ws.Row(currentRow).Height = 8;
            currentRow++;

            // C)
            ws.Range(currentRow, 11, currentRow, 15).Merge();
            ws.Cell(currentRow, 11).Value = s.Signatory2Label;
            ws.Cell(currentRow, 11).Style.Font.Bold = true;
            ws.Cell(currentRow, 11).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 11).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(currentRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currentRow, 11).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(currentRow).Height = 16;
            currentRow++;

            // D) Signing space
            ws.Row(currentRow).Height = 20; currentRow++;
            ws.Row(currentRow).Height = 20; currentRow++;
            ws.Row(currentRow).Height = 20; currentRow++;

            // E) Names underlined
            int sigNamesRow = currentRow;
            ws.Row(sigNamesRow).Height = 20;

            ws.Range(sigNamesRow, 1, sigNamesRow, 4).Merge();
            ws.Cell(sigNamesRow, 1).Value = s.Signatory1Name;
            ws.Cell(sigNamesRow, 1).Style.Font.Bold = true;
            ws.Cell(sigNamesRow, 1).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(sigNamesRow, 1).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(sigNamesRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(sigNamesRow, 1, sigNamesRow, 4).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Range(sigNamesRow, 11, sigNamesRow, 15).Merge();
            ws.Cell(sigNamesRow, 11).Value = s.Signatory2Name;
            ws.Cell(sigNamesRow, 11).Style.Font.Bold = true;
            ws.Cell(sigNamesRow, 11).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(sigNamesRow, 11).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(sigNamesRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(sigNamesRow, 11, sigNamesRow, 15).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            currentRow++;

            // F) Positions
            ws.Range(currentRow, 1, currentRow, 4).Merge();
            ws.Cell(currentRow, 1).Value = s.Signatory1Position;
            ws.Cell(currentRow, 1).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 1).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(currentRow, 11, currentRow, 15).Merge();
            ws.Cell(currentRow, 11).Value = s.Signatory2Position;
            ws.Cell(currentRow, 11).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 11).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(currentRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(currentRow).Height = 16;
            currentRow++;

            // G)
            ws.Row(currentRow).Height = 4;
            currentRow++;

            // H) Oath text
            ws.Range(currentRow, 1, currentRow, 8).Merge();
            ws.Cell(currentRow, 1).Value =
                "R. I/we certify on my/our official oath that on ______________________________________," +
                " I/we have paid in cash to each individual on the payroll, the amount set opposite to each name," +
                " having presented himself/herself, established identity and affixed his/her signature or" +
                " thumbmark on the space provided.";
            ws.Cell(currentRow, 1).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 1).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(currentRow, 1).Style.Alignment.WrapText = true;
            ws.Cell(currentRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            ws.Row(currentRow).Height = 36;
            currentRow++;

            // I) Signing space
            ws.Row(currentRow).Height = 20; currentRow++;
            ws.Row(currentRow).Height = 20; currentRow++;

            // J) Bottom sig row
            int sig3Row = currentRow;
            int labelRow1 = currentRow + 1;
            int labelRow2 = currentRow + 2;

            ws.Row(sig3Row).Height = 20;

            ws.Range(sig3Row, 2, sig3Row, 9).Merge();
            ws.Cell(sig3Row, 2).Value = s.Signatory3Name;
            ws.Cell(sig3Row, 2).Style.Font.Bold = true;
            ws.Cell(sig3Row, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(sig3Row, 2).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(sig3Row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(sig3Row, 2, sig3Row, 9).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Range(sig3Row, 12, sig3Row, 14).Merge();
            ws.Range(sig3Row, 12, sig3Row, 14).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Range(sig3Row, 15, sig3Row, 17).Merge();
            ws.Range(sig3Row, 15, sig3Row, 17).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // K) Labels
            ws.Row(labelRow1).Height = 18;

            ws.Range(labelRow1, 2, labelRow1, 9).Merge();
            ws.Cell(labelRow1, 2).Value = s.Signatory3Position;
            ws.Cell(labelRow1, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow1, 2).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(labelRow1, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(labelRow1, 12, labelRow1, 14).Merge();
            ws.Cell(labelRow1, 12).Value = CommonConstants.PrintedNameAndSignatureOf;
            ws.Cell(labelRow1, 12).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow1, 12).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(labelRow1, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(labelRow1, 12).Style.Alignment.WrapText = true;

            ws.Range(labelRow1, 15, labelRow1, 17).Merge();
            ws.Cell(labelRow1, 15).Value = CommonConstants.PrintedNameAndSignatureOf;
            ws.Cell(labelRow1, 15).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow1, 15).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(labelRow1, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(labelRow1, 15).Style.Alignment.WrapText = true;

            // L) Positions
            ws.Row(labelRow2).Height = 16;

            ws.Range(labelRow2, 15, labelRow2, 17).Merge();
            ws.Cell(labelRow2, 15).Value = s.Signatory4Position;
            ws.Cell(labelRow2, 15).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow2, 15).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(labelRow2, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(labelRow2, 15).Style.Alignment.WrapText = true;

            ws.Range(labelRow2, 12, labelRow2, 14).Merge();
            ws.Cell(labelRow2, 12).Value = s.Signatory4Position;
            ws.Cell(labelRow2, 12).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow2, 12).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(labelRow2, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(labelRow2, 12).Style.Alignment.WrapText = true;

            // =========================================================================
            // SECTION 6: COLUMN WIDTHS + PAGE SETUP
            // =========================================================================
            double[] colWidths =
            {
        18.82, // A  - Batch Code
        9.0,   // B  - Number
        25.18, // C  - Last Name
        20.82, // D  - First Name
        19.46, // E  - Middle Name
        7.82,  // F  - Ext
        16.0,  // G  - Birthdate
        6.72,  // H  - Age
        12.0,  // I  - Sex
        20.0,  // J  - Barangay
        16.0,  // K  - Amount
        14.0,  // L  - Amount Received
        46.0,  // M  - Signature Over Printed Name
        46.0,  // N  - Thumbmark
        22.0,  // O  - For Auth Rep
        12.0,  // P  - Date of Death
        12.0,  // Q  - Date Received
        20.82  // R  - Remarks
    };

            for (int c = 1; c <= colWidths.Length; c++)
                ws.Column(c).Width = colWidths[c - 1];

            // Calculate right offset based on column 18 width
            double column18WidthPx = colWidths[17] * 7;
            double logoWidthPx = 95;
            double padding = 8;
            double offsetRight = Math.Max(0, column18WidthPx - logoWidthPx - padding);

            PayrollLogos.AddLogos(
              ws,
              anchorRow: 1,
              leftCol: 1,
              rightCol: 18,
              widthPx: 95,
              heightPx: 95,
              offsetLeft: 4,
              offsetRight: (int)offsetRight);

            ws.PageSetup.PaperSize = XLPaperSize.LegalPaper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;

            ws.PageSetup.Margins.Left = 1;
            ws.PageSetup.Margins.Right = 0.25;
            ws.PageSetup.Margins.Top = 0.5;
            ws.PageSetup.Margins.Bottom = 0.5;

            ws.PageSetup.CenterHorizontally = false;
            ws.PageSetup.CenterVertically = false;

            // Alternative: Use FitToPages with (wide, tall) parameters
            ws.PageSetup.FitToPages(1, 0);  // 1 page wide, auto height

            ws.PageSetup.Footer.Center.AddText(CommonConstants.Page);
            ws.PageSetup.Footer.Center.AddText(XLHFPredefinedText.PageNumber);
            ws.PageSetup.Footer.Center.AddText(CommonConstants.Of);
            ws.PageSetup.Footer.Center.AddText(XLHFPredefinedText.NumberOfPages);
            ws.PageSetup.Margins.Footer = 0.3;
        }

        private static string SanitizeSheetName(string name)
        {
            var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
            var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return clean.Length > 31 ? clean[..31] : clean;
        }
        // ✅ NEW — extracted from GenerateImportTemplate so the exact same
        // header layout/styling (column widths, title block, the 30-column
        // header row, freeze panes) can be reused by ExportCrossmatchRowsAsTemplate
        // without drifting out of sync with the official template.
        private const int ImportTemplateTotalCols = 30;

        private void BuildImportTemplateHeader(IXLWorksheet worksheet)
        {
            int totalCols = ImportTemplateTotalCols;

            // ── Row heights ──────────────────────────────────────────────────────────
            worksheet.Row(1).Height = 15.5;
            worksheet.Row(2).Height = 15.5;
            worksheet.Row(3).Height = 15.5;
            worksheet.Row(4).Height = 15.5;
            worksheet.Row(5).Height = 15.5;
            worksheet.Row(6).Height = 15.5;
            worksheet.Row(8).Height = 20.5;
            worksheet.Row(9).Height = 20.5;
            worksheet.Row(10).Height = 20.5;
            worksheet.Row(11).Height = 70.0;

            // ── Column widths (exact from template) ──────────────────────────────────
            worksheet.Column(1).Width = 18.54; // B  DATE ENDORSED
            worksheet.Column(2).Width = 18.18; // C  BATCH CODE
            worksheet.Column(3).Width = 4.45;  // D  NO.
            worksheet.Column(4).Width = 14.54; // E  OSCA ID NUMBER
            worksheet.Column(5).Width = 17.63; // F  OSCA ID DATE ISSUED
            worksheet.Column(6).Width = 14.54; // G  NCSC RRN
            worksheet.Column(7).Width = 19.45; // H  LAST NAME
            worksheet.Column(8).Width = 21.45; // I  FIRST NAME
            worksheet.Column(9).Width = 19.18; // J  MIDDLE NAME
            worksheet.Column(10).Width = 12.63; // K  EXTENSION
            worksheet.Column(11).Width = 13.82; // L  MONTH
            worksheet.Column(12).Width = 6.54;  // M  DAY
            worksheet.Column(13).Width = 10.18; // N  YEAR
            worksheet.Column(14).Width = 6.54;  // O  AGE
            worksheet.Column(15).Width = 12.54; // P  SEX
            worksheet.Column(16).Width = 13.63; // Q  CITIZENSHIP
            worksheet.Column(17).Width = 11.18; // R  REGION
            worksheet.Column(18).Width = 22.82; // S  PROVINCE
            worksheet.Column(19).Width = 18.82; // T  MUNICIPALITY/CITY
            worksheet.Column(20).Width = 25.0;  // U  BARANGAY
            worksheet.Column(21).Width = 25.0;  // V  CONTACT NUMBER
            worksheet.Column(22).Width = 18.54; // W  DATE OF DEATH
            worksheet.Column(23).Width = 19.18; // X  DATE APPLIED
            worksheet.Column(24).Width = 14.54; // Y  INDIGENOUS PERSON
            worksheet.Column(25).Width = 14.54; // Z  PERSON WITH DISABILITY
            worksheet.Column(26).Width = 22.0;  // AA COMPLIANCE (FOR NCSC)
            worksheet.Column(27).Width = 18.54; // AB NAME OF VALIDATOR (FOR NCSC)
            worksheet.Column(28).Width = 18.54; // AC VALIDATION DATE (FOR NCSC)
            worksheet.Column(29).Width = 25.63; // AE NCSC ASSESSMENT (FOR NCSC)
            worksheet.Column(30).Width = 19.82; // AF PAYMENT STATUS (FOR NCSC)

            // ── Title block rows 2–4 (G2:AC merged) ─────────────────────────────────
            var title2 = worksheet.Range(2, 7, 2, 29);
            title2.Merge();
            title2.Value = "REPUBLIC OF THE PHILIPPINES";
            title2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            title2.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            title2.Style.Font.FontName = "Arial";
            title2.Style.Font.FontSize = 11;

            var title3 = worksheet.Range(3, 7, 3, 29);
            title3.Merge();
            title3.Value = "PROVINCE OF__________________________";
            title3.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            title3.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            title3.Style.Font.FontName = "Arial";
            title3.Style.Font.FontSize = 11;

            var title4 = worksheet.Range(4, 7, 4, 29);
            title4.Merge();
            title4.Value = "CITY / MUNICIPALITY OF _____________________";
            title4.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            title4.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            title4.Style.Font.FontName = "Arial";
            title4.Style.Font.FontSize = 11;

            // ── Endorsement text row 8 (A8:AF8 merged) ───────────────────────────────
            var endorseRange = worksheet.Range(8, 1, 8, totalCols);
            endorseRange.Merge();
            endorseRange.Value =
                "This is to endorse to the office of National Commission of Senior Citizens, " +
                "Cluster 8 Region XIII, the herein _____ applicants identified and eligible to avail " +
                "the RA 11982 or the Expanded Centenarian Act, to wit:";
            endorseRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            endorseRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            endorseRange.Style.Alignment.WrapText = true;
            endorseRange.Style.Font.FontName = "Arial";
            endorseRange.Style.Font.FontSize = 11;

            // ── Row 10: group headers ────────────────────────────────────────────────
            // "FULL NAME" spanning H10:K10  (cols 8–11)
            var fullNameRange = worksheet.Range(10, 8, 10, 11);
            fullNameRange.Merge();
            fullNameRange.Value = "FULL NAME";
            fullNameRange.Style.Font.Bold = true;
            fullNameRange.Style.Font.FontName = "Arial";
            fullNameRange.Style.Font.FontSize = 11;
            fullNameRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            fullNameRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // "BIRTHDAY" spanning L10:N10  (cols 12–14)
            var birthdayRange = worksheet.Range(10, 12, 10, 14);
            birthdayRange.Merge();
            birthdayRange.Value = "BIRTHDAY";
            birthdayRange.Style.Font.Bold = true;
            birthdayRange.Style.Font.FontName = "Arial";
            birthdayRange.Style.Font.FontSize = 11;
            birthdayRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            birthdayRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // ── Row 11: column headers ───────────────────────────────────────────────
            // Dark navy = #1E4E79 for "FOR NCSC" cols: 1,2,3 and 27–32
            // White background for LGU-filled cols: 4–26

            var navyBlue = XLColor.FromHtml("#1E4E79");
            var lightBlue = XLColor.FromHtml("#9DC3E6"); // lighter shade for LGU cols
            var white = XLColor.White;

            // Helper: apply header style to a single cell
            void HeaderCell(int col, string text, bool isNcsc)
            {
                var cell = worksheet.Cell(11, col);
                cell.Value = text;
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontName = "Arial";
                cell.Style.Font.FontSize = 11;
                cell.Style.Font.FontColor = white;
                cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
                cell.Style.Fill.BackgroundColor = isNcsc ? navyBlue : lightBlue;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            // FOR NCSC columns (dark navy)
            HeaderCell(1, "DATE ENDORSED\n(MMMM dd, yyyy)", true);
            HeaderCell(2, "BATCH CODE\n(FOR NCSC)", true);
            // LGU columns (light blue)
            HeaderCell(3, "NO.", false);
            HeaderCell(4, "OSCA ID NUMBER", false);
            HeaderCell(5, "OSCA ID DATE ISSUED\n(MMMM dd, yyyy)", false);
            HeaderCell(6, "NCSC RRN", false);
            HeaderCell(7, "LAST NAME", false);
            HeaderCell(8, "FIRST NAME", false);
            HeaderCell(9, "MIDDLE NAME", false);
            HeaderCell(10, "EXTENSION", false);
            HeaderCell(11, "MONTH\n(IN WORDS)", false);
            HeaderCell(12, "DAY", false);
            HeaderCell(13, "YEAR", false);
            HeaderCell(14, "AGE", false);
            HeaderCell(15, "SEX\n(MALE OR FEMALE)", false);
            HeaderCell(16, "CITIZENSHIP", false);
            HeaderCell(17, "REGION", false);
            HeaderCell(18, "PROVINCE", false);
            HeaderCell(19, "MUNICIPALITY/\nCITY", false);
            HeaderCell(20, "BARANGAY", false);
            HeaderCell(21, "CONTACT NUMBER", false);
            HeaderCell(22, "DATE OF DEATH\n(If deceased)", false);
            HeaderCell(23, "DATE APPLIED\n(MMMM dd, yyyy)", false);
            HeaderCell(24, "INDIGENOUS PERSON\n(YES/NO)", false);
            HeaderCell(25, "PERSON WITH DISABILITY\n(YES/NO)", false);
            // FOR NCSC columns (dark navy)
            HeaderCell(26, "COMPLIANCE TO DOCUMENTARY\nREQUIREMENTS\n(FOR NCSC)", true);
            HeaderCell(27, "NAME OF VALIDATOR\n(FOR NCSC)", true);
            HeaderCell(28, "VALIDATION DATE\n(MMMM dd, yyyy)", true);
            HeaderCell(29, "NCSC ASSESSMENT\n(FOR NCSC)\n(ELIGIBLE/INELIGIBLE)", true);
            HeaderCell(30, "PAYMENT STATUS\n(FOR NCSC)", true);

            // ── Freeze, filter, page setup ───────────────────────────────────────────
            worksheet.SheetView.FreezeRows(11);
            worksheet.Range(11, 1, 11, totalCols).SetAutoFilter();
            worksheet.PageSetup.PaperSize = XLPaperSize.LegalPaper;
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            worksheet.PageSetup.FitToPages(1, 0);
            worksheet.PageSetup.SetRowsToRepeatAtTop(11, 11);
        }

        public byte[] GenerateImportTemplate()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("HARD COPY OFFICIAL");
            BuildImportTemplateHeader(worksheet);

            // ── Sample data row 12 ───────────────────────────────────────────────────
            var sampleData = new object[]
            {
        "February 01, 2026",  // 1  DATE ENDORSED
        "BC-2026",            // 2  BATCH CODE
        1,                    // 3  NO.
        "OSCA-00001",         // 4  OSCA ID NUMBER
        "February 01, 2026",  // 5  OSCA ID DATE ISSUED
        12345,                // 6  NCSC RRN
        "DELA CRUZ",          // 7  LAST NAME
        "JUAN",               // 8  FIRST NAME
        "SANTOS",             // 9 MIDDLE NAME
        "",                   // 10 EXTENSION
        "JANUARY",            // 11 BIRTH MONTH
        "01",                 // 12 BIRTH DAY
        "1926",               // 13 BIRTH YEAR
        "",                   // 14 AGE (auto)
        "MALE",               // 15 SEX
        "FILIPINO",           // 16 CITIZENSHIP
        "CARAGA",             // 17 REGION
        "AGUSAN DEL NORTE",   // 18 PROVINCE
        "CITY OF BUTUAN",        // 19 MUNICIPALITY
        "AMBAGO",             // 20 BARANGAY
        "09171234567",        // 21 CONTACT NUMBER
        "",                   // 22 DATE OF DEATH
        "January 26, 2026",   // 23 DATE APPLIED
        "NO",                 // 24 INDIGENOUS PERSON
        "NO",                 // 25 PERSON WITH DISABILITY
        "COMPLIANT",          // 26 COMPLIANCE (FOR NCSC)
        "JANE DOE",           // 27 NAME OF VALIDATOR (FOR NCSC)
        "February 01, 2026",  // 28 VALIDATION DATE (FOR NCSC)
        "ELIGIBLE",           // 29 NCSC ASSESSMENT (FOR NCSC)
        "PENDING",                   // 30 PAYMENT STATUS (FOR NCSC)
            };

            for (int col = 1; col <= sampleData.Length; col++)
            {
                var cell = worksheet.Cell(12, col);
                cell.Value = sampleData[col - 1] is string s
                    ? XLCellValue.FromObject(s)
                    : XLCellValue.FromObject(sampleData[col - 1]);
            }

            var sampleRange = worksheet.Range(12, 1, 12, ImportTemplateTotalCols);
            sampleRange.Style.Fill.PatternType = XLFillPatternValues.Solid;
            sampleRange.Style.Fill.BackgroundColor = XLColor.LightYellow;
            sampleRange.Style.Font.Italic = true;
            sampleRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sampleRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // ── Instructions sheet ───────────────────────────────────────────────────
            var instructions = workbook.Worksheets.Add("Instructions");

            var instrHeaders = new[] { "COLUMN", "REQUIRED", "ACCEPTED VALUES / FORMAT", "EXAMPLE" };
            for (int col = 1; col <= instrHeaders.Length; col++)
                instructions.Cell(1, col).Value = instrHeaders[col - 1];

            var instrHeaderRange = instructions.Range(1, 1, 1, 4);
            instrHeaderRange.Style.Font.Bold = true;
            instrHeaderRange.Style.Fill.PatternType = XLFillPatternValues.Solid;
            instrHeaderRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E4E79");
            instrHeaderRange.Style.Font.FontColor = XLColor.White;
            instrHeaderRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            instrHeaderRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var instrData = new[]
            {
        new[] { "DATE ENDORSED",                        "No",  "Month DD, YYYY",                        "February 01, 2026" },
        new[] { "BATCH CODE (FOR NCSC)",                "No",  "Any text",                              "BC-2026" },
        new[] { "NO.",                                  "No",  "Number",                                "1" },
        new[] { "OSCA ID NUMBER",                       "No",  "Any text",                              "OSCA-00001" },
        new[] { "OSCA ID DATE ISSUED",                  "No",  "Month DD, YYYY",                        "February 01, 2026" },
        new[] { "NCSC RRN",                             "No",  "Numbers only, no special characters",   "12345" },
        new[] { "LAST NAME",                            "No",  "Any text",                              "DELA CRUZ" },
        new[] { "FIRST NAME",                           "Yes", "Any text",                              "JUAN" },
        new[] { "MIDDLE NAME",                          "No",  "Any text",                              "SANTOS" },
        new[] { "EXTENSION",                            "No",  "Jr. / Sr. / II / III / IV / V",         "Jr." },
        new[] { "BIRTH MONTH",                          "Yes", "Full month name in CAPS",               "JANUARY" },
        new[] { "BIRTH DAY",                            "Yes", "Two-digit day",                         "01" },
        new[] { "BIRTH YEAR",                           "Yes", "Four-digit year",                       "1926" },
        new[] { "AGE",                                  "No",  "Leave blank — auto-computed",           "" },
        new[] { "SEX",                                  "No",  "MALE or FEMALE",                        "MALE" },
        new[] { "CITIZENSHIP",                          "No",  "FILIPINO or DUAL CITIZENSHIP",          "FILIPINO" },
        new[] { "REGION",                               "No",  "Leave blank to default to Caraga",      "CARAGA" },
        new[] { "PROVINCE",                             "Yes", "Full province name",                    "AGUSAN DEL NORTE" },
        new[] { "MUNICIPALITY / CITY",                  "Yes", "Full municipality or city name",        "CITY OF BUTUAN" },
        new[] { "BARANGAY",                             "Yes", "Full barangay name",                    "AMBAGO" },
        new[] { "CONTACT NUMBER",                       "No",  "Any text",                              "09171234567" },
        new[] { "DATE OF DEATH",                        "No",  "Month DD, YYYY — leave blank if alive", "" },
        new[] { "DATE APPLIED",                         "No",  "Month DD, YYYY",                        "February 01, 2026" },
        new[] { "INDIGENOUS PERSON",                    "No",  "YES or NO",                             "NO" },
        new[] { "PERSON WITH DISABILITY",               "No",  "YES or NO",                             "NO" },
        new[] { "COMPLIANCE (FOR NCSC)",                "No",  "COMPLIANT or NON-COMPLIANT",            "COMPLIANT" },
        new[] { "NAME OF VALIDATOR (FOR NCSC)",         "No",  "Any text",                              "JANE DOE" },
        new[] { "VALIDATION DATE (FOR NCSC)",           "No",  "Month DD, YYYY",                        "February 01, 2026" },
        new[] { "NCSC ASSESSMENT (FOR NCSC)",           "Yes", "ELIGIBLE or INELIGIBLE",                "ELIGIBLE" },
        new[] { "PAYMENT STATUS (FOR NCSC)",            "Yes",  "Pending, Paid, or Unpaid",                     "PENDING" },
    };

            for (int i = 0; i < instrData.Length; i++)
            {
                var rowData = instrData[i];
                for (int col = 1; col <= rowData.Length; col++)
                    instructions.Cell(i + 2, col).Value = rowData[col - 1];

                if (rowData[1] == "Yes")
                {
                    instructions.Range(i + 2, 1, i + 2, 4).Style.Fill.PatternType = XLFillPatternValues.Solid;
                    instructions.Range(i + 2, 1, i + 2, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFD966");
                }
                instructions.Range(i + 2, 1, i + 2, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                instructions.Range(i + 2, 1, i + 2, 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            instructions.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return stream.ToArray();
        }

        // ✅ NEW — exports crossmatch rows (New or Possible Match) into the
        // exact same 30-column template layout the real import pipeline
        // expects, pre-filled with everything crossmatch already parsed.
        // NCSC-only columns (Compliance, Validator, Validation Date,
        // Assessment, Payment Status) are left blank — those still need PDO
        // review and aren't something crossmatch can determine on its own.
        // The file can be handed straight to the Import tab once completed.
        public byte[] ExportCrossmatchRowsAsTemplate(List<CrossmatchRowDto> rows, string sheetName)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);
            BuildImportTemplateHeader(worksheet);

            static string DateOrBlank(DateTime? d) => d?.ToString("MMMM dd, yyyy") ?? string.Empty;
            static string YesNoOrBlank(bool? b) => b switch { true => "YES", false => "NO", _ => string.Empty };

            int rowNumber = 12;
            foreach (var row in rows)
            {
                var data = new object[]
                {
                    DateOrBlank(row.DateEndorsed),                                                  // 1  DATE ENDORSED
                    row.BatchCode ?? string.Empty,                                                   // 2  BATCH CODE
                    string.Empty,                                                                    // 3  NO.
                    row.OscaIdNumber ?? string.Empty,                                                 // 4  OSCA ID NUMBER
                    DateOrBlank(row.OscaIdDateIssued),                                                // 5  OSCA ID DATE ISSUED
                    row.NcscRrn?.ToString() ?? string.Empty,                                          // 6  NCSC RRN
                    row.LastName ?? string.Empty,                                                     // 7  LAST NAME
                    row.FirstName ?? string.Empty,                                                    // 8  FIRST NAME
                    row.MiddleName ?? string.Empty,                                                   // 9  MIDDLE NAME
                    row.Extension ?? string.Empty,                                                    // 10 EXTENSION
                    row.BirthDate?.ToString("MMMM").ToUpperInvariant() ?? string.Empty,               // 11 BIRTH MONTH
                    row.BirthDate?.Day.ToString() ?? string.Empty,                                    // 12 BIRTH DAY
                    row.BirthDate?.Year.ToString() ?? string.Empty,                                   // 13 BIRTH YEAR
                    string.Empty,                                                                     // 14 AGE (auto)
                    row.Sex == 1 ? "MALE" : row.Sex == 2 ? "FEMALE" : string.Empty,                    // 15 SEX
                    row.Citizenship == 1 ? "FILIPINO" : row.Citizenship == 2 ? "DUAL CITIZENSHIP" : string.Empty, // 16 CITIZENSHIP
                    row.RegionName ?? string.Empty,                                                    // 17 REGION
                    row.ProvinceName ?? string.Empty,                                                  // 18 PROVINCE
                    row.MunicipalityName ?? string.Empty,                                              // 19 MUNICIPALITY/CITY
                    row.BarangayName ?? string.Empty,                                                  // 20 BARANGAY
                    row.ContactNumber ?? string.Empty,                                                 // 21 CONTACT NUMBER
                    DateOrBlank(row.DateOfDeath),                                                      // 22 DATE OF DEATH
                    DateOrBlank(row.DateApplied),                                                      // 23 DATE APPLIED
                    YesNoOrBlank(row.IsIndigenousPeople),                                               // 24 INDIGENOUS PERSON
                    YesNoOrBlank(row.IsPersonWithDisability),                                           // 25 PERSON WITH DISABILITY
                    string.Empty,                                                                      // 26 COMPLIANCE — for PDO/NCSC to fill in
                    string.Empty,                                                                      // 27 VALIDATOR — for PDO/NCSC to fill in
                    string.Empty,                                                                      // 28 VALIDATION DATE — for PDO/NCSC to fill in
                    string.Empty,                                                                      // 29 NCSC ASSESSMENT — for PDO/NCSC to fill in
                    string.Empty,                                                                      // 30 PAYMENT STATUS — for PDO/NCSC to fill in
                };

                for (int col = 1; col <= data.Length; col++)
                    worksheet.Cell(rowNumber, col).Value = XLCellValue.FromObject(data[col - 1]);

                rowNumber++;
            }

            var dataRange = worksheet.Range(12, 1, Math.Max(12, rowNumber - 1), ImportTemplateTotalCols);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return stream.ToArray();
        }
        public async Task<BeneficiaryImportResultDto> UpdateExcelAsync(
            Stream fileStream, string fileName, string sheetName, string userName)
        {
            var result = new BeneficiaryImportResultDto();

            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == sheetName)
                ?? throw new Exception($"{CommonConstants.WorksheetNotFound} '{sheetName}'.");

            // ── New template: data starts row 12 ─────────────────────────────────
            const int firstDataRowNumber = 12;
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

            for (int rowNumber = firstDataRowNumber; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);

                if (row.Cells(1, 30).All(c => string.IsNullOrWhiteSpace(c.GetFormattedString())))
                    continue;

                result.TotalRows++;

                try
                {
                    // ── NEW COLUMN MAP (col 30 = NCSC Assessment, col 31 = Payment Status) ──
                    var batchCode = row.Cell(2).GetFormattedString().Trim();  // col 2
                    var lastName = row.Cell(7).GetFormattedString().Trim();  // col 7
                    var firstName = row.Cell(8).GetFormattedString().Trim();  // col 8
                    var middleName = row.Cell(9).GetFormattedString().Trim(); // col 9
                    var birthDate = ParseFlexibleDate(
                        $"{row.Cell(11).GetFormattedString().Trim()} " +
                        $"{row.Cell(12).GetFormattedString().Trim()} " +
                        $"{row.Cell(13).GetFormattedString().Trim()}"
                    ) ?? DateTime.MinValue;

                    var dateOfDeath = ParseFlexibleDate(row.Cell(22).GetFormattedString()); // col 22
                    var validationDateRaw = row.Cell(28).GetFormattedString().Trim(); // VALIDATION DATE
                    var isEligibleRaw = row.Cell(29).GetFormattedString().Trim(); // NCSC ASSESSMENT // col 29 ✅ was 30

                    // ── Parse validation date safely ──────────────────────────────
                    // ✅ Use GetFormattedString() + ParseFlexibleDate instead of .Value
                    // .Value returns XLCellValue struct which cannot be cast to DateTime directly
                    var parsedValidationDate = ParseFlexibleDate(validationDateRaw);
                    var isEligible = MapEligibility(isEligibleRaw);

                    var existing = await FindExistingAsync(
                        lastName, firstName, middleName, birthDate);

                    if (existing == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Record,
                            Message = CommonConstants.RecordNotFoundInDatabase,
                            RawValue = $"{lastName}, {firstName}"
                        });
                        continue;
                    }

                    existing.BatchCode = batchCode;
                    existing.IsEligible = isEligible ?? false;
                    existing.DateOfDeath = dateOfDeath;
                    existing.IsDeceased = dateOfDeath.HasValue;  // ✅ keep in sync

                    // ✅ Only update ValidationDate if a valid date was parsed — never overwrite with null
                    if (parsedValidationDate.HasValue)
                        existing.ValidationDate = parsedValidationDate.Value;

                    await AddLogAsync(
                        existing.Id,
                        $"{CommonConstants.ExcelUpdate} {existing.LastName}, {existing.FirstName}",
                        userName);

                    result.ImportedCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new BeneficiaryImportErrorDto
                    {
                        RowNumber = rowNumber,
                        Field = CommonConstants.General,
                        Message = ex.Message
                    });
                }
            }

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
            return result;
        }
        public async Task<BeneficiaryPreviewResultDto> PreviewImportAsync(
            Stream fileStream, string fileName, string sheetName,
            Dictionary<int, Dictionary<string, string>>? corrections = null)
        {
            if (fileStream == null || !fileStream.CanRead)
                throw new Exception(CommonConstants.InvalidExcelUploaded);

            if (string.IsNullOrWhiteSpace(fileName))
                throw new Exception(CommonConstants.InvalidExcelFile);

            var extension = Path.GetExtension(fileName);
            if (!string.Equals(extension, CommonConstants.ExcelFileExtension, StringComparison.OrdinalIgnoreCase))
                throw new Exception(CommonConstants.OnlyExcelFilesAllowed);

            var preview = new BeneficiaryPreviewResultDto();

            var regions = await _regionRepository.GetAllAsync();
            var provinces = (await _provinceRepository.GetAllProvinceAsync())
                .GroupBy(x => x.Name!.Trim().ToUpperInvariant())
                .Select(g => g.OrderBy(x => x.Id).First())
                .ToList();
            var municipalities = (await _municipalityRepository.GetAllMunicipalityAsync()).ToList();
            var barangays = (await _barangayRepository.GetBarangaysAsync()).ToList();

            // ✅ NEW — load the ENTIRE duplicate-check pool ONCE, instead of issuing
            // a separate ExistsDuplicateAsync + FindSoftDuplicatesAsync DB call per
            // Excel row. This is what was causing the timeout on 40+ row imports —
            // FindSoftDuplicatesAsync alone ran 3 correlated subqueries per candidate,
            // per row. Now it's one flat query for the whole import.
            var duplicatePool = await _repo.GetDuplicateCheckPoolAsync();

            var provinceNameByCode = provinces
                .GroupBy(p => p.PsgcCodeProvince)
                .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);
            var municipalityNameByCode = municipalities
                .GroupBy(m => m.PsgcCodeMunicipality)
                .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);
            var barangayNameByCode = barangays
                .GroupBy(b => b.PsgcCodeBarangay)
                .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);

            // ✅ Exact-duplicate lookup — same normalization rule as
            // ExistsDuplicateAsync in the repository (trim + lower + date-only),
            // just pre-built into a HashSet for O(1) checks instead of a query.
            var exactDupKeys = duplicatePool
                .Select(d => BuildExactDupKey(d.LastName, d.FirstName, d.MiddleName, d.BirthDate))
                .ToHashSet();

            // ✅ Soft-duplicate matching — same 365-day window + 0.75 score threshold
            // as FindSoftDuplicatesAsync, done in-memory against the pool instead of
            // a fresh query (with subqueries) per row.
            List<SoftDuplicateCandidateDto> FindSoftDuplicatesInMemory(string firstName, string lastName, DateTime birthDate)
            {
                if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                    return new List<SoftDuplicateCandidateDto>();

                var incomingFullName = string.Join(" ",
                    new[] { firstName?.Trim(), lastName?.Trim() }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

                return duplicatePool
                    .Where(c => Math.Abs((c.BirthDate - birthDate).TotalDays) <= 365)
                    .Select(c =>
                    {
                        var existingFullName = string.Join(" ",
                            new[] { c.FirstName?.Trim(), c.LastName?.Trim() }
                            .Where(s => !string.IsNullOrWhiteSpace(s)));

                        var score = string.IsNullOrWhiteSpace(existingFullName)
                            ? 0.0
                            : NameSimilarityHelper.ComputeNameSimilarity(incomingFullName, existingFullName);

                        return new { Candidate = c, Score = score };
                    })
                    .Where(x => x.Score >= 0.75)
                    .Select(x => new SoftDuplicateCandidateDto
                    {
                        ExistingId = x.Candidate.Id,
                        ExistingFullName = string.Join(", ",
                            new[] { x.Candidate.LastName?.Trim(), x.Candidate.FirstName?.Trim() }
                            .Where(s => !string.IsNullOrWhiteSpace(s))) +
                            (string.IsNullOrWhiteSpace(x.Candidate.MiddleName)
                                ? string.Empty
                                : $" {x.Candidate.MiddleName.Trim()}"),
                        ExistingMiddleName = x.Candidate.MiddleName?.Trim() ?? string.Empty,
                        ExistingBirthDate = x.Candidate.BirthDate,
                        ExistingOscaId = x.Candidate.OscaIdNumber ?? string.Empty,
                        ExistingProvince = provinceNameByCode.GetValueOrDefault(x.Candidate.Province, string.Empty),
                        ExistingMunicipality = municipalityNameByCode.GetValueOrDefault(x.Candidate.Municipality, string.Empty),
                        ExistingBarangay = barangayNameByCode.GetValueOrDefault(x.Candidate.Barangay, string.Empty),
                        MatchScore = x.Score
                    })
                    .OrderByDescending(x => x.MatchScore)
                    .ToList();
            }

            using var workbook = new XLWorkbook(fileStream);

            if (string.IsNullOrWhiteSpace(sheetName))
                throw new Exception(CommonConstants.PleaseSelectAWorksheet);

            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == sheetName);
            if (worksheet == null)
                throw new Exception($"{CommonConstants.WorksheetNotFound} {sheetName}");

            // ── New template: header row 11, data starts row 12 ──────────────────
            const int firstDataRowNumber = 12;

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < firstDataRowNumber)
                throw new Exception(CommonConstants.ExcelSheet1DoesNotContain);

            var uploadedRowKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ✅ NEW — lets the user "accept" a spelling-suggestion correction
            // in the UI (e.g. Region "CARAGAA" → "CARAGA") and re-validate
            // without re-editing and re-uploading the Excel file. Applied
            // right after the raw cell value is read, before matching.
            string ApplyCorrection(int rowNumber, string field, string rawValue) =>
                corrections != null &&
                corrections.TryGetValue(rowNumber, out var fieldMap) &&
                fieldMap.TryGetValue(field, out var correctedValue)
                    ? correctedValue
                    : rawValue;

            for (int rowNumber = firstDataRowNumber; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);

                // ── New template has 31 columns ───────────────────────────────────
                if (row.Cells(1, 30).All(c => string.IsNullOrWhiteSpace(c.GetFormattedString())))
                    continue;

                preview.TotalRows++;

                try
                {
                    // ── NEW COLUMN MAP ────────────────────────────────────────────
                    var dateEndorsed = row.Cell(1).GetFormattedString().Trim();  // DATE ENDORSED
                    var batchCode = row.Cell(2).GetFormattedString().Trim();  // BATCH CODE (FOR NCSC)
                                                                              // col 3 = NO. (ignored)
                    var oscaIdNumber = row.Cell(4).GetFormattedString().Trim();  // OSCA ID NUMBER
                    var oscaIdDateIssued = row.Cell(5).GetFormattedString().Trim();  // OSCA ID DATE ISSUED
                    var ncscRrnRaw = ApplyCorrection(rowNumber, "NcscRrn", row.Cell(6).GetFormattedString().Trim());  // NCSC RRN
                    var lastName = row.Cell(7).GetFormattedString().Trim();  // LAST NAME
                    var firstName = ApplyCorrection(rowNumber, "FirstName", row.Cell(8).GetFormattedString().Trim());  // FIRST NAME
                    var middleName = row.Cell(9).GetFormattedString().Trim(); // MIDDLE NAME
                    var extensionName = row.Cell(10).GetFormattedString().Trim(); // EXTENSION
                    var birthMonthRaw = row.Cell(11).GetFormattedString().Trim(); // BIRTH MONTH
                    var birthDayRaw = row.Cell(12).GetFormattedString().Trim(); // BIRTH DAY
                    var birthYearRaw = row.Cell(13).GetFormattedString().Trim(); // BIRTH YEAR
                                                                                 // col 14 = AGE (ignored — computed)
                    var sexRaw = row.Cell(15).GetFormattedString().Trim(); // SEX
                    var citizenshipRaw = ApplyCorrection(rowNumber, "Citizenship", row.Cell(16).GetFormattedString().Trim()); // CITIZENSHIP
                    var regionName = ApplyCorrection(rowNumber, "Region", row.Cell(17).GetFormattedString().Trim()); // REGION
                    var provinceName = ApplyCorrection(rowNumber, "Province", row.Cell(18).GetFormattedString().Trim()); // PROVINCE
                    var municipalityName = ApplyCorrection(rowNumber, "Municipality", row.Cell(19).GetFormattedString().Trim()); // MUNICIPALITY/CITY
                    var barangayName = ApplyCorrection(rowNumber, "Barangay", row.Cell(20).GetFormattedString().Trim()); // BARANGAY
                    var contactNumber = row.Cell(21).GetFormattedString().Trim(); // CONTACT NUMBER
                    var dateOfDeath = row.Cell(22).GetFormattedString().Trim(); // DATE OF DEATH
                    var dateApplied = row.Cell(23).GetFormattedString().Trim(); // DATE APPLIED
                    var isIndigenousPeopleRaw = row.Cell(24).GetFormattedString().Trim(); // INDIGENOUS PERSON
                    var isPersonWithDisabilityRaw = row.Cell(25).GetFormattedString().Trim(); // PERSON WITH DISABILITY
                    var complianceRaw = row.Cell(26).GetFormattedString().Trim(); // COMPLIANCE (FOR NCSC)
                    var validator = row.Cell(27).GetFormattedString().Trim(); // NAME OF VALIDATOR (FOR NCSC)
                    var validationDateRaw = row.Cell(28).GetFormattedString().Trim(); // VALIDATION DATE (FOR NCSC)
                    var isEligibleRaw = ApplyCorrection(rowNumber, "NcscAssessment", row.Cell(29).GetFormattedString().Trim()); // IsEligible
                    var paymentStatus = row.Cell(30).GetFormattedString().Trim();  // col 30 = PAYMENT STATUS FOR NCSC —  Paid, Unpaid, Pending

                    bool rowHasHardError = false;

                    // ── First Name required ───────────────────────────────────────
                    if (string.IsNullOrWhiteSpace(firstName))
                    {
                        preview.HardErrors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.FirstName.ToTitleCase(),
                            Message = CommonConstants.FirstNameRequired,
                            RawValue = firstName,
                            CorrectionField = "FirstName"
                        });
                        rowHasHardError = true;
                    }

                    // ── Birth Date — correction applies to the whole combined
                    // "Month Day Year" string, typed by the user in that form. ──
                    var birthDateRaw = ApplyCorrection(rowNumber, "BirthDate", $"{birthMonthRaw} {birthDayRaw} {birthYearRaw}");
                    if (!TryParseExcelDate(birthDateRaw, out var birthDate))
                    {
                        preview.HardErrors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.BirthDate.ToTitleCase(),
                            Message = CommonConstants.InvalidBirthDate,
                            RawValue = birthDateRaw,
                            CorrectionField = "BirthDate"
                        });
                        rowHasHardError = true;
                        birthDate = DateTime.MinValue;
                    }

                    // ── NCSC RRN — digits only ─────────────────────────────────────
                    int? ncscRrn = null;
                    if (!string.IsNullOrWhiteSpace(ncscRrnRaw))
                    {
                        if (int.TryParse(ncscRrnRaw, out var parsedRrn))
                            ncscRrn = parsedRrn;
                        else
                        {
                            preview.HardErrors.Add(new BeneficiaryImportErrorDto
                            {
                                RowNumber = rowNumber,
                                Field = CommonConstants.NcscRrn,
                                Message = CommonConstants.InvalidRrn,
                                RawValue = ncscRrnRaw,
                                Suggestion = CommonConstants.RemoveSpecialCharactersFromName,
                                CorrectionField = "NcscRrn"
                            });
                            rowHasHardError = true;
                        }
                    }

                    // ── Citizenship guard ────────────────────────────────────────
                    var mappedCitizenship = MapCitizenship(citizenshipRaw);
                    if (!string.IsNullOrWhiteSpace(citizenshipRaw) && mappedCitizenship == null)
                    {
                        preview.HardErrors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Citizenship.ToTitleCase(),
                            Message = "Invalid citizenship value. Accepted: FILIPINO or DUAL CITIZENSHIP.",
                            CorrectionField = "Citizenship",
                            RawValue = citizenshipRaw,
                            Suggestion = "Use FILIPINO or DUAL CITIZENSHIP"
                        });
                        rowHasHardError = true;
                    }

                    // ── Region ───────────────────────────────────────────────────
                    var region = FindBestNameMatch(regions, x => x.Name, regionName);

                    if (string.IsNullOrWhiteSpace(regionName))
                    {
                        preview.HardErrors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Region,
                            Message = "Region is required and cannot be left blank.",
                            RawValue = regionName
                        });
                        rowHasHardError = true;
                    }
                    else if (region == null)
                    {
                        var suggestedRegion = GetSuggestedName(regions, x => x.Name, regionName);
                        preview.HardErrors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Region,
                            Message = CommonConstants.RegionNotFound,
                            RawValue = regionName,
                            Suggestion = suggestedRegion is { } sr
                                ? $"{CommonConstants.PossibleMatch} '{sr}'"
                                : CommonConstants.CheckSpelling,
                            SuggestedValue = suggestedRegion,
                            CorrectionField = "Region"
                        });
                        rowHasHardError = true;
                    }

                    // ── Province ─────────────────────────────────────────────────
                    var province = FindBestNameMatch(provinces, x => x.Name, provinceName);
                    if (province == null)
                    {
                        var suggestedProvince = GetSuggestedName(provinces, x => x.Name, provinceName);
                        preview.HardErrors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Province.ToTitleCase(),
                            Message = CommonConstants.ProvinceNotFound,
                            RawValue = provinceName,
                            Suggestion = suggestedProvince is { } sp
                                ? $"{CommonConstants.PossibleMatch} '{sp}'"
                                : CommonConstants.CheckSpelling,
                            SuggestedValue = suggestedProvince,
                            CorrectionField = "Province",
                            RowRegion = regionName
                        });
                        rowHasHardError = true;
                    }

                    // ── Municipality ──────────────────────────────────────────────
                    var municipalitiesInProvince = province != null
                        ? municipalities.Where(m => m.PsgcCodeProvince == province.PsgcCodeProvince).ToList()
                        : municipalities;

                    var municipality = FindBestNameMatch(municipalitiesInProvince, x => x.Name, municipalityName);
                    if (municipality == null)
                    {
                        var suggestedMunicipality = GetSuggestedName(municipalitiesInProvince, x => x.Name, municipalityName);
                        preview.HardErrors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Municipality,
                            Message = CommonConstants.MunicipalityNotFound,
                            RawValue = municipalityName,
                            Suggestion = suggestedMunicipality is { } sm
                                ? $"{CommonConstants.PossibleMatch} '{sm}'"
                                : CommonConstants.CheckSpelling,
                            SuggestedValue = suggestedMunicipality,
                            CorrectionField = "Municipality",
                            RowRegion = regionName,
                            RowProvince = provinceName
                        });
                        rowHasHardError = true;
                    }

                    // ── Barangay ──────────────────────────────────────────────────
                    var barangaysInMunicipality = municipality != null
                        ? barangays.Where(b => b.PsgcCodeMunicipality == municipality.PsgcCodeMunicipality).ToList()
                        : barangays;

                    var barangay = FindBestNameMatch(barangaysInMunicipality, x => x.Name, barangayName);
                    if (barangay == null)
                    {
                        var suggestedBarangay = GetSuggestedName(barangaysInMunicipality, x => x.Name, barangayName);
                        preview.HardErrors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Barangay.ToTitleCase(),
                            Message = CommonConstants.BarangayNotFound,
                            RawValue = barangayName,
                            Suggestion = suggestedBarangay is { } sb
                                ? $"{CommonConstants.PossibleMatch} '{sb}'"
                                : CommonConstants.CheckSpelling,
                            SuggestedValue = suggestedBarangay,
                            CorrectionField = "Barangay",
                            RowRegion = regionName,
                            RowProvince = provinceName,
                            RowMunicipality = municipalityName
                        });
                        rowHasHardError = true;
                    }

                    // ── NCSC Assessment ───────────────────────────────────────────
                    var mappedEligibility = MapEligibility(isEligibleRaw);
                    if (mappedEligibility == null)
                    {
                        preview.HardErrors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.NcscAssessment.ToTitleCase(),
                            Message = CommonConstants.NcscAssessmentNotFound,
                            RawValue = isEligibleRaw,
                            CorrectionField = "NcscAssessment"
                        });
                        rowHasHardError = true;
                    }

                    if (rowHasHardError)
                        continue;

                    // ── Exact (100%) duplicate in DB ───────────────────────────────
                    // ✅ CHANGED — per office policy, an exact match no longer hard-
                    // blocks the row out of the import. It's surfaced in the same
                    // review popup as a soft/fuzzy match (MatchScore = 1.0 marks it
                    // as an exact "Known Duplicate" candidate, not a hard error) — the
                    // user can still Skip it, or Import Anyway to save it as a Known
                    // Duplicate (see ConfirmImportAsync / BeneficiaryDuplicateHistory).
                    var exactKey = BuildExactDupKey(lastName, firstName, middleName, birthDate);
                    var exactMatchCandidate = duplicatePool.FirstOrDefault(d =>
                        BuildExactDupKey(d.LastName, d.FirstName, d.MiddleName, d.BirthDate) == exactKey);

                    if (exactMatchCandidate is not null)
                    {
                        preview.SoftDuplicates.Add(new SoftDuplicateCandidateDto
                        {
                            RowNumber = rowNumber,
                            ImportedName = $"{lastName}, {firstName}",
                            ImportedMiddleName = middleName ?? string.Empty,
                            ImportedBirthDate = birthDate,
                            ExistingId = exactMatchCandidate.Id,
                            ExistingFullName = $"{exactMatchCandidate.LastName}, {exactMatchCandidate.FirstName}",
                            ExistingMiddleName = exactMatchCandidate.MiddleName ?? string.Empty,
                            ExistingBirthDate = exactMatchCandidate.BirthDate,
                            ExistingOscaId = exactMatchCandidate.OscaIdNumber ?? string.Empty,
                            ExistingProvince = provinceNameByCode.GetValueOrDefault(exactMatchCandidate.Province, string.Empty),
                            ExistingMunicipality = municipalityNameByCode.GetValueOrDefault(exactMatchCandidate.Municipality, string.Empty),
                            ExistingBarangay = barangayNameByCode.GetValueOrDefault(exactMatchCandidate.Barangay, string.Empty),
                            MatchScore = 1.0
                        });
                        // A row flagged into SoftDuplicates still counts toward
                        // CleanRows too (matches the existing soft/fuzzy-match
                        // convention just below) — the client's "how many will
                        // actually import" math (BeneficiaryImporting.razor's
                        // WillBeImported) subtracts the flagged-row count back
                        // out of CleanRows to correct for this. Skipping this
                        // increment ONLY for exact matches (as before) broke
                        // that math, undercounting by exactly the number of
                        // exact-match rows and showing "0 will import" even
                        // when Tag & Import was selected.
                        preview.CleanRows++;
                        continue;
                    }

                    // ── Soft duplicate check ──────────────────────────────────────
                    // ✅ CHANGED — in-memory scan against the pre-loaded pool
                    var softMatches = FindSoftDuplicatesInMemory(firstName, lastName, birthDate);

                    foreach (var match in softMatches)
                    {
                        preview.SoftDuplicates.Add(new SoftDuplicateCandidateDto
                        {
                            RowNumber = rowNumber,
                            ImportedName = $"{lastName}, {firstName}",
                            ImportedMiddleName = middleName?.Trim() ?? string.Empty,
                            ImportedBirthDate = birthDate,
                            ImportedProvince = province?.Name ?? string.Empty,
                            ImportedMunicipality = municipality?.Name ?? string.Empty,
                            ImportedBarangay = barangay?.Name ?? string.Empty,
                            ExistingId = match.ExistingId,
                            ExistingFullName = match.ExistingFullName,
                            ExistingMiddleName = match.ExistingMiddleName,
                            ExistingBirthDate = match.ExistingBirthDate,
                            ExistingOscaId = match.ExistingOscaId,
                            ExistingProvince = match.ExistingProvince,
                            ExistingMunicipality = match.ExistingMunicipality,
                            ExistingBarangay = match.ExistingBarangay,
                            MatchScore = match.MatchScore
                        });
                    }

                    preview.CleanRows++;
                }
                catch (Exception ex)
                {
                    preview.HardErrors.Add(new BeneficiaryImportErrorDto
                    {
                        RowNumber = rowNumber,
                        Field = CommonConstants.General,
                        Message = ex.Message,
                        RawValue = null
                    });
                }
            }

            return preview;
        }

        // ✅ NEW — read-only crossmatch scan. Parses the same Excel template as
        // PreviewImportAsync/ConfirmImportAsync but never touches the database:
        // every row is classified as New / Possible / Existing / Error and
        // handed back with its parsed fields so the UI can let the user add the
        // New ones individually afterwards. No rows are imported here.
        public async Task<CrossmatchResultDto> GetCrossmatchPreviewAsync(
            Stream fileStream, string fileName, string sheetName,
            Action<int, int>? onProgress = null, CancellationToken cancellationToken = default)
        {
            if (fileStream == null || !fileStream.CanRead)
                throw new Exception(CommonConstants.InvalidExcelUploaded);

            if (string.IsNullOrWhiteSpace(fileName))
                throw new Exception(CommonConstants.InvalidExcelFile);

            var extension = Path.GetExtension(fileName);
            if (!string.Equals(extension, CommonConstants.ExcelFileExtension, StringComparison.OrdinalIgnoreCase))
                throw new Exception(CommonConstants.OnlyExcelFilesAllowed);

            var result = new CrossmatchResultDto();

            var regions = await _regionRepository.GetAllAsync();
            var provinces = (await _provinceRepository.GetAllProvinceAsync())
                .GroupBy(x => x.Name!.Trim().ToUpperInvariant())
                .Select(g => g.OrderBy(x => x.Id).First())
                .ToList();
            var municipalities = (await _municipalityRepository.GetAllMunicipalityAsync()).ToList();
            var barangays = (await _barangayRepository.GetBarangaysAsync()).ToList();

            var duplicatePool = await _repo.GetDuplicateCheckPoolAsync();

            var provinceNameByCode = provinces
                .GroupBy(p => p.PsgcCodeProvince)
                .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);
            var municipalityNameByCode = municipalities
                .GroupBy(m => m.PsgcCodeMunicipality)
                .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);
            var barangayNameByCode = barangays
                .GroupBy(b => b.PsgcCodeBarangay)
                .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);

            // ✅ FIX — this used to be a HashSet<string> plus a separate
            // duplicatePool.First(c => BuildExactDupKey(...) == exactKey)
            // linear scan every time a row hit an exact match. With ~8,500
            // existing records and thousands of "Existing" rows in a file,
            // that's an O(rows × pool) scan re-building keys for every
            // candidate every time — the actual cause of the scan appearing
            // to freeze partway through. A dictionary makes the lookup O(1).
            var exactKeyIndex = duplicatePool
                .GroupBy(d => BuildExactDupKey(d.LastName, d.FirstName, d.MiddleName, d.BirthDate))
                .ToDictionary(g => g.Key, g => g.First());
            var oscaIdIndex = duplicatePool
                .Where(d => !string.IsNullOrWhiteSpace(d.OscaIdNumber))
                .GroupBy(d => d.OscaIdNumber!.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First());
            var ncscRrnIndex = duplicatePool
                .Where(d => d.NcscRrn.HasValue)
                .GroupBy(d => d.NcscRrn!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            // ✅ Birth-year bucketing — the naive full-pool scan below used to
            // be O(rows × pool) per unmatched row, which is what made large
            // imports (9k+ rows against a large existing pool) time out —
            // and even after bucketing by birth year alone, a program for
            // centenarians has birth years clustered in a fairly narrow
            // range (many people turning 80-100 in the same few years), so
            // a single year's bucket can still hold thousands of candidates
            // — that's what kept stalling the scan partway through. Bucketing
            // by (birth year, last-name initial) as well cuts each bucket
            // down by roughly 26x on top of the year filter.
            char NormalizeInitial(string? name) =>
                string.IsNullOrWhiteSpace(name) ? '#' : char.ToUpperInvariant(name.Trim()[0]);

            var poolByYearAndInitial = duplicatePool
                .GroupBy(c => (Year: c.BirthDate.Year, Initial: NormalizeInitial(c.LastName)))
                .ToDictionary(g => g.Key, g => g.ToList());

            List<DuplicateCheckCandidateDto> GetYearBucketCandidates(int year, string? lastName)
            {
                var initial = NormalizeInitial(lastName);
                var candidates = new List<DuplicateCheckCandidateDto>();
                for (int y = year - 1; y <= year + 1; y++)
                {
                    if (poolByYearAndInitial.TryGetValue((y, initial), out var bucket))
                        candidates.AddRange(bucket);
                }
                return candidates;
            }

            string BuildExistingFullName(DuplicateCheckCandidateDto c) =>
                string.Join(", ", new[] { c.LastName?.Trim(), c.FirstName?.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s))) +
                (string.IsNullOrWhiteSpace(c.MiddleName) ? string.Empty : $" {c.MiddleName.Trim()}");

            void FillExistingInfo(CrossmatchRowDto rowDto, DuplicateCheckCandidateDto c, double score, string reason)
            {
                rowDto.ExistingId = c.Id;
                rowDto.ExistingFullName = BuildExistingFullName(c);
                rowDto.ExistingBirthDate = c.BirthDate;
                rowDto.ExistingOscaId = c.OscaIdNumber;
                rowDto.ExistingMunicipality = municipalityNameByCode.GetValueOrDefault(c.Municipality, string.Empty);
                rowDto.ExistingBarangay = barangayNameByCode.GetValueOrDefault(c.Barangay, string.Empty);
                rowDto.MatchScore = score;
                rowDto.MatchReason = reason;
            }

            // ✅ Same per-component weighting as the whole-grid "Possible
            // Duplicate Records" matcher (BeneficiaryInformationRepository.
            // FindAllPossibleDuplicatesAsync), not a naive concatenated-string
            // comparison. Scoring "ROSARIO ORO" against "ROSARIO DUMANGAS" as
            // one string rewards the shared first name so heavily that a
            // completely different last name barely drags the score down —
            // that's what was producing false "Possible Match" hits at 60-70%
            // for names that only share a first name. Comparing Last/First
            // (and Middle, if both have one) separately, each with its own
            // floor, fixes that.
            (DuplicateCheckCandidateDto Candidate, double Score)? FindBestSoftMatch(
                string? firstName, string? lastName, string? middleName, DateTime birthDate)
            {
                if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                    return null;

                var lastTrim = lastName?.Trim();
                var firstTrim = firstName?.Trim();
                var middleTrim = middleName?.Trim();
                var incomingHasMiddle = !string.IsNullOrWhiteSpace(middleTrim);

                var best = GetYearBucketCandidates(birthDate.Year, lastTrim)
                    .Where(c => Math.Abs((c.BirthDate - birthDate).TotalDays) <= 365)
                    .Select(c =>
                    {
                        var lastNameScore = NameSimilarityHelper.ComputeNameSimilarity(lastTrim, c.LastName?.Trim());
                        var firstNameScore = NameSimilarityHelper.ComputeNameSimilarity(firstTrim, c.FirstName?.Trim());
                        return (Candidate: c, LastNameScore: lastNameScore, FirstNameScore: firstNameScore);
                    })
                    // ── Per-component floor: a weak last-name or first-name
                    // match can't be compensated for by the other field.
                    .Where(x => x.LastNameScore >= 0.60 && x.FirstNameScore >= 0.60)
                    .Select(x =>
                    {
                        var candidateHasMiddle = !string.IsNullOrWhiteSpace(x.Candidate.MiddleName);
                        double score;
                        if (incomingHasMiddle && candidateHasMiddle)
                        {
                            var middleScore = NameSimilarityHelper.ComputeNameSimilarity(middleTrim, x.Candidate.MiddleName?.Trim());
                            score = (x.LastNameScore * 0.40) + (x.FirstNameScore * 0.40) + (middleScore * 0.20);
                        }
                        else
                        {
                            score = (x.LastNameScore * 0.50) + (x.FirstNameScore * 0.50);
                        }
                        return (x.Candidate, Score: score);
                    })
                    .Where(x => x.Score >= 0.75)
                    .OrderByDescending(x => x.Score)
                    .ToList();

                return best.Count == 0 ? null : ((DuplicateCheckCandidateDto Candidate, double Score)?)best[0];
            }

            using var workbook = new XLWorkbook(fileStream);

            if (string.IsNullOrWhiteSpace(sheetName))
                throw new Exception(CommonConstants.PleaseSelectAWorksheet);

            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == sheetName);
            if (worksheet == null)
                throw new Exception($"{CommonConstants.WorksheetNotFound} {sheetName}");

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < 1)
                throw new Exception(CommonConstants.ExcelSheet1DoesNotContain);

            // ✅ NEW — detect columns by header text instead of assuming a fixed
            // template layout. The only hard requirement is that the sheet has
            // a header row with Last Name / First Name / a birth date column
            // somewhere — the rest (location, IDs, etc.) are read wherever
            // they're found and simply left blank if missing. This means the
            // user no longer has to re-arrange their file to match our
            // official template just to run a crossmatch.
            var (headerRowNumber, columns) = DetectCrossmatchColumns(worksheet);
            var firstDataRowNumber = headerRowNumber + 1;
            var maxDetectedColumn = columns.Values.DefaultIfEmpty(1).Max();

            if (lastRow < firstDataRowNumber)
                throw new Exception(CommonConstants.ExcelSheet1DoesNotContain);

            string CellText(IXLRow row, string field) =>
                columns.TryGetValue(field, out var col) ? row.Cell(col).GetFormattedString().Trim() : string.Empty;

            // ✅ Cheap pre-count so the progress bar has a meaningful total
            // from the very first tick instead of growing as rows are found.
            var estimatedTotalRows = 0;
            for (int r = firstDataRowNumber; r <= lastRow; r++)
            {
                if (!worksheet.Row(r).Cells(1, maxDetectedColumn).All(c => string.IsNullOrWhiteSpace(c.GetFormattedString())))
                    estimatedTotalRows++;
            }
            onProgress?.Invoke(0, estimatedTotalRows);

            for (int rowNumber = firstDataRowNumber; rowNumber <= lastRow; rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = worksheet.Row(rowNumber);

                if (row.Cells(1, maxDetectedColumn).All(c => string.IsNullOrWhiteSpace(c.GetFormattedString())))
                    continue;

                result.TotalRows++;
                onProgress?.Invoke(result.TotalRows, estimatedTotalRows);

                var rowDto = new CrossmatchRowDto { RowNumber = rowNumber };

                try
                {
                    var dateEndorsedRaw = CellText(row, "DateEndorsed");
                    var batchCode = CellText(row, "BatchCode");
                    var oscaIdNumber = CellText(row, "OscaId");
                    var oscaIdDateIssuedRaw = CellText(row, "OscaIdDateIssued");
                    var ncscRrnRaw = CellText(row, "NcscRrn");
                    var lastName = CellText(row, "LastName");
                    var firstName = CellText(row, "FirstName");
                    var middleName = CellText(row, "MiddleName");
                    var extensionName = CellText(row, "Extension");
                    var birthDateCombinedRaw = CellText(row, "BirthDate");
                    var birthMonthRaw = CellText(row, "BirthMonth");
                    var birthDayRaw = CellText(row, "BirthDay");
                    var birthYearRaw = CellText(row, "BirthYear");
                    var sexRaw = CellText(row, "Sex");
                    var citizenshipRaw = CellText(row, "Citizenship");
                    var regionName = CellText(row, "Region");
                    var provinceName = CellText(row, "Province");
                    var municipalityName = CellText(row, "Municipality");
                    var barangayName = CellText(row, "Barangay");
                    var contactNumber = CellText(row, "ContactNumber");
                    var dateOfDeathRaw = CellText(row, "DateOfDeath");
                    var dateAppliedRaw = CellText(row, "DateApplied");
                    var isIndigenousPeopleRaw = CellText(row, "IsIndigenousPeople");
                    var isPersonWithDisabilityRaw = CellText(row, "IsPersonWithDisability");

                    rowDto.BatchCode = NullIfEmpty(batchCode);
                    rowDto.OscaIdNumber = NullIfEmpty(oscaIdNumber);
                    rowDto.LastName = NullIfEmpty(lastName);
                    rowDto.FirstName = NullIfEmpty(firstName);
                    rowDto.MiddleName = NullIfEmpty(middleName);
                    rowDto.Extension = NullIfEmpty(extensionName);
                    rowDto.ContactNumber = NullIfEmpty(contactNumber);
                    rowDto.Sex = MapSex(sexRaw);
                    rowDto.Citizenship = MapCitizenship(citizenshipRaw);
                    rowDto.IsIndigenousPeople = MapIndigenousPeople(isIndigenousPeopleRaw);
                    rowDto.IsPersonWithDisability = MapIsPersonWithDisability(isPersonWithDisabilityRaw);

                    if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                        rowDto.ParseErrors.Add("Name is required (First Name and Last Name are both blank).");

                    // ✅ Supports either a single combined "Birth Date" column
                    // or separate Month/Day/Year columns, whichever the
                    // uploaded sheet actually has.
                    DateTime birthDate = DateTime.MinValue;
                    var hasBirthDate = !string.IsNullOrWhiteSpace(birthDateCombinedRaw)
                        ? TryParseExcelDate(birthDateCombinedRaw, out birthDate)
                        : TryParseExcelDate($"{birthMonthRaw} {birthDayRaw} {birthYearRaw}", out birthDate);

                    if (!hasBirthDate)
                    {
                        var reason = !string.IsNullOrWhiteSpace(birthDateCombinedRaw)
                            ? DiagnoseInvalidBirthDate(birthDateCombinedRaw, null, null, null)
                            : DiagnoseInvalidBirthDate(null, birthMonthRaw, birthDayRaw, birthYearRaw);
                        rowDto.ParseErrors.Add(reason);
                    }
                    else
                        rowDto.BirthDate = birthDate;

                    // ✅ NCSC RRN is a matching *signal*, not a required field —
                    // an unparseable value is simply left blank rather than
                    // failing the row. Only Name + Birth Date (and, loosely,
                    // location) actually matter for crossmatch classification.
                    if (!string.IsNullOrWhiteSpace(ncscRrnRaw) && int.TryParse(ncscRrnRaw, out var parsedRrn))
                        rowDto.NcscRrn = parsedRrn;

                    if (ParseNullableDate(oscaIdDateIssuedRaw) is { } oscaIdDateIssued)
                        rowDto.OscaIdDateIssued = oscaIdDateIssued;
                    if (ParseNullableDate(dateAppliedRaw) is { } dateApplied)
                        rowDto.DateApplied = dateApplied;
                    if (ParseNullableDate(dateEndorsedRaw) is { } dateEndorsed)
                        rowDto.DateEndorsed = dateEndorsed;
                    if (ParseNullableDate(dateOfDeathRaw) is { } dateOfDeath)
                    {
                        rowDto.DateOfDeath = dateOfDeath;
                        rowDto.IsDeceased = true;
                    }

                    var region = FindBestNameMatch(regions, x => x.Name, regionName);
                    var province = FindBestNameMatch(provinces, x => x.Name, provinceName);
                    var municipalitiesInProvince = province != null
                        ? municipalities.Where(m => m.PsgcCodeProvince == province.PsgcCodeProvince).ToList()
                        : municipalities;
                    var municipality = FindBestNameMatch(municipalitiesInProvince, x => x.Name, municipalityName);
                    var barangaysInMunicipality = municipality != null
                        ? barangays.Where(b => b.PsgcCodeMunicipality == municipality.PsgcCodeMunicipality).ToList()
                        : barangays;
                    var barangay = FindBestNameMatch(barangaysInMunicipality, x => x.Name, barangayName);

                    // ✅ Location that doesn't resolve to a known PSGC entry no
                    // longer blocks the row — crossmatch only strictly needs
                    // Name + Birth Date to classify New/Possible/Existing.
                    // Unresolved location is shown as raw text instead, since
                    // it's still useful context even if it can't be coded.

                    rowDto.PsgcCodeRegion = region?.PsgcCodeRegion ?? 0;
                    rowDto.RegionName = region?.Name ?? regionName;
                    rowDto.PsgcCodeProvince = province?.PsgcCodeProvince ?? 0;
                    rowDto.ProvinceName = province?.Name ?? provinceName;
                    rowDto.PsgcCodeMunicipality = municipality?.PsgcCodeMunicipality ?? 0;
                    rowDto.MunicipalityName = municipality?.Name ?? municipalityName;
                    rowDto.PsgcCodeBarangay = barangay?.PsgcCodeBarangay ?? 0;
                    rowDto.BarangayName = barangay?.Name ?? barangayName;

                    if (rowDto.ParseErrors.Any())
                    {
                        rowDto.Status = CrossmatchStatus.Error;
                        result.ErrorCount++;
                        result.Rows.Add(rowDto);
                        continue;
                    }

                    // ── Classification: exact ID match > exact name+birthdate > fuzzy > new ──
                    if (rowDto.NcscRrn.HasValue && ncscRrnIndex.TryGetValue(rowDto.NcscRrn.Value, out var byRrn))
                    {
                        rowDto.Status = CrossmatchStatus.Existing;
                        FillExistingInfo(rowDto, byRrn, 1.0, "NCSC RRN match");
                    }
                    else if (!string.IsNullOrWhiteSpace(oscaIdNumber) &&
                             oscaIdIndex.TryGetValue(oscaIdNumber.Trim().ToUpperInvariant(), out var byOsca))
                    {
                        rowDto.Status = CrossmatchStatus.Existing;
                        FillExistingInfo(rowDto, byOsca, 1.0, "OSCA ID match");
                    }
                    else if (exactKeyIndex.TryGetValue(BuildExactDupKey(lastName, firstName, middleName, birthDate), out var exactMatch))
                    {
                        rowDto.Status = CrossmatchStatus.Existing;
                        FillExistingInfo(rowDto, exactMatch, 1.0, "Name + birthdate match");
                    }
                    else if (FindBestSoftMatch(firstName, lastName, middleName, birthDate) is { } soft)
                    {
                        rowDto.Status = CrossmatchStatus.Possible;
                        FillExistingInfo(rowDto, soft.Candidate, soft.Score, "Similar name + birthdate");
                    }
                    else
                    {
                        rowDto.Status = CrossmatchStatus.New;
                    }

                    switch (rowDto.Status)
                    {
                        case CrossmatchStatus.New: result.NewCount++; break;
                        case CrossmatchStatus.Possible: result.PossibleCount++; break;
                        case CrossmatchStatus.Existing: result.ExistingCount++; break;
                    }

                    result.Rows.Add(rowDto);
                }
                catch (Exception ex)
                {
                    rowDto.Status = CrossmatchStatus.Error;
                    rowDto.ParseErrors.Add(ex.Message);
                    result.ErrorCount++;
                    result.Rows.Add(rowDto);
                }
            }

            return result;
        }

        // ✅ NEW — explains *why* a birth date failed to parse instead of a
        // generic "invalid or incomplete", e.g. calling out a Feb 29 that
        // fell on a non-leap year, or a day that doesn't exist in that month.
        private static readonly string[] MonthNames =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        private static string DiagnoseInvalidBirthDate(string? combinedRaw, string? monthRaw, string? dayRaw, string? yearRaw)
        {
            if (combinedRaw is not null)
                return $"Birth Date '{combinedRaw}' could not be recognized as a valid date.";

            if (string.IsNullOrWhiteSpace(monthRaw) && string.IsNullOrWhiteSpace(dayRaw) && string.IsNullOrWhiteSpace(yearRaw))
                return "Birth Date is missing (Month, Day, and Year are all blank).";
            if (string.IsNullOrWhiteSpace(yearRaw))
                return "Birth Year is missing.";
            if (string.IsNullOrWhiteSpace(monthRaw))
                return "Birth Month is missing.";
            if (string.IsNullOrWhiteSpace(dayRaw))
                return "Birth Day is missing.";

            int? month = TryParseMonth(monthRaw);
            if (month is null)
                return $"'{monthRaw}' is not a recognizable month.";

            if (!int.TryParse(yearRaw.Trim(), out var year) || year < 1900 || year > DateTime.Today.Year)
                return $"'{yearRaw}' is not a valid birth year.";

            if (!int.TryParse(dayRaw.Trim(), out var day) || day < 1)
                return $"'{dayRaw}' is not a valid day.";

            var daysInMonth = DateTime.DaysInMonth(year, month.Value);
            if (day > daysInMonth)
            {
                if (month.Value == 2 && day == 29)
                    return $"February 29, {year} is not valid — {year} is not a leap year.";
                return $"{MonthNames[month.Value - 1]} only has {daysInMonth} days, so day {day} does not exist.";
            }

            return $"'{monthRaw} {dayRaw} {yearRaw}' could not be parsed as a date.";
        }

        private static int? TryParseMonth(string monthRaw)
        {
            var trimmed = monthRaw.Trim();
            if (int.TryParse(trimmed, out var num) && num is >= 1 and <= 12)
                return num;

            for (int i = 0; i < MonthNames.Length; i++)
            {
                if (MonthNames[i].Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                    MonthNames[i].StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
                    return i + 1;
            }
            return null;
        }

        // ✅ NEW — header-based column detection for crossmatch, so the user
        // isn't forced to re-arrange an uploaded sheet to match our official
        // import template. Scans the first 30 rows for the one that looks
        // most like a header row (matching the most known field aliases),
        // requiring at minimum Last Name + First Name + a birth date column
        // to accept it — those, plus location, are the fields that actually
        // matter for a crossmatch. Everything else is read wherever it's
        // found and simply left blank if the sheet doesn't have it.
        private static readonly (string Field, string[] Aliases)[] CrossmatchColumnAliases = new[]
        {
            ("LastName", new[] { "LAST NAME", "SURNAME" }),
            ("FirstName", new[] { "FIRST NAME", "GIVEN NAME" }),
            ("MiddleName", new[] { "MIDDLE NAME" }),
            ("Extension", new[] { "EXTENSION NAME", "EXTENSION", "SUFFIX" }),
            ("BirthDate", new[] { "DATE OF BIRTH", "BIRTH DATE", "BIRTHDATE" }),
            ("BirthMonth", new[] { "BIRTH MONTH", "MONTH" }),
            ("BirthDay", new[] { "BIRTH DAY", "DAY" }),
            ("BirthYear", new[] { "BIRTH YEAR", "YEAR" }),
            ("OscaId", new[] { "OSCA ID NUMBER", "OSCA ID NO", "OSCA ID", "OSCA NUMBER", "OSCA NO" }),
            ("OscaIdDateIssued", new[] { "OSCA ID DATE ISSUED", "DATE ISSUED" }),
            ("NcscRrn", new[] { "NCSC REGISTRATION REFERENCE NUMBER", "NCSC RRN", "RRN" }),
            ("Sex", new[] { "SEX", "GENDER" }),
            ("Citizenship", new[] { "CITIZENSHIP" }),
            ("Region", new[] { "REGION" }),
            ("Province", new[] { "PROVINCE" }),
            ("Municipality", new[] { "MUNICIPALITY/CITY", "CITY/MUNICIPALITY", "MUNICIPALITY", "CITY" }),
            ("Barangay", new[] { "BARANGAY" }),
            ("ContactNumber", new[] { "CONTACT NUMBER", "CONTACT NO", "MOBILE NUMBER", "PHONE NUMBER", "PHONE" }),
            ("DateOfDeath", new[] { "DATE OF DEATH" }),
            ("DateApplied", new[] { "DATE APPLIED" }),
            ("DateEndorsed", new[] { "DATE ENDORSED" }),
            ("BatchCode", new[] { "BATCH CODE" }),
            ("IsIndigenousPeople", new[] { "INDIGENOUS PERSON", "INDIGENOUS" }),
            ("IsPersonWithDisability", new[] { "PERSON WITH DISABILITY", "PWD" }),
        };

        private static (int HeaderRow, Dictionary<string, int> Columns) DetectCrossmatchColumns(IXLWorksheet worksheet)
        {
            var lastScanRow = Math.Min(worksheet.LastRowUsed()?.RowNumber() ?? 0, 30);

            var bestRow = -1;
            var bestMap = new Dictionary<string, int>();
            var bestScore = -1;

            for (int r = 1; r <= lastScanRow; r++)
            {
                var row = worksheet.Row(r);
                var lastCol = row.LastCellUsed()?.Address.ColumnNumber ?? 0;
                if (lastCol == 0) continue;

                var map = new Dictionary<string, int>();
                for (int c = 1; c <= lastCol; c++)
                {
                    var text = System.Text.RegularExpressions.Regex.Replace(
                        row.Cell(c).GetFormattedString().Trim().ToUpperInvariant(), @"\s+", " ");
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    foreach (var (field, aliases) in CrossmatchColumnAliases)
                    {
                        if (map.ContainsKey(field)) continue;
                        if (aliases.Any(alias => text == alias || text.Contains(alias)))
                            map[field] = c;
                    }
                }

                var hasCore = map.ContainsKey("LastName") && map.ContainsKey("FirstName") &&
                              (map.ContainsKey("BirthDate") || map.ContainsKey("BirthYear"));

                if (hasCore && map.Count > bestScore)
                {
                    bestScore = map.Count;
                    bestRow = r;
                    bestMap = map;
                }
            }

            if (bestRow < 0)
            {
                throw new Exception(
                    "Could not find a header row with recognizable Last Name, First Name, and Birth Date columns. " +
                    "Make sure the sheet has a header row labeling these columns (any column order is fine).");
            }

            return (bestRow, bestMap);
        }

        public async Task<BeneficiaryImportResultDto> ConfirmImportAsync(
     Stream fileStream,
     string fileName,
     string sheetName,
     string userName,
     HashSet<int> skipRows,
     int? quarter,      // ✅ new
     string? batch,     // ✅ new
     int? refYear,
     Dictionary<int, Dictionary<string, string>>? corrections = null)
        {
            if (fileStream == null || !fileStream.CanRead)
                throw new Exception(CommonConstants.InvalidExcelUploaded);

            if (string.IsNullOrWhiteSpace(fileName))
                throw new Exception(CommonConstants.InvalidExcelFile);

            var extension = Path.GetExtension(fileName);
            if (!string.Equals(extension, CommonConstants.ExcelFileExtension, StringComparison.OrdinalIgnoreCase))
                throw new Exception(CommonConstants.OnlyExcelFilesAllowed);

            var result = new BeneficiaryImportResultDto();
            var beneficiariesToImport = new List<BeneficiaryInformation>();
            var importedPaymentHistoryEntries = new List<BeneficiaryPaymentHistory>(); // ✅ NEW
            // (newId, existingId, newFullName) — rows imported anyway despite being an
            // exact DB match; records a BeneficiaryDuplicateHistory entry and logs
            // both sides once the import itself has been saved.
            var knownDuplicatePairs = new List<(Guid NewId, Guid ExistingId, string NewFullName)>();

            var regions = await _regionRepository.GetAllAsync();
            var provinces = (await _provinceRepository.GetAllProvinceAsync())
                .GroupBy(x => x.Name!.Trim().ToUpperInvariant())
                .Select(g => g.OrderBy(x => x.Id).First())
                .ToList();
            var municipalities = (await _municipalityRepository.GetAllMunicipalityAsync()).ToList();
            var barangays = (await _barangayRepository.GetBarangaysAsync()).ToList();

            // ✅ NEW — same fix as PreviewImportAsync: one flat query for the whole
            // import instead of one ExistsDuplicateAsync DB call per row.
            var duplicatePool = await _repo.GetDuplicateCheckPoolAsync();
            var exactDupKeys = duplicatePool
                .Select(d => BuildExactDupKey(d.LastName, d.FirstName, d.MiddleName, d.BirthDate))
                .ToHashSet();

            using var workbook = new XLWorkbook(fileStream);

            if (string.IsNullOrWhiteSpace(sheetName))
                throw new Exception(CommonConstants.PleaseSelectAWorksheet);

            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == sheetName);
            if (worksheet == null)
                throw new Exception($"{CommonConstants.WorksheetNotFound} {sheetName}");

            const int firstDataRowNumber = 12;

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < firstDataRowNumber)
                throw new Exception(CommonConstants.ExcelSheet1DoesNotContain);

            var uploadedRowKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ✅ Same correction mechanism as PreviewImportAsync — applied here
            // too since this is the method that actually performs the save,
            // and must honor whatever the user accepted during preview.
            string ApplyCorrection(int rowNumber, string field, string rawValue) =>
                corrections != null &&
                corrections.TryGetValue(rowNumber, out var fieldMap) &&
                fieldMap.TryGetValue(field, out var correctedValue)
                    ? correctedValue
                    : rawValue;

            for (int rowNumber = firstDataRowNumber; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);

                if (row.Cells(1, 30).All(c => string.IsNullOrWhiteSpace(c.GetFormattedString())))
                    continue;

                result.TotalRows++;

                // ── Skip rows user chose to skip from soft duplicate modal ────────
                if (skipRows.Contains(rowNumber))
                {
                    result.SkippedDuplicateCount++;
                    continue;
                }

                try
                {
                    // ── NEW COLUMN MAP ────────────────────────────────────────────
                    var dateEndorsed = row.Cell(1).GetFormattedString().Trim();
                    var batchCode = row.Cell(2).GetFormattedString().Trim();
                    // col 3 = NO. (ignored)
                    var oscaIdNumber = row.Cell(4).GetFormattedString().Trim();
                    var oscaIdDateIssued = row.Cell(5).GetFormattedString().Trim();
                    var ncscRrnRaw = ApplyCorrection(rowNumber, "NcscRrn", row.Cell(6).GetFormattedString().Trim());
                    var lastName = row.Cell(7).GetFormattedString().Trim();
                    var firstName = ApplyCorrection(rowNumber, "FirstName", row.Cell(8).GetFormattedString().Trim());
                    var middleName = row.Cell(9).GetFormattedString().Trim();
                    var extensionName = row.Cell(10).GetFormattedString().Trim();
                    var birthMonthRaw = row.Cell(11).GetFormattedString().Trim();
                    var birthDayRaw = row.Cell(12).GetFormattedString().Trim();
                    var birthYearRaw = row.Cell(13).GetFormattedString().Trim();
                    // col 14 = AGE (ignored)
                    var sexRaw = row.Cell(15).GetFormattedString().Trim();
                    var citizenshipRaw = ApplyCorrection(rowNumber, "Citizenship", row.Cell(16).GetFormattedString().Trim());
                    var regionName = ApplyCorrection(rowNumber, "Region", row.Cell(17).GetFormattedString().Trim());
                    var provinceName = ApplyCorrection(rowNumber, "Province", row.Cell(18).GetFormattedString().Trim());
                    var municipalityName = ApplyCorrection(rowNumber, "Municipality", row.Cell(19).GetFormattedString().Trim());
                    var barangayName = ApplyCorrection(rowNumber, "Barangay", row.Cell(20).GetFormattedString().Trim());
                    var contactNumber = row.Cell(21).GetFormattedString().Trim();
                    var dateOfDeath = row.Cell(22).GetFormattedString().Trim();
                    var dateApplied = row.Cell(23).GetFormattedString().Trim();
                    var isIndigenousPeopleRaw = row.Cell(24).GetFormattedString().Trim();
                    var isPersonWithDisabilityRaw = row.Cell(25).GetFormattedString().Trim();
                    var complianceRaw = row.Cell(26).GetFormattedString().Trim();
                    var validator = row.Cell(27).GetFormattedString().Trim();
                    var validationDateRaw = row.Cell(28).GetFormattedString().Trim();
                    var isEligibleRaw = ApplyCorrection(rowNumber, "NcscAssessment", row.Cell(29).GetFormattedString().Trim());
                    var paymentStatusRaw = row.Cell(30).GetFormattedString().Trim();

                    bool rowHasError = false;

                    // ── First Name ────────────────────────────────────────────────
                    if (string.IsNullOrWhiteSpace(firstName))
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.FirstName.ToTitleCase(),
                            Message = CommonConstants.FirstNameRequired,
                            RawValue = firstName
                        });
                        rowHasError = true;
                    }

                    // ── Birth Date ────────────────────────────────────────────────
                    var birthDateRaw = ApplyCorrection(rowNumber, "BirthDate", $"{birthMonthRaw} {birthDayRaw} {birthYearRaw}");
                    if (!TryParseExcelDate(birthDateRaw, out var birthDate))
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.BirthDate.ToTitleCase(),
                            Message = CommonConstants.InvalidBirthDate,
                            RawValue = birthDateRaw
                        });
                        rowHasError = true;
                        birthDate = DateTime.MinValue;
                    }

                    #region Parsed Dates

                    var parsedOscaIdDateIssued = ParseFlexibleDate(oscaIdDateIssued);
                    if (!string.IsNullOrWhiteSpace(oscaIdDateIssued) && parsedOscaIdDateIssued == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.OscaIdDateIssued.ToTitleCase(),
                            Message = CommonConstants.InvalidDateFormat,
                            RawValue = oscaIdDateIssued
                        });
                        rowHasError = true;
                    }

                    var parsedDateEndorsed = ParseFlexibleDate(dateEndorsed);
                    if (!string.IsNullOrWhiteSpace(dateEndorsed) && parsedDateEndorsed == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.DateEndorsed.ToTitleCase(),
                            Message = CommonConstants.InvalidDateFormat,
                            RawValue = dateEndorsed
                        });
                        rowHasError = true;
                    }

                    var parsedDateApplied = ParseFlexibleDate(dateApplied);
                    if (!string.IsNullOrWhiteSpace(dateApplied) && parsedDateApplied == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.DateOfApplication,
                            Message = CommonConstants.InvalidDateFormat,
                            RawValue = dateApplied
                        });
                        rowHasError = true;
                    }

                    var parsedDateAppliedFromCol23 = ParseFlexibleDate(dateApplied);
                    if (!string.IsNullOrWhiteSpace(dateApplied) && parsedDateAppliedFromCol23 == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = "Date Applied (col 24)",
                            Message = CommonConstants.InvalidDateFormat,
                            RawValue = dateApplied
                        });
                        rowHasError = true;
                    }

                    var parsedDateofDeath = ParseFlexibleDate(dateOfDeath);
                    if (!string.IsNullOrWhiteSpace(dateOfDeath) && parsedDateofDeath == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.DateOfDeath,
                            Message = CommonConstants.InvalidDateFormat,
                            RawValue = dateOfDeath
                        });
                        rowHasError = true;
                    }

                    #endregion

                    // ── NCSC RRN ──────────────────────────────────────────────────
                    int? ncscRrn = null;
                    if (!string.IsNullOrWhiteSpace(ncscRrnRaw))
                    {
                        if (int.TryParse(ncscRrnRaw, out var parsedRrn))
                            ncscRrn = parsedRrn;
                        else
                        {
                            result.Errors.Add(new BeneficiaryImportErrorDto
                            {
                                RowNumber = rowNumber,
                                Field = CommonConstants.NcscRrn,
                                Message = CommonConstants.InvalidRrn,
                                RawValue = ncscRrnRaw,
                                Suggestion = CommonConstants.RemoveSpecialCharactersFromName
                            });
                            rowHasError = true;
                        }
                    }

                    // ── Citizenship guard ─────────────────────────────────────────
                    var mappedCitizenship = MapCitizenship(citizenshipRaw);
                    if (!string.IsNullOrWhiteSpace(citizenshipRaw) && mappedCitizenship == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Citizenship.ToTitleCase(),
                            Message = "Invalid citizenship value. Accepted: FILIPINO or DUAL CITIZENSHIP.",
                            RawValue = citizenshipRaw,
                            Suggestion = "Use FILIPINO or DUAL CITIZENSHIP"
                        });
                        rowHasError = true;
                    }
                    // ── Payment Status guard (col 31) ─────────────────────────────────────
                    var mappedPaymentStatus = MapPaymentStatus(paymentStatusRaw);
                    if (!string.IsNullOrWhiteSpace(paymentStatusRaw) && mappedPaymentStatus == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.PaymentStatus.ToTitleCase(),
                            Message = "Invalid payment status. Accepted: PAID, UNPAID, PENDING, or N/A.",
                            RawValue = paymentStatusRaw,
                            Suggestion = "Use PAID, UNPAID, PENDING, or leave blank"
                        });
                        rowHasError = true;
                    }

                    // ── Region ────────────────────────────────────────────────────
                    var region = FindBestNameMatch(regions, x => x.Name, regionName);

                    if (string.IsNullOrWhiteSpace(regionName))
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Region,
                            Message = "Region is required and cannot be left blank.",
                            RawValue = regionName
                        });
                        rowHasError = true;
                    }
                    else if (region == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Region,
                            Message = CommonConstants.RegionNotFound,
                            RawValue = regionName,
                            Suggestion = GetSuggestedName(regions, x => x.Name, regionName) is { } sr
                                ? $"{CommonConstants.PossibleMatch} '{sr}'"
                                : CommonConstants.CheckSpelling
                        });
                        rowHasError = true;
                    }

                    if (region == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Region,
                            Message = CommonConstants.RegionNotFound,
                            RawValue = regionName,
                            Suggestion = GetSuggestedName(regions, x => x.Name, regionName) is { } sr
                                ? $"{CommonConstants.PossibleMatch} '{sr}'"
                                : CommonConstants.CheckSpelling
                        });
                        rowHasError = true;
                    }

                    // ── Province ──────────────────────────────────────────────────
                    var province = FindBestNameMatch(provinces, x => x.Name, provinceName);
                    if (province == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Province.ToTitleCase(),
                            Message = CommonConstants.ProvinceNotFound,
                            RawValue = provinceName,
                            Suggestion = GetSuggestedName(provinces, x => x.Name, provinceName) is { } sp
                                ? $"{CommonConstants.PossibleMatch} '{sp}'"
                                : CommonConstants.CheckSpelling
                        });
                        rowHasError = true;
                    }

                    // ── Municipality ──────────────────────────────────────────────
                    var municipalitiesInProvince = province != null
                        ? municipalities.Where(m => m.PsgcCodeProvince == province.PsgcCodeProvince).ToList()
                        : municipalities;

                    var municipality = FindBestNameMatch(municipalitiesInProvince, x => x.Name, municipalityName);
                    if (municipality == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Municipality,
                            Message = CommonConstants.MunicipalityNotFound,
                            RawValue = municipalityName,
                            Suggestion = GetSuggestedName(municipalitiesInProvince, x => x.Name, municipalityName) is { } sm
                                ? $"{CommonConstants.PossibleMatch} '{sm}'"
                                : CommonConstants.CheckSpelling
                        });
                        rowHasError = true;
                    }

                    // ── Barangay ──────────────────────────────────────────────────
                    var barangaysInMunicipality = municipality != null
                        ? barangays.Where(b => b.PsgcCodeMunicipality == municipality.PsgcCodeMunicipality).ToList()
                        : barangays;

                    var barangay = FindBestNameMatch(barangaysInMunicipality, x => x.Name, barangayName);
                    if (barangay == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Barangay.ToTitleCase(),
                            Message = CommonConstants.BarangayNotFound,
                            RawValue = barangayName,
                            Suggestion = GetSuggestedName(barangaysInMunicipality, x => x.Name, barangayName) is { } sb
                                ? $"{CommonConstants.PossibleMatch} '{sb}'"
                                : CommonConstants.CheckSpelling
                        });
                        rowHasError = true;
                    }

                    // ── NCSC Assessment ───────────────────────────────────────────
                    var mappedEligibility = MapEligibility(isEligibleRaw);
                    if (mappedEligibility == null)
                    {
                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.NcscAssessment.ToTitleCase(),
                            Message = CommonConstants.NcscAssessmentNotFound,
                            RawValue = isEligibleRaw
                        });
                        rowHasError = true;
                    }

                    // ── Mapped fields ─────────────────────────────────────────────
                    var mappedIsIndigenousPeople = MapIndigenousPeople(isIndigenousPeopleRaw);
                    var mappedIsPersonWithDisability = MapIsPersonWithDisability(isPersonWithDisabilityRaw);

                    // ── In-file duplicate check ───────────────────────────────────
                    var duplicateKey = string.Join("|",
                        (lastName ?? string.Empty).Trim().ToLower(),
                        (firstName ?? string.Empty).Trim().ToLower(),
                        (middleName ?? string.Empty).Trim().ToLower(),
                        birthDate == DateTime.MinValue ? "" : birthDate.ToFullDate());

                    if (!string.IsNullOrWhiteSpace(firstName) && birthDate != DateTime.MinValue)
                    {
                        if (!uploadedRowKeys.Add(duplicateKey))
                        {
                            result.Errors.Add(new BeneficiaryImportErrorDto
                            {
                                RowNumber = rowNumber,
                                Field = CommonConstants.Duplicate,
                                Message = CommonConstants.DuplicateRecordFound,
                                RawValue = $"{lastName}, {firstName}"
                            });
                            rowHasError = true;
                        }
                    }

                    // ── Exact (100%) DB duplicate check ────────────────────────────
                    // ✅ CHANGED — per office policy this no longer hard-blocks the
                    // row. The user already saw this exact match in the review popup
                    // (PreviewImportAsync surfaces it as a MatchScore=1.0 candidate)
                    // and chose NOT to skip it — that IS the acknowledgement, same
                    // spirit as AcknowledgeExactDuplicate in the manual Create flow.
                    // Record which existing row it matches so the entity below can be
                    // flagged as a Known Duplicate instead of rejected.
                    Guid? exactDuplicateOfId = null;
                    if (!rowHasError)
                    {
                        var exactKey = BuildExactDupKey(lastName, firstName, middleName, birthDate);
                        exactDuplicateOfId = duplicatePool
                            .FirstOrDefault(d => BuildExactDupKey(d.LastName, d.FirstName, d.MiddleName, d.BirthDate) == exactKey)
                            ?.Id;
                    }

                    if (rowHasError)
                        continue;

                    // ── Build entity ──────────────────────────────────────────────
                    var effectiveDateApplied = parsedDateAppliedFromCol23 ?? parsedDateApplied;

                    var beneficiary = new BeneficiaryInformation
                    {
                        Id = Guid.NewGuid(),
                        Quarter = quarter,        // ✅
                        Batch = batch?.Trim(),   // ✅
                        RefYear = refYear,        // ✅
                        RefCode = (quarter.HasValue &&
                             !string.IsNullOrWhiteSpace(batch) &&
                             refYear.HasValue)
                             ? RegionRomanNumeralHelper.GenerateRefCode()
                            : null,
                        DateApplied = effectiveDateApplied,
                        DateEndorsed = parsedDateEndorsed,
                        BatchCode = NullIfEmpty(batchCode),
                        OscaIdNumber = NullIfEmpty(oscaIdNumber),
                        OscaIdDateIssued = parsedOscaIdDateIssued,
                        NcscRrn = ncscRrn,
                        LastName = NullIfEmpty(lastName),
                        FirstName = firstName!.Trim(),
                        MiddleName = NullIfEmpty(middleName),
                        Extension = NullIfEmpty(extensionName),
                        BirthDate = birthDate,
                        PhoneNumbers = ParsePhoneNumbersFromImport(contactNumber),
                        Sex = MapSex(sexRaw),
                        IsIndigenousPeople = mappedIsIndigenousPeople,
                        IsPersonWithDisability = mappedIsPersonWithDisability,
                        CivilStatus = null,
                        Citizenship = mappedCitizenship,
                        Region = region!.PsgcCodeRegion,
                        Province = province!.PsgcCodeProvince,
                        Municipality = municipality!.PsgcCodeMunicipality,
                        Barangay = barangay!.PsgcCodeBarangay,
                        IsCompliant = MapCompliance(complianceRaw),
                        Validator = string.IsNullOrWhiteSpace(validator)
                                                    ? CommonConstants.None
                                                    : validator.Trim(),
                        ValidationDate = ParseNullableDate(validationDateRaw) ?? DateTime.Today,
                        DateOfDeath = parsedDateofDeath,
                        IsDeceased = parsedDateofDeath.HasValue,
                        IsEligible = mappedEligibility!.Value,
                        AssessmentRemarks = null,
                        RemarkCategory = null,
                        Remarks = null,
                        DateAdded = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    var importedPaymentHistory = new BeneficiaryPaymentHistory
                    {
                        Id = Guid.NewGuid(),
                        BeneficiaryInformationId = beneficiary.Id,
                        PayrollQuarter = null,
                        FiscalYear = null,
                        PaymentStatus = mappedPaymentStatus ?? 3,
                        ModeOfPayment = 0,
                        PaymentDate = null,
                        Remarks = "Set from Excel import.",
                        DateCreated = DateTime.UtcNow,
                        CreatedBy = userName
                    };

                    beneficiary.CurrentPaymentHistoryId = importedPaymentHistory.Id;
                    beneficiary.PayrollQuarter = importedPaymentHistory.PayrollQuarter;
                    beneficiary.FiscalYear = importedPaymentHistory.FiscalYear;
                    beneficiary.PaymentStatus = importedPaymentHistory.PaymentStatus;
                    beneficiary.ModeOfPayment = importedPaymentHistory.ModeOfPayment;
                    beneficiary.PaymentDate = importedPaymentHistory.PaymentDate;

                    beneficiariesToImport.Add(beneficiary);
                    importedPaymentHistoryEntries.Add(importedPaymentHistory);

                    if (exactDuplicateOfId.HasValue)
                        knownDuplicatePairs.Add((beneficiary.Id, exactDuplicateOfId.Value, $"{beneficiary.LastName}, {beneficiary.FirstName}"));
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new BeneficiaryImportErrorDto
                    {
                        RowNumber = rowNumber,
                        Field = CommonConstants.General,
                        Message = ex.Message,
                        RawValue = null
                    });
                }
            }

            result.ErrorCount = result.Errors.Count;
            result.ValidRows = beneficiariesToImport.Count;

            if (result.ErrorCount > 0)
            {
                result.ImportedCount = 0;
                result.SkippedDuplicateCount += result.Errors.Count(x => x.Field == CommonConstants.Duplicate);
                return result;
            }

            foreach (var beneficiary in beneficiariesToImport)
            {
                await _repo.AddAsync(beneficiary);
                await AddLogAsync(
                    beneficiary.Id,
                    $"{CommonConstants.ImportedBeneficiaryFromExcel} {beneficiary.LastName}, {beneficiary.FirstName}",
                    userName);
            }

            foreach (var historyEntry in importedPaymentHistoryEntries)
            {
                await _repo.AddPaymentHistoryEntryAsync(historyEntry);
            }

            // Known Duplicate — recorded in BeneficiaryDuplicateHistory for each
            // pair imported anyway despite being an exact match, plus a log
            // entry on each side.
            foreach (var pair in knownDuplicatePairs)
            {
                await _repo.AddDuplicateHistoryAsync(new BeneficiaryDuplicateHistory
                {
                    Id = Guid.NewGuid(),
                    BeneficiaryInformationId = pair.NewId,
                    DuplicateOfId = pair.ExistingId,
                    Source = "Import",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName
                });

                await AddLogAsync(
                    pair.NewId,
                    $"Imported as a Known Duplicate of an existing record (exact match on name and birth date) — imported anyway by {userName}",
                    userName);

                await AddLogAsync(
                    pair.ExistingId,
                    $"Flagged as having a Known Duplicate — {pair.NewFullName} was imported via Excel as an exact match, imported anyway by {userName}",
                    userName);
            }

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
            result.ImportedCount = beneficiariesToImport.Count;
            result.ImportedIds = beneficiariesToImport.Select(x => x.Id).ToList();
            return result;
        }
        public async Task<List<string>> GetExcelSheetNamesAsync(Stream fileStream, string fileName)
        {
            if (fileStream == null || !fileStream.CanRead)
                throw new Exception(CommonConstants.InvalidExcelUploaded);
            if (string.IsNullOrWhiteSpace(fileName))
                throw new Exception(CommonConstants.InvalidFileName);

            var extension = Path.GetExtension(fileName);

            if (!string.Equals(extension, CommonConstants.ExcelFileExtension, StringComparison.OrdinalIgnoreCase))
                throw new Exception(CommonConstants.OnlyExcelFilesAllowed);

            fileStream.Position = 0;

            using var workbook = new XLWorkbook(fileStream);
            var sheetNames = workbook.Worksheets
                .Select(ws => ws.Name)
                .ToList();

            return await Task.FromResult(sheetNames);
        }

        #endregion Excel updating and Importing - END
        #region Payroll Liquidation - Start
        private async Task<List<LiquidationRowDto>> BuildCdrRowsAsync(
            LiquidationFilterDto filter,
            LiquidationSettingsDto settings)
        {
            var beneficiaryFilter = new BeneficiaryFilterDto
            {
                PageNumber = 1,
                PageSize = 10000,
                PaymentStatus = 2,
                PaymentDate = null,
                PaymentDateFrom = filter.PaymentDateFrom,
                PaymentDateTo = filter.PaymentDateTo,
                PsgcCodeProvince = filter.PsgcCodeProvince,
            };

            var rawData = await _repo.FilterAsync(beneficiaryFilter);
            var data = rawData
                .DistinctBy(x => x.Id)
                .Where(x => x.PaymentDate.HasValue)
                .OrderBy(x => x.PaymentDate)
                .ThenBy(x => x.MunicipalityName)
                .ThenBy(x => x.MilestoneYear)
                .ToList();

            if (!data.Any())
                throw new InvalidOperationException(CommonConstants.NoPaidRecordsMessage);

            // ✅ NEW — build the TRUE full CGP page range per {CgpGenerationId, Municipality,
            // MilestoneYear}, including Unpaid beneficiaries in the same payroll
            // page-block. Paid-only min/max would silently truncate the printed
            // range whenever the block's first/last page belonged to someone Unpaid.
            var cgpLookupKeys = data
                .Where(x => x.CgpGenerationId.HasValue)
                .Select(x => (CgpGenerationId: x.CgpGenerationId!.Value, MunicipalityCode: x.PsgcCodeMunicipality))
                .Distinct()
                .ToList();

            var cgpCandidates = await _repo.GetCgpRangeCandidatesAsync(cgpLookupKeys);

            var cgpRangeMap = cgpCandidates
                .Where(c => c.CgpPageNumber.HasValue)
                .GroupBy(c => (c.CgpGenerationId, c.PsgcCodeMunicipality, c.MilestoneYear))
                .ToDictionary(
                    g => g.Key,
                    g => (Min: g.Min(x => x.CgpPageNumber!.Value), Max: g.Max(x => x.CgpPageNumber!.Value)));

            var cdrRows = new List<LiquidationRowDto>();

            var groups = data
                .GroupBy(x => new
                {
                    x.PaymentDate!.Value.Date,
                    Municipality = x.MunicipalityName ?? string.Empty,
                    Province = x.ProvinceName ?? string.Empty,
                    x.MilestoneYear
                })
                .OrderBy(g => g.Key.Date)
                .ThenBy(g => g.Key.Municipality)
                .ThenBy(g => g.Key.MilestoneYear);

            foreach (var group in groups)
            {
                var records = group.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToList();
                var first = records.First();

                var firstFullName = $"{first.LastName}, {first.FirstName} {first.MiddleName}".Trim().TrimEnd(',');
                var payee = records.Count > 1
                    ? $"{firstFullName} {CommonConstants.ETAL}"
                    : firstFullName;

                var disbursement = records.Sum(x => x.Age >= 100 ? 100_000m : 10_000m);

                var cgpGenerationId = records.FirstOrDefault(r => r.CgpGenerationId.HasValue)?.CgpGenerationId;
                var cgpPrefix = records.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.CgpPrefix))?.CgpPrefix;
                var muniCode = first.PsgcCodeMunicipality;

                string cgpNumber;

                if (cgpGenerationId == null || string.IsNullOrWhiteSpace(cgpPrefix))
                {
                    cgpNumber = $"{CommonConstants.CgpNo} Not yet assigned — run Payroll for this batch first";
                }
                else
                {
                    var lookupKey = (cgpGenerationId.Value, muniCode, group.Key.MilestoneYear);

                    (int Min, int Max) range;
                    if (cgpRangeMap.TryGetValue(lookupKey, out var fullRange))
                    {
                        range = fullRange;
                    }
                    else
                    {
                        // Fallback — shouldn't normally happen, since these exact
                        // records are already part of `data` and thus already
                        // included in the candidate lookup above.
                        var assignedPages = records
                            .Where(r => r.CgpPageNumber.HasValue)
                            .Select(r => r.CgpPageNumber!.Value)
                            .ToList();

                        range = assignedPages.Any()
                            ? (assignedPages.Min(), assignedPages.Max())
                            : (0, 0);
                    }

                    cgpNumber = range.Min == range.Max
                        ? $"{CommonConstants.CgpNo} {cgpPrefix}-{range.Min.ToPaddedPage()}"
                        : $"{CommonConstants.CgpNo} {cgpPrefix}-{range.Min.ToPaddedPage()} to {range.Max.ToPaddedPage()}";
                }

                // ✅ FIXED — City names that already include "City of" no longer get
                // double-prefixed into "City of City of X".
                var location = group.Key.Municipality.Contains(CommonConstants.City, StringComparison.OrdinalIgnoreCase)
                    ? group.Key.Municipality
                    : $"{CommonConstants.MunicipalityOf} {group.Key.Municipality}";

                var provinceStr = !string.IsNullOrWhiteSpace(group.Key.Province)
                    ? $", {CommonConstants.ProvinceOf} {group.Key.Province}"
                    : string.Empty;
                var nature = $"{CommonConstants.RA} {location}{provinceStr} {CommonConstants.CY} {group.Key.MilestoneYear}";

                cdrRows.Add(new LiquidationRowDto
                {
                    PaymentDate = group.Key.Date,
                    CgpNumber = cgpNumber,
                    Payee = payee,
                    NatureOfPayment = nature,
                    Disbursement = disbursement,
                    MilestoneYear = group.Key.MilestoneYear,
                    MunicipalityName = group.Key.Municipality,
                    ProvinceName = group.Key.Province,
                    CgpPrefix = cgpPrefix,                 // ✅ NEW
                    CgpGenerationId = cgpGenerationId,       // ✅ NEW
                    PsgcCodeMunicipality = muniCode          // ✅ NEW
                });
            }

            var runningBalance = settings.InitialCashAdvance;
            foreach (var row in cdrRows)
            {
                runningBalance -= row.Disbursement;
                row.CashAdvanceBalance = runningBalance;
            }

            return cdrRows;
        }

        public async Task<List<LiquidationPreviewRowDto>> BuildCdrPreviewAsync(
            LiquidationFilterDto filter,
            LiquidationSettingsDto settings)
        {
            var rows = await BuildCdrRowsAsync(filter, settings);

            return rows.Select(r => new LiquidationPreviewRowDto
            {
                PaymentDate = r.PaymentDate,
                CgpNumber = r.CgpNumber,
                Payee = r.Payee,
                NatureOfPayment = r.NatureOfPayment,
                Disbursement = r.Disbursement,
                RunningBalance = r.CashAdvanceBalance,
                MunicipalityName = r.MunicipalityName,
                ProvinceName = r.ProvinceName,
                MilestoneYear = r.MilestoneYear,
                CgpPrefix = r.CgpPrefix,
                CgpGenerationId = r.CgpGenerationId,          // ✅ NEW
                PsgcCodeMunicipality = r.PsgcCodeMunicipality
            }).ToList();
        }

        // ── Generate: build and return the Excel workbook bytes ───────────────────
        public async Task<byte[]> GenerateCdrAsync(
            LiquidationFilterDto filter,
            LiquidationSettingsDto settings)
        {
            var cdrRows = await BuildCdrRowsAsync(filter, settings);

            using var workbook = new XLWorkbook();

            int rowsPerPage = settings.RowsPerPage > 0 ? settings.RowsPerPage : 8;
            // Page 1 has the DV cash advance row so it holds (rowsPerPage - 1) data rows
            int page1Max = rowsPerPage - 1;
            int totalData = cdrRows.Count;

            // Estimate total pages
            int remainAfterPage1 = Math.Max(0, totalData - page1Max);
            int totalPages = 1 + (int)Math.Ceiling((double)remainAfterPage1 / rowsPerPage);

            int processedRows = 0;
            int pageNum = 1;
            decimal prevBalance = settings.InitialCashAdvance;

            while (processedRows < totalData)
            {
                bool isFirstPage = pageNum == 1;
                int takeCount = isFirstPage ? page1Max : rowsPerPage;
                var pageRows = cdrRows.Skip(processedRows).Take(takeCount).ToList();

                bool isLastPage = (processedRows + pageRows.Count) >= totalData;

                // ✅ Always put certification on the last page — no complex math
                bool includeCertification = isLastPage;

                string sheetName = isLastPage ? CommonConstants.final : $"{CommonConstants.CashDrPage}{pageNum}";

                var ws = workbook.Worksheets.Add(sheetName);
                BuildCdrSheet(ws, pageNum, totalPages, pageRows, settings,
                  isFirstPage, includeCertification, prevBalance);
                if (pageRows.Any())
                    prevBalance = pageRows.Last().CashAdvanceBalance;

                processedRows += pageRows.Count;
                pageNum++;
            }

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        // Row height for every CDR data/DV row (rows 19+) — matches the
        // original template. Page-fit is now handled by FitToPages(1, 1) in
        // BuildCdrSheet rather than a computed Scale, so this constant only
        // controls row height, not pagination math.
        private const double CdrDataRowHeightPt = 105.75;

        // ── Sheet builder — exact match to CDR_1st-Qtr-2026.xlsx template ────────
        private static void BuildCdrSheet(
            IXLWorksheet ws,
            int pageNum,
            int totalPages,
            List<LiquidationRowDto> rows,
            LiquidationSettingsDto s,
            bool isFirstPage,
            bool includeCertification,
            decimal balanceBefore)
        {
            // ══════════════════════════════════════════════════════════════════
            // EXACT values from template (CDR_1st-Qtr-2026.xlsx CashDR_Page1)
            // ══════════════════════════════════════════════════════════════════
            const string FONT = CommonConstants.TimesNewRoman;

            // ✅ CHANGED — no longer fixed ints. When the user tuned the Font
            // Size slider in the CDR Print Preview tab (CdrPrintDocument.razor),
            // Settings.PrintFontPercent carries that same percentage through
            // here, so the downloaded Excel matches what was previewed on
            // screen instead of always using the template's base sizes.
            double fontScale = (s.PrintFontPercent ?? 100) / 100.0;
            double FS_SM = 11 * fontScale;   // standard cell font size
            double FS_TITLE = 14 * fontScale;  // "CASH DISBURSEMENTS RECORD"
            double FS_SUB = 12 * fontScale;   // sub-headings & appendix

            // ── Column widths (exact from template) ──────────────────────────
            ws.Column(1).Width = 16.60;  // A  Date
            ws.Column(2).Width = 21.70;  // B  ADA/Ref
            ws.Column(3).Width = 29.40;  // C  Payee
            ws.Column(4).Width = 14.00;  // D  UACS
            ws.Column(5).Width = 33.60;  // E  Nature of Payment
            ws.Column(6).Width = 22.30;  // F  Cash Advance Received
            ws.Column(7).Width = 14.70;  // G  Disbursements
            ws.Column(8).Width = 19.00;  // H  Cash Advance Balance

            // ── Page setup (paperSize=9=A4, portrait) ─────────────────────────
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;

            // ✅ CHANGED — when the user tuned the Margin slider in Print
            // Preview, Settings.PrintMarginMm carries that through (converted
            // mm → inches) and overrides the template's default asymmetric
            // margins uniformly on all four sides, matching --cdr-margin in
            // CdrPrintDocument.razor's on-screen preview.
            if (s.PrintMarginMm is { } marginMm)
            {
                double marginIn = marginMm / 25.4;
                ws.PageSetup.Margins.Left = marginIn;
                ws.PageSetup.Margins.Right = marginIn;
                ws.PageSetup.Margins.Top = marginIn;
                ws.PageSetup.Margins.Bottom = marginIn;
            }
            else
            {
                ws.PageSetup.Margins.Left = 0.7;
                ws.PageSetup.Margins.Right = 0.7;
                ws.PageSetup.Margins.Top = 0.12;
                ws.PageSetup.Margins.Bottom = 0.75;
            }
            ws.PageSetup.Margins.Header = 0.12;
            ws.PageSetup.Margins.Footer = 0.3;

            // ✅ CHANGED — gave up hand-computing a Scale percentage from
            // estimated row/header heights (57% hardcoded, then a "fill the
            // page" formula, then increasingly aggressive safety margins —
            // still landing on 2 physical pages instead of 1, because it's
            // all built on point-math approximations of heights ClosedXML
            // doesn't expose a way to verify against Excel's actual renderer).
            // FitToPages(1, 1) hands the job to Excel/LibreOffice itself: it
            // auto-shrinks BOTH width and height to whatever scale actually
            // fits the real, fully-rendered content onto exactly one physical
            // page — no estimation involved, and no separate risk of clipping
            // column A the way a manually-forced "1 page" print option did.
            // This is also what the OTHER report builders in this file already
            // use successfully (see their FitToPages(1, 0) calls) — this one
            // just never had it.
            ws.PageSetup.FitToPages(1, 1);

            // ── Style helper ─────────────────────────────────────────────────
            void S(IXLCell cell,
                double fs, bool bold = false, bool italic = false,
                XLAlignmentHorizontalValues h = XLAlignmentHorizontalValues.Left,
                XLAlignmentVerticalValues v = XLAlignmentVerticalValues.Center,
                bool wrap = false)
            {
                cell.Style.Font.FontName = FONT;
                cell.Style.Font.FontSize = fs;
                cell.Style.Font.Bold = bold;
                cell.Style.Font.Italic = italic;
                cell.Style.Alignment.Horizontal = h;
                cell.Style.Alignment.Vertical = v;
                cell.Style.Alignment.WrapText = wrap;
            }

            // Merge A-H for a row, set value and style
            void MergeAH(int r, string text, double fs, bool bold = false,
                XLAlignmentHorizontalValues h = XLAlignmentHorizontalValues.Center,
                XLAlignmentVerticalValues v = XLAlignmentVerticalValues.Center)
            {
                ws.Range(r, 1, r, 8).Merge();
                var cell = ws.Cell(r, 1);
                cell.Value = text;
                S(cell, fs, bold, false, h, v);
            }

            void BorderAll(int r, int c1, int c2)
            {
                var range = ws.Range(r, c1, r, c2);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            // ══════════════════════════════════════════════════════════════════
            // ROW 1 — blank (height 22.5)
            // ══════════════════════════════════════════════════════════════════
            ws.Row(1).Height = 22.5;

            // ══════════════════════════════════════════════════════════════════
            // ROW 1 col G-H — "Appendix  40" (top-right)
            // ══════════════════════════════════════════════════════════════════
            ws.Range(1, 7, 1, 8).Merge();
            var appendixCell = ws.Cell(1, 7);
            appendixCell.Value = CommonConstants.Appendix40;
            S(appendixCell, FS_SUB, false, true,
              XLAlignmentHorizontalValues.Right, XLAlignmentVerticalValues.Center);

            ws.Row(2).Height = 22.5;
            ws.Row(3).Height = 15.75;
            ws.Row(4).Height = 22.5;
            ws.Row(5).Height = 22.5;

            // Row 2: Republic of the Philippines
            ws.Range(2, 1, 2, 8).Merge();
            ws.Cell(2, 1).Value = CommonConstants.RepublicOfThePhilippines;
            S(ws.Cell(2, 1), FS_SM, false, false,
              XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Center);

            // Row 3: NATIONAL COMMISSION — BOLD
            ws.Range(3, 1, 3, 8).Merge();
            ws.Cell(3, 1).Value = CommonConstants.NCSC;
            S(ws.Cell(3, 1), FS_SM, true, false,  // ✅ bold = true
              XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Center);

            // Row 4: Address
            ws.Range(4, 1, 4, 8).Merge();
            ws.Cell(4, 1).Value = CommonConstants.NcscAddress;
            S(ws.Cell(4, 1), FS_SM, false, false,
              XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Center);

            // Row 5: Website
            ws.Range(5, 1, 5, 8).Merge();
            ws.Cell(5, 1).Value = CommonConstants.NcscWebsite;
            S(ws.Cell(5, 1), FS_SM, false, false,
              XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Center);

            // ══════════════════════════════════════════════════════════════════
            // ROW 6 — blank separator (height 22.5)
            // ══════════════════════════════════════════════════════════════════
            ws.Row(6).Height = 22.5;

            // ══════════════════════════════════════════════════════════════════
            // ROW 7 — "CASH DISBURSEMENTS RECORD" (A7:H7, merged)
            // ══════════════════════════════════════════════════════════════════
            ws.Row(7).Height = 17.65;
            MergeAH(7, CommonConstants.CashDisbursementsRecord, FS_TITLE, true);

            // ══════════════════════════════════════════════════════════════════
            // ROW 8 — Program name (A8:H8, merged)
            // ══════════════════════════════════════════════════════════════════
            ws.Row(8).Height = 15.75;
            MergeAH(8, CommonConstants.SCDSP, FS_SUB, true);

            // ══════════════════════════════════════════════════════════════════
            // ROW 9 — Implementation line (A9:H9, merged)
            // ══════════════════════════════════════════════════════════════════
            ws.Row(9).Height = 15.75;
            MergeAH(9, CommonConstants.Implentation, FS_SUB, true);

            // ══════════════════════════════════════════════════════════════════
            // ROW 10 — blank (height 15.75)
            // ══════════════════════════════════════════════════════════════════
            ws.Row(10).Height = 15.75;

            // ══════════════════════════════════════════════════════════════════
            // ROW 11 — Region / Org Code (height 30)
            // A11:B11 merged = "Region / Organization Unit:"
            // C11 = region name
            // F11 = "New ORG Code:"
            // G11:H11 merged = org code value
            // ══════════════════════════════════════════════════════════════════
            ws.Row(11).Height = 30.0;
            ws.Range(11, 1, 11, 2).Merge();
            ws.Cell(11, 1).Value = CommonConstants.Unit;
            S(ws.Cell(11, 1), FS_SM, true, false,
              XLAlignmentHorizontalValues.Left, XLAlignmentVerticalValues.Center);

            ws.Cell(11, 3).Value = s.RegionOrgUnit;
            S(ws.Cell(11, 3), FS_SM, true, false,
              XLAlignmentHorizontalValues.Left, XLAlignmentVerticalValues.Center);
            ws.Range(11, 3, 11, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            ws.Cell(11, 6).Value = CommonConstants.OrgCode;
            S(ws.Cell(11, 6), FS_SM, true, true,
              XLAlignmentHorizontalValues.Left, XLAlignmentVerticalValues.Center);

            ws.Range(11, 7, 11, 8).Merge();
            ws.Cell(11, 7).Value = s.NewOrgCode;
            S(ws.Cell(11, 7), FS_SM, false, false,
              XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Center);
            ws.Range(11, 7, 11, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // ══════════════════════════════════════════════════════════════════
            // ROW 12 — Fund Cluster / Sheet No (height 19.5)
            // A12 = fund cluster text
            // F12:H12 merged = Sheet No.
            // ══════════════════════════════════════════════════════════════════
            ws.Row(12).Height = 19.5;

            var fundCell = ws.Cell(12, 1);
            fundCell.Style.Font.FontName = FONT;
            fundCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            fundCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            var fundRt = fundCell.GetRichText();

            // Part 1 — "Fund Cluster : " not underlined
            fundRt.AddText(CommonConstants.FundCluster)
              .SetFontName(FONT)
              .SetFontSize(FS_SM)
              .SetBold(true);
            // Part 2 — actual value underlined
            fundRt.AddText(s.FundCluster)
              .SetFontName(FONT)
              .SetFontSize(FS_SM)
              .SetBold(true)
              .SetUnderline(); // ✅ only the value is underlined

            ws.Range(12, 6, 12, 8).Merge();
            var sheetCell = ws.Cell(12, 6);
            sheetCell.Style.Font.FontName = FONT;
            sheetCell.Style.Font.Bold = true;
            sheetCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            sheetCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            var sheetRt = sheetCell.GetRichText();

            // Part 1 — "Sheet No. : " normal, not underlined
            sheetRt.AddText(CommonConstants.SheetNo)
              .SetFontName(FONT)
              .SetFontSize(FS_SM)
              .SetBold(true);

            // Part 2 — "{pageNum} of {totalPages}" underlined
            sheetRt.AddText($"{pageNum}{CommonConstants.Of}{totalPages}")
              .SetFontName(FONT)
              .SetFontSize(FS_SM)
              .SetBold(true)
              .SetUnderline(); // ✅ only the numbers are underlined

            // ══════════════════════════════════════════════════════════════════
            // ROW 13 — blank (height 19.5)
            // ROW 14 — blank (height 9.75)
            // ══════════════════════════════════════════════════════════════════
            ws.Row(13).Height = 19.5;
            ws.Row(14).Height = 9.75;

            // ══════════════════════════════════════════════════════════════════
            // ROW 15 — Accountable officer names (height 36)
            // A15:C15 merged = officer name
            // D15:F15 merged = designation
            // G15:H15 merged = station
            // ══════════════════════════════════════════════════════════════════
            ws.Row(15).Height = 36.0;

            // ── Officer Name ──────────────────────────────────────────────────────
            ws.Range(15, 1, 15, 3).Merge();
            ws.Range(15, 1, 16, 3).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            var officerCell = ws.Cell(15, 1);
            officerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            officerCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
            var officerRt = officerCell.GetRichText();
            officerRt.AddText(s.AccountableOfficerName)
              .SetFontName(FONT).SetFontSize(FS_SM).SetBold(true)
              .SetUnderline(); // ✅ only the name is underlined

            // ── Official Designation ──────────────────────────────────────────────
            ws.Range(15, 4, 15, 6).Merge();
            ws.Range(15, 4, 16, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            var desigCell = ws.Cell(15, 4);
            desigCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            desigCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
            var desigRt = desigCell.GetRichText();
            desigRt.AddText(s.OfficialDesignation)
              .SetFontName(FONT).SetFontSize(FS_SM).SetBold(true)
              .SetUnderline(); // ✅ only the designation is underlined


            // ── Station ───────────────────────────────────────────────────────────
            ws.Range(15, 7, 15, 8).Merge();
            ws.Range(15, 7, 16, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            var stationCell = ws.Cell(15, 7);
            stationCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            stationCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
            var stationRt = stationCell.GetRichText();
            stationRt.AddText(s.Station)
              .SetFontName(FONT).SetFontSize(FS_SM).SetBold(true)
              .SetUnderline(); // ✅ only the station value is underlined

            // ══════════════════════════════════════════════════════════════════
            // ROW 16 — Labels under officer names (height 18)
            // A16:C16 merged = "Accountable Officer"
            // D16:F16 merged = "Official Designation"
            // G16:H16 merged = "Station"
            // ══════════════════════════════════════════════════════════════════
            ws.Row(16).Height = 18.0;
            ws.Range(16, 1, 16, 3).Merge();
            ws.Cell(16, 1).Value = CommonConstants.AccountableOfficer;
            S(ws.Cell(16, 1), FS_SM, false, false,
              XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

            ws.Range(16, 4, 16, 6).Merge();
            ws.Cell(16, 4).Value = CommonConstants.OfficialDesignation;
            S(ws.Cell(16, 4), FS_SM, false, false,
              XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

            ws.Range(16, 7, 16, 8).Merge();
            ws.Cell(16, 7).Value = CommonConstants.Station;
            S(ws.Cell(16, 7), FS_SM, false, false,
              XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

            // ══════════════════════════════════════════════════════════════════
            // ROWS 17-18 — Column headers (merged vertically, height 34.5 each)
            // A17:A18 = Date
            // B17:B18 = ADA/Check/DV/Payroll/Reference No.
            // C17:C18 = Payee
            // D17:D18 = UACS Object Code
            // E17:E18 = Nature of Payment
            // F17:F18 = Cash Advance Received/(Refunded)
            // G17:G18 = Disbursements
            // H17:H18 = Cash Advance Balance
            // ══════════════════════════════════════════════════════════════════
            ws.Row(17).Height = 34.5;
            ws.Row(18).Height = 34.5;

            var colHeaders = new[]
            {
        (1, CommonConstants.Date),
        (2, CommonConstants.Column2Header),
        (3, CommonConstants.Payee),
        (4, CommonConstants.UACS),
        (5, CommonConstants.NatureOfPayment),
        (6, CommonConstants.CashAdvanceReceived),
        (7, CommonConstants.Disbursement),
        (8, CommonConstants.Balance),
    };

            foreach (var (col, label) in colHeaders)
            {
                ws.Range(17, col, 18, col).Merge();
                var cell = ws.Cell(17, col);
                cell.Value = label;
                S(cell, FS_SM, true, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Center, true);
                ws.Range(17, col, 18, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // ══════════════════════════════════════════════════════════════════
            // DATA ROWS — start at row 19
            // Template data rows: height 105.75 (rows 19-25), 87.5 (row 26)
            // We use 105.75 for all data rows to match template
            // ══════════════════════════════════════════════════════════════════
            int R = 19;

            if (isFirstPage)
            {
                ws.Row(R).Height = CdrDataRowHeightPt;

                ws.Cell(R, 1).Value = s.InputDate;
                ws.Cell(R, 1).Style.NumberFormat.Format = CommonConstants.DatePlaceHolder;
                S(ws.Cell(R, 1), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                ws.Cell(R, 2).Value = $"{CommonConstants.Dv} {s.DvYear}-{s.DvMonth}-{CommonConstants.DefaultOrderNo}";
                S(ws.Cell(R, 2), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Left, XLAlignmentVerticalValues.Top, true);

                ws.Cell(R, 3).Value = s.DvPayee;
                S(ws.Cell(R, 3), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Left, XLAlignmentVerticalValues.Top, true);

                // ✅ CHANGED — UACS Object Code intentionally left blank on the DV row.
                // The constant only applies starting from the actual CDR grantee rows below.
                S(ws.Cell(R, 4), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                ws.Cell(R, 5).Value = s.NatureOfPayment;
                S(ws.Cell(R, 5), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Left, XLAlignmentVerticalValues.Top, true);

                ws.Cell(R, 6).Value = s.InitialCashAdvance;
                ws.Cell(R, 6).Style.NumberFormat.Format = CommonConstants.NumberFormat;
                S(ws.Cell(R, 6), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                // Col 7 (Disbursements) blank
                ws.Cell(R, 8).Value = s.InitialCashAdvance; // Balance = initial
                ws.Cell(R, 8).Style.NumberFormat.Format = CommonConstants.NumberFormat;
                S(ws.Cell(R, 8), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                BorderAll(R, 1, 8);
                R++;
            }

            // ── CDR data rows ─────────────────────────────────────────────────
            foreach (var row in rows)
            {
                ws.Row(R).Height = CdrDataRowHeightPt;

                ws.Cell(R, 1).Value = row.PaymentDate;
                ws.Cell(R, 1).Style.NumberFormat.Format = CommonConstants.DatePlaceHolder;
                S(ws.Cell(R, 1), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                ws.Cell(R, 2).Value = row.CgpNumber;
                S(ws.Cell(R, 2), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Left, XLAlignmentVerticalValues.Top, true);

                ws.Cell(R, 3).Value = row.Payee;
                S(ws.Cell(R, 3), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Left, XLAlignmentVerticalValues.Top, true);

                ws.Cell(R, 4).Value = CommonConstants.ConstantUacs;
                S(ws.Cell(R, 4), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                ws.Cell(R, 5).Value = row.NatureOfPayment;
                S(ws.Cell(R, 5), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Left, XLAlignmentVerticalValues.Top, true);

                // Col 6 (Cash Advance Received) blank for data rows

                ws.Cell(R, 7).Value = row.Disbursement;
                ws.Cell(R, 7).Style.NumberFormat.Format = CommonConstants.NumberFormat;
                S(ws.Cell(R, 7), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                ws.Cell(R, 8).Value = row.CashAdvanceBalance;
                ws.Cell(R, 8).Style.NumberFormat.Format = CommonConstants.NumberFormat;
                S(ws.Cell(R, 8), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                BorderAll(R, 1, 8);
                R++;
            }

            // ══════════════════════════════════════════════════════════════════
            // CERTIFICATION BLOCK (final sheet only)
            // Matches exact structure from 'final' sheet in template
            // ══════════════════════════════════════════════════════════════════
            if (includeCertification)
            {
                // ── Unclaimed row ────────────────────────────────────────────
                ws.Row(R).Height = 74.25;

                ws.Cell(R, 1).Value = s.CertificationDate;
                ws.Cell(R, 1).Style.NumberFormat.Format = CommonConstants.DatePlaceHolder;
                S(ws.Cell(R, 1), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                ws.Cell(R, 3).Value = CommonConstants.Cluster;
                S(ws.Cell(R, 3), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Left, XLAlignmentVerticalValues.Top, true);

                ws.Cell(R, 4).Value = CommonConstants.TreasuryConstant;
                S(ws.Cell(R, 4), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                ws.Cell(R, 5).Value = CommonConstants.Unclaimed;
                S(ws.Cell(R, 5), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Left, XLAlignmentVerticalValues.Top, true);

                ws.Cell(R, 6).Value = rows.Any() ? rows.Last().CashAdvanceBalance : balanceBefore;
                ws.Cell(R, 6).Style.NumberFormat.Format = CommonConstants.NumberFormat;
                S(ws.Cell(R, 6), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                ws.Cell(R, 7).Value = "-";
                S(ws.Cell(R, 7), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                ws.Cell(R, 8).Value = "-";
                S(ws.Cell(R, 8), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Top);

                BorderAll(R, 1, 8);
                R++;

                // ── CERTIFICATION title (A:H merged) ─────────────────────────
                ws.Row(R).Height = 14.5;
                ws.Range(R, 1, R + 1, 8).Merge();
                ws.Cell(R, 1).Value = CommonConstants.CertificationSpaced;
                S(ws.Cell(R, 1), FS_SUB, true, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Center);
                R++;

                // ── Blank row ─────────────────────────────────────────────────
                ws.Row(R).Height = 13.0;
                R++;

                // ── Certification text (A:H merged, 4 rows) ───────────────────
                int certTextStart = R;
                ws.Row(R).Height = 13.0;
                ws.Row(R + 1).Height = 13.0;
                ws.Row(R + 2).Height = 13.0;
                ws.Row(R + 3).Height = 13.0;
                ws.Range(R, 1, R + 3, 8).Merge();

                // ✅ Use rich text to bold specific parts
                var certCell = ws.Cell(R, 1);
                certCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                certCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                certCell.Style.Alignment.WrapText = true;

                var rt = certCell.GetRichText();

                // Part 1 — normal
                rt.AddText(
                    "I hereby certify on my official oath that the foregoing is a correct and complete " +
                    "record of all cash disbursements had by me in my capacity as\n")
                  .SetFontName(FONT)
                  .SetFontSize(FS_SUB)
                  .SetBold(false);

                // Part 2 — BOLD: designation + org name
                rt.AddText(
                    $"{s.OfficialDesignation} / Special Disbursing Officer ")
                  .SetFontName(FONT)
                  .SetFontSize(FS_SUB)
                  .SetBold(true)
                  .SetUnderline(); // ✅ bold

                // Part 2 — BOLD: designation + org name
                rt.AddText(
                    "of the")
                  .SetFontName(FONT)
                  .SetFontSize(FS_SUB)
                  .SetBold(false);

                // Part 2 — BOLD: designation + org name
                rt.AddText(
                    $" National Commission of " +
                    "Senior Citizens Cluster 8 - Caraga Region")
                  .SetFontName(FONT)
                  .SetFontSize(FS_SUB)
                  .SetBold(true)
                  .SetUnderline(); // ✅ bold


                // Part 3 — normal: rest of the sentence
                rt.AddText(
                    $" during the period from\n" +
                    $"{s.CertificationPeriodFrom} to {s.CertificationPeriodTo}, inclusive, as indicated " +
                    $"in the corresponding columns.")
                  .SetFontName(FONT)
                  .SetFontSize(FS_SUB)
                  .SetBold(false);

                R += 4;

                // ── Blank gap rows ────────────────────────────────────────────
                ws.Row(R).Height = 13.0; R++;
                ws.Row(R).Height = 13.0; R++;
                ws.Row(R).Height = 13.0; R++;

                // ── Signature name (E:G merged, underlined) ───────────────────
                ws.Row(R).Height = 14.0;
                ws.Range(R, 5, R, 7).Merge();
                ws.Cell(R, 5).Value = s.AccountableOfficerName;
                S(ws.Cell(R, 5), FS_SUB, true, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Center);
                ws.Range(R, 5, R, 7).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                R++;

                // ── "Name and Signature of Disbursing Officer" ────────────────
                ws.Row(R).Height = 15.75;
                ws.Range(R, 5, R, 7).Merge();
                ws.Cell(R, 5).Value = CommonConstants.NameAndSignatureOfDisbursingOfficer;
                S(ws.Cell(R, 5), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Center);
                R++;

                // ── Blank ─────────────────────────────────────────────────────
                ws.Row(R).Height = 15.75; R++;

                // ── Certification date (E:G merged, underlined) ───────────────
                ws.Row(R).Height = 14.0;
                ws.Range(R, 5, R, 7).Merge();
                ws.Cell(R, 5).Value = s.CertificationDate;
                ws.Cell(R, 5).Style.NumberFormat.Format = CommonConstants.DatePlaceHolderUpperCase;
                S(ws.Cell(R, 5), FS_SUB, true, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Center);
                ws.Range(R, 5, R, 7).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                R++;

                // ── "Date" label ──────────────────────────────────────────────
                ws.Row(R).Height = 15.75;
                ws.Range(R, 5, R, 7).Merge();
                ws.Cell(R, 5).Value = CommonConstants.Date;
                S(ws.Cell(R, 5), FS_SM, false, false,
                  XLAlignmentHorizontalValues.Center, XLAlignmentVerticalValues.Center);
            }
        }
        #endregion Payroll Liquidation - End
        public async Task<PagedResultDto<BeneficiaryListItemDto>> GetPagedListAsync(BeneficiaryFilterDto filter)
        {
            const int MaxPageSize = 10000;
            filter.PageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);
            filter.PageNumber = Math.Max(filter.PageNumber, 1);

            var cacheKey = BuildPaginatedCacheKey(filter);

            if (_memoryCache.TryGetValue(cacheKey, out PagedResultDto<BeneficiaryListItemDto>? cached)
                && cached is not null)
            {
                return cached;
            }

            var pagedResult = await _repo.GetPagedListAsync(filter);
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(5)
            };

            _memoryCache.Set(cacheKey, pagedResult, cacheOptions);

            return pagedResult;
        }

        public async Task<int> GetMatchingCountAsync(BeneficiaryFilterDto filter)
        {
            return await _repo.CountMatchingAsync(filter);
        }

        public async Task<List<BatchCodeCoStatusSummaryDto>> GetEndorsedBatchCodeSummaryAsync()
        {
            return await _repo.GetEndorsedBatchCodeSummaryAsync();
        }
        public async Task<PagedResultDto<BeneficiaryInformationDto>> GetPaginatedAsync(BeneficiaryFilterDto filter)
        {
            // WHY: [FromQuery]/[FromBody] model binding does zero validation on
            // PageSize. Your Blazor dropdown caps it at 5000, but that's a UI
            // constraint, not a server guarantee — anyone hitting the API directly
            // (Swagger, a future client, a bug) could request an unbounded page.

            const int MaxPageSize = 5000;
            filter.PageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);
            filter.PageNumber = Math.Max(filter.PageNumber, 1);

            var cacheKey = BuildPaginatedCacheKey(filter);

            if (_memoryCache.TryGetValue(cacheKey, out PagedResultDto<BeneficiaryInformationDto>? cached)
                && cached is not null)
            {
                return cached;
            }

            var pagedResult = await _repo.GetPagedAsync(filter);

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(5)
            };

            _memoryCache.Set(cacheKey, pagedResult, cacheOptions);

            return pagedResult;
        }
        public async Task<PossibleDuplicateSummaryDto> GetGlobalDuplicateSummaryAsync()
        {
            if (_memoryCache.TryGetValue(GlobalDuplicateScanCacheKey, out PossibleDuplicateSummaryDto? cached) && cached is not null)
            {
                return cached;
            }
            //Empty filter = scan the entire non-deleted population, no narrowing.
            var unfiltered = new BeneficiaryFilterDto
            {
                PageNumber = 1,
                PageSize = int.MinValue
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            //Longer timeout than the filtered scan (15s) since this covers our ENTIRE dataset
            //rater than a filtered subste - give it more room
            //before giving up, since this only runs once per login, not per click.

            List<PossibleDuplicatePairDto> pairs;
            bool timedOut = false;
            try
            {
                pairs = await _repo.FindAllPossibleDuplicatesAsync(
                    unfiltered, maxPairs: 100, cancellationToken: cts.Token);
                //maxPairs raised to 100 (vs 50 for filtered scans) since this is
                // the authorative system-wide count the bell badge displays -
                //worth capturing more before truncating, given it runs rarely.

            }
            catch (OperationCanceledException)
            {
                pairs = new List<PossibleDuplicatePairDto>();
                timedOut = true;
            }

            var summary = new PossibleDuplicateSummaryDto
            {
                TotalPairs = pairs.Count(),
                Pairs = pairs,
                TimedOut = timedOut
            };

            // ✅ No fixed expiration — this cache is invalidated EXPLICITLY by
            // InvalidateGlobalDuplicateCache() below, called from every mutation
            // path (Create/Update/BulkUpdate/SoftDelete). NeverRemove priority
            // matches the pattern you already use for SummaryCacheVersionKey.
            _memoryCache.Set(GlobalDuplicateScanCacheKey, summary, new MemoryCacheEntryOptions
            {
                Priority = CacheItemPriority.NeverRemove
            });

            return summary;

        }
        #region Private helpers

        // ✅ NEW — PH mobile number: exactly 11 digits, starts with "09". Shared by
        // both CreateAsync and UpdateAsync since a grantee must always have at
        // least one valid contact number.
        private static readonly System.Text.RegularExpressions.Regex PhoneNumberRegex =
            new(@"^09\d{9}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static void ValidatePhoneNumbers(List<BeneficiaryPhoneNumberDto>? numbers)
        {
            if (numbers == null || !numbers.Any(n => !string.IsNullOrWhiteSpace(n.Number)))
                throw new Exception("At least one contact number is required.");

            foreach (var n in numbers.Where(n => !string.IsNullOrWhiteSpace(n.Number)))
            {
                if (!PhoneNumberRegex.IsMatch(n.Number.Trim()))
                    throw new Exception($"'{n.Number}' is not a valid Philippine mobile number. It must be 11 digits and start with '09' (e.g. 09171234567).");
            }
        }

        // ✅ NEW — Excel import's "CONTACT NUMBER" column may contain more than one
        // number separated by common delimiters (comma, slash, semicolon, "&", or
        // a newline within the cell). Unlike ValidatePhoneNumbers (used by the
        // interactive form), this is intentionally lenient: historical import data
        // was never validated, so entries that don't match the PH format are simply
        // dropped rather than failing the whole row.
        private static List<BeneficiaryPhoneNumber> ParsePhoneNumbersFromImport(string? rawContactNumber)
        {
            if (string.IsNullOrWhiteSpace(rawContactNumber))
                return new List<BeneficiaryPhoneNumber>();

            var candidates = rawContactNumber.Split(
                new[] { ',', '/', ';', '&', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return candidates
                .Where(c => PhoneNumberRegex.IsMatch(c))
                .Distinct()
                .Select((c, i) => new BeneficiaryPhoneNumber { Number = c, SortOrder = i })
                .ToList();
        }

        private static string MapTriStateLabel(bool? value) => value switch
        {
            true => CommonConstants.Yes,
            false => CommonConstants.No,
            null => "Not yet answered"
        };
        private static string MapPlaceOfSubmissionLabel(int? value) => value switch
        {
            1 => "Local",
            2 => "Abroad",
            _ => CommonConstants.None
        };
        private static string MapCivilStatusLabel(int? status) => status switch
        {
            1 => CommonConstants.Single.ToTitleCase(),
            2 => CommonConstants.Widowed.ToTitleCase(),
            3 => CommonConstants.Married.ToTitleCase(),
            4 => CommonConstants.LiveIn.ToTitleCase(), // relabeled "Common-Law" in the UI, same underlying int
            5 => "Others",
            _ => CommonConstants.None
        };
        // ✅ Single source of truth for the "exact duplicate" key, used by both
        // PreviewImportAsync and ConfirmImportAsync against the in-memory pool.
        // Must match the normalization ExistsDuplicateAsync uses in the repository
        // (trim + lowercase + date-only) so behavior stays identical to before.
        private static string BuildExactDupKey(string? lastName, string? firstName, string? middleName, DateTime birthDate)
        {
            return string.Join("|",
                (lastName ?? string.Empty).Trim().ToLower(),
                (firstName ?? string.Empty).Trim().ToLower(),
                (middleName ?? string.Empty).Trim().ToLower(),
                birthDate.Date.ToString("yyyy-MM-dd"));
        }
        private static string GetComplianceExportLabel(bool isCompliant, string? assessmentRemarks) =>
        isCompliant && !string.IsNullOrWhiteSpace(assessmentRemarks)
        ? "Yes (w/ Minor Findings)"
        : isCompliant
            ? CommonConstants.Compliant
            : CommonConstants.NonCompliant;
        private string BuildFilterDescription(BeneficiaryFilterDto filter)
        {
            var parts = new List<string>();

            if (filter.PsgcCodeRegion.HasValue)
            {
                var name = _psgcNameCache.GetRegionName(filter.PsgcCodeRegion.Value) ?? filter.PsgcCodeRegion.Value.ToString();
                parts.Add($"Region: {name}");
            }
            if (filter.PsgcCodeProvinces != null && filter.PsgcCodeProvinces.Any())
            {
                var names = filter.PsgcCodeProvinces.Select(p => _psgcNameCache.GetProvinceName(p) ?? p.ToString());
                parts.Add($"Provinces: {string.Join(", ", names)}");
            }
            if (filter.PsgcCodeMunicipalities != null && filter.PsgcCodeMunicipalities.Any())
            {
                var names = filter.PsgcCodeMunicipalities.Select(m => _psgcNameCache.GetMunicipalityName(m) ?? m.ToString());
                parts.Add($"Municipalities: {string.Join(", ", names)}");
            }
            if (filter.PsgcCodeBarangay.HasValue)
            {
                var name = _psgcNameCache.GetBarangayName(filter.PsgcCodeBarangay.Value) ?? filter.PsgcCodeBarangay.Value.ToString();
                parts.Add($"Barangay: {name}");
            }

            if (!string.IsNullOrWhiteSpace(filter.FullName))
                parts.Add($"Name: {filter.FullName}");
            else
            {
                if (!string.IsNullOrWhiteSpace(filter.LastName))
                    parts.Add($"Last: {filter.LastName}");
                if (!string.IsNullOrWhiteSpace(filter.FirstName))
                    parts.Add($"First: {filter.FirstName}");
            }

            if (filter.PaymentStatuses != null && filter.PaymentStatuses.Any())
                parts.Add($"Payment: {string.Join(", ", filter.PaymentStatuses.Select(GetPaymentStatusLabelForDescription))}");
            if (filter.IsEligible.HasValue)
                parts.Add($"Eligible: {(filter.IsEligible.Value ? "Yes" : "No")}");
            if (!string.IsNullOrWhiteSpace(filter.EligibilityMode))
                parts.Add($"Eligibility: {filter.EligibilityMode}");
            if (filter.IsCompliant.HasValue)
                parts.Add($"Compliant: {(filter.IsCompliant.Value ? "Yes" : "No")}");
            if (!string.IsNullOrWhiteSpace(filter.ComplianceMode))
                parts.Add($"Compliance: {filter.ComplianceMode}");
            if (filter.Sex.HasValue)
                parts.Add($"Sex: {(filter.Sex.Value == 1 ? "Male" : "Female")}");
            if (filter.CoStatus.HasValue)
                parts.Add($"CO Status: {CoStatusLabel(filter.CoStatus.Value)}");
            if (filter.ReplacementStatus.HasValue)
                parts.Add($"Replacement Status: {ReplacementStatusLabel(filter.ReplacementStatus.Value)}");
            if (filter.FindingStatus.HasValue && filter.FindingStatus != 3)
                parts.Add($"Findings: {GetFindingStatusLabelForDescription(filter.FindingStatus.Value)}");
            if (filter.FilterModeOfPayment.HasValue)
                parts.Add($"Mode of Payment: {GetModeOfPaymentLabelForDescription(filter.FilterModeOfPayment.Value)}");
            if (filter.IsLivenessVerified.HasValue)
                parts.Add($"Liveness: {(filter.IsLivenessVerified.Value ? "Verified" : "Not Verified")}");
            if (filter.IsReadyForEft.HasValue)
                parts.Add($"EFT: {(filter.IsReadyForEft.Value ? "Ready" : "Not Ready")}");

            if (filter.SpecificAge.HasValue)
                parts.Add($"Age: {filter.SpecificAge}");
            if (filter.MilestoneYear.HasValue)
                parts.Add($"Milestone: {filter.MilestoneYear}");
            if (filter.SpecificBirthday.HasValue)
                parts.Add($"Birthday: {filter.SpecificBirthday:MMM dd}");
            if (filter.BirthdayFrom.HasValue || filter.BirthdayTo.HasValue)
            {
                var from = filter.BirthdayFrom?.ToString("MMM dd") ?? "any";
                var to = filter.BirthdayTo?.ToString("MMM dd") ?? "any";
                parts.Add($"Birthday Range: {from} - {to}");
            }

            if (filter.FilterQuarter.HasValue)
                parts.Add($"Quarter: Q{filter.FilterQuarter}");
            if (!string.IsNullOrWhiteSpace(filter.FilterBatch))
                parts.Add($"Batch: {filter.FilterBatch}");
            if (filter.FilterRefYear.HasValue)
                parts.Add($"Year: {filter.FilterRefYear}");
            if (!string.IsNullOrWhiteSpace(filter.FilterRegionRoman))
                parts.Add($"Region: {filter.FilterRegionRoman}");

            if (filter.PaymentDateFrom.HasValue || filter.PaymentDateTo.HasValue)
            {
                var from = filter.PaymentDateFrom?.ToShortDateString() ?? "any";
                var to = filter.PaymentDateTo?.ToShortDateString() ?? "any";
                parts.Add($"Payment Date: {from} - {to}");
            }
            if (filter.DateAddedFrom.HasValue || filter.DateAddedTo.HasValue)
            {
                var from = filter.DateAddedFrom?.ToShortDateString() ?? "any";
                var to = filter.DateAddedTo?.ToShortDateString() ?? "any";
                parts.Add($"Date Added: {from} - {to}");
            }

            if (!string.IsNullOrWhiteSpace(filter.Validator))
                parts.Add($"Validator: {filter.Validator}");
            if (!string.IsNullOrWhiteSpace(filter.BatchCode))
                parts.Add($"Batch Code: {filter.BatchCode}");
            if (!string.IsNullOrWhiteSpace(filter.GeneralSearch))
                parts.Add($"Search: {filter.GeneralSearch}");

            if (!string.IsNullOrWhiteSpace(filter.DataQualityIssue))
            {
                var label = filter.DataQualityIssue switch
                {
                    "location" => "Caution — Location Needs Correction",
                    "headsup" => "Heads-Up — Missing Payment Info",
                    "incomplete" => "Incomplete — Missing Grantee Details",
                    _ => filter.DataQualityIssue
                };
                parts.Add($"Data Quality: {label}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "Full Dataset";
        }

        private static string GetFindingStatusLabelForDescription(int status) => status switch
        {
            0 => "N/A",
            1 => "Solved",
            2 => "Unresolved",
            _ => status.ToString()
        };

        private static string GetModeOfPaymentLabelForDescription(int mode) => mode switch
        {
            1 => "Cash Advance",
            2 => "Bank Transfer",
            _ => mode.ToString()
        };

        private static string GetPaymentStatusLabelForDescription(int status) => status switch
        {
            0 => "N/A",
            1 => "Unpaid",
            2 => "Paid",
            3 => "Pending",
            _ => status.ToString()
        };
        private static double EstimateTextWidth(string text, int fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            const double charWidthFactor = 1.15; // bold + currency symbol padding
            return text.Length * charWidthFactor;
        }
        private static string CoStatusLabel(int status) => status switch
        {
            1 => "Endorsed",
            2 => "Approved",
            _ => "Not Set"
        };
        private static string ReplacementStatusLabel(int status) => status switch
        {
            1 => "Replaced",
            2 => "Is Replacement",
            _ => "Not Replaced"
        };
        // ── Map Payment Status from col 31 ───────────────────────────────────────
        private static int? MapPaymentStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null; // blank = not provided, default to 0 (N/A)

            return value.Trim().ToUpperInvariant() switch
            {
                "PAID" => 2,
                "UNPAID" => 1,
                "PENDING" => 3,
                "N/A" => 0,
                _ => null  // null = invalid — caller adds error
            };
        }
        // ── Map Citizenship from col 17 ───────────────────────────────────────────
        private static int? MapCitizenship(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null; // null = not provided — leave as null in DB, no error

            return value.Trim().ToUpperInvariant() switch
            {
                "FILIPINO" => 1,
                "DUAL CITIZENSHIP" => 2,
                "DUAL" => 2, // common shorthand
                _ => null              // ✅ null = invalid — caller will add error
            };
        }
        private string BuildPaginatedCacheKey(BeneficiaryFilterDto filter)
        {
            var version = GetCurrentSummaryCacheVersion();

            return string.Join("|",
                version,
                CommonConstants.BeneficiaryPaginated,
                filter.IncludeKnownDuplicates.ToString(),

                // BuildNarrowFilterQuery applies filter.Ids ahead of every
                // other field — without it in the key, two different Ids
                // filters that happen to match on every OTHER field (e.g.
                // both otherwise blank) would collide on the same cache
                // entry and one caller could be served another's results.
                (filter.Ids != null && filter.Ids.Any())
                    ? string.Join(",", filter.Ids.OrderBy(x => x))
                    : CommonConstants.Null,

                filter.PsgcCodeRegion?.ToString() ?? CommonConstants.Null,
                filter.PageNumber.ToString(),
                filter.PageSize.ToString(),

                // ✅ FIXED — properly serialize the actual multi-select contents,
                // sorted so the same set of IDs always produces the same key
                // regardless of selection order.
                (filter.PsgcCodeProvinces != null && filter.PsgcCodeProvinces.Any())
                    ? string.Join(",", filter.PsgcCodeProvinces.OrderBy(x => x))
                    : CommonConstants.Null,

                (filter.PsgcCodeMunicipalities != null && filter.PsgcCodeMunicipalities.Any())
                    ? string.Join(",", filter.PsgcCodeMunicipalities.OrderBy(x => x))
                    : CommonConstants.Null,

                filter.PsgcCodeBarangay?.ToString() ?? CommonConstants.Null,  // ✅ this one IS still single-select, per your design — int?.ToString() is fine here
                filter.DataQualityIssue ?? CommonConstants.Null,
                filter.LastName ?? string.Empty,
                filter.FirstName ?? string.Empty,
                filter.MiddleName ?? string.Empty,
                filter.Suffix ?? string.Empty,
                filter.FullName ?? string.Empty,
                filter.Validator ?? string.Empty,
                filter.BatchCode ?? string.Empty,
                filter.Sex != null ? filter.Sex : CommonConstants.Null,

                // ✅ FIXED — PaymentStatus is also now multi-select; this key needs
                // to reflect PaymentStatuses (plural), not the dead singular field
                (filter.PaymentStatuses != null && filter.PaymentStatuses.Any())
                    ? string.Join(",", filter.PaymentStatuses.OrderBy(x => x))
                    : CommonConstants.Null,

                filter.PaymentDate?.ToFullDate() ?? CommonConstants.Null,
                filter.SpecificAge?.ToString() ?? CommonConstants.Null,
                filter.MilestoneYear?.ToString() ?? CommonConstants.Null,
                filter.SpecificBirthday?.ToFullDate() ?? CommonConstants.Null,
                filter.BirthdayFrom?.ToFullDate() ?? CommonConstants.Null,
                filter.BirthdayTo?.ToFullDate() ?? CommonConstants.Null,
                filter.PaymentDateFrom?.ToFullDate() ?? CommonConstants.Null,
                filter.PaymentDateTo?.ToFullDate() ?? CommonConstants.Null,
                filter.FindingStatus != null ? filter.FindingStatus : CommonConstants.Null,
                filter.SortColumn ?? CommonConstants.Default,
                filter.SortAscending.ToString(),
                filter.IsCompliant != null ? filter.IsCompliant.ToString() : CommonConstants.Null,
                filter.ComplianceMode ?? CommonConstants.Null,
                filter.IsEligible != null ? filter.IsEligible.ToString() : CommonConstants.Null,
                filter.EligibilityMode ?? CommonConstants.Null,
                filter.GeneralSearch ?? string.Empty,
                filter.CoStatus != null ? filter.CoStatus.ToString() : CommonConstants.Null,
                filter.ReplacementStatus != null ? filter.ReplacementStatus.ToString() : CommonConstants.Null,
                filter.FilterQuarter?.ToString() ?? CommonConstants.Null,
                filter.FilterFiscalYear?.ToString() ?? CommonConstants.Null,
                filter.FilterBatch ?? CommonConstants.Null,
                filter.FilterRefYear?.ToString() ?? CommonConstants.Null,
                filter.FilterRegionRoman ?? CommonConstants.Null,
                filter.FilterModeOfPayment?.ToString() ?? CommonConstants.Null,
                filter.IsLivenessVerified != null ? filter.IsLivenessVerified.ToString() : CommonConstants.Null,
                filter.IsReadyForEft != null ? filter.IsReadyForEft.ToString() : CommonConstants.Null,
                filter.DateAddedFrom?.ToString("yyyy-MM-dd") ?? CommonConstants.Null,
                filter.DateAddedTo?.ToString("yyyy-MM-dd") ?? CommonConstants.Null,
                filter.DateEndorsedFrom?.ToString("yyyy-MM-dd") ?? CommonConstants.Null,
                filter.DateEndorsedTo?.ToString("yyyy-MM-dd") ?? CommonConstants.Null,
                (filter.FilterPayrollQuarters != null && filter.FilterPayrollQuarters.Any()) // ✅ new
                    ? string.Join(",", filter.FilterPayrollQuarters.OrderBy(x => x))
                    : CommonConstants.Null
            );
        }
        private async Task<BeneficiaryInformation?> FindExistingAsync(string lastName, string firstName, string middleName, DateTime birthDate)
        {
            return await _repo.FindExistingAsync(
                lastName,
                firstName,
                middleName,
                birthDate);
        }
        #region Mapping method
        //Compliance of documentary requirements
        private static bool MapCompliance(string? value)
        {
            return value?.Trim().ToUpper() switch
            {
                CommonConstants.Compliant => true,
                CommonConstants.Compliance => true,
                CommonConstants.Yes => true,
                CommonConstants.NonCompliant => false,
                _ => false
            };
        }
        //Person with disability Map
        private static bool MapIsPersonWithDisability(string value)
        {
            var normalized = value.Trim().ToUpper();

            return normalized switch
            {
                CommonConstants.Yes => true,
                CommonConstants.NoUpperCase => false,
                _ => false
            };
        }
        //Map Indigenous People
        private static bool MapIndigenousPeople(string value)
        {
            var normalized = value.Trim().ToUpper();

            return normalized switch
            {
                CommonConstants.Yes => true,
                CommonConstants.NoUpperCase => false,
                _ => false
            };
        }
        //Map NCSC Assessment
        private static bool? MapEligibility(string value)
        {

            var normalized = value.Trim().ToUpper();

            return normalized switch
            {
                CommonConstants.Eligible => true,
                CommonConstants.Ineligible => false,
                _ => null
            }; ;
        }
        #endregion Mapping method end

        private string GetMonthName(int month)
        {
            return new DateTime(2000, month, 1)
                .ToMonth()
                .ToUpperInvariant();
        }

        private int GetAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
        }

        private static string? GetSuggestedName<T>(IEnumerable<T> items, Func<T, string?> nameSelector, string rawName, int minimumScore = 40) where T : class
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return null;

            var bestMatch = items
                .Select(item => new
                {
                    Name = nameSelector(item) ?? string.Empty,
                    Score = GetMatchScore(rawName, nameSelector(item) ?? string.Empty)
                })
                .Where(x => x.Score >= minimumScore)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Name)
                .FirstOrDefault();

            return bestMatch?.Name;
        }
        // ✅ Call this from the SAME places InvalidateSummaryCache() is already
        // called — Create, Update, BulkUpdate*, SoftDelete. A new/edited/deleted
        // record can change whether a duplicate pair exists, so the cache must
        // be explicitly cleared, not just left to expire on a timer.
        private void InvalidateGlobalDuplicateCache()
        {
            _memoryCache.Remove(GlobalDuplicateScanCacheKey);
        }
        // Wire InvalidateGlobalDuplicateCache() into InvalidateSummaryCache()
        // itself, since every call site that already calls the summary invalidation
        // should also invalidate this — one method, one call site per mutation,
        // no risk of forgetting one or the other.

        private void InvalidateSummaryCache()
        {
            // ✅ Bump the version token — invalidates all summary cache entries
            var newVersion = Guid.NewGuid().ToString();
            _memoryCache.Set(CommonConstants.SummaryCacheVersionKey, newVersion);

            // ✅ Remove all duplicate scan cache entries
            // We can't enumerate IMemoryCache keys directly, so we use
            // a version token pattern for the duplicate scan too
            _memoryCache.Set(
                CommonConstants.DuplicateScanCacheVersionKey,
                Guid.NewGuid().ToString());

            //New - bust the global (navbar bell) duplicate cache too
            InvalidateGlobalDuplicateCache();

            // ✅ NEW: Invalidate statistics cache
            _statisticsService.InvalidateStatisticsCacheAsync().GetAwaiter().GetResult();
        }

        private string GetCurrentSummaryCacheVersion()
        {
            return _memoryCache.GetOrCreate(CommonConstants.SummaryCacheVersionKey, entry =>
            {
                entry.Priority = CacheItemPriority.NeverRemove;
                return CommonConstants.V1;
            })!;
        }

        private string BuildSummaryCacheKey(BeneficiaryFilterDto filter)
        {
            var version = GetCurrentSummaryCacheVersion();

            return string.Join("|",
                version,
                CommonConstants.BeneficiarySummary,
                filter.PsgcCodeRegion?.ToString() ?? CommonConstants.Null,
                (filter.PsgcCodeProvinces != null && filter.PsgcCodeProvinces.Any() ? string.Join(",", filter.PsgcCodeProvinces.OrderBy(x => x)) : CommonConstants.Null),
                (filter.PsgcCodeMunicipalities != null && filter.PsgcCodeMunicipalities.Any() ? string.Join(",", filter.PsgcCodeMunicipalities.OrderBy(x => x)) : CommonConstants.Null),
                filter.PsgcCodeBarangay?.ToString() ?? CommonConstants.Null,   // ✅ FIX: singular, matches actual query field
                filter.DataQualityIssue ?? CommonConstants.Null,
                filter.LastName ?? string.Empty,
                filter.FirstName ?? string.Empty,
                filter.MiddleName ?? string.Empty,
                filter.Suffix ?? string.Empty,
                filter.FullName ?? string.Empty,                              // ✅ ADDED
                filter.Sex != null ? filter.Sex : CommonConstants.Null,
                (filter.PaymentStatuses != null && filter.PaymentStatuses.Any())
                    ? string.Join(",", filter.PaymentStatuses.OrderBy(x => x))
                        : CommonConstants.Null,
                filter.PaymentDate?.ToFullDate() ?? CommonConstants.Null,
                filter.SpecificAge?.ToString() ?? CommonConstants.Null,
                filter.MilestoneYear?.ToString() ?? CommonConstants.Null,
                filter.SpecificBirthday?.ToFullDate() ?? CommonConstants.Null,
                filter.BirthdayFrom?.ToFullDate() ?? CommonConstants.Null,
                filter.BirthdayTo?.ToFullDate() ?? CommonConstants.Null,
                filter.PaymentDateFrom?.ToFullDate() ?? CommonConstants.Null,
                filter.PaymentDateTo?.ToFullDate() ?? CommonConstants.Null,
                filter.FindingStatus != null ? filter.FindingStatus : CommonConstants.Null,
                filter.IsCompliant != null ? filter.IsCompliant.ToString() : CommonConstants.Null,
                filter.IsEligible != null ? filter.IsEligible.ToString() : CommonConstants.Null,
                filter.ComplianceMode ?? CommonConstants.Null,
                filter.EligibilityMode ?? CommonConstants.Null,
                filter.CoStatus != null ? filter.CoStatus.ToString() : CommonConstants.Null,
                filter.ReplacementStatus != null ? filter.ReplacementStatus.ToString() : CommonConstants.Null,
                filter.FilterQuarter?.ToString() ?? CommonConstants.Null,
                filter.FilterFiscalYear?.ToString() ?? CommonConstants.Null,
                filter.FilterBatch ?? CommonConstants.Null,
                filter.FilterRefYear?.ToString() ?? CommonConstants.Null,
                filter.FilterRegionRoman ?? CommonConstants.Null,
                filter.FilterModeOfPayment?.ToString() ?? CommonConstants.Null,
                filter.IsLivenessVerified != null ? filter.IsLivenessVerified.ToString() : CommonConstants.Null,
                filter.IsReadyForEft != null ? filter.IsReadyForEft.ToString() : CommonConstants.Null,
                filter.Validator ?? string.Empty,                             // ✅ ADDED
                filter.BatchCode ?? string.Empty,                             // ✅ ADDED
                filter.GeneralSearch ?? string.Empty,                         // ✅ ADDED — the critical one
                filter.DateAddedFrom?.ToString("yyyy-MM-dd") ?? CommonConstants.Null,  // ✅ ADDED
                filter.DateAddedTo?.ToString("yyyy-MM-dd") ?? CommonConstants.Null,    // ✅ ADDED
                filter.DateEndorsedFrom?.ToString("yyyy-MM-dd") ?? CommonConstants.Null,
                filter.DateEndorsedTo?.ToString("yyyy-MM-dd") ?? CommonConstants.Null,
                (filter.FilterPayrollQuarters != null && filter.FilterPayrollQuarters.Any())  // ✅ new
                    ? string.Join(",", filter.FilterPayrollQuarters.OrderBy(x => x))
                    : CommonConstants.Null
            );
        }
        //Updating a beneficiary record involves comparing the existing values with the new values from the DTO and logging any changes. This method generates a list of changed fields for logging purposes.
        private async Task<List<string>> GetChangedFields(
     BeneficiaryInformation beneficiary, BeneficiaryInformationDto dto)
        {
            var changes = new List<string>();

            if (beneficiary.Quarter != dto.Quarter)
                changes.Add($"Quarter '{beneficiary.Quarter}' → '{dto.Quarter}'");

            if (beneficiary.Batch != dto.Batch)
                changes.Add($"Batch '{beneficiary.Batch}' → '{dto.Batch}'");

            if (beneficiary.RefYear != dto.RefYear)
                changes.Add($"Ref Year '{beneficiary.RefYear}' → '{dto.RefYear}'");

            // ── Simple text fields ────────────────────────────────
            if (beneficiary.DateApplied != dto.DateApplied)
                changes.Add($"{CommonConstants.DateApplied.ToTitleCase()} '{beneficiary.DateApplied.ToFullDate()}' → '{dto.DateApplied.ToFullDate()}'");

            if (beneficiary.DateEndorsed != dto.DateEndorsed)
                changes.Add($"{CommonConstants.DateEndorsed.ToTitleCase()} '{beneficiary.DateEndorsed.ToFullDate()}' → '{dto.DateEndorsed.ToFullDate()}'");

            if (beneficiary.BatchCode != dto.BatchCode)
                changes.Add($"{CommonConstants.BatchCode.ToTitleCase()} '{beneficiary.BatchCode}' → '{dto.BatchCode}'");

            if (beneficiary.OscaIdNumber != dto.OscaIdNumber)
                changes.Add($"{CommonConstants.OscaIdNumber.ToTitleCase()} '{beneficiary.OscaIdNumber}' → '{dto.OscaIdNumber}'");

            if (beneficiary.OscaIdDateIssued != dto.OscaIdDateIssued)
                changes.Add($"{CommonConstants.OscaIdDateIssued.ToTitleCase()} '{beneficiary.OscaIdDateIssued.ToFullDate()}' → '{dto.OscaIdDateIssued.ToFullDate()}'");

            if (beneficiary.NcscRrn != dto.NcscRrn)
                changes.Add($"{CommonConstants.NcscRrn.ToTitleCase()} '{beneficiary.NcscRrn}' → '{dto.NcscRrn}'");

            if (beneficiary.LastName != dto.LastName)
                changes.Add($"{CommonConstants.LastName.ToTitleCase()} '{beneficiary.LastName}' → '{dto.LastName}'");

            if (beneficiary.FirstName != dto.FirstName)
                changes.Add($"{CommonConstants.FirstName.ToTitleCase()} '{beneficiary.FirstName}' → '{dto.FirstName}'");

            if (beneficiary.MiddleName != dto.MiddleName)
                changes.Add($"{CommonConstants.MiddleName.ToTitleCase()} '{beneficiary.MiddleName}' → '{dto.MiddleName}'");

            if (beneficiary.Extension != dto.Extension)
                changes.Add($"{CommonConstants.Extension.ToTitleCase()} '{beneficiary.Extension}' → '{dto.Extension}'");

            if (beneficiary.BirthDate.Date != dto.BirthDate.Date)
                changes.Add($"{CommonConstants.BirthDate.ToTitleCase()} '{beneficiary.BirthDate.ToFullDate()}' → '{dto.BirthDate.ToFullDate()}'");

            // ── Mapped fields ─────────────────────────────────────
            if (beneficiary.Sex != dto.Sex)
                changes.Add($"{CommonConstants.Sex.ToTitleCase()} '{MapSexLabel(beneficiary.Sex)}' → '{MapSexLabel(dto.Sex)}'");

            if (beneficiary.IsIndigenousPeople != dto.IsIndigenousPeople)
                changes.Add($"{CommonConstants.IP.ToTitleCase()} '{MapTriStateLabel(beneficiary.IsIndigenousPeople)}' → '{MapTriStateLabel(dto.IsIndigenousPeople)}'");

            if (beneficiary.IsPersonWithDisability != dto.IsPersonWithDisability)
                changes.Add($"{CommonConstants.PWD.ToTitleCase()} '{MapTriStateLabel(beneficiary.IsPersonWithDisability)}' → '{MapTriStateLabel(dto.IsPersonWithDisability)}'");

            if (beneficiary.CivilStatus != dto.CivilStatus)
                changes.Add($"{CommonConstants.CivilStatus.ToTitleCase()} '{MapCivilStatusLabel(beneficiary.CivilStatus)}' → '{MapCivilStatusLabel(dto.CivilStatus)}'");

            if (beneficiary.Citizenship != dto.Citizenship)
                changes.Add($"{CommonConstants.Citizenship.ToTitleCase()} '{MapCitizenshipLabel(beneficiary.Citizenship)}' → '{MapCitizenshipLabel(dto.Citizenship)}'");

            // ── More mapped fields ────────────────────────────────
            if (beneficiary.IsCompliant != dto.IsCompliant)
                changes.Add($"{CommonConstants.Compliant.ToTitleCase()} '{(beneficiary.IsCompliant ? CommonConstants.Yes : CommonConstants.No)}' → '{(dto.IsCompliant ? CommonConstants.Yes : CommonConstants.No)}'");
            if (beneficiary.Validator != dto.Validator)
                changes.Add($"{CommonConstants.Validator.ToTitleCase()} '{beneficiary.Validator}' → '{dto.Validator}'");

            if (beneficiary.ValidationDate != dto.ValidationDate)
                changes.Add($"{CommonConstants.ValidationDate.ToTitleCase()} '{beneficiary.ValidationDate.ToFullDate()}' → '{dto.ValidationDate.ToFullDate()}'");

            if (beneficiary.PaymentStatus != dto.PaymentStatus)
                changes.Add($"{CommonConstants.PaymentStatus.ToTitleCase()} '{MapPaymentStatusLabel(beneficiary.PaymentStatus)}' → '{MapPaymentStatusLabel(dto.PaymentStatus)}'");

            if (beneficiary.ModeOfPayment != dto.ModeOfPayment)
                changes.Add($"{CommonConstants.ModeOfPayment} '{MapModeOfPaymentLabel(beneficiary.ModeOfPayment)}' → '{MapModeOfPaymentLabel(dto.ModeOfPayment)}'");

            if (beneficiary.PaymentDate != dto.PaymentDate)
                changes.Add($"{CommonConstants.PaymentDate.ToTitleCase()} '{beneficiary.PaymentDate.ToFullDate()}' → '{dto.PaymentDate.ToFullDate()}'");

            if (beneficiary.IsDeceased != dto.IsDeceased)
                changes.Add($"{CommonConstants.IsDeceased.ToTitleCase()} '{(beneficiary.IsDeceased ? CommonConstants.Yes : CommonConstants.No)}' → '{(dto.IsDeceased ? CommonConstants.Yes : CommonConstants.No)}'");

            if (beneficiary.DateOfDeath != dto.DateOfDeath)
                changes.Add($"{CommonConstants.DateOfDeath} '{beneficiary.DateOfDeath.ToFullDate()}' → '{dto.DateOfDeath.ToFullDate()}'");

            if (beneficiary.IsEligible != dto.IsEligible)
                changes.Add($"{CommonConstants.Eligible.ToTitleCase()} '{(beneficiary.IsEligible ? CommonConstants.Yes : CommonConstants.No)}' → '{(dto.IsEligible ? CommonConstants.Yes : CommonConstants.No)}'");
            //Compliant remarks
            if (beneficiary.AssessmentRemarks != dto.AssessmentRemarks)
                changes.Add($"{CommonConstants.AssessmentRemarks} '{beneficiary.AssessmentRemarks}' → '{dto.AssessmentRemarks}'");

            //Eligibility remarks
            if (beneficiary.EligibilityRemarks != dto.EligibilityRemarks)
                changes.Add($"{CommonConstants.EligibilityRemarks} '{beneficiary.EligibilityRemarks}' → '{dto.EligibilityRemarks}'");

            if (beneficiary.RemarkCategory != dto.RemarkCategory)
                changes.Add($"{CommonConstants.RemarkCategory} '{MapRemarkCategoryLabel(beneficiary.RemarkCategory)}' → '{MapRemarkCategoryLabel(dto.RemarkCategory)}'");

            if (beneficiary.CoStatus != dto.CoStatus)
                changes.Add($"CO Status '{CoStatusLabel(beneficiary.CoStatus ?? 0)}'" +
                            $" → '{CoStatusLabel(dto.CoStatus ?? 0)}'");

            if (beneficiary.CoDateEndorsed != dto.CoDateEndorsed)
                changes.Add($"CO Date Endorsed '{beneficiary.CoDateEndorsed.ToFullDate()}'" +
                            $" → '{dto.CoDateEndorsed.ToFullDate()}'");

            if (beneficiary.CoDateApproved != dto.CoDateApproved)
                changes.Add($"CO Date Approved '{beneficiary.CoDateApproved.ToFullDate()}'" +
                            $" → '{dto.CoDateApproved.ToFullDate()}'");

            if (beneficiary.Remarks != dto.Remarks)
                changes.Add($"{CommonConstants.Remarks.ToTitleCase()} '{beneficiary.Remarks}' → '{dto.Remarks}'");

            if (beneficiary.PayrollQuarter != dto.PayrollQuarter)
                changes.Add($"Payroll Quarter '{beneficiary.PayrollQuarter}' → '{dto.PayrollQuarter}'");

            if (beneficiary.FiscalYear != dto.FiscalYear)
                changes.Add($"Fiscal Year '{beneficiary.FiscalYear}' → '{dto.FiscalYear}'");
            // ✅ NEW — Annex A fields. Without this, edits to these columns (which
            // UpdateAnnexADetails() does persist correctly) never show up in the
            // beneficiary's Logs modal — the diff list is what actually feeds the
            // audit trail, so a field can be saved correctly and still be invisible
            // to anyone reviewing history.
            if (beneficiary.TrackingNumber != dto.TrackingNumber)
                changes.Add($"Tracking Number '{beneficiary.TrackingNumber}' → '{dto.TrackingNumber}'");

            if (beneficiary.DataPrivacyConsent != dto.DataPrivacyConsent)
                changes.Add($"Data Privacy Consent '{(beneficiary.DataPrivacyConsent == true ? CommonConstants.Yes : CommonConstants.No)}' → '{(dto.DataPrivacyConsent == true ? CommonConstants.Yes : CommonConstants.No)}'");

            if (beneficiary.PlaceOfSubmission != dto.PlaceOfSubmission)
                changes.Add($"Place of Submission '{MapPlaceOfSubmissionLabel(beneficiary.PlaceOfSubmission)}' → '{MapPlaceOfSubmissionLabel(dto.PlaceOfSubmission)}'");

            if (beneficiary.HouseNumber != dto.HouseNumber)
                changes.Add($"House Number '{beneficiary.HouseNumber}' → '{dto.HouseNumber}'");

            if (beneficiary.StreetName != dto.StreetName)
                changes.Add($"Street Name '{beneficiary.StreetName}' → '{dto.StreetName}'");

            if (beneficiary.ZipCode != dto.ZipCode)
                changes.Add($"Zip Code '{beneficiary.ZipCode}' → '{dto.ZipCode}'");

            if (beneficiary.DisabilityType != dto.DisabilityType)
                changes.Add($"Disability Type '{beneficiary.DisabilityType}' → '{dto.DisabilityType}'");

            if (beneficiary.EthnicityName != dto.EthnicityName)
                changes.Add($"Ethnicity / IP Group '{beneficiary.EthnicityName}' → '{dto.EthnicityName}'");

            if (beneficiary.DualCitizenshipDetails != dto.DualCitizenshipDetails)
                changes.Add($"Dual Citizenship Details '{beneficiary.DualCitizenshipDetails}' → '{dto.DualCitizenshipDetails}'");

            if (beneficiary.CivilStatusOtherDetail != dto.CivilStatusOtherDetail)
                changes.Add($"Civil Status (Other) '{beneficiary.CivilStatusOtherDetail}' → '{dto.CivilStatusOtherDetail}'");

            if (beneficiary.IsSignedDeclaration != dto.IsSignedDeclaration)
                changes.Add($"Signed Declaration '{(beneficiary.IsSignedDeclaration ? CommonConstants.Yes : CommonConstants.No)}' → '{(dto.IsSignedDeclaration ? CommonConstants.Yes : CommonConstants.No)}'");

            if (beneficiary.DateSigned != dto.DateSigned)
                changes.Add($"Date Signed '{beneficiary.DateSigned.ToFullDate()}' → '{dto.DateSigned.ToFullDate()}'");

            if (beneficiary.IsLivenessVerified != dto.IsLivenessVerified)
                changes.Add($"Liveness Verified '{MapTriStateLabel(beneficiary.IsLivenessVerified)}' → '{MapTriStateLabel(dto.IsLivenessVerified)}'");

            if (beneficiary.DateOfLiveness != dto.DateOfLiveness)
                changes.Add($"Date of Liveness '{beneficiary.DateOfLiveness.ToFullDate()}' → '{dto.DateOfLiveness.ToFullDate()}'");

            if (beneficiary.IsReadyForEft != dto.IsReadyForEft)
                changes.Add($"Ready for EFT '{MapTriStateLabel(beneficiary.IsReadyForEft)}' → '{MapTriStateLabel(dto.IsReadyForEft)}'");
            return changes;
        }
        private async Task AddLogAsync(Guid beneficiaryId, string activity, string userName)
        {
            var log = new Log
            {
                Id = Guid.NewGuid(),
                BeneficiaryInformationId = beneficiaryId,
                Activity = activity,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            };
            await _logRepository.AddAsync(log);
        }

        // Not tied to a single beneficiary — e.g. exports/payroll downloads covering many records.
        private async Task AddSystemLogAsync(string activity, string userName, string category)
        {
            var log = new Log
            {
                Id = Guid.NewGuid(),
                BeneficiaryInformationId = null,
                Activity = activity,
                UserName = userName,
                CreatedAt = DateTime.UtcNow,
                Category = category
            };
            await _logRepository.AddAsync(log);
            await _logRepository.SaveChangesAsync();
        }
        private static T? FindBestNameMatch<T>(IEnumerable<T> items, Func<T, string?> nameSelector, string rawName,
        int minimumScore = 60) where T : class
        {
            var matches = items
                .Select(item => new
                {
                    Item = item,
                    Name = nameSelector(item) ?? string.Empty,
                    Score = GetMatchScore(rawName, nameSelector(item) ?? string.Empty)
                })
                .Where(x => x.Score >= minimumScore)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Name)
                .ToList();

            return matches.FirstOrDefault()?.Item;
        }

        private static string? NullIfEmpty(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string NormalizeName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Trim().ToUpperInvariant();

            normalized = normalized.Replace(".", " ");
            normalized = normalized.Replace(",", " ");
            normalized = normalized.Replace("-", " ");
            normalized = normalized.Replace("_", " ");
            normalized = normalized.Replace("(", " ");
            normalized = normalized.Replace(")", " ");
            normalized = normalized.Replace("/", " ");
            normalized = normalized.Replace("'", " ");

            normalized = normalized.Replace("BRGY", "BARANGAY");
            normalized = normalized.Replace("BGY", "BARANGAY");
            normalized = normalized.Replace("POB.", "POBLACION");
            normalized = normalized.Replace("POB", "POBLACION");
            normalized = normalized.Replace("MUN.", "MUNICIPALITY");
            normalized = normalized.Replace("CITY OF", "CITY");
            normalized = normalized.Replace("MUNICIPALITY OF", "MUNICIPALITY");
            normalized = normalized.Replace("ST.", "SAINT");
            normalized = normalized.Replace("STA.", "SANTA");
            normalized = normalized.Replace("STO.", "SANTO");

            while (normalized.Contains("  "))
                normalized = normalized.Replace("  ", " ");

            return normalized.Trim();
        }
        private static int GetMatchScore(string sourceName, string candidateName)
        {
            var sourceNormalized = NormalizeName(sourceName);
            var candidateNormalized = NormalizeName(candidateName);

            if (string.IsNullOrWhiteSpace(sourceNormalized) || string.IsNullOrWhiteSpace(candidateNormalized))
                return 0;

            // Best case: exact normalized match
            if (sourceNormalized == candidateNormalized)
                return 100;

            // Good case: one fully contains the other
            if (candidateNormalized.Contains(sourceNormalized) || sourceNormalized.Contains(candidateNormalized))
                return 80;

            var sourceTokens = GetMeaningfulTokens(sourceNormalized);
            var candidateTokens = GetMeaningfulTokens(candidateNormalized);

            if (!sourceTokens.Any() || !candidateTokens.Any())
                return 0;

            var matchedTokens = sourceTokens.Intersect(candidateTokens).Count();

            if (matchedTokens == 0)
                return 0;

            // Score based on token overlap
            var score = matchedTokens * 20;

            // Bonus if all source tokens are present in candidate
            if (sourceTokens.All(t => candidateTokens.Contains(t)))
                score += 20;

            return score;
        }

        private static HashSet<string> GetMeaningfulTokens(string? value)
        {
            var normalized = NormalizeName(value);

            var ignoredWords = new HashSet<string>
             {
                 "BARANGAY",
                 "POBLACION",
                 "CITY",
                 "MUNICIPALITY",
                 "THE",
                 "AND",
                 "OF"
             };

            return normalized
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(token => !ignoredWords.Contains(token))
                .ToHashSet();
        }

        private static int MapSex(string? value)
        {
            return value?.Trim().ToUpper() switch
            {
                CommonConstants.Male => 1,
                CommonConstants.Female => 2,
                _ => 0
            };
        }

        private DateTime? ParseFlexibleDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // Try exact formats first (faster & safer)
            var formats = new[]
            {
                 "MMMM d, yyyy",   // March 17, 2026
                 "MMM d, yyyy",    // Mar 17, 2026
                 "MM/dd/yyyy",
                 "M/d/yyyy",
                 "yyyy-MM-dd"
            };

            if (DateTime.TryParseExact(value.Trim(), formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            {
                return parsed;
            }

            // Fallback (handles Excel weird formats)
            if (DateTime.TryParse(value, out parsed))
                return parsed;

            return null;
        }
        private static bool TryParseExcelDate(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value)) return false;

            // ✅ Normalize to title case so "JANUARY 31 1936" becomes "January 31 1936"
            var normalized = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Trim().ToLower());

            if (DateTime.TryParse(normalized, out date))
                return true;

            var formats = new[]
            {
        "MMMM d yyyy",
        "MMMM dd yyyy",
        "MMM d yyyy",
        "MMM dd yyyy",
        "M/d/yyyy",
        "MM/dd/yyyy",
        "M/d/yy",
        "MM/dd/yy",
        "yyyy-MM-dd",
        "M-d-yyyy",
        "MM-d-yyyy",
    };

            return DateTime.TryParseExact(
                normalized,            // ✅ Use normalized (title case) value
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);
        }



        private static DateTime? ParseNullableDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (DateTime.TryParse(value, out var parsed))
                return parsed;

            var formats = new[]
            {
        "M/d/yyyy",
        "MM/dd/yyyy",
        "M/d/yy",
        "MM/dd/yy",
        "yyyy-MM-dd",
        "M-d-yyyy",
        "MM-d-yyyy"
    };

            if (DateTime.TryParseExact(
                value,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed))
                return parsed;

            return null;
        }

        private static string MapSexLabel(int sex) => sex switch
        {
            1 => CommonConstants.Male.ToTitleCase(),
            2 => CommonConstants.Female.ToTitleCase(),
            _ => CommonConstants.Unknown
        };
        private static string MapCitizenshipLabel(int? citizenship) => citizenship switch
        {
            1 => CommonConstants.Filipino.ToTitleCase(),
            2 => CommonConstants.DualCitizenship.ToTitleCase(),
            _ => CommonConstants.None
        };

        private static string MapPaymentStatusLabel(int status) => status switch
        {
            0 => CommonConstants.None,
            1 => CommonConstants.Unpaid,
            2 => CommonConstants.Paid,
            3 => CommonConstants.Pending,
            _ => CommonConstants.Unknown
        };

        private static string MapModeOfPaymentLabel(int mode) => mode switch
        {
            0 => CommonConstants.None,
            1 => CommonConstants.CashAdvanceBySdo,
            2 => CommonConstants.BankTransfer,
            _ => CommonConstants.Unknown
        };

        private static string MapRemarkCategoryLabel(int? category) => category switch
        {
            1 => CommonConstants.DeceasedPriorReachingMilestoneAge,
            2 => CommonConstants.OutOfTownOrCountry,
            3 => CommonConstants.IncompleteRequiredDocuments,
            4 => CommonConstants.InconsistentDocuments,
            5 => CommonConstants.CannotBeReachedOrLocated,
            6 => CommonConstants.DidNotReachTheMilestoneAge,
            7 => CommonConstants.LackingOfDocumentsOrRequirements,
            8 => CommonConstants.ForCorrection,
            9 => CommonConstants.Waived,
            10 => CommonConstants.LackingProofOfRelationship,
            11 => CommonConstants.NoShow,
            12 => CommonConstants.DoubleApplicationWithDifferentSurnameUsed,
            13 => CommonConstants.ForCGDIslandMunicipality,
            14 => CommonConstants.Others,
            15 => CommonConstants.TransferToOtherRegion,
            _ => CommonConstants.None
        };

        #endregion

    }
}
