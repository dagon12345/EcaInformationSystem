using ClosedXML.Excel;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

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
        private const int DefaultRegionCode = 1600000000;
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
            // AFTER:
            var rawData = await _repo.FilterAsync(filter);
            var data = rawData
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.MiddleName)
                .ToList();

            if (data == null || !data.Any())
                throw new InvalidOperationException("No data available to export.");

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Grantees");

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
            AddCenteredTitle(1, "NATIONAL COMMISSION OF SENIOR CITIZENS");

            // Row 2
            AddCenteredTitle(2, "Expanded Centenarian Act");

            // Row 3
            AddCenteredTitle(3, "Regional Office Caraga");

            // Row 4
            AddCenteredTitle(4, $"(List of Validated/Paid Beneficiaries for FY {DateTime.UtcNow.ToString("yyyy")})");

            // Row 5–9 intentionally blank (no content)

            // =========================
            // ✅ HEADER (ROW 10)
            // =========================
            int headerRow = 10;

            string[] headers = new[]
             {           
                "BATCH CODE","NO.","OSCA ID NUMBER","NCSC RRN",
                "LAST NAME","FIRST NAME","MIDDLE NAME","EXTENSION",
                "BIRTH MONTH","BIRTH DAY","BIRTH YEAR","AGE",
                "SEX","REGION","PROVINCE","MUNICIPALITY/CITY","BARANGAY",
                "COMPLIANCE TO DOCUMENTARY REQUIREMENTS",
                "NAME OF VALIDATOR","VALIDATION DATE","REMARKS"
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
                worksheet.Cell(row, 10).Value = item.BirthDate.Day.ToString("D2");
                worksheet.Cell(row, 11).Value = item.BirthDate.Year;

                // AGE (NEW COLUMN)
                worksheet.Cell(row, 12).Value = GetAge(item.BirthDate);

                worksheet.Cell(row, 13).Value = item.Sex == 1 ? "MALE" : "FEMALE";

                worksheet.Cell(row, 14).Value = item.Region?.ToString().ToUpperInvariant();
                worksheet.Cell(row, 15).Value = item.Province?.ToString().ToUpperInvariant();
                worksheet.Cell(row, 16).Value = item.Municipality?.ToString().ToUpperInvariant();
                worksheet.Cell(row, 17).Value = item.Barangay?.ToString().ToUpperInvariant();

                // COMPLIANCE MAPPING
                worksheet.Cell(row, 18).Value = item.IsCompliant
                    ? "COMPLIANT"
                    : "NON-COMPLIANT";

                worksheet.Cell(row, 19).Value = item.Validator?.ToUpperInvariant();
                worksheet.Cell(row, 20).Value = item.ValidationDate.ToString("dd/MM/yyyy");
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
            string today = DateTime.Today.ToString("MMMM dd, yyyy");

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
                nameRange.Value = "(Enter name)";
                nameRange.Style.Font.Italic = true;
                nameRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                // Position — italic placeholder, flush left, with underline
                var posRange = worksheet.Range(nameRow + 1, nameStartCol, nameRow + 1, nameEndCol);
                posRange.Merge();
                posRange.Value = "(Enter position)";
                posRange.Style.Font.Italic = true;
                posRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                // "Signature over printed name" — flush left, no center
                var sigRange = worksheet.Range(nameRow + 2, blockStartCol, nameRow + 2, blockEndCol);
                sigRange.Merge();
                sigRange.Value = "Signature over printed name";
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
                role: "Prepared by:");

            // NOTED BY — middle, label at col 8, underlines cols 8–13
            AddSignatureBlock(
                labelCol: 8,
                blockStartCol: 8, blockEndCol: 14,
                nameStartCol: 8, nameEndCol: 13,
                role: "Noted by:");

            // APPROVED BY — right, label at col 16, underlines cols 16–20
            AddSignatureBlock(
                labelCol: 16,
                blockStartCol: 16, blockEndCol: 21,
                nameStartCol: 16, nameEndCol: 20,
                role: "Approved by:");
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
            worksheet.PageSetup.Footer.Center.AddText("Page ");
            worksheet.PageSetup.Footer.Center.AddText(XLHFPredefinedText.PageNumber);
            worksheet.PageSetup.Footer.Center.AddText(" of ");
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
                throw new Exception("Duplicate beneficiary found. Same name, birth date, OSCA ID, and RRN already exist.");


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
                Region = DefaultRegionCode,
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
        $"Created beneficiary record for {beneficiary.LastName}, {beneficiary.FirstName}",
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
                PsgcCodeRegion = DefaultRegionCode,
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


        public async Task<BeneficiarySummaryResultDto> GetSummaryAsync(BeneficiaryFilterDto filter)
        {
            filter.PsgcCodeRegion = DefaultRegionCode;

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
                throw new Exception("Grantee not found");

            selectedBeneficiary.IsDeleted = true;

            await _repo.UpdateAsync(selectedBeneficiary);

            //Logging
            await AddLogAsync(
             selectedBeneficiary.Id,
             "Soft deleted beneficiary record",
             userName);


            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }
        public async Task BulkUpdatePaymentStatusAsync(List<Guid> ids, int paymentStatus, DateTime? paymentDate, string userName)
        {
            if (ids == null || !ids.Any())
                throw new Exception("No records selected");

            if (paymentStatus != 1 && paymentStatus != 2)
                throw new Exception("Invalid payment status. Must be 1 (Unpaid) or 2 (Paid).");

            // ✅ Paid requires a date
            if (paymentStatus == 2 && paymentDate == null)
                throw new Exception("Payment date is required when status is Paid.");



            await _repo.BulkUpdatePaymentStatusAsync(ids, paymentStatus, paymentDate);

            var statusLabel = paymentStatus == 2 ? $"Paid (Date: {paymentDate:yyyy-MM-dd})" : "Unpaid";

            foreach (var id in ids)
            {
                await AddLogAsync(id, $"Bulk payment status updated to: {statusLabel}", userName);
            }

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
        }
        public async Task UpdateAsync(Guid Id, BeneficiaryInformationDto dto, string userName)
        {
            var beneficiary = await _repo.GetEntityByIdAsync(Id);
            if (beneficiary == null)
                throw new Exception("Grantee not found");

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

            beneficiary.Update(dto.DateApplied, dto.DateEndorsed, dto.BatchCode, dto.OscaIdNumber, dto.OscaIdDateIssued, dto.NcscRrn, dto.LastName, dto.FirstName, dto.MiddleName, dto.Extension, dto.BirthDate, dto.PhoneNumber,
                dto.Sex, dto.IsIndigenousPeople, dto.IsPersonWithDisability, dto.CivilStatus, dto.Citizenship, DefaultRegionCode, dto.PsgcCodeProvince, dto.PsgcCodeMunicipality, dto.PsgcCodeBarangay,
                dto.IsCompliant, dto.Validator, dto.ValidationDate, dto.PaymentStatus, dto.ModeOfPayment, dto.PaymentDate,
                dto.IsDeceased, dto.DateOfDeath, dto.IsEligible, dto.AssessmentRemarks, dto.RemarkCategory, dto.Remarks);

            await _repo.UpdateAsync(beneficiary);

            if (changes.Any())
            {
                await AddLogAsync(
                    beneficiary.Id,
                    $"Updated beneficiary. Changes: {string.Join("; ", changes)}",
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
        // FIXES applied to your existing BuildPayrollSheet:
        // 1. PAGE2_ROW_HT = PAGE1_ROW_HT = 35 (already same, but
        //    the issue is CGP row pushes the height — fixed by
        //    NOT changing currentRow height after CGP insert)
        // 2. labelRow1 and labelRow2 were both = currentRow (same row!)
        //    Fixed: labelRow1 = sig3Row + 1, labelRow2 = sig3Row + 2
        // 3. SDO label was written to labelRow1 AND sig3Row (conflict)
        //    Fixed: ALMIRA on sig3Row, SDO label on sig3Row+1,
        //    "other officer" on sig3Row+2
        // 4. CGP border was XLBorderStyleValues.None — should be Thin
        // 5. Purpose text range was D–H (4–8), should be D–P (4–16)
        // 6. Subtotal was currentRow+1 (skips a row) — matched to your code
        // ============================================================

        public async Task<byte[]> GeneratePayrollAsync(PayrollSettingsDto settings)
        {
            if (settings.Ids == null || !settings.Ids.Any())
                throw new InvalidOperationException("No records selected.");

            var allData = await _repo.GetByIdsAsync(settings.Ids);
            if (!allData.Any())
                throw new InvalidOperationException("None of the selected records were found.");

            var groups = allData
                .GroupBy(x => (x.BatchCode ?? "NO BATCH").ToUpperInvariant())
                .OrderBy(g => g.Key)
                .ToList();

            using var workbook = new XLWorkbook();

            foreach (var group in groups)
            {
                var batchCode = group.Key;
                var records = group
                    .OrderBy(x => x.BarangayName)
                    .ThenBy(x => x.LastName)
                    .ThenBy(x => x.FirstName)
                    .ToList();

                var sheetName = SanitizeSheetName(batchCode);
                var ws = workbook.Worksheets.Add(sheetName);
                BuildPayrollSheet(ws, batchCode, records, settings);
            }

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        private static void BuildPayrollSheet(
            IXLWorksheet ws,
            string batchCode,
            List<BeneficiaryInformationDto> records,
            PayrollSettingsDto s)
        {
            const int COLS = 19;
            const int PAGE1_RECORDS = 6;
            const int PAGE2_RECORDS = 9;
            // ✅ FIX 1: Both pages use the same row height — uniform
            const double DATA_ROW_HT = 140;
            const int FONT_SIZE = 14;

            var first = records.FirstOrDefault();
            var municipality = first?.MunicipalityName ?? "";
            var province = first?.ProvinceName ?? "";

            // ── Helpers ──────────────────────────────────────────────────────────────
            void NavyHeader(IXLRange r, string text)
            {
                r.Merge();
                r.Value = text;
                r.Style.Font.Bold = true;
                r.Style.Font.FontSize = FONT_SIZE;
                r.Style.Font.FontName = "Arial";
                r.Style.Font.FontColor = XLColor.White;
                r.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F3864");
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
                cell.Style.Font.FontName = "Arial";
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            void DataCell(int row, int col, object? val,
                          XLAlignmentHorizontalValues align = XLAlignmentHorizontalValues.Left)
            {
                if (val != null) ws.Cell(row, col).Value = XLCellValue.FromObject(val);
                ws.Cell(row, col).Style.Font.FontSize = 16;
                ws.Cell(row, col).Style.Font.FontName = "Arial";
                ws.Cell(row, col).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Cell(row, col).Style.Alignment.Horizontal = align;
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, col).Style.Alignment.WrapText = true;
            }

            // ✅ FIX 4: CGP border = Thin (was None)
            void CgpCell(int row, string text)
            {
                ws.Range(row, 16, row, 17);
                ws.Cell(row, 17).Value = text;
                ws.Cell(row, 17).Style.Font.Bold = false;
                ws.Cell(row, 17).Style.Font.FontSize = FONT_SIZE;
                ws.Cell(row, 17).Style.Font.FontName = "Arial";
                ws.Cell(row, 17).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Cell(row, 17).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Cell(row, 17).Style.Border.OutsideBorder = XLBorderStyleValues.None;
            }

            // =========================================================================
            // SECTION 1: HEADER
            // =========================================================================
            ws.Row(3).Height = 24;
            MergeCenter(3, 2, 2, "NATIONAL COMMISSION OF SENIOR CITIZENS", bold: true);

            ws.Row(4).Height = 24;
            MergeCenter(4, 2, 2, $"Regional Office XIII, Province of {province}, {municipality}");

            ws.Row(5).Height = 21.75;
            MergeCenter(5, 2, 2, "Expanded Centenarian Act");

            ws.Row(6).Height = 10.5;
            ws.Row(7).Height = 10.5;

            ws.Row(8).Height = 18.75;
            MergeCenter(8, 2, 2, "CASH GIFT PAYROLL", bold: true);

            ws.Row(9).Height = 14.25;

            // Row 10: A. PURPOSE: | D–P purpose text | Q CGP-0001
            ws.Row(10).Height = 23.25;
            ws.Cell(10, 2).Value = "A. PURPOSE:";
            ws.Cell(10, 2).Style.Font.Bold = true;
            ws.Cell(10, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(10, 2).Style.Font.FontName = "Arial";
            ws.Cell(10, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // ✅ FIX 5: Purpose text D(4)–P(16) not just D–H
            ws.Range(10, 4, 10, 15).Merge();
            ws.Cell(10, 4).Value = "Cash gift payout for Octogenarians, Nonagenarians, and Centenarians pursuant to R.A. No. 11982 - Expanded Centenarian Act.";
            ws.Cell(10, 4).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(10, 4).Style.Font.FontName = "Arial";
            ws.Cell(10, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            ws.Cell(10, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Cell(10, 4).Style.Alignment.WrapText = true;

            CgpCell(10, $"CGP No.: {s.RegionCode}-{s.YearMonth}-{s.FixedSegment}-{s.ShortenYear}-0001");

            ws.Row(11).Height = 11.25;

            // =========================================================================
            // SECTION 2: COLUMN HEADERS rows 12–14
            // Row 12: ht=14.25  level-1
            // Row 13: ht=24.75  spacer
            // Row 14: ht=108.75 level-2 sub-headers
            // =========================================================================
            ws.Row(12).Height = 14.25;
            ws.Row(13).Height = 24.75;
            ws.Row(14).Height = 108.75;

            NavyHeader(ws.Range(12, 2, 14, 2), "Batch\nCode");
            NavyHeader(ws.Range(12, 3, 14, 3), "No.");
            NavyHeader(ws.Range(12, 4, 13, 7), "FULL NAME OF BENEFICIARY");
            NavyHeader(ws.Range(14, 4, 14, 4), "Last Name");
            NavyHeader(ws.Range(14, 5, 14, 5), "First Name");
            NavyHeader(ws.Range(14, 6, 14, 6), "Middle Name");
            NavyHeader(ws.Range(14, 7, 14, 7), "Ext.");
            NavyHeader(ws.Range(12, 8, 14, 8), "BDate\n(mm/dd/yyyy)");
            NavyHeader(ws.Range(12, 9, 14, 9), "Age");
            NavyHeader(ws.Range(12, 10, 14, 10), "Sex");
            NavyHeader(ws.Range(12, 11, 14, 11), "Barangay");
            NavyHeader(ws.Range(12, 12, 14, 12), "Amount");
            NavyHeader(ws.Range(12, 13, 14, 13), "Amount\nReceived");
            NavyHeader(ws.Range(12, 14, 13, 15), "Beneficiary / Authorized\nRepresentative");
            NavyHeader(ws.Range(14, 14, 14, 14), "Signature Over\nPrinted Name");
            NavyHeader(ws.Range(14, 15, 14, 15), "Thumbmark");
            NavyHeader(ws.Range(12, 16, 14, 16), "For Authorized Representative\n(Relationship/Witness)");
            NavyHeader(ws.Range(12, 17, 14, 17), "Date of\nDeath");
            NavyHeader(ws.Range(12, 18, 14, 18), "Date\nReceived");
            NavyHeader(ws.Range(12, 19, 14, 19), "Remarks");

            // =========================================================================
            // SECTION 3: DATA ROWS — start row 15
            // =========================================================================
            int currentRow = 15;
            int globalSeq = 1;
            int page = 1;
            int processed = 0;

            while (processed < records.Count)
            {
                int pageSize = page == 1 ? PAGE1_RECORDS : PAGE2_RECORDS;
                var pageRecs = records.Skip(processed).Take(pageSize).ToList();

                // CGP for pages 2+ — Q(17) only, ht=23.25 (exact from file)
                if (page > 1)
                {
                    CgpCell(currentRow,
                        $"CGP No.: {s.RegionCode}-{s.YearMonth}-{s.FixedSegment}-{s.ShortenYear}-{page:D4}");
                    ws.Row(currentRow).Height = 23.25;
                    currentRow++;
                }

                foreach (var rec in pageRecs)
                {
                    int dr = currentRow;
                    // ✅ FIX 1: Same height for ALL data rows regardless of page
                    ws.Row(dr).Height = DATA_ROW_HT;

                    DataCell(dr, 2, (rec.BatchCode ?? "").ToUpperInvariant());

                    DataCell(dr, 3, globalSeq, XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 4, (rec.LastName ?? "").ToUpperInvariant());
                    DataCell(dr, 5, (rec.FirstName ?? "").ToUpperInvariant());
                    DataCell(dr, 6, (rec.MiddleName ?? "").ToUpperInvariant());
                    DataCell(dr, 7, (rec.Extension ?? "").ToUpperInvariant());
                    DataCell(dr, 8, rec.BirthDate.ToString("MM/dd/yyyy"), XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 9, rec.Age, XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 10, rec.Sex == 1 ? "MALE" : "FEMALE", XLAlignmentHorizontalValues.Center);
                    DataCell(dr, 11, rec.BarangayName.ToUpperInvariant());
                    DataCell(dr, 12, s.CashGiftAmount, XLAlignmentHorizontalValues.Right);
                    ws.Cell(dr, 12).Style.NumberFormat.Format = "#,##0.00";
                    if (rec.IsDeceased && rec.DateOfDeath.HasValue)
                        DataCell(dr, 17, rec.DateOfDeath.Value.ToString("MM/dd/yyyy"),
                                 XLAlignmentHorizontalValues.Center);

                    ws.Range(dr, 2, dr, COLS).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Range(dr, 2, dr, COLS).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    globalSeq++;
                    currentRow++;
                }

                processed += pageRecs.Count;
                page++;
            }

            // =========================================================================
            // SECTION 4: SUBTOTAL — ht=18, I(9)=label, L(12)=formula
            // =========================================================================
            // ✅ Keep your +1 gap before subtotal
            int subtotalRow = currentRow + 1;
            int dataStartRow = 15;
            ws.Row(subtotalRow).Height = 18;

            ws.Range(subtotalRow, 2, subtotalRow, 9).Merge();
            ws.Cell(subtotalRow, 2).Value = "SUBTOTAL";
            ws.Cell(subtotalRow, 2).Style.Font.Bold = true;
            ws.Cell(subtotalRow, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(subtotalRow, 2).Style.Font.FontName = "Arial";
            ws.Cell(subtotalRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(subtotalRow, 12).FormulaA1 = $"=SUM(L{dataStartRow}:L{subtotalRow - 2})";
            ws.Cell(subtotalRow, 12).Style.Font.Bold = true;
            ws.Cell(subtotalRow, 12).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(subtotalRow, 12).Style.Font.FontName = "Arial";
            ws.Cell(subtotalRow, 12).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(subtotalRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Range(subtotalRow, 13, subtotalRow, COLS).Merge();

            currentRow = subtotalRow + 2;

            // =========================================================================
            // SECTION 5: SIGNATORIES
            // =========================================================================

            // Cert text — B(2)–I(9)
            ws.Range(currentRow, 2, currentRow, 9).Merge();
            ws.Cell(currentRow, 2).Value = s.Signatory1Label;
            ws.Cell(currentRow, 2).Style.Font.Italic = true;
            ws.Cell(currentRow, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 2).Style.Font.FontName = "Arial";
            ws.Cell(currentRow, 2).Style.Alignment.WrapText = true;
            ws.Cell(currentRow, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(currentRow).Height = 18;
            currentRow++;

            // Blank
            ws.Row(currentRow).Height = 14.25; currentRow++;

            // "Approved for Payment:" — L(12)–P(16)
            ws.Range(currentRow, 12, currentRow, 16).Merge();
            ws.Cell(currentRow, 12).Value = s.Signatory2Label;
            ws.Cell(currentRow, 12).Style.Font.Bold = true;
            ws.Cell(currentRow, 12).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 12).Style.Font.FontName = "Arial";
            ws.Cell(currentRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currentRow, 12).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(currentRow).Height = 18;
            currentRow++;

            // Blank signing space rows (3)
            ws.Row(currentRow).Height = 14.25; currentRow++;
            ws.Row(currentRow).Height = 14.25; currentRow++;
            ws.Row(currentRow).Height = 14.25; currentRow++;

            // SARAH ROSE B(2)–E(5) | CESAR L(12)–P(16) — underlined
            int sigNamesRow = currentRow;
            ws.Range(sigNamesRow, 2, sigNamesRow, 5).Merge();
            ws.Cell(sigNamesRow, 2).Value = s.Signatory1Name;
            ws.Cell(sigNamesRow, 2).Style.Font.Bold = true;
            ws.Cell(sigNamesRow, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(sigNamesRow, 2).Style.Font.FontName = "Arial";
            ws.Cell(sigNamesRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(sigNamesRow, 2, sigNamesRow, 5).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Range(sigNamesRow, 12, sigNamesRow, 16).Merge();
            ws.Cell(sigNamesRow, 12).Value = s.Signatory2Name;
            ws.Cell(sigNamesRow, 12).Style.Font.Bold = true;
            ws.Cell(sigNamesRow, 12).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(sigNamesRow, 12).Style.Font.FontName = "Arial";
            ws.Cell(sigNamesRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(sigNamesRow, 12, sigNamesRow, 16).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            ws.Row(sigNamesRow).Height = 18;
            currentRow++;

            // Positions
            ws.Range(currentRow, 2, currentRow, 5).Merge();
            ws.Cell(currentRow, 2).Value = s.Signatory1Position;
            ws.Cell(currentRow, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 2).Style.Font.FontName = "Arial";
            ws.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(currentRow, 12, currentRow, 16).Merge();
            ws.Cell(currentRow, 12).Value = s.Signatory2Position;
            ws.Cell(currentRow, 12).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 12).Style.Font.FontName = "Arial";
            ws.Cell(currentRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(currentRow).Height = 18;
            currentRow++;

            // Blank gap
            ws.Row(currentRow).Height = 8; currentRow++;

            // Oath text — B(2)–I(9), left side only
            ws.Range(currentRow, 2, currentRow, 9).Merge();
            ws.Cell(currentRow, 2).Value =
                "R. I/we certify on my/our official oath that on ______________________________________," +
                "I/we have paid in cash to each individual on the payroll, the amount set opposite to each name," +
                " having presented himself/herself, established identity and affixed his/her signature or" +
                " thumbmark on the space provided.";
            ws.Cell(currentRow, 2).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(currentRow, 2).Style.Font.FontName = "Arial";
            ws.Cell(currentRow, 2).Style.Alignment.WrapText = true;
            ws.Row(currentRow).Height = 38;
            currentRow++;

            // Blank signing space
            ws.Row(currentRow).Height = 14.25; currentRow++;
            ws.Row(currentRow).Height = 14.25; currentRow++;

            // ✅ FIX 2: Correct row offsets — three separate rows
            int sig3Row = currentRow;       // ALMIRA + underlines
            int labelRow1 = currentRow + 1;   // SDO label + "Printed Name and Signature of"
            int labelRow2 = currentRow + 2;   // "other officer present during Payout"

            // ALMIRA — C(3)–J(10) underlined
            ws.Row(sig3Row).Height = 21;
            ws.Range(sig3Row, 3, sig3Row, 10).Merge();
            ws.Cell(sig3Row, 3).Value = s.Signatory3Name;
            ws.Cell(sig3Row, 3).Style.Font.Bold = true;
            ws.Cell(sig3Row, 3).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(sig3Row, 3).Style.Font.FontName = "Arial";
            ws.Cell(sig3Row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(sig3Row, 3, sig3Row, 10).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Other officer 1 — N(14)–O(15) underlined blank
            ws.Range(sig3Row, 14, sig3Row, 15).Merge();
            ws.Range(sig3Row, 14, sig3Row, 15).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Other officer 2 — P(16)–Q(17) underlined blank
            ws.Range(sig3Row, 16, sig3Row, 17).Merge();
            ws.Range(sig3Row, 16, sig3Row, 17).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // SDO label row
            ws.Row(labelRow1).Height = 21;
            ws.Range(labelRow1, 3, labelRow1, 10).Merge();
            ws.Cell(labelRow1, 3).Value = s.Signatory3Position;
            ws.Cell(labelRow1, 3).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow1, 3).Style.Font.FontName = "Arial";
            ws.Cell(labelRow1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(labelRow1, 14, labelRow1, 15).Merge();
            ws.Cell(labelRow1, 14).Value = "Printed Name and Signature of";
            ws.Cell(labelRow1, 14).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow1, 14).Style.Font.FontName = "Arial";
            ws.Cell(labelRow1, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(labelRow1, 16, labelRow1, 17).Merge();
            ws.Cell(labelRow1, 16).Value = "Printed Name and Signature of";
            ws.Cell(labelRow1, 16).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow1, 16).Style.Font.FontName = "Arial";
            ws.Cell(labelRow1, 16).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // "other officer present during Payout" row
            ws.Row(labelRow2).Height = 14.25;
            ws.Range(labelRow2, 14, labelRow2, 15).Merge();
            ws.Cell(labelRow2, 14).Value = s.Signatory4Position;
            ws.Cell(labelRow2, 14).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow2, 14).Style.Font.FontName = "Arial";
            ws.Cell(labelRow2, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(labelRow2, 16, labelRow2, 17).Merge();
            ws.Cell(labelRow2, 16).Value = s.Signatory4Position;
            ws.Cell(labelRow2, 16).Style.Font.FontSize = FONT_SIZE;
            ws.Cell(labelRow2, 16).Style.Font.FontName = "Arial";
            ws.Cell(labelRow2, 16).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // =========================================================================
            // SECTION 6: COLUMN WIDTHS (exact from file)
            // =========================================================================
            double[] colWidths = {
             //  A      B       C     D       E       F       G       H       I
                 1.82,  50.0,   8.0,  33.18,  28.82,  27.46,  12.82,  17.82,  8.72,
             //  J       K       L       M       N      O       P      Q       R       S
                 13.27,  22.82,  16.46,  15.72,  39.0,  32.18,  48.0,  14.27,  15.72,  25.82
             };
            for (int c = 1; c <= colWidths.Length; c++)
                ws.Column(c).Width = colWidths[c - 1];

            ws.PageSetup.PaperSize = XLPaperSize.LegalPaper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.Margins.Top = 0.5;
            ws.PageSetup.Margins.Bottom = 0.5;
            ws.PageSetup.Margins.Left = 0.5;
            ws.PageSetup.Margins.Right = 0.5;

            ws.PageSetup.Footer.Center.AddText("Page ");
            ws.PageSetup.Footer.Center.AddText(XLHFPredefinedText.PageNumber);
            ws.PageSetup.Footer.Center.AddText(" of ");
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
            var worksheet = workbook.Worksheets.Add("Grantees");

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

            AddCenteredTitle(1, "NATIONAL COMMISSION OF SENIOR CITIZENS");
            AddCenteredTitle(2, "Expanded Centenarian Act");
            AddCenteredTitle(3, "Regional Office Caraga");
            AddCenteredTitle(4, $"(Import Template for FY {DateTime.UtcNow.Year})");

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
        "BATCH CODE",
        "NO.",
        "OSCA ID NUMBER",
        "NCSC RRN",
        "LAST NAME",
        "FIRST NAME",        // required
        "MIDDLE NAME",
        "EXTENSION",
        // Col 9–12: Birth date (split)
        "BIRTH MONTH",       // e.g. JANUARY
        "BIRTH DAY",         // e.g. 01
        "BIRTH YEAR",        // e.g. 1926
        "AGE",               // auto-computed, leave blank
        // Col 13–18: Demographics & location
        "SEX",               // MALE or FEMALE
        "REGION",            // e.g. REGION XIII (CARAGA) — leave blank to default
        "PROVINCE",
        "MUNICIPALITY/CITY",
        "BARANGAY",
        "COMPLIANCE TO DOCUMENTARY REQUIREMENTS", // COMPLIANT or NON-COMPLIANT
        // Col 19–20: Validation
        "NAME OF VALIDATOR",
        "VALIDATION DATE",   // e.g. March 17, 2026
        // Col 21–28: Newly added columns
        "CONTACT NUMBER",
        "DATE OF DEATH",     // e.g. March 17, 2026
        "DATE APPLIED",      // e.g. March 17, 2026
        "DATE ENDORSED",     // e.g. March 17, 2026
        "OSCA ID DATE ISSUED",
        "INDIGENOUS PEOPLE", // YES or NO
        "PERSON WITH DISABILITY", // YES or NO
        "NCSC ASSESSMENT"    // ELIGIBLE or INELIGIBLE
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
            var instructions = workbook.Worksheets.Add("Instructions");

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
        new[] { "BATCH CODE",                      "No",  "Any text",                             "BC-2026" },
        new[] { "NO.",                             "No",  "Number (auto if left blank)",           "1" },
        new[] { "OSCA ID NUMBER",                  "No",  "Any text",                             "OSCA-00001" },
        new[] { "NCSC RRN",                        "No",  "Numbers only, no special characters",  "12345" },
        new[] { "LAST NAME",                       "No",  "Any text",                             "DELA CRUZ" },
        new[] { "FIRST NAME",                      "YES", "Any text",                             "JUAN" },
        new[] { "MIDDLE NAME",                     "No",  "Any text",                             "SANTOS" },
        new[] { "EXTENSION",                       "No",  "Jr. / Sr. / II / III / IV / V",        "Jr." },
        new[] { "BIRTH MONTH",                     "YES", "Full month name in CAPS",              "JANUARY" },
        new[] { "BIRTH DAY",                       "YES", "Two-digit day",                        "01" },
        new[] { "BIRTH YEAR",                      "YES", "Four-digit year",                      "1926" },
        new[] { "AGE",                             "No",  "Leave blank — auto-computed",          "" },
        new[] { "SEX",                             "No",  "MALE or FEMALE",                       "MALE" },
        new[] { "REGION",                          "No",  "Leave blank to default to Caraga",     "REGION XIII (CARAGA)" },
        new[] { "PROVINCE",                        "YES", "Full province name",                   "AGUSAN DEL NORTE" },
        new[] { "MUNICIPALITY/CITY",               "YES", "Full municipality or city name",       "BUTUAN CITY" },
        new[] { "BARANGAY",                        "YES", "Full barangay name",                   "AMBAGO" },
        new[] { "COMPLIANCE",                      "No",  "COMPLIANT or NON-COMPLIANT",           "COMPLIANT" },
        new[] { "NAME OF VALIDATOR",               "No",  "Any text",                             "JANE DOE" },
        new[] { "VALIDATION DATE",                 "No",  "Month DD, YYYY",                       "March 17, 2026" },
        new[] { "CONTACT NUMBER",                  "No",  "Any text",                             "09171234567" },
        new[] { "DATE OF DEATH",                   "No",  "Month DD, YYYY — leave blank if alive","" },
        new[] { "DATE APPLIED",                    "No",  "Month DD, YYYY",                       "January 5, 2026" },
        new[] { "DATE ENDORSED",                   "No",  "Month DD, YYYY",                       "February 1, 2026" },
        new[] { "OSCA ID DATE ISSUED",             "No",  "Month DD, YYYY",                       "March 1, 2020" },
        new[] { "INDIGENOUS PEOPLE",               "No",  "YES or NO",                            "NO" },
        new[] { "PERSON WITH DISABILITY",          "No",  "YES or NO",                            "NO" },
        new[] { "NCSC ASSESSMENT",                 "YES", "ELIGIBLE or INELIGIBLE",               "ELIGIBLE" },
    };

            for (int i = 0; i < instrData.Length; i++)
            {
                var rowData = instrData[i];
                for (int col = 1; col <= rowData.Length; col++)
                {
                    instructions.Cell(i + 2, col).Value = rowData[col - 1];
                }
                // Highlight required rows
                if (rowData[1] == "YES")
                {
                    instructions.Range(i + 2, 1, i + 2, 4)
                        .Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3CD");
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
        public async Task<BeneficiaryImportResultDto> UpdateExcelAsync(Stream fileStream,string fileName, string sheetName, string userName)
        {
            var result = new BeneficiaryImportResultDto();

            using var workbook = new XLWorkbook(fileStream);

            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == sheetName)
                ?? throw new Exception($"Worksheet '{sheetName}' not found.");

            const int firstDataRowNumber = 11;
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

            var regions = await _regionRepository.GetAllAsync();
            var provinces = await _provinceRepository.GetAllProvinceAsync();
            var municipalities = await _municipalityRepository.GetAllMunicipalityAsync();
            var barangays = await _barangayRepository.GetBarangaysAsync();

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
                            Field = "Record",
                            Message = "Record not found in database. Update not allowed.",
                            RawValue = $"{lastName}, {firstName}"
                        });

                        continue;
                    }

                    // 🔥 MAP FIELDS
                    var isEligible = MapEligibility(row.Cell(28).GetFormattedString());
                    var dateOfDeath = ParseFlexibleDate(row.Cell(22).GetFormattedString());
                    var validationDate = row.Cell(20).Value;

                    // 🔥 UPDATE ONLY
                    existing.IsEligible = isEligible.HasValue ? isEligible.Value : false;
                    existing.DateOfDeath = dateOfDeath;
                    existing.ValidationDate = validationDate;


                    await AddLogAsync(
                        existing.Id,
                        $"Excel Update: {existing.LastName}, {existing.FirstName}",
                        userName);

                    result.ImportedCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new BeneficiaryImportErrorDto
                    {
                        RowNumber = rowNumber,
                        Field = "General",
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
                throw new Exception("Please upload a valid Excel file.");

            if (string.IsNullOrWhiteSpace(fileName))
                throw new Exception("Invalid file name.");

            var extension = Path.GetExtension(fileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Only .xlsx Excel files are allowed.");

            var result = new BeneficiaryImportResultDto();
            var beneficiariesToImport = new List<BeneficiaryInformation>();

            var regions = await _regionRepository.GetAllAsync();
            var provinces = await _provinceRepository.GetAllProvinceAsync();
            var municipalities = await _municipalityRepository.GetAllMunicipalityAsync();
            var barangays = await _barangayRepository.GetBarangaysAsync();

            using var workbook = new XLWorkbook(fileStream);


            if (string.IsNullOrWhiteSpace(sheetName))
                throw new Exception("Please select a worksheet.");

            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == sheetName);

            if (worksheet == null)
                throw new Exception($"Worksheet '{sheetName}' not found.");

            const int firstDataRowNumber = 11;

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < firstDataRowNumber)
                throw new Exception("Sheet1 does not contain data rows.");

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
                            Field = "First Name", 
                            Message = "First Name is required.", 
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
                            Field = "Birth Date",
                            Message = "Invalid Birth Date from Month/Day/Year values.",
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
                            Field = "Osca Id Date Issued",
                            Message = "Invalid date format. Example valid format: 'March 17, 2026'.",
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
                            Field = "Date Endrosed",
                            Message = "Invalid date format. Example valid format: 'March 17, 2026'.",
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
                            Field = "Date of Application",
                            Message = "Invalid date format. Example valid format: 'March 17, 2026'.",
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
                            Field = "Date of Death",
                            Message = "Invalid date format. Example valid format: 'March 17, 2026'.",
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
                                Field = "NCSC RRN",
                                Message = "Invalid NCSC RRN.",
                                RawValue = ncscRrnRaw,
                                Suggestion = "Remove special character."
                            });

                            rowHasError = true;
                        }
                    }

                    var region = string.IsNullOrWhiteSpace(regionName)
                        ? regions.FirstOrDefault(x => x.PsgcCodeRegion == DefaultRegionCode)
                        : FindBestNameMatch(regions, x => x.Name, regionName);

                    if (region == null)
                    {
                        var suggestion = GetSuggestedName(regions, x => x.Name, regionName);

                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = "Region",
                            Message = "Region not found.",
                            RawValue = regionName,
                            Suggestion = suggestion != null
                            ? $"Possible match: '{suggestion}'"
                            : "Check spelling and spacing."
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
                            Field = "Province",
                            Message = "Province not found.",
                            RawValue = provinceName,
                            Suggestion = suggestion != null
                                ? $"Possible match: '{suggestion}'"
                                : "Check spelling and spacing."
                        });
                        rowHasError = true;
                    }


                    var municipality = FindBestNameMatch(municipalities, x => x.Name, municipalityName);
                    if (municipality == null)
                    {
                        var suggestion = GetSuggestedName(municipalities, x => x.Name, municipalityName);

                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = "Municipality/City",
                            Message = "Municipality/City not found.",
                            RawValue = municipalityName,
                            Suggestion = suggestion != null
                                ? $"Possible match: '{suggestion}'"
                                : "Check spelling and spacing."
                        });
                        rowHasError = true;
                    }

                    var barangay = FindBestNameMatch(barangays, x => x.Name, barangayName);
                    if (barangay == null)
                    {
                        var suggestion = GetSuggestedName(barangays, x => x.Name, barangayName);

                        result.Errors.Add(new BeneficiaryImportErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = "Barangay",
                            Message = "Barangay not found.",
                            RawValue = barangayName,
                            Suggestion = suggestion != null
                                ? $"Possible match: '{suggestion}'"
                                : "Check spelling and spacing."
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
                            Field = "NCSC Assessment",
                            Message = "Please enter Eligible or InEligible only.",
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
                 birthDate == DateTime.MinValue ? "" : birthDate.ToString("yyyy-MM-dd"),
                 (oscaIdNumber ?? string.Empty).Trim().ToLower(),
                 ncscRrn?.ToString() ?? "");

                    if (!string.IsNullOrWhiteSpace(firstName) && birthDate != DateTime.MinValue)
                    {
                        if (!uploadedRowKeys.Add(duplicateKey))
                        {
                            result.Errors.Add(new BeneficiaryImportErrorDto
                            {
                                RowNumber = rowNumber,
                                Field = "Duplicate",
                                Message = "Duplicate record found within the uploaded file.",
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
                                Field = "Duplicate",
                                Message = "Duplicate record already exists in the database.",
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
                        Validator = string.IsNullOrWhiteSpace(validator) ? "N/A" : validator.Trim(),
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
                        Field = "General",
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
                result.SkippedDuplicateCount = result.Errors.Count(x => x.Field == "Duplicate");
                return result;
            }
            foreach (var beneficiary in beneficiariesToImport)
            {
                await _repo.AddAsync(beneficiary);
                await AddLogAsync(
                    beneficiary.Id,
                    $"Imported beneficiary from Excel: {beneficiary.LastName}, {beneficiary.FirstName}",
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
                throw new Exception("Please upload a valid Excel file.");
            if (string.IsNullOrWhiteSpace(fileName))
                throw new Exception("Invalid file name.");

            var extension = Path.GetExtension(fileName);

            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Only .xlsx Excel files are allowed.");

            fileStream.Position = 0;

            using var workbook = new XLWorkbook(fileStream);
            var sheetNames = workbook.Worksheets
                .Select(ws => ws.Name)
                .ToList();

            return await Task.FromResult(sheetNames);
        }

        #endregion Excel updating and Importing - END
        public async Task<PagedResultDto<BeneficiaryInformationDto>> GetPaginatedAsync(BeneficiaryFilterDto filter)
        {
            filter.PsgcCodeRegion = DefaultRegionCode;

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
                "beneficiary-paginated",
                filter.PsgcCodeRegion?.ToString() ?? "null",
                filter.PageNumber.ToString(),
                filter.PageSize.ToString(),
                filter.PsgcCodeProvince.ToString() ??  "null",
                filter.PsgcCodeMunicipality.ToString() ?? "null",
                filter.PsgcCodeBarangays.ToString() ?? "null",
                filter.LastName ?? string.Empty,
                filter.FirstName ?? string.Empty,
                filter.FullName ?? string.Empty,
                filter.Validator ?? string.Empty,
                filter.BatchCode ?? string.Empty,
                filter.Sex != null ? filter.Sex : "null",
                filter.PaymentStatus != null ? filter.PaymentStatus : "null",
                filter.PaymentDate?.ToString("yyyy-MM-dd") ?? "null",
                filter.SpecificAge?.ToString() ?? "null",
                filter.MilestoneYear?.ToString() ?? "null",
                filter.SpecificBirthday?.ToString("yyyy-MM-dd") ?? "null",
                filter.BirthdayFrom?.ToString("yyyy-MM-dd") ?? "null",
                filter.BirthdayTo?.ToString("yyyy-MM-dd") ?? "null"
            );
        }
        private async Task<BeneficiaryInformation?> FindExistingAsync(string lastName, string firstName,string middleName, DateTime birthDate, string oscaIdNumber, int? ncscRrn)
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
                "COMPLIANT" => true,
                "YES" => true,
                _ => false
            };
        }
        //Person with disability Map
        private static bool MapIsPersonWithDisability(string value)
        {
            var normalized = value.Trim().ToUpper();

            return normalized switch
            {
                "YES" => true,
                "NO" => false,
                _ => false
            };
        }
        //Payment Status Map
        private int? MapPaymentStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null; // ✅ allow empty = N/A

            var normalized = value.Trim().ToUpper();

            return normalized switch
            {
                "PAID" => 2,
                "UNPAID" => 1,
                _ => null
            };
        }
        //Map Indigenous People
        private static bool MapIndigenousPeople(string value)
        {
            var normalized = value.Trim().ToUpper();

            return normalized switch
            {
                "YES" => true,
                "NO" => false,
                _ => false
            };
        }
        //Map NCSC Assessment
        private static bool? MapEligibility(string value)
        {

            var normalized = value.Trim().ToUpper();

            return normalized switch
            {
                "ELIGIBLE" => true,
                "INELIGIBLE" => false,
                _ => null
            }; ;
        }
        #endregion Mapping method end

        private string GetMonthName(int month)
        {
            return new DateTime(2000, month, 1)
                .ToString("MMMM")
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


        private const string SummaryCacheVersionKey = "beneficiary-summary-version";

        private void InvalidateSummaryCache()
        {
            var newVersion = Guid.NewGuid().ToString();
            _memoryCache.Set(SummaryCacheVersionKey, newVersion);
        }

        private string GetCurrentSummaryCacheVersion()
        {
            return _memoryCache.GetOrCreate(SummaryCacheVersionKey, entry =>
            {
                entry.Priority = CacheItemPriority.NeverRemove;
                return "v1";
            })!;
        }

        private string BuildSummaryCacheKey(BeneficiaryFilterDto filter)
        {
            var version = GetCurrentSummaryCacheVersion();

            return string.Join("|",
                version,
                "beneficiary-summary",
                filter.PsgcCodeRegion?.ToString() ?? "null",
                (filter.PsgcCodeProvinces != null && filter.PsgcCodeProvinces.Any() ? string.Join(",", filter.PsgcCodeProvinces.OrderBy(x => x)) : "null"),
                (filter.PsgcCodeMunicipalities != null && filter.PsgcCodeMunicipalities.Any() ? string.Join(",", filter.PsgcCodeMunicipalities.OrderBy(x => x)) : "null"),
                (filter.PsgcCodeBarangays != null && filter.PsgcCodeBarangays.Any() ? string.Join(",", filter.PsgcCodeBarangays.OrderBy(x => x)) : "null"),
                filter.LastName ?? string.Empty,
                filter.FirstName ?? string.Empty,
                filter.Sex != null ? filter.Sex : "null",
                filter.PaymentStatus != null ? filter.PaymentStatus : "null",
                filter.PaymentDate?.ToString("yyyy-MM-dd") ?? "null",
                filter.SpecificAge?.ToString() ?? "null",
                filter.MilestoneYear?.ToString() ?? "null",
                filter.SpecificBirthday?.ToString("yyyy-MM-dd") ?? "null",
                filter.BirthdayFrom?.ToString("yyyy-MM-dd") ?? "null",
                filter.BirthdayTo?.ToString("yyyy-MM-dd") ?? "null"
            );
        }
        //Updating a beneficiary record involves comparing the existing values with the new values from the DTO and logging any changes. This method generates a list of changed fields for logging purposes.
        private async Task<List<string>> GetChangedFields(
     BeneficiaryInformation beneficiary, BeneficiaryInformationDto dto)
        {
            var changes = new List<string>();

            // ── Simple text fields ────────────────────────────────
            if (beneficiary.DateApplied != dto.DateApplied)
                changes.Add($"Date Applied: '{beneficiary.DateApplied:yyyy-MM-dd}' → '{dto.DateApplied:yyyy-MM-dd}'");

            if (beneficiary.DateEndorsed != dto.DateEndorsed)
                changes.Add($"Date Endorsed: '{beneficiary.DateEndorsed:yyyy-MM-dd}' → '{dto.DateEndorsed:yyyy-MM-dd}'");

            if (beneficiary.BatchCode != dto.BatchCode)
                changes.Add($"Batch Code: '{beneficiary.BatchCode}' → '{dto.BatchCode}'");

            if (beneficiary.OscaIdNumber != dto.OscaIdNumber)
                changes.Add($"OSCA ID Number: '{beneficiary.OscaIdNumber}' → '{dto.OscaIdNumber}'");

            if (beneficiary.OscaIdDateIssued != dto.OscaIdDateIssued)
                changes.Add($"OSCA ID Date Issued: '{beneficiary.OscaIdDateIssued:yyyy-MM-dd}' → '{dto.OscaIdDateIssued:yyyy-MM-dd}'");

            if (beneficiary.NcscRrn != dto.NcscRrn)
                changes.Add($"NCSC RRN: '{beneficiary.NcscRrn}' → '{dto.NcscRrn}'");

            if (beneficiary.LastName != dto.LastName)
                changes.Add($"Last Name: '{beneficiary.LastName}' → '{dto.LastName}'");

            if (beneficiary.FirstName != dto.FirstName)
                changes.Add($"First Name: '{beneficiary.FirstName}' → '{dto.FirstName}'");

            if (beneficiary.MiddleName != dto.MiddleName)
                changes.Add($"Middle Name: '{beneficiary.MiddleName}' → '{dto.MiddleName}'");

            if (beneficiary.Extension != dto.Extension)
                changes.Add($"Extension: '{beneficiary.Extension}' → '{dto.Extension}'");

            if (beneficiary.BirthDate.Date != dto.BirthDate.Date)
                changes.Add($"Birth Date: '{beneficiary.BirthDate:yyyy-MM-dd}' → '{dto.BirthDate:yyyy-MM-dd}'");

            if (beneficiary.PhoneNumber != dto.PhoneNumber)
                changes.Add($"Phone Number: '{beneficiary.PhoneNumber}' → '{dto.PhoneNumber}'");

            // ── Mapped fields ─────────────────────────────────────
            if (beneficiary.Sex != dto.Sex)
                changes.Add($"Sex: '{MapSexLabel(beneficiary.Sex)}' → '{MapSexLabel(dto.Sex)}'");

            if (beneficiary.IsIndigenousPeople != dto.IsIndigenousPeople)
                changes.Add($"Indigenous People: '{(beneficiary.IsIndigenousPeople ? "Yes" : "No")}' → '{(dto.IsIndigenousPeople ? "Yes" : "No")}'");

            if (beneficiary.IsPersonWithDisability != dto.IsPersonWithDisability)
                changes.Add($"Person with Disability: '{(beneficiary.IsPersonWithDisability ? "Yes" : "No")}' → '{(dto.IsPersonWithDisability ? "Yes" : "No")}'");

            if (beneficiary.CivilStatus != dto.CivilStatus)
                changes.Add($"Civil Status: '{MapCivilStatusLabel(beneficiary.CivilStatus)}' → '{MapCivilStatusLabel(dto.CivilStatus)}'");

            if (beneficiary.Citizenship != dto.Citizenship)
                changes.Add($"Citizenship: '{MapCitizenshipLabel(beneficiary.Citizenship)}' → '{MapCitizenshipLabel(dto.Citizenship)}'");

            // ── More mapped fields ────────────────────────────────
            if (beneficiary.IsCompliant != dto.IsCompliant)
                changes.Add($"Compliant: '{(beneficiary.IsCompliant ? "Yes" : "No")}' → '{(dto.IsCompliant ? "Yes" : "No")}'");

            if (beneficiary.Validator != dto.Validator)
                changes.Add($"Validator: '{beneficiary.Validator}' → '{dto.Validator}'");

            if (beneficiary.ValidationDate != dto.ValidationDate)
                changes.Add($"Validation Date: '{beneficiary.ValidationDate:yyyy-MM-dd}' → '{dto.ValidationDate:yyyy-MM-dd}'");

            if (beneficiary.PaymentStatus != dto.PaymentStatus)
                changes.Add($"Payment Status: '{MapPaymentStatusLabel(beneficiary.PaymentStatus)}' → '{MapPaymentStatusLabel(dto.PaymentStatus)}'");

            if (beneficiary.ModeOfPayment != dto.ModeOfPayment)
                changes.Add($"Mode of Payment: '{MapModeOfPaymentLabel(beneficiary.ModeOfPayment)}' → '{MapModeOfPaymentLabel(dto.ModeOfPayment)}'");

            if (beneficiary.PaymentDate != dto.PaymentDate)
                changes.Add($"Payment Date: '{beneficiary.PaymentDate:yyyy-MM-dd}' → '{dto.PaymentDate:yyyy-MM-dd}'");

            if (beneficiary.IsDeceased != dto.IsDeceased)
                changes.Add($"Is Deceased: '{(beneficiary.IsDeceased ? "Yes" : "No")}' → '{(dto.IsDeceased ? "Yes" : "No")}'");

            if (beneficiary.DateOfDeath != dto.DateOfDeath)
                changes.Add($"Date of Death: '{beneficiary.DateOfDeath:yyyy-MM-dd}' → '{dto.DateOfDeath:yyyy-MM-dd}'");

            if (beneficiary.IsEligible != dto.IsEligible)
                changes.Add($"Eligible: '{(beneficiary.IsEligible ? "Yes" : "No")}' → '{(dto.IsEligible ? "Yes" : "No")}'");

            if (beneficiary.AssessmentRemarks != dto.AssessmentRemarks)
                changes.Add($"Assessment Remarks: '{beneficiary.AssessmentRemarks}' → '{dto.AssessmentRemarks}'");

            if (beneficiary.RemarkCategory != dto.RemarkCategory)
                changes.Add($"Remark Category: '{MapRemarkCategoryLabel(beneficiary.RemarkCategory)}' → '{MapRemarkCategoryLabel(dto.RemarkCategory)}'");

            if (beneficiary.Remarks != dto.Remarks)
                changes.Add($"Remarks: '{beneficiary.Remarks}' → '{dto.Remarks}'");

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
        private static T? FindBestNameMatch<T>(
     IEnumerable<T> items,
     Func<T, string?> nameSelector,
     string rawName,
     int minimumScore = 60)
     where T : class
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
                "MALE" => 1,
                "FEMALE" => 2,
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
            1 => "Male",
            2 => "Female",
            _ => "Unknown"
        };

        private static string MapCivilStatusLabel(int? status) => status switch
        {
            1 => "Single",
            2 => "Widowed",
            3 => "Married",
            4 => "Live In",
            _ => "N/A"
        };

        private static string MapCitizenshipLabel(int? citizenship) => citizenship switch
        {
            1 => "Filipino",
            2 => "Dual Citizenship",
            _ => "N/A"
        };

        private static string MapPaymentStatusLabel(int status) => status switch
        {
            0 => "N/A",
            1 => "Unpaid",
            2 => "Paid",
            _ => "Unknown"
        };

        private static string MapModeOfPaymentLabel(int mode) => mode switch
        {
            0 => "N/A",
            1 => "Cash Advance by SDO",
            2 => "Bank Transfer",
            _ => "Unknown"
        };

        private static string MapRemarkCategoryLabel(int? category) => category switch
        {
            1 => "Deceased prior reaching milestone age",
            2 => "Out of town/Country",
            3 => "Incomplete required documents",
            4 => "Inconsistent documents",
            5 => "Cannot be reached/Located",
            6 => "Did not reach the milestone age",
            7 => "Lacking of documents/Requirements",
            8 => "For Correction",
            9 => "Waived",
            10 => "Lacking Proof of Relationship",
            11 => "No show",
            12 => "Double Application with Different Surename used",
            13 => "For CGD Island Municipality",
            _ => "N/A"
        };

        #endregion

    }
}
