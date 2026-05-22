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
            //IsCompliant Filter
            if(filter.IsCompliant.HasValue)
            {
                query = query.Where(x => x.Beneficiary.IsCompliant == filter.IsCompliant.Value);
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

            // ── Other ─────────────────────────────────────────────────────────────
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
