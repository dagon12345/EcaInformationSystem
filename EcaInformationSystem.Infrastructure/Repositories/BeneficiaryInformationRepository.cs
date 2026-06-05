using EcaInformationService.Shared.DTOs;
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

        public BeneficiaryInformationRepository(AppDbContext context)
        {
            _context = context;
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
        // Infrastructure/Repositories/BeneficiaryInformationRepository.cs
        public async Task<List<PossibleDuplicatePairDto>> FindAllPossibleDuplicatesAsync(
            BeneficiaryFilterDto filter,
            int maxPairs = 50,
            CancellationToken cancellationToken = default)
        {
            // ✅ REUSE your existing BuildBeneficiaryFilteredQuery
            // This applies ALL active filters — province, payment status,
            // municipality, barangay, eligibility, compliance, etc.
            // Now the scan only sees the same records the grid shows
            var candidates = await BuildBeneficiaryFilteredQuery(filter)
                .Select(x => new
                {
                    x.Beneficiary.Id,
                    x.Beneficiary.FirstName,
                    x.Beneficiary.LastName,
                    x.Beneficiary.MiddleName,
                    x.Beneficiary.BirthDate,
                    x.Beneficiary.OscaIdNumber,
                    x.Beneficiary.PaymentStatus,
                    MunicipalityName = x.Municipality,
                    BarangayName = x.Barangay
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            // ✅ Need at least 2 records to form a pair
            if (candidates.Count < 2)
                return new List<PossibleDuplicatePairDto>();

            // ✅ Group by birth year — same optimization as before
            // but now only operating on the already-filtered set
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

                        var pairKey = string.Join("|",
                            new[] { a.Id, b.Id }.OrderBy(x => x));
                        if (!seen.Add(pairKey)) continue;

                        // ── Gate 1: Birthdate window ──────────────────────────────
                        var daysDiff = Math.Abs(
                            (a.BirthDate - b.BirthDate).TotalDays);
                        if (daysDiff > 365) continue;

                        // ── Gate 2: Last name ─────────────────────────────────────
                        var lastNameScore = ComputeNameSimilarity(
                            a.LastName?.Trim(), b.LastName?.Trim());
                        if (lastNameScore < 0.60) continue;

                        // ── Gate 3: First name ────────────────────────────────────
                        var firstNameScore = ComputeNameSimilarity(
                            a.FirstName?.Trim(), b.FirstName?.Trim());
                        if (firstNameScore < 0.60) continue;

                        // ── Gate 4: Middle name analysis ──────────────────────────
                        bool aHasMiddle = !string.IsNullOrWhiteSpace(a.MiddleName);
                        bool bHasMiddle = !string.IsNullOrWhiteSpace(b.MiddleName);
                        bool bothHaveMiddle = aHasMiddle && bHasMiddle;
                        bool neitherHas = !aHasMiddle && !bHasMiddle;
                        bool oneHas = aHasMiddle ^ bHasMiddle;

                        double middleScore = 1.0;
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
                            middleScore = ComputeNameSimilarity(
                                a.MiddleName!.Trim(), b.MiddleName!.Trim());

                            middleStatus = middleScore >= 0.75
                                ? MiddleNameStatus.Similar
                                : middleScore >= 0.40
                                    ? MiddleNameStatus.PartiallyDifferent
                                    : MiddleNameStatus.Conflicting;
                        }

                        // ── Weighted final score ──────────────────────────────────
                        double finalScore = neitherHas || oneHas
                            ? (lastNameScore * 0.50) + (firstNameScore * 0.50)
                            : (lastNameScore * 0.40) + (firstNameScore * 0.40)
                                + (middleScore * 0.20);

                        // ✅ Force flag exact first+last with conflicting middle
                        bool firstLastExact = lastNameScore >= 0.99
                                           && firstNameScore >= 0.99;

                        if (firstLastExact && middleStatus == MiddleNameStatus.Conflicting)
                            finalScore = Math.Max(finalScore, 0.80);

                        if (finalScore < 0.75) continue;

                        var reason = BuildDuplicateReason(
                            firstLastExact, middleStatus, daysDiff,
                            a.MiddleName, b.MiddleName);

                        pairs.Add(new PossibleDuplicatePairDto
                        {
                            Record1Id = a.Id,
                            Record1FullName = FormatDuplicateName(a.LastName, a.FirstName, a.MiddleName),
                            Record1BirthDate = a.BirthDate.ToString("MMMM dd, yyyy"),
                            Record1Municipality = a.MunicipalityName ?? string.Empty,
                            Record1Barangay = a.BarangayName ?? string.Empty,
                            Record1OscaId = a.OscaIdNumber ?? string.Empty,
                            Record1PaymentStatus = a.PaymentStatus,

                            Record2Id = b.Id,
                            Record2FullName = FormatDuplicateName(b.LastName, b.FirstName, b.MiddleName),
                            Record2BirthDate = b.BirthDate.ToString("MMMM dd, yyyy"),
                            Record2Municipality = b.MunicipalityName ?? string.Empty,
                            Record2Barangay = b.BarangayName ?? string.Empty,
                            Record2OscaId = b.OscaIdNumber ?? string.Empty,
                            Record2PaymentStatus = b.PaymentStatus,

                            MatchScore = Math.Round(finalScore, 2),
                            MatchReason = reason
                        });
                    }
                }
            }

        Done:
            return pairs
                .OrderByDescending(x => x.MatchScore)
                .ToList();
            // ✅ No extra muni/barangay batch queries needed
            // BuildBeneficiaryFilteredQuery already joins them via x.Municipality / x.Barangay
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
                    RowVersion = b.RowVersion
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

            if (!string.IsNullOrWhiteSpace(filter.FullName))
            {
                var name = filter.FullName.Trim().ToLower();
                query = query.Where(x =>
                    (x.Beneficiary.LastName + " " +
                     x.Beneficiary.FirstName + " " +
                     x.Beneficiary.MiddleName).ToLower().Contains(name) ||
                    (x.Beneficiary.FirstName + " " +
                     x.Beneficiary.MiddleName + " " +
                     x.Beneficiary.LastName).ToLower().Contains(name));
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
                    RowVersion = x.Beneficiary.RowVersion
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
            RowVersion = x.RowVersion
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
            // Step 1: Fetch the raw data with joins (no JsonElement in projection)
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

                where ids.Contains(b.Id) && !b.IsDeleted

                select new
                {
                    b.Id,
                    b.Quarter,
                    b.Batch,
                    b.RefYear,
                    b.RefCode,
                    b.BatchCode,
                    b.OscaIdNumber,
                    b.OscaIdDateIssued,
                    b.NcscRrn,
                    b.LastName,
                    b.FirstName,
                    b.MiddleName,
                    b.Extension,
                    b.BirthDate,
                    b.Sex,
                    b.IsIndigenousPeople,
                    b.IsPersonWithDisability,
                    b.CivilStatus,
                    b.Citizenship,
                    b.IsCompliant,
                    b.Validator,
                    b.ValidationDate,
                    b.PaymentStatus,
                    b.ModeOfPayment,
                    b.PaymentDate,
                    b.IsDeceased,
                    b.DateOfDeath,
                    b.IsEligible,
                    b.AssessmentRemarks,
                    b.EligibilityRemarks,
                    b.RemarkCategory,
                    b.Remarks,
                    b.DateAdded,
                    b.CoStatus,
                    b.CoDateEndorsed,
                    b.CoDateApproved,
                    b.IsDeleted,
                    b.DateApplied,
                    b.DateEndorsed,
                    b.PhoneNumber,
                    PsgcCodeRegion = b.Region,
                    PsgcCodeProvince = b.Province,
                    PsgcCodeMunicipality = b.Municipality,
                    PsgcCodeBarangay = b.Barangay,
                    RegionName = region != null ? region.Name : null,
                    ProvinceName = province != null ? province.Name : null,
                    MunicipalityName = municipality != null ? municipality.Name : null,
                    BarangayName = barangay != null ? barangay.Name : null,
                    b.RowVersion
                }
            ).AsNoTracking().ToListAsync();

            // Step 2: Deduplicate raw rows by beneficiary Id before mapping.
            // The LEFT JOINs on Provinces/Municipalities/Barangays can produce multiple
            // rows per beneficiary when the lookup table contains duplicate codes
            // (e.g. legacy DB codes co-existing with PSGC-seeded codes for the same province).
            // Keeping the first occurrence is safe because all duplicate rows carry the same
            // beneficiary fields; only the joined name columns might differ, and they're
            // the same value (same province name for the same code).
            var deduped = raw
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();

            // Step 3: Map to DTO in-memory (safe to use JsonSerializer here)
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

                // ✅ Wrap string → JsonElement so the type matches JsonElement?
                Region = x.RegionName != null ? JsonSerializer.SerializeToElement(x.RegionName) : null,
                Province = x.ProvinceName != null ? JsonSerializer.SerializeToElement(x.ProvinceName) : null,
                Municipality = x.MunicipalityName != null ? JsonSerializer.SerializeToElement(x.MunicipalityName) : null,
                Barangay = x.BarangayName != null ? JsonSerializer.SerializeToElement(x.BarangayName) : null,

                IsCompliant = x.IsCompliant,
                Validator = x.Validator ?? string.Empty,
                ValidationDate = x.ValidationDate,
                PaymentStatus = x.PaymentStatus,
                ModeOfPayment = x.ModeOfPayment,
                PaymentDate = x.PaymentDate,
                IsDeceased = x.IsDeceased,
                DateOfDeath = x.DateOfDeath,
                IsEligible = x.IsEligible,
                AssessmentRemarks = x.AssessmentRemarks,
                EligibilityRemarks = x.EligibilityRemarks,  // ✅
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
