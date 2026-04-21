using ClosedXML.Excel;
using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
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
            var selectedBeneficiary = await _repo.GetByIdAsync(Id);
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

        public async Task UpdateAsync(Guid Id, BeneficiaryInformationDto dto, string userName)
        {
            var beneficiary = await _repo.GetByIdAsync(Id);
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


            var changes = GetChangedFields(beneficiary, dto);

            beneficiary.Update(dto.BatchCode, dto.OscaIdNumber, dto.OscaIdDateIssued, dto.NcscRrn, dto.LastName, dto.FirstName, dto.MiddleName, dto.Extension, dto.BirthDate, dto.PhoneNumber,
                dto.Sex, dto.IsIndigenousPeople, dto.IsPersonWithDisability, dto.CivilStatus, dto.Citizenship, DefaultRegionCode, dto.PsgcCodeProvince, dto.PsgcCodeMunicipality, dto.PsgcCodeBarangay,
                dto.IsCompliant, dto.Validator, dto.ValidationDate, dto.PaymentStatus, dto.ModeOfPayment, dto.PaymentDate,
                dto.IsDeceased, dto.DateOfDeath, dto.IsEligible, dto.RemarkCategory, dto.Remarks);

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

        public async Task<BeneficiaryImportResultDto> ImportExcelAsync(Stream fileStream, string fileName, string userName)
        {
            if (fileStream == null || !fileStream.CanRead)
                throw new Exception("Please upload a valid Excel file.");

            if (string.IsNullOrWhiteSpace(fileName))
                throw new Exception("Invalid file name.");

            var extension = Path.GetExtension(fileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Only .xlsx Excel files are allowed.");

            var result = new BeneficiaryImportResultDto();

            var regions = await _regionRepository.GetAllAsync();
            var provinces = await _provinceRepository.GetAllProvinceAsync();
            var municipalities = await _municipalityRepository.GetAllMunicipalityAsync();
            var barangays = await _barangayRepository.GetBarangaysAsync();

            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheet("Sheet1");

            if (worksheet == null)
                throw new Exception("Sheet1 not found.");

            const int headerRowNumber = 10;
            const int firstDataRowNumber = 11;

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < firstDataRowNumber)
                throw new Exception("Sheet1 does not contain data rows.");

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
                    var remarks = row.Cell(21).GetFormattedString().Trim();
                    if (string.IsNullOrWhiteSpace(firstName))
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {rowNumber}: First Name is required.");
                        continue;
                    }

                    var birthDateRaw = $"{birthMonthRaw} {birthDayRaw} {birthYearRaw}";
                    if (!TryParseExcelDate(birthDateRaw, out var birthDate))
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {rowNumber}: Invalid Birth Date from Month/Day/Year values '{birthDateRaw}'.");
                        continue;
                    }

                    int? ncscRrn = null;
                    if (!string.IsNullOrWhiteSpace(ncscRrnRaw))
                    {
                        if (int.TryParse(ncscRrnRaw, out var parsedRrn))
                            ncscRrn = parsedRrn;
                        else
                        {
                            result.ErrorCount++;
                            result.Errors.Add($"Row {rowNumber}: Invalid NCSC RRN '{ncscRrnRaw}'.");
                            continue;
                        }
                    }

                    var region = string.IsNullOrWhiteSpace(regionName)
                        ? regions.FirstOrDefault(x => x.PsgcCodeRegion == DefaultRegionCode)
                        : FindBestNameMatch(regions, x => x.Name, regionName);

                    if (region == null)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {rowNumber}: Region '{regionName}' not found.");
                        continue;
                    }

                    var province = FindBestNameMatch(provinces, x => x.Name, provinceName);
                    if (province == null)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {rowNumber}: Province '{provinceName}' not found.");
                        continue;
                    }

                    var municipality = FindBestNameMatch(municipalities, x => x.Name, municipalityName);
                    if (municipality == null)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {rowNumber}: Municipality/City '{municipalityName}' not found.");
                        continue;
                    }

                    var barangay = FindBestNameMatch(barangays, x => x.Name, barangayName);
                    if (barangay == null)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {rowNumber}: Barangay '{barangayName}' not found.");
                        continue;
                    }

                    var isDuplicate = await _repo.ExistsDuplicateAsync(
                        lastName,
                        firstName,
                        middleName,
                        birthDate,
                        oscaIdNumber,
                        ncscRrn);

                    if (isDuplicate)
                    {
                        result.SkippedDuplicateCount++;
                        result.Errors.Add($"Row {rowNumber}: Duplicate record found.");
                        continue;
                    }

                    var beneficiary = new BeneficiaryInformation
                    {
                        Id = Guid.NewGuid(),
                        BatchCode = NullIfEmpty(batchCode),
                        OscaIdNumber = NullIfEmpty(oscaIdNumber),
                        OscaIdDateIssued = null,
                        NcscRrn = ncscRrn,
                        LastName = NullIfEmpty(lastName),
                        FirstName = firstName.Trim(),
                        MiddleName = NullIfEmpty(middleName),
                        Extension = NullIfEmpty(extensionName),
                        BirthDate = birthDate,
                        PhoneNumber = null,
                        Sex = MapSex(sexRaw),
                        IsIndigenousPeople = false,
                        IsPersonWithDisability = false,
                        CivilStatus = null,
                        Citizenship = null,
                        Region = region.PsgcCodeRegion,
                        Province = province.PsgcCodeProvince,
                        Municipality = municipality.PsgcCodeMunicipality,
                        Barangay = barangay.PsgcCodeBarangay,
                        IsCompliant = MapCompliance(complianceRaw),
                        Validator = string.IsNullOrWhiteSpace(validator) ? "N/A" : validator.Trim(),
                        ValidationDate = ParseNullableDate(validationDateRaw) ?? DateTime.Today,
                        PaymentStatus = 0,
                        ModeOfPayment = 0,
                        PaymentDate = null,
                        IsDeceased = false,
                        DateOfDeath = null,
                        IsEligible = false,
                        RemarkCategory = null,
                        Remarks = remarks,
                        DateAdded = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    await _repo.AddAsync(beneficiary);

                    await AddLogAsync(
                        beneficiary.Id,
                        $"Imported beneficiary from Excel: {beneficiary.LastName}, {beneficiary.FirstName}",
                        userName);

                    result.ImportedCount++;
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Row {rowNumber}: {ex.Message}");
                }
            }

            await _repo.SaveChangesAsync();
            InvalidateSummaryCache();
            return result;
        }

        public Task<PagedResultDto<BeneficiaryInformationDto>> GetPaginatedAsync(BeneficiaryFilterDto filter)
        {
            var pagedResult = _repo.GetPagedAsync(filter);
            return pagedResult;
        }
        #region Private helpers
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
                (filter.Sexes != null && filter.Sexes.Any() ? string.Join(",", filter.Sexes.OrderBy(x => x)) : "null"),
                filter.SpecificAge?.ToString() ?? "null",
                filter.MilestoneYear?.ToString() ?? "null",
                filter.SpecificBirthday?.ToString("yyyy-MM-dd") ?? "null",
                filter.BirthdayFrom?.ToString("yyyy-MM-dd") ?? "null",
                filter.BirthdayTo?.ToString("yyyy-MM-dd") ?? "null"
            );
        }
        //Updating a beneficiary record involves comparing the existing values with the new values from the DTO and logging any changes. This method generates a list of changed fields for logging purposes.
        private List<string> GetChangedFields(BeneficiaryInformation beneficiary, BeneficiaryInformationDto dto)
        {
            var changes = new List<string>();

            if (beneficiary.BatchCode != dto.BatchCode)
                changes.Add($"BatchCode: '{beneficiary.BatchCode}' -> '{dto.BatchCode}'");

            if (beneficiary.OscaIdNumber != dto.OscaIdNumber)
                changes.Add($"OscaIdNumber: '{beneficiary.OscaIdNumber}' -> '{dto.OscaIdNumber}'");

            if (beneficiary.OscaIdDateIssued != dto.OscaIdDateIssued)
                changes.Add($"OscaIdDateIssued: '{beneficiary.OscaIdDateIssued}' -> '{dto.OscaIdDateIssued}'");

            if (beneficiary.NcscRrn != dto.NcscRrn)
                changes.Add($"NcscRrn: '{beneficiary.NcscRrn}' -> '{dto.NcscRrn}'");

            if (beneficiary.LastName != dto.LastName)
                changes.Add($"LastName: '{beneficiary.LastName}' -> '{dto.LastName}'");

            if (beneficiary.FirstName != dto.FirstName)
                changes.Add($"FirstName: '{beneficiary.FirstName}' -> '{dto.FirstName}'");

            if (beneficiary.MiddleName != dto.MiddleName)
                changes.Add($"MiddleName: '{beneficiary.MiddleName}' -> '{dto.MiddleName}'");

            if (beneficiary.Extension != dto.Extension)
                changes.Add($"Extension: '{beneficiary.Extension}' -> '{dto.Extension}'");

            if (beneficiary.BirthDate.Date != dto.BirthDate.Date)
                changes.Add($"BirthDate: '{beneficiary.BirthDate:yyyy-MM-dd}' -> '{dto.BirthDate:yyyy-MM-dd}'");

            if (beneficiary.PhoneNumber != dto.PhoneNumber)
                changes.Add($"PhoneNumber: '{beneficiary.PhoneNumber}' -> '{dto.PhoneNumber}'");

            if (beneficiary.Sex != dto.Sex)
                changes.Add($"Sex: '{beneficiary.Sex}' -> '{dto.Sex}'");

            if (beneficiary.IsIndigenousPeople != dto.IsIndigenousPeople)
                changes.Add($"IsIndigenousPeople: '{beneficiary.IsIndigenousPeople}' -> '{dto.IsIndigenousPeople}'");

            if (beneficiary.IsPersonWithDisability != dto.IsPersonWithDisability)
                changes.Add($"IsPersonWithDisability: '{beneficiary.IsPersonWithDisability}' -> '{dto.IsPersonWithDisability}'");

            if (beneficiary.CivilStatus != dto.CivilStatus)
                changes.Add($"CivilStatus: '{beneficiary.CivilStatus}' -> '{dto.CivilStatus}'");

            if (beneficiary.Citizenship != dto.Citizenship)
                changes.Add($"Citizenship: '{beneficiary.Citizenship}' -> '{dto.Citizenship}'");

            if (beneficiary.Province != dto.PsgcCodeProvince)
                changes.Add($"Province: '{beneficiary.Province}' -> '{dto.PsgcCodeProvince}'");

            if (beneficiary.Municipality != dto.PsgcCodeMunicipality)
                changes.Add($"Municipality: '{beneficiary.Municipality}' -> '{dto.PsgcCodeMunicipality}'");

            if (beneficiary.Barangay != dto.PsgcCodeBarangay)
                changes.Add($"Barangay: '{beneficiary.Barangay}' -> '{dto.PsgcCodeBarangay}'");

            if (beneficiary.IsCompliant != dto.IsCompliant)
                changes.Add($"IsCompliant: '{beneficiary.IsCompliant}' -> '{dto.IsCompliant}'");

            if (beneficiary.Validator != dto.Validator)
                changes.Add($"Validator: '{beneficiary.Validator}' -> '{dto.Validator}'");

            if (beneficiary.ValidationDate != dto.ValidationDate)
                changes.Add($"ValidationDate: '{beneficiary.ValidationDate}' -> '{dto.ValidationDate}'");

            if (beneficiary.PaymentStatus != dto.PaymentStatus)
                changes.Add($"PaymentStatus: '{beneficiary.PaymentStatus}' -> '{dto.PaymentStatus}'");

            if (beneficiary.ModeOfPayment != dto.ModeOfPayment)
                changes.Add($"ModeOfPayment: '{beneficiary.ModeOfPayment}' -> '{dto.ModeOfPayment}'");

            if (beneficiary.PaymentDate != dto.PaymentDate)
                changes.Add($"PaymentDate: '{beneficiary.PaymentDate}' -> '{dto.PaymentDate}'");

            if (beneficiary.IsDeceased != dto.IsDeceased)
                changes.Add($"IsDeceased: '{beneficiary.IsDeceased}' -> '{dto.IsDeceased}'");

            if (beneficiary.DateOfDeath != dto.DateOfDeath)
                changes.Add($"DateOfDeath: '{beneficiary.DateOfDeath}' -> '{dto.DateOfDeath}'");

            if (beneficiary.IsEligible != dto.IsEligible)
                changes.Add($"IsEligible: '{beneficiary.IsEligible}' -> '{dto.IsEligible}'");

            if (beneficiary.RemarkCategory != dto.RemarkCategory)
                changes.Add($"RemarkCategory: '{beneficiary.RemarkCategory}' -> '{dto.RemarkCategory}'");

            if (beneficiary.Remarks != dto.Remarks)
                changes.Add($"Remarks: '{beneficiary.Remarks}' -> '{dto.Remarks}'");

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

        private static bool MapCompliance(string? value)
        {
            return value?.Trim().ToUpper() switch
            {
                "COMPLIANT" => true,
                "YES" => true,
                _ => false
            };
        }

        private static int MapPaymentStatus(string? value)
        {
            return value?.Trim().ToUpper() switch
            {
                "UNPAID" => 1,
                "PAID" => 2,
                _ => 0
            };
        }

        private static bool MapEligibility(string? value)
        {
            return value?.Trim().ToUpper() switch
            {
                "ELIGIBLE" => true,
                "YES" => true,
                _ => false
            };
        }

        private static bool TryParseExcelDate(string? value, out DateTime date)
        {
            if (DateTime.TryParse(value, out date))
                return true;

            var formats = new[]
            {
        "M/d/yyyy",
        "MM/dd/yyyy",
        "M/d/yy",
        "MM/dd/yy",
        "yyyy-MM-dd",
        "M-d-yyyy",
        "MM-d-yyyy",
        "MMMM d yyyy",
        "MMMM dd yyyy",
        "MMM d yyyy",
        "MMM dd yyyy"
    };

            return DateTime.TryParseExact(
                value,
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

        #endregion

    }
}
