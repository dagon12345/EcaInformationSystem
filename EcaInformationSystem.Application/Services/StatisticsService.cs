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

        // "Download FINDES Upload File" — the payout-channel card's export in
        // the FINDES & WEACCESS fund-transfer upload layout. Same filtered set
        // (and same bucket narrowing) as ExportGranteesAsync above, so what
        // gets banked out always matches the count that was drilled into; only
        // the shape of the sheet is different.
        //
        // Exception: the Landbank bucket uses Landbank's own Account Number /
        // Name / Amount layout instead (see IsLandbankBucket below), since those
        // payouts are credited inside Landbank rather than sent over PESONet.
        public async Task<byte[]> ExportFindesUploadAsync(StatisticsMembersRequestDto request)
        {
            var rows = await _repo.GetGranteeExportRowsAsync(request.Filter, request.Bucket);

            // A grantee with no payout account (PreferredChannel 0, "Not Yet
            // Set") has nothing to deposit to and can't satisfy the file's
            // mandatory Bank Code/BICFI — skipped rather than emitted as a
            // half-blank row the bank would reject outright.
            var payable = rows.Where(r => r.PreferredChannel != 0).ToList();

            // ── Landbank is the one bucket that does NOT use the PESONet
            // inter-bank file: a Landbank payout is credited straight from the
            // source account, so the bank's own template ("LBP" tab) asks for
            // only Account Number / Name / Amount and has no Bank Code/BICFI to
            // look up. Scoped to exactly the "channellandbank" drill-down — the
            // other payout buckets keep the FINDES & WEACCESS layout below.
            if (IsLandbankBucket(request.Bucket))
                return BuildLandbankWorkbook(payable);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Other Bank");

            string[] headers =
            {
                "Bank Code", "BICFI", "TelMobile", "Email", "Purpose",
                "CreditorName", "CreditorAddress", "CreditorAcctNum", "TransferAmount"
            };

            for (var col = 1; col <= headers.Length; col++)
            {
                ws.Cell(1, col).Value = headers[col - 1];
                ws.Cell(1, col).Style.Font.Bold = true;
            }

            var row = 2;
            foreach (var r in payable)
            {
                // Channel 1 is "Landbank of the Philippines" by definition, so
                // a row on that channel with no Bank Name typed in still
                // resolves; any other unrecognised name is left blank rather
                // than guessed.
                var matched = FindesBankDirectory.TryResolve(r.BankOrWalletName, out var bankEntry);
                if (!matched && r.PreferredChannel == 1 && FindesBankDirectory.Landbank is { } landbankEntry)
                {
                    bankEntry = landbankEntry;
                    matched = true;
                }

                // Bank Code + BICFI — looked up from the hand-typed bank/wallet
                // name against the Annex A list. An unrecognised name is left
                // blank rather than guessed; BICFI falls back to the BIC/SWIFT
                // already stored on the account (that's where the abroad /
                // other-bank entries keep theirs — Annex A Section E).
                WriteTextCell(ws.Cell(row, 1), matched ? bankEntry.EmiCode : null);
                WriteTextCell(ws.Cell(row, 2),
                    matched && !string.IsNullOrWhiteSpace(bankEntry.Bic) ? bankEntry.Bic : r.SwiftCode);

                // TelMobile — the grantee's mobile number, 11 digits. Written
                // as text so Excel keeps the leading zero ("09766140473", not
                // 9766140473).
                var granteeMobile = FindesBankDirectory.TryNormalizePhMobile(PrimaryPhone(r.ContactNumber), out var mobile11)
                    ? mobile11
                    : null;
                WriteTextCell(ws.Cell(row, 3), granteeMobile);

                // Email — nothing in the schema stores a grantee email (Section
                // C has no such field), so this stays genuinely blank.
                WriteTextCell(ws.Cell(row, 4), null);

                ws.Cell(row, 5).Value = "Grant or Gift";
                WriteTextCell(ws.Cell(row, 6), BuildCreditorName(r));
                WriteTextCell(ws.Cell(row, 7), BuildCreditorAddress(r));
                WriteTextCell(ws.Cell(row, 8), BuildCreditorAcctNum(r, granteeMobile, matched ? bankEntry : null));

                // TransferAmount — 100,000 for centenarians (100 and above),
                // 10,000 otherwise. A plain number: no decimal point and no
                // thousands separator, since the file reads the last two digits
                // as centavos.
                ws.Cell(row, 9).Value = r.Age >= 100 ? 100000 : 10000;

                row++;
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // The Landbank grouped list, and only that one — the template's "LBP"
        // tab. Matched case-insensitively so the bucket string is never trusted
        // to arrive in one exact casing.
        private static bool IsLandbankBucket(string? bucket) =>
            string.Equals(bucket, "channellandbank", StringComparison.OrdinalIgnoreCase);

        // Landbank's own upload layout: Account Number, Name, Amount. No bank
        // code or BICFI, because there is no inter-bank leg to route; that also
        // means nothing here needs the Annex A lookup, and a name that doesn't
        // match a bank is no reason to drop the row.
        private static byte[] BuildLandbankWorkbook(List<StatisticsGranteeExportRowDto> payable)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("LBP");

            string[] headers = { "Account Number", "Name", "Amount" };
            for (var col = 1; col <= headers.Length; col++)
            {
                ws.Cell(1, col).Value = headers[col - 1];
                ws.Cell(1, col).Style.Font.Bold = true;
            }

            var row = 2;
            foreach (var r in payable)
            {
                // The Landbank account number, digits only. Left blank when the
                // record has none — deliberately not substituted with the
                // mobile-number form the FINDES file uses, since Landbank can't
                // credit a phone number; the row stays so the count still
                // matches the card the reviewer clicked, and the gap is visible.
                WriteTextCell(ws.Cell(row, 1), FindesBankDirectory.DigitsOnly(r.AccountNumber));
                WriteTextCell(ws.Cell(row, 2), BuildCreditorName(r));

                // Same amount rule as the FINDES file: no decimal point, the
                // last two digits are read as centavos.
                ws.Cell(row, 3).Value = r.Age >= 100 ? 100000 : 10000;

                row++;
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // Always a string cell with Excel's Text number format: the transfer
        // file's bank codes, mobile numbers and account numbers all carry
        // leading zeros that a numeric cell would silently drop.
        //
        // A value that starts with an apostrophe has to go in as RICH TEXT.
        // ClosedXML reads a leading ' in a plain string as Excel's "quote
        // prefix" text marker: it moves the character into the cell style and
        // drops it from the stored string, so the export came out as
        // "0000009631963701" instead of the "'0000009631963701" the template
        // asks for. Verified against the shipped ClosedXML 0.105.0 —
        // cell.Value = "'00000..." stores 16 characters with QuotePrefix=true,
        // while a rich-text run stores all 17. Everything else takes the
        // normal path.
        private static void WriteTextCell(IXLCell cell, string? value)
        {
            if (value is null || string.IsNullOrWhiteSpace(value))
                return;

            var text = value.Trim();

            if (text.StartsWith("'", StringComparison.Ordinal))
                cell.CreateRichText().AddText(text);
            else
                cell.Value = text;

            cell.Style.NumberFormat.Format = "@";
        }

        // {LAST NAME}, {FIRST NAME} {MIDDLE} {EXT.} — upper case, no periods
        // (the file's CreditorName is a "no special character" field), and no
        // dangling comma when a part is missing.
        private static string? BuildCreditorName(StatisticsGranteeExportRowDto r)
        {
            var last = SanitizeNamePart(r.LastName);
            var given = string.Join(' ', new[] { r.FirstName, r.MiddleName, r.Extension }
                .Select(SanitizeNamePart)
                .Where(p => p.Length > 0));

            if (last.Length == 0 && given.Length == 0) return null;
            if (last.Length == 0) return given;
            if (given.Length == 0) return last;

            return $"{last}, {given}";
        }

        private static string SanitizeNamePart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var cleaned = new string(value
                .Where(ch => char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '\'')
                .ToArray());

            return string.Join(' ', cleaned
                .ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        // "ADDRESS STORED IN SYSTEM" — every address part the record actually
        // has, street detail first, skipping the ones left blank.
        private static string? BuildCreditorAddress(StatisticsGranteeExportRowDto r)
        {
            var streetLine = string.Join(' ', new[] { r.HouseNumber, r.StreetName }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim()));

            var parts = new[]
            {
                streetLine,
                r.BarangayName ?? string.Empty,
                r.MunicipalityName ?? string.Empty,
                r.ProvinceName ?? string.Empty,
                r.RegionName ?? string.Empty,
                r.ZipCode ?? string.Empty
            }.Where(p => !string.IsNullOrWhiteSpace(p));

            var address = string.Join(", ", parts);
            return address.Length == 0 ? null : address;
        }

        // Either a real account number or a mobile number, depending on how the
        // grantee receives the payout: wallets (GCash, Maya/PayMaya, Coins.ph,
        // JuanCash, TayoCash) and Palawan Pawnshop pay out to a number, in the
        // file's own prefixed form ('0000009766140473 — see
        // FindesBankDirectory.BuildMobileCreditorAcctNum), while banks pay out
        // to the account number.
        private static string? BuildCreditorAcctNum(
            StatisticsGranteeExportRowDto r,
            string? granteeMobile,
            FindesBankDirectory.FindesBankEntry? bankEntry)
        {
            // Channel 3 (EMI) and channel 4 (Palawan Pawnshop) are mobile payout
            // channels by definition; a wallet filed under "Other Banks"
            // (channel 2) is one in practice even though its channel says
            // otherwise, which is why the matched Annex A issuer counts too.
            var paysToMobile = r.PreferredChannel is 3 or 4 || bankEntry?.IsMobileWallet == true;

            if (paysToMobile)
            {
                // The preferred payment's own number first — "GCash Account No."
                // for an EMI, "Active Mobile Number for Palawan Pawnshop" for the
                // PSP — then the account fields in case it was typed there
                // instead, then the grantee's own phone.
                var mobile = FindesBankDirectory.TryNormalizePhMobile(r.GCashOrMobileNumber, out var wallet11)
                    ? wallet11
                    : FindesBankDirectory.TryNormalizePhMobile(r.AccountNumber, out var accountMobile11)
                        ? accountMobile11
                        : granteeMobile;

                if (mobile is not null)
                    return FindesBankDirectory.BuildMobileCreditorAcctNum(mobile);
            }

            var accountNumber = FindesBankDirectory.DigitsOnly(r.AccountNumber);
            if (accountNumber is not null)
                return accountNumber;

            // No account number on file — the mobile number is the only payout
            // reference left, so use it rather than leave the row unusable.
            var fallbackMobile = FindesBankDirectory.TryNormalizePhMobile(r.GCashOrMobileNumber, out var fallback11)
                ? fallback11
                : granteeMobile;
            return fallbackMobile is null
                ? null
                : FindesBankDirectory.BuildMobileCreditorAcctNum(fallbackMobile);
        }

        // ContactNumber is every stored phone joined with ", " (see
        // GetGranteeExportRowsAsync) — the first one is the grantee's own.
        private static string? PrimaryPhone(string? contactNumber)
        {
            if (string.IsNullOrWhiteSpace(contactNumber))
                return null;

            var first = contactNumber.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(first) ? null : first.Trim();
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