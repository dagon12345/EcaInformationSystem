using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class NcscTeamDirectoryService : INcscTeamDirectoryService
    {
        private readonly INcscTeamDirectoryRepository _repo;

        public NcscTeamDirectoryService(INcscTeamDirectoryRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<NcscTeamDirectoryEntryDto>> GetAllAsync() => await _repo.GetAllAsync();

        public async Task<NcscTeamDirectoryEntryDto?> GetByIdAsync(Guid id) => await _repo.GetByIdAsync(id);

        public async Task<List<NcscTeamDirectoryHistoryDto>> GetHistoryAsync(Guid id) => await _repo.GetHistoryAsync(id);

        public async Task<NcscTeamDirectoryEntryDto> CreateAsync(UpsertNcscTeamDirectoryEntryDto dto, string userName)
        {
            if (dto.PsgcCodeRegion <= 0)
                throw new Exception("Region is required.");

            if (string.IsNullOrWhiteSpace(dto.FullName))
                throw new Exception("Full Name is required.");

            var entry = new NcscTeamDirectoryEntry
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };
            ApplyFields(entry, dto);

            await _repo.AddAsync(entry);
            await _repo.AddLogAsync(entry.Id, $"Added NCSC Team Directory entry for {EntryLabel(entry)}", userName);
            await _repo.SaveChangesAsync();

            var result = await _repo.GetByIdAsync(entry.Id);
            return result!;
        }

        public async Task<NcscTeamDirectoryEntryDto> UpdateAsync(UpsertNcscTeamDirectoryEntryDto dto, string userName)
        {
            if (dto.Id is null || dto.Id == Guid.Empty)
                throw new Exception("Entry Id is required for an update.");

            if (dto.PsgcCodeRegion <= 0)
                throw new Exception("Region is required.");

            if (string.IsNullOrWhiteSpace(dto.FullName))
                throw new Exception("Full Name is required.");

            var entity = await _repo.GetEntityByIdAsync(dto.Id.Value)
                ?? throw new Exception("Directory entry not found.");

            if (dto.RowVersion != null)
                _repo.SetOriginalRowVersion(entity, dto.RowVersion);

            var changes = BuildChangeSummary(entity, dto);

            ApplyFields(entity, dto);
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userName;

            if (changes.Count > 0)
            {
                var activity = $"Updated NCSC Team Directory entry for {EntryLabel(entity)} — " +
                                string.Join("; ", changes);
                await _repo.AddLogAsync(entity.Id, activity, userName);
            }

            await _repo.SaveChangesAsync();

            var result = await _repo.GetByIdAsync(entity.Id);
            return result!;
        }

        public async Task DeleteAsync(Guid id, string userName)
        {
            var entity = await _repo.GetEntityByIdAsync(id)
                ?? throw new Exception("Directory entry not found.");

            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userName;

            await _repo.AddLogAsync(entity.Id, $"Deleted NCSC Team Directory entry for {EntryLabel(entity)}", userName);
            await _repo.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        // Excel import — flat template (unlike the source Google Sheet, which
        // pivots each region across 4 rows × ~13 position columns). Admin
        // fills in this normalized one-row-per-person template instead.
        // ══════════════════════════════════════════════════════════════════

        private static readonly string[] ImportHeaders =
        {
            "Region", "Position", "Full Name", "Nickname", "Email Address", "Mobile Number"
        };

        public Task<List<string>> GetExcelSheetNamesAsync(Stream fileStream)
        {
            fileStream.Position = 0;
            using var workbook = new XLWorkbook(fileStream);
            return Task.FromResult(workbook.Worksheets.Select(ws => ws.Name).ToList());
        }

        public byte[] GenerateImportTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("NCSC Team Directory");

            for (int i = 0; i < ImportHeaders.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = ImportHeaders[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            ws.SheetView.FreezeRows(1);
            ws.Range(1, 1, 1, ImportHeaders.Length).SetAutoFilter();
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<NcscTeamDirectoryPreviewResultDto> PreviewImportAsync(Stream fileStream, string sheetName)
        {
            var rows = await ParseImportRowsAsync(fileStream, sheetName);
            var result = new NcscTeamDirectoryPreviewResultDto { TotalRows = rows.Count };

            foreach (var row in rows.Where(r => r.Dto is null))
                result.HardErrors.AddRange(row.Errors);

            result.CleanRows = rows.Count(r => r.Dto != null);
            return result;
        }

        public async Task<NcscTeamDirectoryImportResultDto> ConfirmImportAsync(Stream fileStream, string sheetName, string userName)
        {
            var rows = await ParseImportRowsAsync(fileStream, sheetName);
            var result = new NcscTeamDirectoryImportResultDto { TotalRows = rows.Count };

            foreach (var row in rows)
            {
                if (row.Dto is null)
                {
                    result.Errors.AddRange(row.Errors);
                    result.ErrorCount++;
                    continue;
                }

                result.ValidRows++;

                var entry = new NcscTeamDirectoryEntry
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName
                };
                ApplyFields(entry, row.Dto);

                await _repo.AddAsync(entry);
                await _repo.AddLogAsync(entry.Id, $"Imported via Excel — {EntryLabel(entry)}", userName);

                result.ImportedIds.Add(entry.Id);
                result.ImportedCount++;
            }

            await _repo.SaveChangesAsync();

            result.HasErrors = result.Errors.Any();
            result.IsSuccess = result.ImportedCount > 0;
            return result;
        }

        private class ParsedImportRow
        {
            public int RowNumber { get; init; }
            public UpsertNcscTeamDirectoryEntryDto? Dto { get; set; }
            public List<NcscTeamDirectoryImportErrorDto> Errors { get; } = new();
        }

        // Shared row-parsing used by both Preview and Confirm — keeps the two
        // in lockstep so a row that previews clean always confirms clean too.
        // Auto-detects which of two shapes the uploaded sheet is in: the flat
        // one-row-per-person template this app generates, or the raw pivoted
        // "Directory of NCSC ECA Team" Google Sheet export (downloaded as-is,
        // no reshaping) — see ParsePivotedRosterRowsAsync below.
        private async Task<List<ParsedImportRow>> ParseImportRowsAsync(Stream fileStream, string sheetName)
        {
            fileStream.Position = 0;
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == sheetName)
                ?? throw new Exception($"Worksheet not found: '{sheetName}'.");

            // The flat template's headers live in row 1. The pivoted roster's
            // row 1 is blank (its title is row 2, its real headers are row 3),
            // so this map comes back empty for that shape — a clean, cheap
            // way to tell the two apart without scanning ahead.
            var flatHeaderMap = BuildHeaderMap(worksheet);
            if (flatHeaderMap.ContainsKey("Region") && flatHeaderMap.ContainsKey("Full Name"))
                return await ParseFlatRowsAsync(worksheet, flatHeaderMap);

            return await ParsePivotedRosterRowsAsync(worksheet);
        }

        private async Task<List<ParsedImportRow>> ParseFlatRowsAsync(IXLWorksheet worksheet, Dictionary<string, int> headerMap)
        {
            var results = new List<ParsedImportRow>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            var lastCol = headerMap.Values.DefaultIfEmpty(1).Max();

            for (int rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                if (row.Cells(1, lastCol).All(c => string.IsNullOrWhiteSpace(c.GetFormattedString())))
                    continue;

                var parsed = new ParsedImportRow { RowNumber = rowNumber };

                var regionName = GetCellValue(worksheet, rowNumber, headerMap, "Region");
                var fullName = GetCellValue(worksheet, rowNumber, headerMap, "Full Name");

                if (string.IsNullOrWhiteSpace(regionName))
                    parsed.Errors.Add(new NcscTeamDirectoryImportErrorDto { RowNumber = rowNumber, Field = "Region", Message = "Region is required." });

                if (string.IsNullOrWhiteSpace(fullName))
                    parsed.Errors.Add(new NcscTeamDirectoryImportErrorDto { RowNumber = rowNumber, Field = "Full Name", Message = "Full Name is required." });

                int? regionCode = null;
                if (!string.IsNullOrWhiteSpace(regionName))
                {
                    regionCode = await _repo.GetRegionCodeByNameAsync(regionName);
                    if (regionCode is null)
                        parsed.Errors.Add(new NcscTeamDirectoryImportErrorDto { RowNumber = rowNumber, Field = "Region", Message = "Region not recognized — use the exact name shown in the app's Region filter (e.g. \"Caraga\", \"NCR\", \"CAR\").", RawValue = regionName });
                }

                if (parsed.Errors.Any())
                {
                    results.Add(parsed);
                    continue;
                }

                parsed.Dto = new UpsertNcscTeamDirectoryEntryDto
                {
                    PsgcCodeRegion = regionCode!.Value,
                    Position = GetCellValue(worksheet, rowNumber, headerMap, "Position"),
                    FullName = fullName,
                    Nickname = GetCellValue(worksheet, rowNumber, headerMap, "Nickname"),
                    Email = GetCellValue(worksheet, rowNumber, headerMap, "Email Address"),
                    MobileNumber = GetCellValue(worksheet, rowNumber, headerMap, "Mobile Number")
                };

                results.Add(parsed);
            }

            return results;
        }

        // Maps the sheet's short region labels (column A, one per region
        // block) to this app's actual Region.Name values — the roster uses
        // PSA-style short codes ("I", "IV-A", "CARAGA") while this system's
        // Region table uses descriptive names ("Ilocos Region", "CALABARZON",
        // "Caraga"), so a plain exact-match lookup would never hit.
        private static readonly Dictionary<string, string> PivotedRosterRegionAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["NCR"] = "NCR",
            ["CAR"] = "CAR",
            ["I"] = "Ilocos Region",
            ["II"] = "Cagayan Valley",
            ["III"] = "Central Luzon",
            ["IV-A"] = "CALABARZON",
            ["IV-B"] = "MIMAROPA Region",
            ["V"] = "Bicol Region",
            ["VI"] = "Western Visayas",
            ["VII"] = "Central Visayas",
            ["VIII"] = "Eastern Visayas",
            ["IX"] = "Zamboanga Peninsula",
            ["X"] = "Northern Mindanao",
            ["XI"] = "Davao Region",
            ["XII"] = "SOCCSKSARGEN",
            ["CARAGA"] = "Caraga",
            ["BARMM"] = "BARMM"
        };

        // Parses the "Directory of NCSC ECA Team" roster in its native,
        // downloaded-as-is shape: a header row (column A literally "Regions",
        // columns B onward each a position title), then one block per region —
        // a label row (column A = the region's short code), immediately
        // followed by a "Full Name" row, a "Nick Name" row, an email row, and
        // an "Active mobile number" row, each column holding one person's data
        // under whichever position that column represents.
        //
        // Anchored on the "Full Name" row rather than fixed row numbers —
        // column A reliably says exactly "Full Name" at the start of every
        // block (confirmed against the real exported file), so this survives
        // rows being inserted/removed elsewhere without needing hardcoded
        // offsets for where each region "starts". The row directly below
        // "Full Name" is always "Nick Name", two below is the email row (its
        // own column-A label is inconsistent — sometimes literal text, some-
        // times a stray email value — so it's ignored and only B onward is
        // read), three below is the mobile row.
        //
        // Known gap: one region (CALABARZON/IV-A in the current export) has a
        // 6th person's data tacked on as extra freestanding rows below its
        // normal 5-row block, with no "Full Name" label of its own — those
        // aren't picked up by this anchor and would need adding manually.
        private async Task<List<ParsedImportRow>> ParsePivotedRosterRowsAsync(IXLWorksheet worksheet)
        {
            var results = new List<ParsedImportRow>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

            int headerRow = -1;
            for (int r = 1; r <= Math.Min(6, lastRow); r++)
            {
                if (string.Equals(worksheet.Cell(r, 1).GetFormattedString().Trim(), "Regions", StringComparison.OrdinalIgnoreCase))
                {
                    headerRow = r;
                    break;
                }
            }

            if (headerRow < 0)
                throw new Exception("Could not recognize this sheet's layout — expected either the flat import template (Region/Position/Full Name/... headers in row 1) or the NCSC Team Directory roster export (a \"Regions\" label in column A).");

            var lastCol = Math.Min(worksheet.Row(headerRow).LastCellUsed()?.Address.ColumnNumber ?? 14, 14);

            var positions = new Dictionary<int, string>();
            for (int c = 2; c <= lastCol; c++)
            {
                var title = Regex.Replace(worksheet.Cell(headerRow, c).GetFormattedString().Trim(), @"\s+", " ");
                if (!string.IsNullOrWhiteSpace(title))
                    positions[c] = title;
            }

            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                if (!string.Equals(worksheet.Cell(r, 1).GetFormattedString().Trim(), "Full Name", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fullNameRow = r;
                var nicknameRow = r + 1;
                var emailRow = r + 2;
                var mobileRow = r + 3;
                var regionRaw = worksheet.Cell(r - 1, 1).GetFormattedString().Trim();

                if (string.IsNullOrWhiteSpace(regionRaw) || !PivotedRosterRegionAliases.TryGetValue(regionRaw, out var mappedRegionName))
                {
                    var err = new ParsedImportRow { RowNumber = fullNameRow };
                    err.Errors.Add(new NcscTeamDirectoryImportErrorDto { RowNumber = fullNameRow, Field = "Region", Message = $"Unrecognized region code '{regionRaw}' above this block — skipped.", RawValue = regionRaw });
                    results.Add(err);
                    continue;
                }

                var regionCode = await _repo.GetRegionCodeByNameAsync(mappedRegionName);
                if (regionCode is null)
                {
                    var err = new ParsedImportRow { RowNumber = fullNameRow };
                    err.Errors.Add(new NcscTeamDirectoryImportErrorDto { RowNumber = fullNameRow, Field = "Region", Message = $"Region '{mappedRegionName}' not found in the system — skipped.", RawValue = regionRaw });
                    results.Add(err);
                    continue;
                }

                foreach (var (col, position) in positions)
                {
                    var name = worksheet.Cell(fullNameRow, col).GetFormattedString().Trim();
                    if (string.IsNullOrWhiteSpace(name) || name.Equals("-", StringComparison.Ordinal) || name.Contains("vacant", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var nickname = worksheet.Cell(nicknameRow, col).GetFormattedString().Trim();
                    var email = worksheet.Cell(emailRow, col).GetFormattedString().Trim();
                    var mobile = CleanPhoneNumber(worksheet.Cell(mobileRow, col).GetFormattedString().Trim());

                    results.Add(new ParsedImportRow
                    {
                        RowNumber = fullNameRow,
                        Dto = new UpsertNcscTeamDirectoryEntryDto
                        {
                            PsgcCodeRegion = regionCode.Value,
                            Position = position,
                            FullName = name,
                            Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname,
                            Email = string.IsNullOrWhiteSpace(email) ? null : email,
                            MobileNumber = string.IsNullOrWhiteSpace(mobile) ? null : mobile
                        }
                    });
                }
            }

            return results;
        }

        // The source sheet has some mobile numbers stored as actual Excel
        // numbers rather than text, which strips the leading 0 and can render
        // in scientific notation (e.g. "9.084240687E9" for "09084240687") —
        // a pre-existing data-quality issue in the spreadsheet itself, not
        // something this parser can fully undo, but this recovers the common
        // case. Also strips a stray leading apostrophe some cells carry
        // (Excel's own "force text" marker leaking into the stored value).
        private static string CleanPhoneNumber(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            var v = raw.Trim().TrimStart('\'');

            if (Regex.IsMatch(v, @"^\d+(\.\d+)?E\+?\d+$", RegexOptions.IgnoreCase) &&
                double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            {
                var digits = ((long)num).ToString(CultureInfo.InvariantCulture);
                if (digits.Length == 10) digits = "0" + digits;
                v = digits;
            }

            return v;
        }

        private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet worksheet)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = worksheet.Row(1);
            var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

            for (int c = 1; c <= lastCol; c++)
            {
                var header = headerRow.Cell(c).GetFormattedString().Trim();
                if (!string.IsNullOrWhiteSpace(header) && !map.ContainsKey(header))
                    map[header] = c;
            }

            return map;
        }

        private static string? GetCellValue(IXLWorksheet worksheet, int rowNumber, Dictionary<string, int> headerMap, string header)
        {
            if (!headerMap.TryGetValue(header, out var col)) return null;
            var value = worksheet.Row(rowNumber).Cell(col).GetFormattedString().Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string EntryLabel(NcscTeamDirectoryEntry entity) =>
            string.IsNullOrWhiteSpace(entity.FullName) ? "an unnamed entry" : entity.FullName;

        private static void ApplyFields(NcscTeamDirectoryEntry entity, UpsertNcscTeamDirectoryEntryDto dto)
        {
            entity.PsgcCodeRegion = dto.PsgcCodeRegion;
            entity.Position = dto.Position;
            entity.FullName = dto.FullName;
            entity.Nickname = dto.Nickname;
            entity.Email = dto.Email;
            entity.MobileNumber = dto.MobileNumber;
        }

        // ── Field-level diff for the audit log ──────────────────────────────
        private static List<string> BuildChangeSummary(NcscTeamDirectoryEntry before, UpsertNcscTeamDirectoryEntryDto after)
        {
            var changes = new List<string>();

            void Diff(string label, object? oldVal, object? newVal)
            {
                var oldStr = FormatValue(oldVal);
                var newStr = FormatValue(newVal);
                if (oldStr != newStr)
                    changes.Add($"{label} '{oldStr}' → '{newStr}'");
            }

            Diff("Position", before.Position, after.Position);
            Diff("Full Name", before.FullName, after.FullName);
            Diff("Nickname", before.Nickname, after.Nickname);
            Diff("Email", before.Email, after.Email);
            Diff("Mobile Number", before.MobileNumber, after.MobileNumber);

            if (before.PsgcCodeRegion != after.PsgcCodeRegion)
                changes.Add("Region");

            return changes;
        }

        private static string FormatValue(object? value) => value switch
        {
            null => "—",
            string s when string.IsNullOrWhiteSpace(s) => "—",
            _ => value.ToString() ?? "—"
        };
    }
}
