using ClosedXML.Excel;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Common.Extensions;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;
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
        public BeneficiaryInformationService(IBeneficiaryInformationRepository repo, IRegionRepository regionRepository
            , IProvinceRepository provinceRepository, IMunicipalityRepository municipalityRepository, IBarangayRepository barangayRepository,
            ILogRepository logRepository, IMemoryCache memoryCache)
        {
            _repo = repo;
            _regionRepository = regionRepository;
            _provinceRepository = provinceRepository;
            _municipalityRepository = municipalityRepository;
            _barangayRepository = barangayRepository;
            _logRepository = logRepository;
            _memoryCache = memoryCache;
        }
        public async Task<byte[]> ExportFilteredAsTemplateAsync(BeneficiaryFilterDto filter)
        {
            var rawData = await _repo.FilterAsync(filter);
            var data = rawData
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.MiddleName)
                .ToList();

            if (data == null || !data.Any())
                throw new InvalidOperationException(CommonConstants.NoDataAvailableToExport);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(CommonConstants.Grantees);

            // =========================
            // ✅ TITLE HEADER (ROW 1–9)
            // =========================

            int colCount = 21; // total columns in your sheet

            void AddCenteredTitle(int row, string text)
            {
                var range = worksheet.Range(row, 1, row, colCount);
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
                CommonConstants.NameOfValidator, CommonConstants.ValidationDate, CommonConstants.Remarks
            };

            for (int col = 1; col <= headers.Length; col++)
            {
                worksheet.Cell(headerRow, col).Value = headers[col - 1];
            }

            // STYLE HEADER
            var headerRange = worksheet.Range(headerRow, 1, headerRow, 21);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // After AddCenteredTitle(4, ...)
            PayrollLogos.AddLogos(
                worksheet,
                anchorRow: 1,      // anchor to row 1 (title rows are 1-4)
                leftCol: 1,      // col A — left edge
                rightCol: 21,     // col U — right edge (your last column)
                widthPx: 60,
                heightPx: 60,
                offsetLeft: 4,
                offsetRight: 270);

            // =========================
            // ✅ DATA (ROW 11+)
            // =========================
            int row = 11;
            int counter = 1;

            foreach (var item in data)
            {
                worksheet.Cell(row, 1).Value = item.BatchCode?.ToUpperInvariant();
                worksheet.Cell(row, 2).Value = counter++;

                worksheet.Cell(row, 3).Value = item.OscaIdNumber?.ToUpperInvariant();
                worksheet.Cell(row, 4).Value = item.NcscRrn;

                worksheet.Cell(row, 5).Value = item.LastName?.ToUpperInvariant();
                worksheet.Cell(row, 6).Value = item.FirstName?.ToUpperInvariant();
                worksheet.Cell(row, 7).Value = item.MiddleName?.ToUpperInvariant();
                worksheet.Cell(row, 8).Value = item.Extension?.ToUpperInvariant();

                // MONTH AS TEXT
                worksheet.Cell(row, 9).Value = GetMonthName(item.BirthDate.Month);
                worksheet.Cell(row, 10).Value = item.BirthDate.Day.ToPaddedDay();
                worksheet.Cell(row, 11).Value = item.BirthDate.Year;

                // AGE (NEW COLUMN)
                worksheet.Cell(row, 12).Value = GetAge(item.BirthDate);

                worksheet.Cell(row, 13).Value = item.Sex == 1 ? CommonConstants.Male : CommonConstants.Female;

                worksheet.Cell(row, 14).Value = item.Region?.ToString().ToUpperInvariant();
                worksheet.Cell(row, 15).Value = item.Province?.ToString().ToUpperInvariant();
                worksheet.Cell(row, 16).Value = item.Municipality?.ToString().ToUpperInvariant();
                worksheet.Cell(row, 17).Value = item.Barangay?.ToString().ToUpperInvariant();

                // COMPLIANCE MAPPING
                worksheet.Cell(row, 18).Value = item.IsCompliant
                    ? CommonConstants.Compliant
                    : CommonConstants.NonCompliant;

                worksheet.Cell(row, 19).Value = item.Validator?.ToUpperInvariant();
                worksheet.Cell(row, 20).Value = item.ValidationDate.ToDefaultFormat();
                worksheet.Cell(row, 21).Value = item.Remarks?.ToUpperInvariant();

                worksheet.Range(row, 1, row, 21)
                    .Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                row++;
            }
            // ✅ ADD HERE — border entire used range
            worksheet.Range(11, 1, row - 1, 21).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            worksheet.Range(11, 1, row - 1, 21).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // =========================
            // ✅ AUTO FORMAT
            // =========================
            worksheet.Columns().AdjustToContents();

            // Freeze header
            worksheet.SheetView.FreezeRows(10);

            // Auto filter
            worksheet.Range(headerRow, 1, headerRow, 21).SetAutoFilter();
            // =========================
            // ✅ SIGNATURE BLOCK
            // =========================
            int signatureStartRow = row + 2;
            string today = DateTime.Today.ToCompeleteDate();

            // Helper to build one signature block
            void AddSignatureBlock(int labelCol, int blockStartCol, int blockEndCol, int nameStartCol, int nameEndCol, string role)
            {
                // Role label — flush left
                worksheet.Cell(signatureStartRow, labelCol).Value = role;
                worksheet.Cell(signatureStartRow, labelCol).Style.Font.Bold = true;

                int nameRow = signatureStartRow + 3;

                // Name — italic placeholder, flush left, with underline
                var nameRange = worksheet.Range(nameRow, nameStartCol, nameRow, nameEndCol);
                nameRange.Merge();
                nameRange.Value = CommonConstants.EnterName;
                nameRange.Style.Font.Italic = true;
                nameRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                // Position — italic placeholder, flush left, with underline
                var posRange = worksheet.Range(nameRow + 1, nameStartCol, nameRow + 1, nameEndCol);
                posRange.Merge();
                posRange.Value = CommonConstants.EnterPosition;
                posRange.Style.Font.Italic = true;
                posRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                // "Signature over printed name" — flush left, no center
                var sigRange = worksheet.Range(nameRow + 2, blockStartCol, nameRow + 2, blockEndCol);
                sigRange.Merge();
                sigRange.Value = CommonConstants.SignatureOverPrintedName;
                sigRange.Style.Font.Italic = true;
                sigRange.Style.Font.FontSize = 8;
                sigRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                // Date — flush left, no center
                var dateRange = worksheet.Range(nameRow + 3, blockStartCol, nameRow + 3, blockEndCol);
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
            worksheet.PageSetup.PaperSize = XLPaperSize.LegalPaper;

            // Landscape orientation
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;

            // Fit all columns on one page (scale to width), unlimited rows
            worksheet.PageSetup.FitToPages(1, 0);

            // Repeat ONLY the column header row (row 10) on every printed page
            // This excludes rows 1-9 (the main title header)
            worksheet.PageSetup.SetRowsToRepeatAtTop(10, 10);

            // Page numbering — "Page 1 of 12" format
            // Center footer
            worksheet.PageSetup.Footer.Center.AddText(CommonConstants.Page);
            worksheet.PageSetup.Footer.Center.AddText(XLHFPredefinedText.PageNumber);
            worksheet.PageSetup.Footer.Center.AddText(CommonConstants.Of);
            worksheet.PageSetup.Footer.Center.AddText(XLHFPredefinedText.NumberOfPages);
            // Push footer below content area
            worksheet.PageSetup.Margins.Bottom = 0.7; // inches — gives footer room
            worksheet.PageSetup.Margins.Footer = 0.5; // inches — footer distance from bottom edge
            // =========================

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
        public async Task<BeneficiaryInformationDto> CreateAsync(CreateBeneficiaryInformationDto dto, string userName)
        {
            //Check duplicates
            var isDuplicate = await _repo.ExistsDuplicateAsync(
                dto.LastName,
                dto.FirstName,
                dto.MiddleName,
                dto.BirthDate,
                dto.OscaIdNumber,
                dto.NcscRrn);
            if (isDuplicate)
                throw new Exception(CommonConstants.DuplicateFound);

            var beneficiary = new BeneficiaryInformation
            {
                Id = Guid.NewGuid(),
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
                PhoneNumber = dto.PhoneNumber,
                IsIndigenousPeople = dto.IsIndigenousPeople,
                IsPersonWithDisability = dto.IsPersonWithDisability,
                CivilStatus = dto.CivilStatus,
                Citizenship = dto.Citizenship,
                Sex = dto.Sex,
                Region = CaragaEnum.DefaultRegionCode,
                Province = dto.PsgcCodeProvince,
                Municipality = dto.PsgcCodeMunicipality,
                Barangay = dto.PsgcCodeBarangay,
                IsCompliant = dto.IsCompliant,
                Validator = dto.Validator,
                ValidationDate = dto.ValidationDate,
                PaymentStatus = dto.PaymentStatus,
                ModeOfPayment = dto.ModeOfPayment,
                PaymentDate = dto.PaymentDate,
                IsDeceased = dto.IsDeceased,
                DateOfDeath = dto.DateOfDeath,
                IsEligible = dto.IsEligible,
                AssessmentRemarks = dto.AssessmentRemarks,
                RemarkCategory = dto.RemarkCategory,
                Remarks = dto.Remarks,
                DateAdded = DateTime.UtcNow,
                IsDeleted = false
            };

            await _repo.AddAsync(beneficiary);

            //Logging
            await AddLogAsync(
        beneficiary.Id,
        $"{CommonConstants.CreatedBeneficiary} {beneficiary.LastName}, {beneficiary.FirstName}",
        userName);

            await _repo.SaveChangesAsync();

            InvalidateSummaryCache();

            return new BeneficiaryInformationDto
            {
                Id = beneficiary.Id,
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
                PhoneNumber = beneficiary.PhoneNumber,
                Sex = beneficiary.Sex,
                IsIndigenousPeople = beneficiary.IsIndigenousPeople,
                IsPersonWithDisability = beneficiary.IsPersonWithDisability,
                CivilStatus = beneficiary.CivilStatus,
                Citizenship = beneficiary.Citizenship,
                PsgcCodeRegion = CaragaEnum.DefaultRegionCode,
                PsgcCodeProvince = beneficiary.Province,
                PsgcCodeMunicipality = beneficiary.Municipality,
                PsgcCodeBarangay = beneficiary.Barangay,
                IsCompliant = beneficiary.IsCompliant,
                Validator = beneficiary.Validator,
                ValidationDate = beneficiary.ValidationDate,
                PaymentStatus = beneficiary.PaymentStatus,
                ModeOfPayment = beneficiary.ModeOfPayment,
                PaymentDate = beneficiary.PaymentDate,
                IsDeceased = beneficiary.IsDeceased,
                DateOfDeath = beneficiary.DateOfDeath,
                IsEligible = beneficiary.IsEligible,
                AssessmentRemarks = beneficiary.AssessmentRemarks,
                RemarkCategory = beneficiary.RemarkCategory,
                Remarks = beneficiary.Remarks,
                DateAdded = beneficiary.DateAdded,
                IsDeleted = beneficiary.IsDeleted
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

            return getById;
        }

        public async Task<List<BeneficiaryInformationDto>> GetByIdsAsync(List<Guid> ids)
            => await _repo.GetByIdsAsync(ids);


        public async Task<BeneficiarySummaryResultDto> GetSummaryAsync(BeneficiaryFilterDto filter)
        {
            filter.PsgcCodeRegion = CaragaEnum.DefaultRegionCode;

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
        public async Task BulkUpdatePaymentStatusAsync(List<Guid> ids, int paymentStatus, DateTime? paymentDate, string userName)
        {
            if (ids == null || !ids.Any())
                throw new Exception(CommonConstants.NoRecordsSelected);

            if (paymentStatus != 1 && paymentStatus != 2)
                throw new Exception(CommonConstants.InvalidPaymentStatus);

            // ✅ Paid requires a date
            if (paymentStatus == 2 && paymentDate == null)
                throw new Exception(CommonConstants.PaymentDateRequiredForPaidStatus);



            await _repo.BulkUpdatePaymentStatusAsync(ids, paymentStatus, paymentDate);

            var statusLabel = paymentStatus == 2 ? $"{CommonConstants.PaidDate} {paymentDate.ToFullDate()})" : CommonConstants.Unpaid;

            foreach (var id in ids)
            {
                await AddLogAsync(id, $"{CommonConstants.BulkPaymentStatusUpdatedTo} {statusLabel}", userName);
            }

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }
        public async Task UpdateAsync(Guid Id, BeneficiaryInformationDto dto, string userName)
        {
            var beneficiary = await _repo.GetEntityByIdAsync(Id);
            if (beneficiary == null)
                throw new Exception(CommonConstants.GranteeNotFound);

            //Check Duplicates
            var isDuplicate = await _repo.ExistsDuplicateAsync(
                  dto.LastName,
                  dto.FirstName,
                  dto.MiddleName,
                  dto.BirthDate,
                  dto.OscaIdNumber,
                  dto.NcscRrn,
                  Id);


            var changes = await GetChangedFields(beneficiary, dto);

            beneficiary.Update(dto.DateApplied, dto.DateEndorsed, dto.BatchCode,
                dto.OscaIdNumber, dto.OscaIdDateIssued, dto.NcscRrn,
                dto.LastName, dto.FirstName, dto.MiddleName,
                dto.Extension, dto.BirthDate, dto.PhoneNumber,
                dto.Sex, dto.IsIndigenousPeople, dto.IsPersonWithDisability,
                dto.CivilStatus, dto.Citizenship, CaragaEnum.DefaultRegionCode,
                dto.PsgcCodeProvince, dto.PsgcCodeMunicipality, dto.PsgcCodeBarangay,
                dto.IsCompliant, dto.Validator, dto.ValidationDate,
                dto.PaymentStatus, dto.ModeOfPayment, dto.PaymentDate,
                dto.IsDeceased, dto.DateOfDeath, dto.IsEligible,
                dto.AssessmentRemarks, dto.RemarkCategory, dto.Remarks);

            await _repo.UpdateAsync(beneficiary);

            if (changes.Any())
            {
                await AddLogAsync(
                    beneficiary.Id,
                    $"{CommonConstants.UpdatedBeneficiaryChanges} {string.Join("; ", changes)}",
                    userName);
            }
            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();

        }
        public async Task<IEnumerable<LogSummaryResultDto>> GetLogSummaryAsync(Guid beneficiaryId)
        {
            var result = await _logRepository.GetLogSummaryAsync(beneficiaryId);
            return result;
        }
        #region Excel Updating/Importing and creating Payroll - START

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

        public async Task<byte[]> GeneratePayrollAsync(PayrollSettingsDto settings)
        {
            if (settings.Ids == null || !settings.Ids.Any())
                throw new InvalidOperationException(CommonConstants.NoRecordsSelected);

            var allData = (await _repo.GetByIdsAsync(settings.Ids))
                    .DistinctBy(x => x.Id)   // ✅ keep here, remove from repository
                    .ToList();


            if (!allData.Any())
                throw new InvalidOperationException(CommonConstants.NoneOfTheRecordsFound);

            // Both counters are global across the entire ZIP —
            // they never reset between provinces or municipalities.
            int continousNo = 1;
            int cgpPageNumber = 1;

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
                                    .OrderBy(x => x.BarangayName)
                                    .ThenBy(x => x.LastName)
                                    .ThenBy(x => x.FirstName)
                                    .ToList();

                                var sheetName = SanitizeSheetName(municipality);
                                var ws = workbookNew.Worksheets.Add(sheetName);

                                BuildPayrollSheet(
                                    ws,
                                    records,
                                    settings,
                                    ref continousNo,
                                    ref cgpPageNumber);
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

            return finalizedResult;
        }

        private static void BuildPayrollSheet(
            IXLWorksheet ws,
            List<BeneficiaryInformationDto> records,
            PayrollSettingsDto s,
            ref int continousNo,
            ref int cgpPageNumber)
        {
            const int COLS = 19;
            const int FONT_SIZE = 14;
            const double PAGE_TWO_PLUS_HT = 210.0;

            // Page 1 layout budget (Excel row-height units, legal landscape 0.5" margins):
            //   Header rows 1–14  ≈ 237 units
            //   Signatory block   ≈ 330 units  (with generous signing space)
            //   Subtotal + gap    ≈  36 units
            //   Available for data rows = total page capacity − 237 − 330 − 36 = 1097
            const double PAGE1_DATA_BUDGET = 1097.0;

            // ── Page capacity constants ───────────────────────────────────────────────
            // Page 1: maximum 5 data rows (hard cap so footer always fits at full height)
            // Page 2+: maximum 7 data rows
            // LAST PAGE: always exactly 1 data row — guaranteed alongside the footer
            const int PAGE1_MAX = 5;
            const int PAGE2_MAX = 7;

            var first = records.FirstOrDefault();
            var municipality = first?.MunicipalityName ?? "";
            var province = first?.ProvinceName ?? "";
            var milestoneYear = first?.MilestoneYear ?? 0;

            // =========================================================================
            // BUILD PAGE PLAN UPFRONT
            // =========================================================================
            // Rule: the very last page ALWAYS has exactly 1 record alongside the footer.
            // Working backwards:
            //   - Reserve 1 record for the last page.
            //   - Distribute the rest: page 1 gets up to PAGE1_MAX, page 2+ get PAGE2_MAX.
            //   - The last page then always gets exactly 1.
            //
            // Special case: if there is only 1 record total, it stays on page 1 alone
            // (we cannot split a single record) and the footer prints with it.
            //
            // Examples (records → page sizes):
            //   1  → [1]              (single page: 1 row + footer)
            //   2  → [1, 1]           (page1: 1 row  | page2: 1 row + footer)
            //   3  → [2, 1]           (page1: 2 rows | page2: 1 row + footer)
            //   6  → [5, 1]           (page1: 5 rows | page2: 1 row + footer)
            //   7  → [5, 1, 1]        (page1: 5 rows | page2: 1 row | page3: 1 row + footer)
            //   8  → [5, 2, 1]        (page1: 5 rows | page2: 2 rows | page3: 1 row + footer)
            //  13  → [5, 7, 1]        (page1: 5 rows | page2: 7 rows | page3: 1 row + footer)
            //  14  → [5, 7, 1, 1]     (page1: 5 rows | page2: 7 rows | page3: 1 | page4: 1 + footer)
            //  20  → [5, 7, 7, 1]     (page1: 5 rows | page2: 7 | page3: 7 | page4: 1 + footer)
            //  21  → [5, 7, 7, 1, 1]  (page1: 5 | page2: 7 | page3: 7 | page4: 1 | page5: 1 + footer)
            // =========================================================================
            var pagePlan = new List<int>();

            if (records.Count <= 1)
            {
                // Single record — keep on page 1, footer prints with it
                pagePlan.Add(records.Count);
            }
            else
            {
                int remaining = records.Count;

                // Page 1
                int p1 = Math.Min(remaining - 1, PAGE1_MAX); // always leave at least 1
                pagePlan.Add(p1);
                remaining -= p1;

                // Middle pages — keep going until only 1 left
                while (remaining > 1)
                {
                    int take = Math.Min(remaining - 1, PAGE2_MAX); // always leave at least 1
                    pagePlan.Add(take);
                    remaining -= take;
                }

                // Last page — always exactly 1
                pagePlan.Add(remaining); // remaining is always 1 here
            }

            // ── Dynamic page 1 row height ─────────────────────────────────────────────
            // Divide the data budget by however many rows page 1 will hold.
            // Capped at 192 (baseline for 6 rows), floored at 60 (readability).
            int p1Count = pagePlan.Count > 0 ? pagePlan[0] : 1;
            double page1RowHeight = p1Count > 0
                ? Math.Max(60.0, Math.Min(192.0, Math.Floor(PAGE1_DATA_BUDGET / p1Count)))
                : 192.0;

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
                          XLAlignmentHorizontalValues align = XLAlignmentHorizontalValues.Left)
            {
                if (val != null) ws.Cell(row, col).Value = XLCellValue.FromObject(val);
                ws.Cell(row, col).Style.Font.FontSize = 16;
                ws.Cell(row, col).Style.Font.FontName = CommonConstants.Arial;
                ws.Cell(row, col).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Cell(row, col).Style.Alignment.Horizontal = align;
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, col).Style.Alignment.WrapText = true;
            }

            void CgpCell(int row, string text)
            {
                ws.Cell(row, 19).Value = text;
                ws.Cell(row, 19).Style.Font.Bold = false;
                ws.Cell(row, 19).Style.Font.FontSize = FONT_SIZE;
                ws.Cell(row, 19).Style.Font.FontName = CommonConstants.Arial;
                ws.Cell(row, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Cell(row, 19).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Cell(row, 19).Style.Border.OutsideBorder = XLBorderStyleValues.None;
            }

            // =========================================================================
            // SECTION 1: HEADER
            // =========================================================================
            ws.Row(3).Height = 24;
            MergeCenter(3, 11, 11, CommonConstants.NCSC, bold: true);

            ws.Row(4).Height = 24;
            string municipalityDisplay = municipality.Contains("City", StringComparison.OrdinalIgnoreCase)
                ? municipality
                : $"{CommonConstants.MunicipalityOf} {municipality}";
            MergeCenter(4, 11, 11, $"{CommonConstants.RegionalOfficeProvinceOf} {province}, {municipalityDisplay}");

            ws.Row(5).Height = 21.75;
            MergeCenter(5, 11, 11, CommonConstants.Act);

            ws.Row(6).Height = 10.5;
            ws.Row(7).Height = 10.5;

            ws.Row(8).Height = 18.75;
            MergeCenter(8, 11, 11, CommonConstants.CashGiftPayroll, bold: true);

            ws.Row(9).Height = 14.25;

            // Row 10: A. PURPOSE + CGP page 1
            ws.Row(10).Height = 23.25;
            ws.Cell(10, 2).Value = CommonConstants.Apurpose;
            ws.Cell(10, 2).Style.Font.Bold = true;
            ws.Cell(10, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(10, 2).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(10, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Cell(10, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Range(10, 4, 10, 15).Merge();
            ws.Cell(10, 4).Value = CommonConstants.PayrollPurpose;
            ws.Cell(10, 4).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(10, 4).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(10, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            ws.Cell(10, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Cell(10, 4).Style.Alignment.WrapText = true;

            CgpCell(10, $"{CommonConstants.CgpNo} {s.RegionCode}-{milestoneYear}{s.Month}-{s.FixedSegment}-{s.ShortenYear}-{cgpPageNumber.ToPaddedPage()}");
            cgpPageNumber++;

            ws.Row(11).Height = 11.25;

            // After AddCenteredTitle(4, ...)
            PayrollLogos.AddLogos(
              ws,
              anchorRow: 1,
              leftCol: 1,
              rightCol: 19,   // ✅ col 19 (S) — right edge of table
              widthPx: 95,
              heightPx: 95,
              offsetLeft: 4,
              offsetRight: 251); // ✅ right-aligns logo within col 19
                  // =========================================================================
            // SECTION 2: COLUMN HEADERS rows 12–14
            // =========================================================================
            ws.Row(12).Height = 14.25;
            ws.Row(13).Height = 24.75;
            ws.Row(14).Height = 108.75;

            NavyHeader(ws.Range(12, 2, 14, 2), CommonConstants.BatchCode.ToTitleCase());
            NavyHeader(ws.Range(12, 3, 14, 3), CommonConstants.Number);
            NavyHeader(ws.Range(12, 4, 13, 7), CommonConstants.FullNameOfBeneficiary);
            NavyHeader(ws.Range(14, 4, 14, 4), CommonConstants.LastName.ToTitleCase());
            NavyHeader(ws.Range(14, 5, 14, 5), CommonConstants.FirstName.ToTitleCase());
            NavyHeader(ws.Range(14, 6, 14, 6), CommonConstants.MiddleName.ToTitleCase());
            NavyHeader(ws.Range(14, 7, 14, 7), CommonConstants.Ext);
            NavyHeader(ws.Range(12, 8, 14, 8), CommonConstants.PayrollBirthdate);
            NavyHeader(ws.Range(12, 9, 14, 9), CommonConstants.Age.ToTitleCase());
            NavyHeader(ws.Range(12, 10, 14, 10), CommonConstants.Sex.ToTitleCase());
            NavyHeader(ws.Range(12, 11, 14, 11), CommonConstants.Barangay.ToTitleCase());
            NavyHeader(ws.Range(12, 12, 14, 12), CommonConstants.Amount);
            NavyHeader(ws.Range(12, 13, 14, 13), CommonConstants.AmountReceived);
            NavyHeader(ws.Range(12, 14, 13, 15), CommonConstants.BeneficiaryAuthRepresentative);
            NavyHeader(ws.Range(14, 14, 14, 14), CommonConstants.SignatureOverPrintedName);
            NavyHeader(ws.Range(14, 15, 14, 15), CommonConstants.Thumbmark);
            NavyHeader(ws.Range(12, 16, 14, 16), CommonConstants.ForAuthRep);
            NavyHeader(ws.Range(12, 17, 14, 17), CommonConstants.DateOfDeath);
            NavyHeader(ws.Range(12, 18, 14, 18), CommonConstants.DateReceived);
            NavyHeader(ws.Range(12, 19, 14, 19), CommonConstants.Remarks.ToTitleCase());

            // =========================================================================
            // SECTION 3: DATA ROWS — driven by the pre-built pagePlan
            // =========================================================================
            int currentRow = 15;
            int processed = 0;

            for (int pageIndex = 0; pageIndex < pagePlan.Count; pageIndex++)
            {
                int pageSize = pagePlan[pageIndex];
                var pageRecs = records.Skip(processed).Take(pageSize).ToList();
                bool isFirstPage = pageIndex == 0;

                // ── Pages 2+: manual page break then CGP row ──────────────────────
                // AddHorizontalPageBreak(row) ends the page AFTER that row number,
                // so our CGP row becomes the very first row of the new page —
                // it will never appear at the bottom of the previous page.
                if (!isFirstPage)
                {
                    ws.PageSetup.AddHorizontalPageBreak(currentRow - 1);

                    CgpCell(currentRow, $"{CommonConstants.CgpNo} {s.RegionCode}-{milestoneYear}{s.Month}-{s.FixedSegment}-{s.ShortenYear}-{cgpPageNumber.ToPaddedPage()}");
                    cgpPageNumber++;
                    ws.Row(currentRow).Height = 23.25;
                    currentRow++;
                }

                foreach (var rec in pageRecs)
                {
                    int dr = currentRow;

                    // Page 1: dynamic height so the footer never overflows.
                    // Page 2+: fixed taller height for comfortable reading/signing.
                    ws.Row(dr).Height = isFirstPage ? page1RowHeight : PAGE_TWO_PLUS_HT;

                    DataCell(dr, 2, (rec.BatchCode ?? "").ToUpperInvariant());
                    DataCell(dr, 3, continousNo++, XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 4, (rec.LastName ?? "").ToUpperInvariant());
                    DataCell(dr, 5, (rec.FirstName ?? "").ToUpperInvariant());
                    DataCell(dr, 6, (rec.MiddleName ?? "").ToUpperInvariant());
                    DataCell(dr, 7, (rec.Extension ?? "").ToUpperInvariant());
                    DataCell(dr, 8, rec.BirthDate.ToStandardDate(), XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 9, rec.Age, XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 10, rec.Sex == 1 ? CommonConstants.Male : CommonConstants.Female,
                                     XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 11, rec.BarangayName.ToUpperInvariant(), XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 12, s.CashGiftAmount, XLAlignmentHorizontalValues.Center);
                    ws.Cell(dr, 12).Style.NumberFormat.Format = CommonConstants.NumberFormat;

                    if (rec.IsDeceased && rec.DateOfDeath.HasValue)
                        DataCell(dr, 17, rec.DateOfDeath.Value.ToStandardDate(),
                                         XLAlignmentHorizontalValues.Center);

                    ws.Range(dr, 2, dr, COLS).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Range(dr, 2, dr, COLS).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    currentRow++;
                }

                processed += pageSize;
            }

            // =========================================================================
            // SECTION 4: SUBTOTAL
            // =========================================================================
            int subtotalRow = currentRow + 1;
            int dataStartRow = 15;
            ws.Row(subtotalRow).Height = 18;

            ws.Range(subtotalRow, 2, subtotalRow, 9).Merge();
            ws.Cell(subtotalRow, 2).Value = CommonConstants.SubTotal;
            ws.Cell(subtotalRow, 2).Style.Font.Bold = true;
            ws.Cell(subtotalRow, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(subtotalRow, 2).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(subtotalRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(subtotalRow, 12).FormulaA1 = $"=SUM(L{dataStartRow}:L{subtotalRow - 2})";
            ws.Cell(subtotalRow, 12).Style.Font.Bold = true;
            ws.Cell(subtotalRow, 12).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(subtotalRow, 12).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(subtotalRow, 12).Style.NumberFormat.Format = CommonConstants.NumberFormat;
            ws.Cell(subtotalRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Range(subtotalRow, 13, subtotalRow, COLS).Merge();

            currentRow = subtotalRow + 2;

            // =========================================================================
            // SECTION 5: SIGNATORIES
            // Layout (A–L):
            //   A) Cert italic text B–I                        18 pt
            //   B) Gap                                         14 pt
            //   C) "Approved for Payment:" L–P                 18 pt
            //   D) Signing space ×3                            28 pt each
            //   E) Name row underlined — Sig1 | Sig2           22 pt
            //   F) Position row                                 18 pt
            //   G) Gap before oath                              8 pt
            //   H) Oath text B–I                               52 pt
            //   I) Signing space ×2                            28 pt each
            //   J) Sig3 name underlined | Officer1 | Officer2  22 pt
            //   K) SDO label | "Printed Name and Sig of" ×2    21 pt
            //   L) "other officer present during Payout" ×2    18 pt
            // =========================================================================

            // A)
            ws.Range(currentRow, 2, currentRow, 9).Merge();
            ws.Cell(currentRow, 2).Value = s.Signatory1Label;
            ws.Cell(currentRow, 2).Style.Font.Italic = true;
            ws.Cell(currentRow, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 2).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(currentRow, 2).Style.Alignment.WrapText = true;
            ws.Cell(currentRow, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(currentRow).Height = 18;
            currentRow++;

            // B)
            ws.Row(currentRow).Height = 14.25;
            currentRow++;

            // C)
            ws.Range(currentRow, 12, currentRow, 16).Merge();
            ws.Cell(currentRow, 12).Value = s.Signatory2Label;
            ws.Cell(currentRow, 12).Style.Font.Bold = true;
            ws.Cell(currentRow, 12).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 12).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(currentRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currentRow, 12).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(currentRow).Height = 18;
            currentRow++;

            // D) Signing space — 3 rows at 28 pt for actual room to sign
            ws.Row(currentRow).Height = 28; currentRow++;
            ws.Row(currentRow).Height = 28; currentRow++;
            ws.Row(currentRow).Height = 28; currentRow++;

            // E) Names underlined
            int sigNamesRow = currentRow;
            ws.Row(sigNamesRow).Height = 22;

            ws.Range(sigNamesRow, 2, sigNamesRow, 5).Merge();
            ws.Cell(sigNamesRow, 2).Value = s.Signatory1Name;
            ws.Cell(sigNamesRow, 2).Style.Font.Bold = true;
            ws.Cell(sigNamesRow, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(sigNamesRow, 2).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(sigNamesRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(sigNamesRow, 2, sigNamesRow, 5).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Range(sigNamesRow, 12, sigNamesRow, 16).Merge();
            ws.Cell(sigNamesRow, 12).Value = s.Signatory2Name;
            ws.Cell(sigNamesRow, 12).Style.Font.Bold = true;
            ws.Cell(sigNamesRow, 12).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(sigNamesRow, 12).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(sigNamesRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(sigNamesRow, 12, sigNamesRow, 16).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            currentRow++;

            // F) Positions
            ws.Range(currentRow, 2, currentRow, 5).Merge();
            ws.Cell(currentRow, 2).Value = s.Signatory1Position;
            ws.Cell(currentRow, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 2).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(currentRow, 12, currentRow, 16).Merge();
            ws.Cell(currentRow, 12).Value = s.Signatory2Position;
            ws.Cell(currentRow, 12).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 12).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(currentRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(currentRow).Height = 18;
            currentRow++;

            // G)
            ws.Row(currentRow).Height = 8;
            currentRow++;

            // H) Oath text
            ws.Range(currentRow, 2, currentRow, 9).Merge();
            ws.Cell(currentRow, 2).Value =
                "R. I/we certify on my/our official oath that on ______________________________________," +
                " I/we have paid in cash to each individual on the payroll, the amount set opposite to each name," +
                " having presented himself/herself, established identity and affixed his/her signature or" +
                " thumbmark on the space provided.";
            ws.Cell(currentRow, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 2).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(currentRow, 2).Style.Alignment.WrapText = true;
            ws.Cell(currentRow, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            ws.Row(currentRow).Height = 52;
            currentRow++;

            // I) Signing space — 2 rows at 28 pt
            ws.Row(currentRow).Height = 28; currentRow++;
            ws.Row(currentRow).Height = 28; currentRow++;

            // J) Bottom sig row
            int sig3Row = currentRow;
            int labelRow1 = currentRow + 1;
            int labelRow2 = currentRow + 2;

            ws.Row(sig3Row).Height = 22;

            ws.Range(sig3Row, 3, sig3Row, 10).Merge();
            ws.Cell(sig3Row, 3).Value = s.Signatory3Name;
            ws.Cell(sig3Row, 3).Style.Font.Bold = true;
            ws.Cell(sig3Row, 3).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(sig3Row, 3).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(sig3Row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(sig3Row, 3, sig3Row, 10).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Range(sig3Row, 14, sig3Row, 15).Merge();
            ws.Range(sig3Row, 14, sig3Row, 15).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Range(sig3Row, 16, sig3Row, 17).Merge();
            ws.Range(sig3Row, 16, sig3Row, 17).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // K)
            ws.Row(labelRow1).Height = 21;

            ws.Range(labelRow1, 3, labelRow1, 10).Merge();
            ws.Cell(labelRow1, 3).Value = s.Signatory3Position;
            ws.Cell(labelRow1, 3).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow1, 3).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(labelRow1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(labelRow1, 14, labelRow1, 15).Merge();
            ws.Cell(labelRow1, 14).Value = CommonConstants.PrintedNameAndSignatureOf;
            ws.Cell(labelRow1, 14).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow1, 14).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(labelRow1, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(labelRow1, 16, labelRow1, 17).Merge();
            ws.Cell(labelRow1, 16).Value = CommonConstants.PrintedNameAndSignatureOf;
            ws.Cell(labelRow1, 16).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow1, 16).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(labelRow1, 16).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // L)
            ws.Row(labelRow2).Height = 18;

            ws.Range(labelRow2, 14, labelRow2, 15).Merge();
            ws.Cell(labelRow2, 14).Value = s.Signatory4Position;
            ws.Cell(labelRow2, 14).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow2, 14).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(labelRow2, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(labelRow2, 16, labelRow2, 17).Merge();
            ws.Cell(labelRow2, 16).Value = s.Signatory4Position;
            ws.Cell(labelRow2, 16).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow2, 16).Style.Font.FontName = CommonConstants.Arial;
            ws.Cell(labelRow2, 16).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // =========================================================================
            // SECTION 6: COLUMN WIDTHS + PAGE SETUP
            // =========================================================================
            double[] colWidths =
            {
         1.82, 25.82,  8.0,  33.18, 28.82, 27.46, 12.82, 17.82,  8.72,
        13.27, 22.82, 16.46, 15.72, 39.0,  32.18, 48.0,  14.27, 15.72, 50.0
    };
            for (int c = 1; c <= colWidths.Length; c++)
                ws.Column(c).Width = colWidths[c - 1];

            ws.PageSetup.PaperSize = XLPaperSize.LegalPaper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.Margins.Top = 0.5;
            ws.PageSetup.Margins.Bottom = 0.5;
            ws.PageSetup.Margins.Left = 0.5;
            ws.PageSetup.Margins.Right = 0.5;

            // ✅ FitToPages(1, 0):
            //   - Width  = 1: columns always fit on one page wide (no horizontal overflow)
            //   - Height = 0: free — rows flow across as many pages as needed
            // This is what allows AddHorizontalPageBreak to control page splits correctly.
            // Each worksheet is independent so page numbering (&P of &N) resets per sheet,
            // giving "1 of N" per municipality automatically.
            ws.PageSetup.FitToPages(1, 0);

            // Page number footer — resets to "1 of N" for each worksheet independently
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
        //Template Generation method
        public byte[] GenerateImportTemplate()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(CommonConstants.Grantees);

            int colCount = 28;

            // =========================
            // TITLE HEADER (ROW 1–4)
            // =========================
            void AddCenteredTitle(int row, string text)
            {
                var range = worksheet.Range(row, 1, row, colCount);
                range.Merge();
                range.Value = text;
                range.Style.Font.Bold = true;
                range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                range.Style.Font.FontSize = 11;
            }

            AddCenteredTitle(1, CommonConstants.NCSC);
            AddCenteredTitle(2, CommonConstants.Act);
            AddCenteredTitle(3, CommonConstants.RegionalOfficeCaraga);
            AddCenteredTitle(4, $"({CommonConstants.ImportTemplateForFy} {DateTime.UtcNow.Year})");

            // Rows 5–9: blank (data starts at row 11, header at row 10)
            for (int r = 5; r <= 9; r++)
            {
                var blankRange = worksheet.Range(r, 1, r, colCount);
                blankRange.Merge();
                blankRange.Value = string.Empty;
            }

            // =========================
            // HEADER ROW (ROW 10)
            // =========================
            int headerRow = 10;

            string[] headers = new[]
            {
        // Col 1–8: Identity
        CommonConstants.BatchCode,
        CommonConstants.Number.ToUpperInvariant(),
        CommonConstants.OscaIdNumber,
        CommonConstants.NcscRrn,
        CommonConstants.LastName,
        CommonConstants.FirstName,        // required
        CommonConstants.MiddleName,
        CommonConstants.Extension,
        // Col 9–12: Birth date (split)
        CommonConstants.BirthMonth,       // e.g. JANUARY
        CommonConstants.BirthDay,         // e.g. 01
        CommonConstants.BirthYear,        // e.g. 1926
        CommonConstants.Age,               // auto-computed, leave blank
        // Col 13–18: Demographics & location
        CommonConstants.Sex,               // MALE or FEMALE
        CommonConstants.Region,            // e.g. REGION XIII (CARAGA) — leave blank to default
        CommonConstants.Province,
        CommonConstants.Municipality,
        CommonConstants.Barangay,
        CommonConstants.ComplianceToDocumentaryRequirements, // COMPLIANT or NON-COMPLIANT
        // Col 19–20: Validation
        CommonConstants.NameOfValidator,
        CommonConstants.ValidationDate,   // e.g. March 17, 2026
        // Col 21–28: Newly added columns
        CommonConstants.ContactNumber,
        CommonConstants.DateOfDeath.ToUpperInvariant(),     // e.g. March 17, 2026
        CommonConstants.DateApplied,      // e.g. March 17, 2026
        CommonConstants.DateEndorsed,     // e.g. March 17, 2026
        CommonConstants.OscaIdDateIssued,
        CommonConstants.IP, // YES or NO
        CommonConstants.PWD, // YES or NO
        CommonConstants.NcscAssessment    // ELIGIBLE or INELIGIBLE
    };

            for (int col = 1; col <= headers.Length; col++)
            {
                worksheet.Cell(headerRow, col).Value = headers[col - 1];
            }

            // Style header
            var headerRange = worksheet.Range(headerRow, 1, headerRow, colCount);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.WrapText = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // =========================
            // SAMPLE ROW (ROW 11) — so users know the format
            // =========================
            var sampleRow = new object[]
            {
        "BC-2026",       // 1  BATCH CODE
        1,               // 2  NO.
        "OSCA-00001",    // 3  OSCA ID
        12345,           // 4  NCSC RRN
        "DELA CRUZ",     // 5  LAST NAME
        "JUAN",          // 6  FIRST NAME
        "SANTOS",        // 7  MIDDLE NAME
        "",              // 8  EXTENSION (Jr., Sr., II, III...)
        "JANUARY",       // 9  BIRTH MONTH
        "01",            // 10 BIRTH DAY
        "1926",          // 11 BIRTH YEAR
        "",              // 12 AGE (leave blank)
        "MALE",          // 13 SEX
        "REGION XIII (CARAGA)", // 14 REGION (or leave blank)
        "AGUSAN DEL NORTE",     // 15 PROVINCE
        "BUTUAN CITY",          // 16 MUNICIPALITY
        "AMBAGO",               // 17 BARANGAY
        "COMPLIANT",            // 18 COMPLIANCE
        "JANE DOE",             // 19 VALIDATOR
        "March 17, 2026",       // 20 VALIDATION DATE
        "09171234567",          // 21 CONTACT NUMBER
        "",                     // 22 DATE OF DEATH (leave blank if alive)
        "January 5, 2026",      // 23 DATE APPLIED
        "February 1, 2026",     // 24 DATE ENDORSED
        "March 1, 2020",        // 25 OSCA ID DATE ISSUED
        "NO",                   // 26 INDIGENOUS PEOPLE
        "NO",                   // 27 PWD
        "ELIGIBLE"              // 28 NCSC ASSESSMENT
            };

            for (int col = 1; col <= sampleRow.Length; col++)
            {
                var cell = worksheet.Cell(11, col);
                cell.Value = sampleRow[col - 1] is string s
                    ? XLCellValue.FromObject(s)
                    : XLCellValue.FromObject(sampleRow[col - 1]);
            }

            // Style the sample row so users can see it's just an example
            var sampleRange = worksheet.Range(11, 1, 11, colCount);
            sampleRange.Style.Fill.BackgroundColor = XLColor.LightYellow;
            sampleRange.Style.Font.Italic = true;
            sampleRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sampleRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // =========================
            // INSTRUCTIONS SHEET
            // =========================
            var instructions = workbook.Worksheets.Add(CommonConstants.Instructions);

            var instrHeaders = new[] { "COLUMN", "REQUIRED", "ACCEPTED VALUES / FORMAT", "EXAMPLE" };
            for (int col = 1; col <= instrHeaders.Length; col++)
            {
                instructions.Cell(1, col).Value = instrHeaders[col - 1];
            }
            var instrHeaderRange = instructions.Range(1, 1, 1, 4);
            instrHeaderRange.Style.Font.Bold = true;
            instrHeaderRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            instrHeaderRange.Style.Font.FontColor = XLColor.White;
            instrHeaderRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            instrHeaderRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var instrData = new[]
            {
        new[] { CommonConstants.BatchCode,                      CommonConstants.No,  "Any text",                             "BC-2026" },
        new[] { CommonConstants.Number.ToUpperInvariant(),      CommonConstants.No,  "Number (auto if left blank)",           "1" },
        new[] { CommonConstants.OscaIdNumber,                   CommonConstants.No,  "Any text",                             "OSCA-00001" },
        new[] { CommonConstants.NcscRrn,                        CommonConstants.No,  "Numbers only, no special characters",  "12345" },
        new[] { CommonConstants.LastName,                       CommonConstants.No,  "Any text",                             "DELA CRUZ" },
        new[] { CommonConstants.FirstName,                      CommonConstants.Yes, "Any text",                             "JUAN" },
        new[] { CommonConstants.MiddleName,                     CommonConstants.No,  "Any text",                             "SANTOS" },
        new[] { CommonConstants.Extension,                      CommonConstants.No,  "Jr. / Sr. / II / III / IV / V",        "Jr." },
        new[] { CommonConstants.BirthMonth,                     CommonConstants.Yes, "Full month name in CAPS",              "JANUARY" },
        new[] { CommonConstants.BirthDay,                       CommonConstants.Yes, "Two-digit day",                        "01" },
        new[] { CommonConstants.BirthYear,                      CommonConstants.Yes, "Four-digit year",                      "1926" },
        new[] { CommonConstants.Age,                            CommonConstants.No,  "Leave blank — auto-computed",          "" },
        new[] { CommonConstants.Sex,                            CommonConstants.No,  "MALE or FEMALE",                       "MALE" },
        new[] { CommonConstants.Region,                         CommonConstants.No,  "Leave blank to default to Caraga",     "REGION XIII (CARAGA)" },
        new[] { CommonConstants.Province,                       CommonConstants.Yes, "Full province name",                   "AGUSAN DEL NORTE" },
        new[] { CommonConstants.Municipality,                   CommonConstants.Yes, "Full municipality or city name",       "BUTUAN CITY" },
        new[] { CommonConstants.Barangay,                       CommonConstants.Yes, "Full barangay name",                   "AMBAGO" },
        new[] { CommonConstants.Compliance,                     CommonConstants.No,  "COMPLIANT or NON-COMPLIANT",           "COMPLIANT" },
        new[] { CommonConstants.NameOfValidator,                CommonConstants.No,  "Any text",                             "JANE DOE" },
        new[] { CommonConstants.ValidationDate,                 CommonConstants.No,  "Month DD, YYYY",                       "March 17, 2026" },
        new[] { CommonConstants.ContactNumber,                  CommonConstants.No,  "Any text",                             "09171234567" },
        new[] { CommonConstants.DateOfDeath,                    CommonConstants.No,  "Month DD, YYYY — leave blank if alive","" },
        new[] { CommonConstants.DateApplied,                    CommonConstants.No,  "Month DD, YYYY",                       "January 5, 2026" },
        new[] { CommonConstants.DateEndorsed,                   CommonConstants.No,  "Month DD, YYYY",                       "February 1, 2026" },
        new[] { CommonConstants.OscaIdDateIssued,               CommonConstants.No,  "Month DD, YYYY",                       "March 1, 2020" },
        new[] { CommonConstants.IP,                             CommonConstants.No,  "YES or NO",                            "NO" },
        new[] { CommonConstants.PWD,                            CommonConstants.No,  "YES or NO",                            "NO" },
        new[] { CommonConstants.NcscAssessment,                 CommonConstants.Yes, "ELIGIBLE or INELIGIBLE",               "ELIGIBLE" },
    };

            for (int i = 0; i < instrData.Length; i++)
            {
                var rowData = instrData[i];
                for (int col = 1; col <= rowData.Length; col++)
                {
                    instructions.Cell(i + 2, col).Value = rowData[col - 1];
                }
                // Highlight required rows
                if (rowData[1] == CommonConstants.Yes)
                {
                    instructions.Range(i + 2, 1, i + 2, 4)
                        .Style.Fill.BackgroundColor = XLColor.FromHtml(CommonConstants.AmberYellow);
                }
                instructions.Range(i + 2, 1, i + 2, 4)
                    .Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                instructions.Range(i + 2, 1, i + 2, 4)
                    .Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            instructions.Columns().AdjustToContents();

            // =========================
            // FORMAT THE MAIN SHEET
            // =========================
            worksheet.Row(headerRow).Height = 40;
            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(10);
            worksheet.Range(headerRow, 1, headerRow, colCount).SetAutoFilter();

            worksheet.PageSetup.PaperSize = XLPaperSize.LegalPaper;
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            worksheet.PageSetup.FitToPages(1, 0);
            worksheet.PageSetup.SetRowsToRepeatAtTop(10, 10);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;      // 👈 Crucial: Reset pointer to start
            return stream.ToArray();
        }
        public async Task<BeneficiaryImportResultDto> UpdateExcelAsync(Stream fileStream, string fileName, string sheetName, string userName)
        {
            var result = new BeneficiaryImportResultDto();

            using var workbook = new XLWorkbook(fileStream);

            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == sheetName)
                ?? throw new Exception($"{CommonConstants.WorksheetNotFound} '{sheetName}'.");

            const int firstDataRowNumber = 11;
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

            // UpdateExcelAsync only updates BatchCode, IsEligible, DateOfDeath, ValidationDate.
            // Address fields are not re-resolved on update, so address lookup tables are not needed here.

            for (int rowNumber = firstDataRowNumber; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);

                if (row.Cells(1, 21).All(c => string.IsNullOrWhiteSpace(c.GetFormattedString())))
                    continue;

                result.TotalRows++;

                try
                {
                    var lastName = row.Cell(5).GetFormattedString().Trim();
                    var firstName = row.Cell(6).GetFormattedString().Trim();
                    var middleName = row.Cell(7).GetFormattedString().Trim();
                    var oscaIdNumber = row.Cell(3).GetFormattedString().Trim();
                    var ncscRrnRaw = row.Cell(4).GetFormattedString().Trim();

                    var birthDate = ParseFlexibleDate(
                        $"{row.Cell(9).GetFormattedString()} {row.Cell(10).GetFormattedString()} {row.Cell(11).GetFormattedString()}"
                    ) ?? DateTime.MinValue;

                    var ncscRrn = int.TryParse(ncscRrnRaw, out var r) ? r : (int?)null;

                    // 🔥 FIND EXISTING RECORD
                    var existing = await FindExistingAsync(
                        lastName,
                        firstName,
                        middleName,
                        birthDate,
                        oscaIdNumber,
                        ncscRrn);

                    // ❌ NOT FOUND → ERROR (NO INSERT)
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

                    // 🔥 MAP FIELDS
                    var batchCode = row.Cell(1).GetFormattedString().Trim();
                    var isEligible = MapEligibility(row.Cell(28).GetFormattedString());
                    var dateOfDeath = ParseFlexibleDate(row.Cell(22).GetFormattedString());
                    var validationDate = row.Cell(20).Value;

                    // 🔥 UPDATE ONLY
                    existing.BatchCode = batchCode;
                    existing.IsEligible = isEligible.HasValue ? isEligible.Value : false;
                    existing.DateOfDeath = dateOfDeath;
                    existing.ValidationDate = validationDate;


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

        public async Task<BeneficiaryImportResultDto> ImportExcelAsync(Stream fileStream, string fileName, string sheetName, string userName)
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

            var regions = await _regionRepository.GetAllAsync();

            // Deduplicate provinces by name and prefer the original/legacy entry (lowest Id).
            // The PSGC seeder can add a second row for the same province with a different
            // PsgcCodeProvince. Without deduplication, FindBestNameMatch picks one
            // non-deterministically, risking a PSGC code being saved while existing
            // beneficiary records carry the legacy code.
            var provinces = (await _provinceRepository.GetAllProvinceAsync())
                .GroupBy(x => x.Name!.Trim().ToUpperInvariant())
                .Select(g => g.OrderBy(x => x.Id).First())
                .ToList();

            // Load municipalities and barangays once; they are scoped per-row below.
            var municipalities = (await _municipalityRepository.GetAllMunicipalityAsync()).ToList();
            var barangays = (await _barangayRepository.GetBarangaysAsync()).ToList();

            using var workbook = new XLWorkbook(fileStream);


            if (string.IsNullOrWhiteSpace(sheetName))
                throw new Exception(CommonConstants.PleaseSelectAWorksheet);

            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == sheetName);

            if (worksheet == null)
                throw new Exception($"{CommonConstants.WorksheetNotFound} {sheetName}");

            const int firstDataRowNumber = 11;

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < firstDataRowNumber)
                throw new Exception(CommonConstants.ExcelSheet1DoesNotContain);

            //Track duplicated inside the upload file itself
            var uploadedRowKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int rowNumber = firstDataRowNumber; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);

                // Skip blank rows
                if (row.Cells(1, 21).All(c => string.IsNullOrWhiteSpace(c.GetFormattedString())))
                    continue;

                result.TotalRows++;

                try
                {
                    var batchCode = row.Cell(1).GetFormattedString().Trim();
                    // row.Cell(2) = No. (ignored)
                    var oscaIdNumber = row.Cell(3).GetFormattedString().Trim();
                    var ncscRrnRaw = row.Cell(4).GetFormattedString().Trim();
                    var lastName = row.Cell(5).GetFormattedString().Trim();
                    var firstName = row.Cell(6).GetFormattedString().Trim();
                    var middleName = row.Cell(7).GetFormattedString().Trim();
                    var extensionName = row.Cell(8).GetFormattedString().Trim();

                    var birthMonthRaw = row.Cell(9).GetFormattedString().Trim();
                    var birthDayRaw = row.Cell(10).GetFormattedString().Trim();
                    var birthYearRaw = row.Cell(11).GetFormattedString().Trim();

                    // row.Cell(12) = Age (ignored, computed from BirthDate instead)
                    var sexRaw = row.Cell(13).GetFormattedString().Trim();
                    var regionName = row.Cell(14).GetFormattedString().Trim();
                    var provinceName = row.Cell(15).GetFormattedString().Trim();
                    var municipalityName = row.Cell(16).GetFormattedString().Trim();
                    var barangayName = row.Cell(17).GetFormattedString().Trim();
                    var complianceRaw = row.Cell(18).GetFormattedString().Trim();
                    var validator = row.Cell(19).GetFormattedString().Trim();
                    var validationDateRaw = row.Cell(20).GetFormattedString().Trim();

                    //Newly added column for importing.
                    var contactNumber = row.Cell(21).GetFormattedString().Trim();//21 -Contact Number
                    var dateOfDeath = row.Cell(22).GetFormattedString().Trim();//22 - Date of Death
                    var dateApplied = row.Cell(23).GetFormattedString().Trim();//23 - Date Applied/Date of Application
                    var dateEndorsed = row.Cell(24).GetFormattedString().Trim();//24 - Date Enorsed
                    var oscaIdDateIssued = row.Cell(25).GetFormattedString().Trim();//25 - OSCA ID Date Issued
                    var isIndigenousPeopleRaw = row.Cell(26).GetFormattedString().Trim();//26 - IP
                    var isPersonWithDisabilityRaw = row.Cell(27).GetFormattedString().Trim();//27 - PWD
                    var isEligibleRaw = row.Cell(28).GetFormattedString().Trim(); // NCSC Assessment 

                    bool rowHasError = false;

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

                    var birthDateRaw = $"{birthMonthRaw} {birthDayRaw} {birthYearRaw}";
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
                        birthDate = DateTime.MinValue; // Assign a default value to avoid uninitialized variable error
                    }


                    #region Parsed Dates
                    //Osca Id Date Issued
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

                    //Date Endorsed
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

                    // Date of Application
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


                    //Date of Death
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

                    #endregion Parsed Dates end

                    int? ncscRrn = null;
                    if (!string.IsNullOrWhiteSpace(ncscRrnRaw))
                    {
                        if (int.TryParse(ncscRrnRaw, out var parsedRrn))
                        {
                            ncscRrn = parsedRrn;
                        }
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

                    var region = string.IsNullOrWhiteSpace(regionName)
                        ? regions.FirstOrDefault(x => x.PsgcCodeRegion == CaragaEnum.DefaultRegionCode)
                        : FindBestNameMatch(regions, x => x.Name, regionName);

                    if (region == null)
                    {
                        var suggestion = GetSuggestedName(regions, x => x.Name, regionName);

                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Region,
                            Message = CommonConstants.RegionNotFound,
                            RawValue = regionName,
                            Suggestion = suggestion != null
                            ? $"{CommonConstants.PossibleMatch} '{suggestion}'"
                            : CommonConstants.CheckSpelling
                        });
                        rowHasError = true;
                    }
                    var province = FindBestNameMatch(provinces, x => x.Name, provinceName);
                    if (province == null)
                    {
                        var suggestion = GetSuggestedName(provinces, x => x.Name, provinceName);

                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Province.ToTitleCase(),
                            Message = CommonConstants.ProvinceNotFound,
                            RawValue = provinceName,
                            Suggestion = suggestion != null
                                ? $"{CommonConstants.PossibleMatch} '{suggestion}'"
                                : CommonConstants.CheckSpelling
                        });
                        rowHasError = true;
                    }


                    // Scope municipality search to the matched province.
                    // Without scoping, common names like "San Jose" or "Barobo" would match
                    // the wrong municipality in a different province.
                    var municipalitiesInProvince = province != null
                        ? municipalities.Where(m => m.PsgcCodeProvince == province.PsgcCodeProvince).ToList()
                        : municipalities; // province not found — fall back to all so we can still suggest

                    var municipality = FindBestNameMatch(municipalitiesInProvince, x => x.Name, municipalityName);
                    if (municipality == null)
                    {
                        var suggestion = GetSuggestedName(municipalitiesInProvince, x => x.Name, municipalityName);

                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Municipality,
                            Message = CommonConstants.MunicipalityNotFound,
                            RawValue = municipalityName,
                            Suggestion = suggestion != null
                                ? $"{CommonConstants.PossibleMatch} '{suggestion}'"
                                : CommonConstants.CheckSpelling
                        });
                        rowHasError = true;
                    }

                    // Scope barangay search to the matched municipality.
                    // "Poblacion" alone exists in virtually every municipality — without
                    // scoping the match would be random across 42,000+ barangays.
                    var barangaysInMunicipality = municipality != null
                        ? barangays.Where(b => b.PsgcCodeMunicipality == municipality.PsgcCodeMunicipality).ToList()
                        : barangays; // municipality not found — fall back to all

                    var barangay = FindBestNameMatch(barangaysInMunicipality, x => x.Name, barangayName);
                    if (barangay == null)
                    {
                        var suggestion = GetSuggestedName(barangaysInMunicipality, x => x.Name, barangayName);

                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = CommonConstants.Barangay.ToTitleCase(),
                            Message = CommonConstants.BarangayNotFound,
                            RawValue = barangayName,
                            Suggestion = suggestion != null
                                ? $"{CommonConstants.PossibleMatch} '{suggestion}'"
                                : CommonConstants.CheckSpelling
                        });
                        rowHasError = true;
                    }
                    #region Mappers
                    //Mapped NCSC Assessment Eligible/InEligible
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
                    //Mapped IP
                    var mappedIsIndigenousPeople = MapIndigenousPeople(isIndigenousPeopleRaw);
                    //Mapped PWD
                    var mappedIsPersonWithDisability = MapIsPersonWithDisability(isPersonWithDisabilityRaw);
                    #endregion Mappers end


                    var duplicateKey = string.Join("|",
                 (lastName ?? string.Empty).Trim().ToLower(),
                 (firstName ?? string.Empty).Trim().ToLower(),
                 (middleName ?? string.Empty).Trim().ToLower(),
                 birthDate == DateTime.MinValue ? "" : birthDate.ToFullDate(),
                 (oscaIdNumber ?? string.Empty).Trim().ToLower(),
                 ncscRrn?.ToString() ?? "");

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
                    if (!rowHasError)
                    {
                        var isDuplicateInDatabase = await _repo.ExistsDuplicateAsync(
                            lastName,
                            firstName,
                            middleName,
                            birthDate,
                            oscaIdNumber,
                            ncscRrn);
                        if (isDuplicateInDatabase)
                        {
                            result.Errors.Add(new BeneficiaryImportErrorDto
                            {
                                RowNumber = rowNumber,
                                Field = CommonConstants.Duplicate,
                                Message = CommonConstants.DuplicateRecordExistInDatabase,
                                RawValue = $"{lastName}, {firstName}"
                            });
                            rowHasError = true;
                        }
                    }
                    if (rowHasError)
                        continue;

                    beneficiariesToImport.Add(new BeneficiaryInformation
                    {
                        Id = Guid.NewGuid(),
                        DateApplied = parsedDateApplied,
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
                        PhoneNumber = contactNumber,
                        Sex = MapSex(sexRaw),
                        IsIndigenousPeople = mappedIsIndigenousPeople,
                        IsPersonWithDisability = mappedIsPersonWithDisability,
                        CivilStatus = null,
                        Citizenship = null,
                        Region = region!.PsgcCodeRegion,
                        Province = province!.PsgcCodeProvince,
                        Municipality = municipality!.PsgcCodeMunicipality,
                        Barangay = barangay!.PsgcCodeBarangay,
                        IsCompliant = MapCompliance(complianceRaw),
                        Validator = string.IsNullOrWhiteSpace(validator) ? CommonConstants.None : validator.Trim(),
                        ValidationDate = ParseNullableDate(validationDateRaw) ?? DateTime.Today,
                        PaymentStatus = 0,
                        ModeOfPayment = 0,
                        PaymentDate = null,
                        DateOfDeath = parsedDateofDeath,
                        IsDeceased = parsedDateofDeath.HasValue, // true if date exists, false if null
                        IsEligible = mappedEligibility!.Value,
                        AssessmentRemarks = null,
                        RemarkCategory = null,
                        Remarks = null,
                        DateAdded = DateTime.UtcNow,
                        IsDeleted = false
                    });
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

            //If there are any errors, do not save anything
            if (result.ErrorCount > 0)
            {
                result.ImportedCount = 0;
                result.SkippedDuplicateCount = result.Errors.Count(x => x.Field == CommonConstants.Duplicate);
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


            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();

            result.ImportedCount = beneficiariesToImport.Count;
            result.SkippedDuplicateCount = 0;

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
        // ── Shared helper: build grouped CDR rows from filtered data ──────────────
        private async Task<List<LiquidationRowDto>> BuildCdrRowsAsync(
            LiquidationFilterDto filter,
            LiquidationSettingsDto settings)
        {
            // ✅ Internally map to BeneficiaryFilterDto — no conflict with other filters
            var beneficiaryFilter = new BeneficiaryFilterDto
            {
                PageNumber = 1,
                PageSize = 10000,
                PaymentStatus = 2,           // always Paid
                PaymentDate = null,        // use range not single date
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

            // CGP counter starts at 2 (row 1 = DV cash advance = 0001)
            int cgpCounter = 2;
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

                // Payee: LASTNAME, FIRSTNAME MIDDLENAME ET AL.
                var firstFullName = $"{first.LastName}, {first.FirstName} {first.MiddleName}".Trim().TrimEnd(',');
                var payee = records.Count > 1
                    ? $"{firstFullName} {CommonConstants.ETAL}"
                    : firstFullName;

                // Total disbursement: age 100 = ₱100,000; others = ₱10,000
                var disbursement = records.Sum(x => x.Age >= 100 ? 100_000m : 10_000m);

                // CGP format: CGP No.: {RegionCode}-{MilestoneYear}{Month}-{FixedSegment}-{ShortenYear}-{counter:D4}
                // Example:    CGP No.: RegionXIII-202403-01-26-0002
                var paymentMonth = group.Key.Date.Month.ToPaddedDay();
                var cgpNumber = $"{CommonConstants.CgpNo} {settings.RegionCode}-{group.Key.MilestoneYear}{paymentMonth}-{settings.FixedSegment}-{settings.ShortenYear}-{cgpCounter.ToPaddedPage()}";

                // Nature of Payment
                var locType = group.Key.Municipality.Contains(CommonConstants.City, StringComparison.OrdinalIgnoreCase)
                    ? CommonConstants.CityOf
                    : CommonConstants.MunicipalityOf;
                var location = $"{locType} {group.Key.Municipality}";
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
                    ProvinceName = group.Key.Province
                });

                cgpCounter++;
            }

            // Compute running balance (starts after the DV row = InitialCashAdvance)
            var runningBalance = settings.InitialCashAdvance;
            foreach (var row in cdrRows)
            {
                runningBalance -= row.Disbursement;
                row.CashAdvanceBalance = runningBalance;
            }

            return cdrRows;
        }

        // ── Preview: return rows for the modal table ───────────────────────────────
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
                MilestoneYear = r.MilestoneYear
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
            const int FS_SM = 11;   // standard cell font size
            const int FS_TITLE = 14;  // "CASH DISBURSEMENTS RECORD"
            const int FS_SUB = 12;   // sub-headings & appendix

            // ── Column widths (exact from template) ──────────────────────────
            ws.Column(1).Width = 16.60;  // A  Date
            ws.Column(2).Width = 21.70;  // B  ADA/Ref
            ws.Column(3).Width = 29.40;  // C  Payee
            ws.Column(4).Width = 14.00;  // D  UACS
            ws.Column(5).Width = 33.60;  // E  Nature of Payment
            ws.Column(6).Width = 22.30;  // F  Cash Advance Received
            ws.Column(7).Width = 14.70;  // G  Disbursements
            ws.Column(8).Width = 19.00;  // H  Cash Advance Balance

            // ── Page setup (exact from template: paperSize=9=A4, portrait, scale=57) ──
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            ws.PageSetup.Scale = 57;
            ws.PageSetup.Margins.Left = 0.7;
            ws.PageSetup.Margins.Right = 0.7;
            ws.PageSetup.Margins.Top = 0.12;
            ws.PageSetup.Margins.Bottom = 0.75;
            ws.PageSetup.Margins.Header = 0.12;
            ws.PageSetup.Margins.Footer = 0.3;

            // ── Style helper ─────────────────────────────────────────────────
            void S(IXLCell cell,
                int fs = FS_SM, bool bold = false, bool italic = false,
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
            void MergeAH(int r, string text, int fs = FS_SM, bool bold = false,
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

            // ── DV Cash Advance row (page 1 only) ────────────────────────────
            if (isFirstPage)
            {
                ws.Row(R).Height = 105.75;

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

                ws.Cell(R, 4).Value = CommonConstants.ConstantUacs;
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
                ws.Row(R).Height = 105.75;

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
        public async Task<PagedResultDto<BeneficiaryInformationDto>> GetPaginatedAsync(BeneficiaryFilterDto filter)
        {
            filter.PsgcCodeRegion = CaragaEnum.DefaultRegionCode;

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


        #region Private helpers
        private string BuildPaginatedCacheKey(BeneficiaryFilterDto filter)
        {
            var version = GetCurrentSummaryCacheVersion();

            return string.Join("|",
                version,
                CommonConstants.BeneficiaryPaginated,
                filter.PsgcCodeRegion?.ToString() ?? CommonConstants.Null,
                filter.PageNumber.ToString(),
                filter.PageSize.ToString(),
                filter.PsgcCodeProvince.ToString() ?? CommonConstants.Null,
                filter.PsgcCodeMunicipality.ToString() ?? CommonConstants.Null,
                filter.PsgcCodeBarangays.ToString() ?? CommonConstants.Null,
                filter.LastName ?? string.Empty,
                filter.FirstName ?? string.Empty,
                filter.FullName ?? string.Empty,
                filter.Validator ?? string.Empty,
                filter.BatchCode ?? string.Empty,
                filter.Sex != null ? filter.Sex : CommonConstants.Null,
                filter.PaymentStatus != null ? filter.PaymentStatus : CommonConstants.Null,
                filter.PaymentDate?.ToFullDate() ?? CommonConstants.Null,
                filter.SpecificAge?.ToString() ?? CommonConstants.Null,
                filter.MilestoneYear?.ToString() ?? CommonConstants.Null,
                filter.SpecificBirthday?.ToFullDate() ?? CommonConstants.Null,
                filter.BirthdayFrom?.ToFullDate() ?? CommonConstants.Null,
                filter.BirthdayTo?.ToFullDate() ?? CommonConstants.Null,
                filter.PaymentDateFrom?.ToFullDate() ?? CommonConstants.Null,
                filter.PaymentDateTo?.ToFullDate() ?? CommonConstants.Null,
                filter.FindingStatus != null ? filter.FindingStatus : CommonConstants.Null
            );
        }
        private async Task<BeneficiaryInformation?> FindExistingAsync(string lastName, string firstName, string middleName, DateTime birthDate, string oscaIdNumber, int? ncscRrn)
        {
            return await _repo.FindExistingAsync(
                lastName,
                firstName,
                middleName,
                birthDate,
                oscaIdNumber,
                ncscRrn);
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

        private void InvalidateSummaryCache()
        {
            var newVersion = Guid.NewGuid().ToString();
            _memoryCache.Set(CommonConstants.SummaryCacheVersionKey, newVersion);
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
                (filter.PsgcCodeBarangays != null && filter.PsgcCodeBarangays.Any() ? string.Join(",", filter.PsgcCodeBarangays.OrderBy(x => x)) : CommonConstants.Null),
                filter.LastName ?? string.Empty,
                filter.FirstName ?? string.Empty,
                filter.Sex != null ? filter.Sex : CommonConstants.Null,
                filter.PaymentStatus != null ? filter.PaymentStatus : CommonConstants.Null,
                filter.PaymentDate?.ToFullDate() ?? CommonConstants.Null,
                filter.SpecificAge?.ToString() ?? CommonConstants.Null,
                filter.MilestoneYear?.ToString() ?? CommonConstants.Null,
                filter.SpecificBirthday?.ToFullDate() ?? CommonConstants.Null,
                filter.BirthdayFrom?.ToFullDate() ?? CommonConstants.Null,
                filter.BirthdayTo?.ToFullDate() ?? CommonConstants.Null,
                filter.PaymentDateFrom?.ToFullDate() ?? CommonConstants.Null,
                filter.PaymentDateTo?.ToFullDate() ?? CommonConstants.Null,
                filter.FindingStatus != null ? filter.FindingStatus : CommonConstants.Null
            );
        }
        //Updating a beneficiary record involves comparing the existing values with the new values from the DTO and logging any changes. This method generates a list of changed fields for logging purposes.
        private async Task<List<string>> GetChangedFields(
     BeneficiaryInformation beneficiary, BeneficiaryInformationDto dto)
        {
            var changes = new List<string>();

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

            if (beneficiary.PhoneNumber != dto.PhoneNumber)
                changes.Add($"{CommonConstants.ContactNumber.ToTitleCase()} '{beneficiary.PhoneNumber}' → '{dto.PhoneNumber}'");

            // ── Mapped fields ─────────────────────────────────────
            if (beneficiary.Sex != dto.Sex)
                changes.Add($"{CommonConstants.Sex.ToTitleCase()} '{MapSexLabel(beneficiary.Sex)}' → '{MapSexLabel(dto.Sex)}'");
            if (beneficiary.IsIndigenousPeople != dto.IsIndigenousPeople)
                changes.Add($"{CommonConstants.IP.ToTitleCase()} '{(beneficiary.IsIndigenousPeople ? CommonConstants.Yes : CommonConstants.No)}' → '{(dto.IsIndigenousPeople ? CommonConstants.Yes : CommonConstants.No)}'");

            if (beneficiary.IsPersonWithDisability != dto.IsPersonWithDisability)
                changes.Add($"{CommonConstants.PWD.ToTitleCase()} '{(beneficiary.IsPersonWithDisability ? CommonConstants.Yes : CommonConstants.No)}' → '{(dto.IsPersonWithDisability ? CommonConstants.Yes : CommonConstants.No)}'");

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

            if (beneficiary.AssessmentRemarks != dto.AssessmentRemarks)
                changes.Add($"{CommonConstants.AssessmentRemarks} '{beneficiary.AssessmentRemarks}' → '{dto.AssessmentRemarks}'");

            if (beneficiary.RemarkCategory != dto.RemarkCategory)
                changes.Add($"{CommonConstants.RemarkCategory} '{MapRemarkCategoryLabel(beneficiary.RemarkCategory)}' → '{MapRemarkCategoryLabel(dto.RemarkCategory)}'");

            if (beneficiary.Remarks != dto.Remarks)
                changes.Add($"{CommonConstants.Remarks.ToTitleCase()} '{beneficiary.Remarks}' → '{dto.Remarks}'");

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

        private static string MapCivilStatusLabel(int? status) => status switch
        {
            1 => CommonConstants.Single.ToTitleCase(),
            2 => CommonConstants.Widowed.ToTitleCase(),
            3 => CommonConstants.Married.ToTitleCase(),
            4 => CommonConstants.LiveIn.ToTitleCase(),
            _ => CommonConstants.None
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
            _ =>  CommonConstants.None
        };

        #endregion

    }
}
