using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
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

        public async Task<bool> ExistsDuplicateAsync(string? lastName, string? firstName, string? middleName, DateTime birthDate, string? oscaIdNumber, int? ncscRrn, Guid? excludeId = null)
        {
            var normalizedLastName = (lastName ?? string.Empty).Trim().ToLower();
            var normalizedFirstName = (firstName ?? string.Empty).Trim().ToLower();
            var normalizedMiddleName = (middleName ?? string.Empty).Trim().ToLower();
            var normalizedOscaIdNumber = (oscaIdNumber ?? string.Empty).Trim().ToLower();
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
                x.BirthDate.Date == normalizedBirthDate &&
                (x.OscaIdNumber ?? string.Empty).Trim().ToLower() == normalizedOscaIdNumber &&
                x.NcscRrn == ncscRrn
            );
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
                    RemarkCategory = b.RemarkCategory,
                    Remarks = b.Remarks,
                    DateAdded = b.DateAdded,
                    IsDeleted = b.IsDeleted
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
                 RemarkCategory = b.RemarkCategory != null ? b.RemarkCategory : null,
                 DateAdded = b.DateAdded,
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
            await _context.SaveChangesAsync();
        }

        public Task UpdateAsync(BeneficiaryInformation beneficiaryInformation)
        {
            _context.BeneficiaryInformations.Update(beneficiaryInformation);
            return Task.CompletedTask;
        }
        public async Task BulkUpdatePaymentStatusAsync(List<Guid> ids, int paymentStatus, DateTime? paymentDate)
        {
            var beneficiaries = await _context.BeneficiaryInformations
                .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync();

            foreach (var b in beneficiaries)
            {
                b.PaymentStatus = paymentStatus;
                b.PaymentDate = paymentStatus == 2  // ✅ 2 = Paid → save date
                    ? paymentDate
                    : null; //✅ 1 = Unpaid → always null
            }

            await _context.SaveChangesAsync();
        }
        public async Task<PagedResultDto<BeneficiaryInformationDto>> GetPagedAsync(BeneficiaryFilterDto filter)
        {
            var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

            var query = BuildBeneficiaryRawQuery(filter);

            // ✅ Count distinct IDs only — EF Core CAN translate this
            var totalCount = await query
                .Select(x => x.Id)
                .Distinct()
                .CountAsync();

            // ✅ Fetch the page — deduplicate in memory after ToList
            var items = await query
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.MiddleName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ✅ Deduplicate in memory, then page
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

                    //Beneficiary Finding here:
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

            // ── Location ─────────────────────────────────────────────────────────
            if (filter.PsgcCodeRegion.HasValue && filter.PsgcCodeRegion.Value > 0)
                query = query.Where(x => x.Beneficiary.Region == filter.PsgcCodeRegion.Value);

            if (filter.PsgcCodeProvince != null)
                query = query.Where(x => x.Beneficiary.Province == filter.PsgcCodeProvince);

            if (filter.PsgcCodeMunicipality != null)
                query = query.Where(x => x.Beneficiary.Municipality == filter.PsgcCodeMunicipality);


            if (filter.PsgcCodeBarangay != null)
                query = query.Where(x => x.Beneficiary.Barangay == filter.PsgcCodeBarangay);

            // ✅ Fix — N/A (0) also includes records with no finding row at all
            if (filter.FindingStatus.HasValue && filter.FindingStatus.Value != 3)
            {
                var status = filter.FindingStatus.Value;

                if (status == 0)
                {
                    // N/A = explicitly set to 0 OR no finding record exists yet (null from left join)
                    query = query.Where(x =>
                        x.FindingStatus == null ||
                        x.FindingStatus == 0);
                }
                else
                {
                    // Solved (1) or Unresolved (2) — only exact matches
                    query = query.Where(x => x.FindingStatus == status);
                }
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



            // ── Age — computed directly from BirthDate ────────────────────────────
            // ✅ Moved from BuildBeneficiaryDtoQuery — now part of the single query
            if (filter.SpecificAge.HasValue)
            {
                var cutoffEnd = DateTime.Today.AddYears(-filter.SpecificAge.Value).Date;
                var cutoffStart = DateTime.Today.AddYears(-filter.SpecificAge.Value - 1).Date;
                query = query.Where(x =>
                    x.Beneficiary.BirthDate > cutoffStart &&
                    x.Beneficiary.BirthDate <= cutoffEnd);
            }

            //// ── Birthday ──────────────────────────────────────────────────────────
            if (filter.SpecificBirthday.HasValue)
            {
                var start = filter.SpecificBirthday.Value.Date;
                var end = start.AddDays(1);
                query = query.Where(x =>
                    x.Beneficiary.BirthDate >= start &&
                    x.Beneficiary.BirthDate < end);
            }
            else
            {
                if (filter.BirthdayFrom.HasValue)
                {
                    var from = filter.BirthdayFrom.Value.Date;
                    query = query.Where(x => x.Beneficiary.BirthDate >= from);
                }
                if (filter.BirthdayTo.HasValue)
                {
                    var to = filter.BirthdayTo.Value.Date.AddDays(1);
                    query = query.Where(x => x.Beneficiary.BirthDate < to);
                }
            }

            // ── Milestone Year — computed from BirthDate ──────────────────────────
            // ✅ Moved from BuildBeneficiaryDtoQuery — translated directly to SQL
            if (filter.MilestoneYear.HasValue)
            {
                var milestoneYear = filter.MilestoneYear.Value;
                var milestones = new[] { 80, 85, 90, 95, 100 };

                query = query.Where(x => milestones.Any(m =>
                    x.Beneficiary.BirthDate.Year + m == milestoneYear));
            }

            // ✅ Sex — only filter when a real selection was made (1=Male, 2=Female)
            if (filter.Sex.HasValue && filter.Sex.Value > 0)
                query = query.Where(x => x.Beneficiary.Sex == filter.Sex.Value);

            // ✅ PaymentStatus — only filter when a real selection was made (0=N/A, 1=Unpaid, 2=Paid)
            // -1 means "not selected" — exclude it
            if (filter.PaymentStatus.HasValue && filter.PaymentStatus.Value >= 0)
                query = query.Where(x => x.Beneficiary.PaymentStatus == filter.PaymentStatus.Value);

            if (filter.PaymentDate.HasValue) // ✅ && instead of &
            {
                var paymentStart = filter.PaymentDate.Value.Date;
                var paymentEnd = paymentStart.AddDays(1);
                query = query.Where(x =>
                    x.Beneficiary.PaymentDate >= paymentStart &&
                    x.Beneficiary.PaymentDate < paymentEnd);
            }

            // ✅ Add payment date range support
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

            //// ── Other ─────────────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.Validator))
                query = query.Where(x =>
                    x.Beneficiary.Validator != null &&
                    x.Beneficiary.Validator.Contains(filter.Validator));

            if (!string.IsNullOrWhiteSpace(filter.BatchCode))
                query = query.Where(x =>
                    x.Beneficiary.BatchCode != null &&
                    x.Beneficiary.BatchCode.Contains(filter.BatchCode));
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
                    FindingRemarks = x.FindingRemarks
                });
        }

        // ── Mapper: call this AFTER .ToListAsync() ───────────────────────────────────
        private static BeneficiaryInformationDto MapToDto(BeneficiaryRawDto x) => new()
        {
            Id = x.Id,
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
            RemarkCategory = x.RemarkCategory,
            DateAdded = x.DateAdded,
            Remarks = x.Remarks,
            IsDeleted = x.IsDeleted,
            FindingStatus = x.FindingStatus,
            FindingRemarks = x.FindingRemarks
        };

        private static int ComputeMilestoneYear(DateTime birthDate)
        {
            var today = DateTime.Today;
            foreach (var m in new[] { 100, 95, 90, 85, 80 })
            {
                int y = birthDate.Year + m;
                if (y >= 2024 && (y < today.Year || (y == today.Year && birthDate.DayOfYear <= today.DayOfYear)))
                    return y;
            }
            return 0;
        }


        public async Task<BeneficiaryInformation?> FindExistingAsync(string? lastName, string? firstName, string? middleName, DateTime birthDate, string? oscaIdNumber, int? ncscRrn)
        {
            var normalizedLastName = (lastName ?? string.Empty).Trim().ToLower();
            var normalizedFirstName = (firstName ?? string.Empty).Trim().ToLower();
            var normalizedMiddleName = (middleName ?? string.Empty).Trim().ToLower();
            var normalizedOscaIdNumber = (oscaIdNumber ?? string.Empty).Trim().ToLower();
            var normalizedBirthDate = birthDate.Date;

            return await _context.BeneficiaryInformations
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    (x.LastName ?? string.Empty).Trim().ToLower() == normalizedLastName &&
                    (x.FirstName ?? string.Empty).Trim().ToLower() == normalizedFirstName &&
                    (x.MiddleName ?? string.Empty).Trim().ToLower() == normalizedMiddleName &&
                    x.BirthDate.Date == normalizedBirthDate &&
                    (x.OscaIdNumber ?? string.Empty).Trim().ToLower() == normalizedOscaIdNumber &&
                    x.NcscRrn == ncscRrn
                );
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
                    b.RemarkCategory,
                    b.Remarks,
                    b.DateAdded,
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
                }
            ).AsNoTracking().ToListAsync();

            // Step 2: Map to DTO in-memory (safe to use JsonSerializer here)
            var result = raw.Select(x => new BeneficiaryInformationDto
            {
                Id = x.Id,
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
                RemarkCategory = x.RemarkCategory,
                Remarks = x.Remarks,
                DateAdded = x.DateAdded,
                IsDeleted = x.IsDeleted,

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
            public int? RemarkCategory { get; set; }
            public DateTime DateAdded { get; set; }
            public string? Remarks { get; set; }
            public bool IsDeleted { get; set; }
            public int? FindingStatus { get; set; }
            public string? FindingRemarks { get; set; }
        }

    }
}
