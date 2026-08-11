using System.Text.RegularExpressions;
using ClosedXML.Excel;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class SeniorCitizenDirectoryService : ISeniorCitizenDirectoryService
    {
        private readonly ISeniorCitizenDirectoryRepository _repo;
        private readonly IPsgcNameCache _psgcNameCache;

        public SeniorCitizenDirectoryService(ISeniorCitizenDirectoryRepository repo, IPsgcNameCache psgcNameCache)
        {
            _repo = repo;
            _psgcNameCache = psgcNameCache;
        }

        public async Task<List<SeniorCitizenDirectoryListItemDto>> GetAllAsync(int regionCode) =>
            await _repo.GetAllAsync(regionCode);

        public async Task<SeniorCitizenDirectoryDto?> GetByIdAsync(Guid id, int regionCode)
        {
            var result = await _repo.GetByIdAsync(id);
            // Treat a cross-region entry as "not found" rather than leaking its
            // existence/detail to a user outside that region.
            return result != null && result.PsgcCodeRegion == regionCode ? result : null;
        }

        public async Task<List<SeniorCitizenDirectoryHistoryDto>> GetHistoryAsync(Guid id, int regionCode)
        {
            var entity = await _repo.GetEntityByIdAsync(id);
            if (entity == null || entity.PsgcCodeRegion != regionCode) return new();
            return await _repo.GetHistoryAsync(id);
        }

        public async Task<SeniorCitizenDirectoryDto> CreateAsync(UpsertSeniorCitizenDirectoryDto dto, string userName, int regionCode)
        {
            if (dto.PsgcCodeRegion <= 0 || dto.PsgcCodeProvince <= 0 || dto.PsgcCodeMunicipality <= 0)
                throw new Exception("Region, Province, and Municipality are required.");

            if (dto.PsgcCodeRegion != regionCode)
                throw new Exception("You can only add directory entries for your own region.");

            var existing = await _repo.FindActiveByMunicipalityAsync(dto.PsgcCodeMunicipality);
            if (existing != null)
                throw new Exception("A directory entry for this municipality/city already exists. Edit that entry instead of creating a new one.");

            var entry = new SeniorCitizenDirectoryEntry
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };
            ApplyFields(entry, dto);

            await _repo.AddAsync(entry);
            await _repo.AddLogAsync(entry.Id,
                $"Added Senior Citizens Directory entry for {MunicipalityLabel(entry)}", userName);
            await _repo.SaveChangesAsync();

            var result = await _repo.GetByIdAsync(entry.Id);
            return result!;
        }

        public async Task<SeniorCitizenDirectoryDto> UpdateAsync(UpsertSeniorCitizenDirectoryDto dto, string userName, int regionCode)
        {
            if (dto.Id is null || dto.Id == Guid.Empty)
                throw new Exception("Entry Id is required for an update.");

            if (dto.PsgcCodeRegion <= 0 || dto.PsgcCodeProvince <= 0 || dto.PsgcCodeMunicipality <= 0)
                throw new Exception("Region, Province, and Municipality are required.");

            if (dto.PsgcCodeRegion != regionCode)
                throw new Exception("You can only edit directory entries for your own region.");

            var entity = await _repo.GetEntityByIdAsync(dto.Id.Value)
                ?? throw new Exception("Directory entry not found.");

            if (entity.PsgcCodeRegion != regionCode)
                throw new Exception("You can only edit directory entries for your own region.");

            var duplicate = await _repo.FindActiveByMunicipalityAsync(dto.PsgcCodeMunicipality, dto.Id);
            if (duplicate != null)
                throw new Exception("Another directory entry already exists for this municipality/city.");

            if (dto.RowVersion != null)
                _repo.SetOriginalRowVersion(entity, dto.RowVersion);

            var changes = BuildChangeSummary(entity, dto);

            ApplyFields(entity, dto);
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userName;

            if (changes.Count > 0)
            {
                var activity = $"Updated Senior Citizens Directory entry for {MunicipalityLabel(entity)} — " +
                                string.Join("; ", changes);
                await _repo.AddLogAsync(entity.Id, activity, userName);
            }

            await _repo.SaveChangesAsync();

            var result = await _repo.GetByIdAsync(entity.Id);
            return result!;
        }

        public async Task DeleteAsync(Guid id, string userName, int regionCode)
        {
            var entity = await _repo.GetEntityByIdAsync(id)
                ?? throw new Exception("Directory entry not found.");

            if (entity.PsgcCodeRegion != regionCode)
                throw new Exception("You can only delete directory entries for your own region.");

            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userName;

            await _repo.AddLogAsync(entity.Id,
                $"Deleted Senior Citizens Directory entry for {MunicipalityLabel(entity)}", userName);
            await _repo.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        // Excel import — header-matched columns (row 1 = headers, any order).
        // Header names/order mirror the CARAGA Senior Citizens Directory
        // Google Form response export exactly, MINUS the form's "Timestamp"
        // and "Please select one:" (data-privacy agreement) columns, so a
        // PDO/Admin can upload that export directly without renaming
        // anything. "Email Address" and "Region" are accepted but ignored:
        // the entry's CreatedAt is always today, and Region is locked to the
        // importing user's own account region rather than trusted from the
        // sheet.
        // ══════════════════════════════════════════════════════════════════

        private static readonly string[] ImportHeaders =
        {
            "Email Address", "Region", "Province", "City / Municipality",
            "Income Classification", "Updated / Latest Senior Citizens Population",
            "Name of Local Social Welfare and Development Officer (LSWDO)",
            "Position / Designation of Local Social Welfare and Development Officer (LSWDO)",
            "Official contact number(s) of Local Social Welfare and Development Officer (LSWDO)",
            "Official Email Address of Local Social Welfare and Development Officer (LSWDO)",
            "Name of SC Focal", "Official contact number(s) of SC Focal", "Official Email Address of SC Focal",
            "Name of OSCA Head", "Length of Service as OSCA Head", "Official contact number(s) of OSCA Head", "Official Email Address of OSCA Head",
            "Name of FSCAP President", "Official contact number(s) of FSCAP President", "Official Email Address of FSCAP President", "Length of Service as FSCAP President",
            "With Senior Citizen Center?", "Is the SCC duly accredited?",
            "If Yes, kindly indicate the validity date of the SCC accreditation.",
            "If without existing SCC, does the LGU have available resources (manpower, programs, etc.)",
            "Who manages the Senior Citizen Center?", "Services Offered",
            "Does the LGU provide its own Cash Incentive to Senior Citizens?",
            "If Yes, kindly indicate the amount, frequency, and if it is applicable to all Senior Citizens.",
            "Is there a local ordinance or other policy that enables and supports the provision of LGU cash incentives?",
            "If Yes, kindly upload a copy of the Ordinance or other policy.",
            "Does the LGU have a VAOP (Violence Against Older Persons) Help Desk?",
            "If No VAOP Help Desk, is there a reporting or referral mechanism in the LGU to monitor or address violence against older persons? Kindly briefly describe the process.",
            "Name of City / Municipal Mayor", "Official Email Address of the Office of the City/Municipal Mayor"
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
            var ws = workbook.Worksheets.Add("Senior Citizen Directory");

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

        public async Task<SeniorCitizenDirectoryPreviewResultDto> PreviewImportAsync(Stream fileStream, string sheetName, int regionCode)
        {
            var rows = await ParseImportRowsAsync(fileStream, sheetName, regionCode);
            var result = new SeniorCitizenDirectoryPreviewResultDto { TotalRows = rows.Count };

            foreach (var row in rows)
            {
                if (row.Dto is null)
                {
                    result.HardErrors.AddRange(row.Errors);
                    continue;
                }

                var existing = await _repo.FindActiveByMunicipalityAsync(row.Dto.PsgcCodeMunicipality);
                if (existing != null)
                {
                    result.SoftDuplicates.Add(new SeniorCitizenDirectorySoftDuplicateDto
                    {
                        RowNumber = row.RowNumber,
                        ProvinceName = _psgcNameCache.GetProvinceName(row.Dto.PsgcCodeProvince) ?? string.Empty,
                        MunicipalityName = _psgcNameCache.GetMunicipalityName(row.Dto.PsgcCodeMunicipality) ?? string.Empty,
                        ExistingLswdoName = existing.LswdoName,
                        ExistingOscaHeadName = existing.OscaHeadName,
                        ExistingMayorName = existing.MayorName,
                        ExistingUpdatedAt = existing.UpdatedAt ?? existing.CreatedAt,
                        // Exact municipality-key match — not a fuzzy score like Beneficiary's
                        // name/birthdate matching, but kept as a field so the client can reuse
                        // the same score-badge review UI.
                        MatchScore = 1.0
                    });
                }
            }

            result.CleanRows = rows.Count(r => r.Dto != null);
            return result;
        }

        public async Task<SeniorCitizenDirectoryImportResultDto> ConfirmImportAsync(
            Stream fileStream, string sheetName, int regionCode, string userName, HashSet<int> skipRows)
        {
            var rows = await ParseImportRowsAsync(fileStream, sheetName, regionCode);
            var result = new SeniorCitizenDirectoryImportResultDto { TotalRows = rows.Count };

            foreach (var row in rows)
            {
                if (row.Dto is null)
                {
                    result.Errors.AddRange(row.Errors);
                    result.ErrorCount++;
                    continue;
                }

                result.ValidRows++;

                if (skipRows.Contains(row.RowNumber))
                {
                    result.SkippedDuplicateCount++;
                    continue;
                }

                var entry = new SeniorCitizenDirectoryEntry
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName
                };
                ApplyFields(entry, row.Dto!);

                await _repo.AddAsync(entry);
                await _repo.AddLogAsync(entry.Id, $"Imported via Excel — {MunicipalityLabel(entry)}", userName);

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
            public UpsertSeniorCitizenDirectoryDto? Dto { get; set; }
            public List<SeniorCitizenDirectoryImportErrorDto> Errors { get; } = new();
        }

        // Shared row-parsing used by both Preview and Confirm — keeps the two
        // in lockstep so a row that previews clean always confirms clean too.
        private async Task<List<ParsedImportRow>> ParseImportRowsAsync(Stream fileStream, string sheetName, int regionCode)
        {
            fileStream.Position = 0;
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == sheetName)
                ?? throw new Exception($"Worksheet not found: '{sheetName}'.");

            var headerMap = BuildHeaderMap(worksheet);
            var missingRequired = new[] { "Province", "City / Municipality" }.Where(h => !headerMap.ContainsKey(h)).ToList();
            if (missingRequired.Any())
                throw new Exception($"The uploaded sheet is missing required column(s): {string.Join(", ", missingRequired)}.");

            var results = new List<ParsedImportRow>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            var lastCol = headerMap.Values.DefaultIfEmpty(1).Max();

            for (int rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                if (row.Cells(1, lastCol).All(c => string.IsNullOrWhiteSpace(c.GetFormattedString())))
                    continue;

                var parsed = new ParsedImportRow { RowNumber = rowNumber };

                var provinceName = GetCellValue(worksheet, rowNumber, headerMap, "Province");
                var municipalityName = GetCellValue(worksheet, rowNumber, headerMap, "City / Municipality");

                if (string.IsNullOrWhiteSpace(provinceName))
                    parsed.Errors.Add(new SeniorCitizenDirectoryImportErrorDto { RowNumber = rowNumber, Field = "Province", Message = "Province is required." });

                if (string.IsNullOrWhiteSpace(municipalityName))
                    parsed.Errors.Add(new SeniorCitizenDirectoryImportErrorDto { RowNumber = rowNumber, Field = "Municipality", Message = "Municipality is required." });

                int? provinceCode = null, municipalityCode = null;

                if (!string.IsNullOrWhiteSpace(provinceName))
                {
                    provinceCode = await _repo.GetProvinceCodeByNameAsync(provinceName, regionCode);
                    if (provinceCode is null)
                        parsed.Errors.Add(new SeniorCitizenDirectoryImportErrorDto { RowNumber = rowNumber, Field = "Province", Message = "Province not found in your region.", RawValue = provinceName });
                }

                if (provinceCode.HasValue && !string.IsNullOrWhiteSpace(municipalityName))
                {
                    foreach (var candidate in BuildMunicipalityNameCandidates(municipalityName))
                    {
                        municipalityCode = await _repo.GetMunicipalityCodeByNameAsync(candidate, provinceCode.Value);
                        if (municipalityCode.HasValue) break;
                    }

                    if (municipalityCode is null)
                        parsed.Errors.Add(new SeniorCitizenDirectoryImportErrorDto { RowNumber = rowNumber, Field = "Municipality", Message = "Municipality not found in that province.", RawValue = municipalityName });
                }

                if (parsed.Errors.Any())
                {
                    results.Add(parsed);
                    continue;
                }

                // The form crams the population figure and free-text context into one
                // cell (e.g. "35,623 (as of December 31, 2024)") — pull the leading
                // number out for the numeric field, keep the full raw cell as the note.
                var populationRaw = GetCellValue(worksheet, rowNumber, headerMap, "Updated / Latest Senior Citizens Population");
                int? population = null;
                if (!string.IsNullOrWhiteSpace(populationRaw))
                {
                    var digitsMatch = Regex.Match(populationRaw, @"[\d][\d,]*");
                    if (digitsMatch.Success && int.TryParse(digitsMatch.Value.Replace(",", ""), out var pop))
                        population = pop;
                }

                parsed.Dto = new UpsertSeniorCitizenDirectoryDto
                {
                    PsgcCodeRegion = regionCode,
                    PsgcCodeProvince = provinceCode!.Value,
                    PsgcCodeMunicipality = municipalityCode!.Value,
                    IncomeClassification = GetCellValue(worksheet, rowNumber, headerMap, "Income Classification"),
                    SeniorCitizensPopulation = population,
                    PopulationAsOfNote = populationRaw,
                    LswdoName = GetCellValue(worksheet, rowNumber, headerMap, "Name of Local Social Welfare and Development Officer (LSWDO)"),
                    LswdoPosition = GetCellValue(worksheet, rowNumber, headerMap, "Position / Designation of Local Social Welfare and Development Officer (LSWDO)"),
                    LswdoContactNumber = GetCellValue(worksheet, rowNumber, headerMap, "Official contact number(s) of Local Social Welfare and Development Officer (LSWDO)"),
                    LswdoEmail = GetCellValue(worksheet, rowNumber, headerMap, "Official Email Address of Local Social Welfare and Development Officer (LSWDO)"),
                    ScFocalName = GetCellValue(worksheet, rowNumber, headerMap, "Name of SC Focal"),
                    ScFocalContactNumber = GetCellValue(worksheet, rowNumber, headerMap, "Official contact number(s) of SC Focal"),
                    ScFocalEmail = GetCellValue(worksheet, rowNumber, headerMap, "Official Email Address of SC Focal"),
                    OscaHeadName = GetCellValue(worksheet, rowNumber, headerMap, "Name of OSCA Head"),
                    OscaHeadLengthOfService = GetCellValue(worksheet, rowNumber, headerMap, "Length of Service as OSCA Head"),
                    OscaHeadContactNumber = GetCellValue(worksheet, rowNumber, headerMap, "Official contact number(s) of OSCA Head"),
                    OscaHeadEmail = GetCellValue(worksheet, rowNumber, headerMap, "Official Email Address of OSCA Head"),
                    FscapPresidentName = GetCellValue(worksheet, rowNumber, headerMap, "Name of FSCAP President"),
                    FscapPresidentLengthOfService = GetCellValue(worksheet, rowNumber, headerMap, "Length of Service as FSCAP President"),
                    FscapPresidentContactNumber = GetCellValue(worksheet, rowNumber, headerMap, "Official contact number(s) of FSCAP President"),
                    FscapPresidentEmail = GetCellValue(worksheet, rowNumber, headerMap, "Official Email Address of FSCAP President"),
                    HasSeniorCitizenCenter = ParseTriState(GetCellValue(worksheet, rowNumber, headerMap, "With Senior Citizen Center?")),
                    IsSccAccredited = ParseTriState(GetCellValue(worksheet, rowNumber, headerMap, "Is the SCC duly accredited?")),
                    SccAccreditationValidity = GetCellValue(worksheet, rowNumber, headerMap, "If Yes, kindly indicate the validity date of the SCC accreditation."),
                    WithoutSccResourcesNote = GetCellValue(worksheet, rowNumber, headerMap, "If without existing SCC, does the LGU have available resources (manpower, programs, etc.)"),
                    SccManagedBy = GetCellValue(worksheet, rowNumber, headerMap, "Who manages the Senior Citizen Center?"),
                    ServicesOffered = GetCellValue(worksheet, rowNumber, headerMap, "Services Offered"),
                    HasCashIncentive = ParseTriState(GetCellValue(worksheet, rowNumber, headerMap, "Does the LGU provide its own Cash Incentive to Senior Citizens?")),
                    CashIncentiveDetails = GetCellValue(worksheet, rowNumber, headerMap, "If Yes, kindly indicate the amount, frequency, and if it is applicable to all Senior Citizens."),
                    HasSupportingOrdinance = ParseTriState(GetCellValue(worksheet, rowNumber, headerMap, "Is there a local ordinance or other policy that enables and supports the provision of LGU cash incentives?")),
                    OrdinanceDocumentLinks = GetCellValue(worksheet, rowNumber, headerMap, "If Yes, kindly upload a copy of the Ordinance or other policy."),
                    HasVaopHelpDesk = ParseTriState(GetCellValue(worksheet, rowNumber, headerMap, "Does the LGU have a VAOP (Violence Against Older Persons) Help Desk?")),
                    VaopReferralMechanism = GetCellValue(worksheet, rowNumber, headerMap, "If No VAOP Help Desk, is there a reporting or referral mechanism in the LGU to monitor or address violence against older persons? Kindly briefly describe the process."),
                    MayorName = GetCellValue(worksheet, rowNumber, headerMap, "Name of City / Municipal Mayor"),
                    MayorOfficeEmail = GetCellValue(worksheet, rowNumber, headerMap, "Official Email Address of the Office of the City/Municipal Mayor")
                };

                results.Add(parsed);
            }

            return results;
        }

        // The form's municipality names don't always match our PSGC data verbatim —
        // component cities are typed as "Butuan City" where PSGC spells it "City of
        // Butuan", "Sta." is abbreviated where PSGC spells out "Santa", some rows
        // append a disambiguating "(2ND CLASS COMPONENT CITY)"/", Agusan del Sur"
        // suffix, and a lone city name like "SURIGAO" omits "City" entirely. Rather
        // than reject those, try the raw name first, then a handful of normalized
        // variants, and use whichever one is found first in that province.
        private static IEnumerable<string> BuildMunicipalityNameCandidates(string raw)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<string> { raw.Trim() };

            // Strip a parenthetical suffix, e.g. "Bislig City (2ND CLASS COMPONENT CITY)".
            var noParens = Regex.Replace(raw, @"\s*\([^)]*\)\s*", " ").Trim();
            if (noParens.Length > 0)
                candidates.Add(noParens);

            // Strip a trailing ", <qualifier>" some rows append to disambiguate
            // municipalities that share a name across provinces, e.g. "Rosario, Agusan del Sur".
            var commaIdx = noParens.IndexOf(',');
            if (commaIdx > 0)
                candidates.Add(noParens[..commaIdx].Trim());

            foreach (var c in candidates.ToList())
            {
                // "Sta." -> "Santa" — PSGC data spells it out in full.
                if (Regex.IsMatch(c, @"^Sta\.?\s", RegexOptions.IgnoreCase))
                    candidates.Add(Regex.Replace(c, @"^Sta\.?\s", "Santa ", RegexOptions.IgnoreCase));

                if (Regex.IsMatch(c, @"\sCity$", RegexOptions.IgnoreCase))
                {
                    // "<Name> City" -> "City of <Name>" — PSGC data uses the "City of X" form.
                    var baseName = Regex.Replace(c, @"\sCity$", "", RegexOptions.IgnoreCase).Trim();
                    candidates.Add($"City of {baseName}");
                }
                else if (!Regex.IsMatch(c, @"^City of\s", RegexOptions.IgnoreCase))
                {
                    // A bare city name with no "City" qualifier at all, e.g. "SURIGAO".
                    candidates.Add($"City of {c}");
                }
            }

            foreach (var c in candidates)
            {
                if (c.Length > 0 && seen.Add(c))
                    yield return c;
            }
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

        private static bool? ParseTriState(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var v = raw.Trim().ToLowerInvariant();
            if (v is "yes" or "y" or "true" or "1") return true;
            if (v is "no" or "n" or "false" or "0") return false;
            return null;
        }

        private string MunicipalityLabel(SeniorCitizenDirectoryEntry entity) =>
            _psgcNameCache.GetMunicipalityName(entity.PsgcCodeMunicipality) ?? $"municipality code {entity.PsgcCodeMunicipality}";

        private static void ApplyFields(SeniorCitizenDirectoryEntry entity, UpsertSeniorCitizenDirectoryDto dto)
        {
            entity.PsgcCodeRegion = dto.PsgcCodeRegion;
            entity.PsgcCodeProvince = dto.PsgcCodeProvince;
            entity.PsgcCodeMunicipality = dto.PsgcCodeMunicipality;

            entity.IncomeClassification = dto.IncomeClassification;
            entity.SeniorCitizensPopulation = dto.SeniorCitizensPopulation;
            entity.PopulationAsOfNote = dto.PopulationAsOfNote;

            entity.LswdoName = dto.LswdoName;
            entity.LswdoPosition = dto.LswdoPosition;
            entity.LswdoContactNumber = dto.LswdoContactNumber;
            entity.LswdoEmail = dto.LswdoEmail;

            entity.ScFocalName = dto.ScFocalName;
            entity.ScFocalContactNumber = dto.ScFocalContactNumber;
            entity.ScFocalEmail = dto.ScFocalEmail;

            entity.OscaHeadName = dto.OscaHeadName;
            entity.OscaHeadLengthOfService = dto.OscaHeadLengthOfService;
            entity.OscaHeadContactNumber = dto.OscaHeadContactNumber;
            entity.OscaHeadEmail = dto.OscaHeadEmail;

            entity.FscapPresidentName = dto.FscapPresidentName;
            entity.FscapPresidentContactNumber = dto.FscapPresidentContactNumber;
            entity.FscapPresidentEmail = dto.FscapPresidentEmail;
            entity.FscapPresidentLengthOfService = dto.FscapPresidentLengthOfService;

            entity.HasSeniorCitizenCenter = dto.HasSeniorCitizenCenter;
            entity.IsSccAccredited = dto.IsSccAccredited;
            entity.SccAccreditationValidity = dto.SccAccreditationValidity;
            entity.WithoutSccResourcesNote = dto.WithoutSccResourcesNote;
            entity.SccManagedBy = dto.SccManagedBy;
            entity.ServicesOffered = dto.ServicesOffered;

            entity.HasCashIncentive = dto.HasCashIncentive;
            entity.CashIncentiveDetails = dto.CashIncentiveDetails;
            entity.HasSupportingOrdinance = dto.HasSupportingOrdinance;
            entity.OrdinanceDocumentLinks = dto.OrdinanceDocumentLinks;

            entity.HasVaopHelpDesk = dto.HasVaopHelpDesk;
            entity.VaopReferralMechanism = dto.VaopReferralMechanism;

            entity.MayorName = dto.MayorName;
            entity.MayorOfficeEmail = dto.MayorOfficeEmail;
        }

        // ── Field-level diff for the audit log — labels chosen to read as a
        // sentence fragment after "Updated ... for <municipality> — ". ──────
        private static List<string> BuildChangeSummary(SeniorCitizenDirectoryEntry before, UpsertSeniorCitizenDirectoryDto after)
        {
            var changes = new List<string>();

            void Diff(string label, object? oldVal, object? newVal)
            {
                var oldStr = FormatValue(oldVal);
                var newStr = FormatValue(newVal);
                if (oldStr != newStr)
                    changes.Add($"{label} '{oldStr}' → '{newStr}'");
            }

            Diff("Income Classification", before.IncomeClassification, after.IncomeClassification);
            Diff("SC Population", before.SeniorCitizensPopulation, after.SeniorCitizensPopulation);
            Diff("Population As-Of Note", before.PopulationAsOfNote, after.PopulationAsOfNote);

            Diff("LSWDO Name", before.LswdoName, after.LswdoName);
            Diff("LSWDO Position", before.LswdoPosition, after.LswdoPosition);
            Diff("LSWDO Contact", before.LswdoContactNumber, after.LswdoContactNumber);
            Diff("LSWDO Email", before.LswdoEmail, after.LswdoEmail);

            Diff("SC Focal Name", before.ScFocalName, after.ScFocalName);
            Diff("SC Focal Contact", before.ScFocalContactNumber, after.ScFocalContactNumber);
            Diff("SC Focal Email", before.ScFocalEmail, after.ScFocalEmail);

            Diff("OSCA Head Name", before.OscaHeadName, after.OscaHeadName);
            Diff("OSCA Head Length of Service", before.OscaHeadLengthOfService, after.OscaHeadLengthOfService);
            Diff("OSCA Head Contact", before.OscaHeadContactNumber, after.OscaHeadContactNumber);
            Diff("OSCA Head Email", before.OscaHeadEmail, after.OscaHeadEmail);

            Diff("FSCAP President Name", before.FscapPresidentName, after.FscapPresidentName);
            Diff("FSCAP President Contact", before.FscapPresidentContactNumber, after.FscapPresidentContactNumber);
            Diff("FSCAP President Email", before.FscapPresidentEmail, after.FscapPresidentEmail);
            Diff("FSCAP President Length of Service", before.FscapPresidentLengthOfService, after.FscapPresidentLengthOfService);

            Diff("Has Senior Citizen Center", before.HasSeniorCitizenCenter, after.HasSeniorCitizenCenter);
            Diff("SCC Accredited", before.IsSccAccredited, after.IsSccAccredited);
            Diff("SCC Accreditation Validity", before.SccAccreditationValidity, after.SccAccreditationValidity);
            Diff("Without-SCC Resources Note", before.WithoutSccResourcesNote, after.WithoutSccResourcesNote);
            Diff("SCC Managed By", before.SccManagedBy, after.SccManagedBy);
            Diff("Services Offered", before.ServicesOffered, after.ServicesOffered);

            Diff("Has Cash Incentive", before.HasCashIncentive, after.HasCashIncentive);
            Diff("Cash Incentive Details", before.CashIncentiveDetails, after.CashIncentiveDetails);
            Diff("Has Supporting Ordinance", before.HasSupportingOrdinance, after.HasSupportingOrdinance);
            Diff("Ordinance Document Links", before.OrdinanceDocumentLinks, after.OrdinanceDocumentLinks);

            Diff("Has VAOP Help Desk", before.HasVaopHelpDesk, after.HasVaopHelpDesk);
            Diff("VAOP Referral Mechanism", before.VaopReferralMechanism, after.VaopReferralMechanism);

            Diff("Mayor Name", before.MayorName, after.MayorName);
            Diff("Mayor Office Email", before.MayorOfficeEmail, after.MayorOfficeEmail);

            if (before.PsgcCodeRegion != after.PsgcCodeRegion ||
                before.PsgcCodeProvince != after.PsgcCodeProvince ||
                before.PsgcCodeMunicipality != after.PsgcCodeMunicipality)
            {
                changes.Add("Location");
            }

            return changes;
        }

        private static string FormatValue(object? value) => value switch
        {
            null => "—",
            bool b => b ? "Yes" : "No",
            string s when string.IsNullOrWhiteSpace(s) => "—",
            _ => value.ToString() ?? "—"
        };
    }
}
