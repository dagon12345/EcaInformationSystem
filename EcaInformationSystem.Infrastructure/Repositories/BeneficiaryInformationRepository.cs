using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
        public async Task<BeneficiaryInformation?> GetByIdAsync(Guid id)
        {
            return await _context.BeneficiaryInformations.FirstOrDefaultAsync(x => x.Id == id);
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
            return await BuildBeneficiaryDtoQuery(filter)
                         .OrderBy(x => x.LastName)
                         .ThenBy(x => x.FirstName)
                         .ThenBy(x => x.MiddleName)
                         .ToListAsync();
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
                 Region = region != null ? region.Name : null,
                 PsgcCodeProvince = b.Province,
                 Province = province != null ? province.Name : null,
                 PsgcCodeMunicipality = b.Municipality,
                 Municipality = municipality != null ? municipality.Name : null,
                 PsgcCodeBarangay = b.Barangay,
                 Barangay = barangay != null ? barangay.Name : null,
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
            var allItems = await BuildBeneficiaryDtoQuery(filter)
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.MiddleName)
                .ToListAsync();

            // Deduplicate in memory — safe here since it's already a List
            var deduplicated = allItems
                .DistinctBy(x => x.Id)
                .ToList();

            var result = new BeneficiarySummaryResultDto
            {
                Beneficiaries = deduplicated,
                TotalBeneficiaries = deduplicated.Count,
                TotalMale = deduplicated.Count(x => x.Sex == 1),
                TotalFemale = deduplicated.Count(x => x.Sex == 2),

                ProvinceCounts = deduplicated
                    .Where(x => !string.IsNullOrWhiteSpace(x.Province))
                    .GroupBy(x => x.Province!)
                    .Select(g => new ProvinceCountDto
                    {
                        Province = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Province)
                    .ToList(),

                MunicipalityCounts = deduplicated
                    .Where(x => !string.IsNullOrWhiteSpace(x.Municipality))
                    .GroupBy(x => x.Municipality!)
                    .Select(g => new MunicipalityCountDto
                    {
                        Municipality = g.Key,
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
        public async Task BulkUpdatePaymentStatusAsync(List<Guid> ids, int paymentStatus)
        {
            var beneficiaries = await _context.BeneficiaryInformations
                .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync();

            foreach(var b in beneficiaries)
            {
                b.PaymentStatus = paymentStatus;
            }

            await _context.SaveChangesAsync();
        }
        public async Task<PagedResultDto<BeneficiaryInformationDto>> GetPagedAsync(BeneficiaryFilterDto filter)
        {
            var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

            // Materialize first, then deduplicate in memory
            var allItems = await BuildBeneficiaryDtoQuery(filter)
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.MiddleName)
                .ToListAsync();

            // Deduplicate in memory — safe here since it's already a List
            var deduplicated = allItems
                .DistinctBy(x => x.Id)
                .ToList();

            var totalCount = deduplicated.Count;
            var items = deduplicated
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResultDto<BeneficiaryInformationDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
        private IQueryable<BeneficiaryQueryModel> BuildBeneficiaryFilteredQuery(BeneficiaryFilterDto filter)
        {
            var query =
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
                select new BeneficiaryQueryModel
                {
                    Beneficiary = b,
                    Region = region != null ? region.Name : null,
                    Province = province != null ? province.Name : null,
                    Municipality = municipality != null ? municipality.Name : null,
                    Barangay = barangay != null ? barangay.Name : null
                };

            if (filter.PsgcCodeRegion.HasValue && filter.PsgcCodeRegion.Value > 0)
                query = query.Where(x => x.Beneficiary.Region == filter.PsgcCodeRegion.Value);

            if (filter.PsgcCodeProvinces != null && filter.PsgcCodeProvinces.Any())
                query = query.Where(x => filter.PsgcCodeProvinces.Contains(x.Beneficiary.Province));

            if (filter.PsgcCodeMunicipalities != null && filter.PsgcCodeMunicipalities.Any())
                query = query.Where(x => filter.PsgcCodeMunicipalities.Contains(x.Beneficiary.Municipality));

            if (filter.PsgcCodeBarangays != null && filter.PsgcCodeBarangays.Any())
                query = query.Where(x => filter.PsgcCodeBarangays.Contains(x.Beneficiary.Barangay));

            if (!string.IsNullOrWhiteSpace(filter.LastName))
                query = query.Where(x => x.Beneficiary.LastName!.Contains(filter.LastName));

            if (!string.IsNullOrWhiteSpace(filter.FirstName))
                query = query.Where(x => x.Beneficiary.FirstName.Contains(filter.FirstName));

            if (!string.IsNullOrWhiteSpace(filter.FullName))
            {
                var name = filter.FullName.Trim().ToLower();
                query = query.Where(x =>
                    (x.Beneficiary.LastName + " " + x.Beneficiary.FirstName + " " + x.Beneficiary.MiddleName).ToLower().Contains(name) ||
                    (x.Beneficiary.FirstName + " " + x.Beneficiary.MiddleName + " " + x.Beneficiary.LastName).ToLower().Contains(name));
            }

            if (filter.Sexes != null && filter.Sexes.Any())
                query = query.Where(x => filter.Sexes.Contains(x.Beneficiary.Sex));

            if (filter.PaymentStatuses != null && filter.PaymentStatuses.Any())
                query = query.Where(x => filter.PaymentStatuses.Contains(x.Beneficiary.PaymentStatus));

            if (!string.IsNullOrWhiteSpace(filter.Validator))
                query = query.Where(x => x.Beneficiary.Validator!.Contains(filter.Validator));

            if (!string.IsNullOrWhiteSpace(filter.BatchCode))
                query = query.Where(x => x.Beneficiary.BatchCode!.Contains(filter.BatchCode));

            if (filter.SpecificBirthday.HasValue)
            {
                var specificBirthday = filter.SpecificBirthday.Value.Date;
                query = query.Where(x => x.Beneficiary.BirthDate.Date == specificBirthday);
            }
            else
            {
                if (filter.BirthdayFrom.HasValue)
                {
                    var birthdayFrom = filter.BirthdayFrom.Value.Date;
                    query = query.Where(x => x.Beneficiary.BirthDate.Date >= birthdayFrom);
                }

                if (filter.BirthdayTo.HasValue)
                {
                    var birthdayTo = filter.BirthdayTo.Value.Date;
                    query = query.Where(x => x.Beneficiary.BirthDate.Date <= birthdayTo);
                }
            }
            return query.AsNoTracking();
        }

        private IQueryable<BeneficiaryInformationDto> BuildBeneficiaryDtoQuery(BeneficiaryFilterDto filter)
        {
            var query = BuildBeneficiaryFilteredQuery(filter)
                .Select(x => new BeneficiaryInformationDto
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
                    Age = DateTime.Today.Year - x.Beneficiary.BirthDate.Year -
                          (x.Beneficiary.BirthDate.Date > DateTime.Today.AddYears(-(DateTime.Today.Year - x.Beneficiary.BirthDate.Year)) ? 1 : 0),
                    // Milestone year = BirthYear + the highest milestone age already reached
                    // that is on or after the program start year 2024
                    MilestoneYear =
                   (x.Beneficiary.BirthDate.Year + 100) < DateTime.Today.Year && (x.Beneficiary.BirthDate.Year + 100) >= 2024 ? x.Beneficiary.BirthDate.Year + 100 :
                   (x.Beneficiary.BirthDate.Year + 100) == DateTime.Today.Year && x.Beneficiary.BirthDate.DayOfYear <= DateTime.Today.DayOfYear && (x.Beneficiary.BirthDate.Year + 100) >= 2024 ? x.Beneficiary.BirthDate.Year + 100 :
                   (x.Beneficiary.BirthDate.Year + 95) < DateTime.Today.Year && (x.Beneficiary.BirthDate.Year + 95) >= 2024 ? x.Beneficiary.BirthDate.Year + 95 :
                   (x.Beneficiary.BirthDate.Year + 95) == DateTime.Today.Year && x.Beneficiary.BirthDate.DayOfYear <= DateTime.Today.DayOfYear && (x.Beneficiary.BirthDate.Year + 95) >= 2024 ? x.Beneficiary.BirthDate.Year + 95 :
                   (x.Beneficiary.BirthDate.Year + 90) < DateTime.Today.Year && (x.Beneficiary.BirthDate.Year + 90) >= 2024 ? x.Beneficiary.BirthDate.Year + 90 :
                   (x.Beneficiary.BirthDate.Year + 90) == DateTime.Today.Year && x.Beneficiary.BirthDate.DayOfYear <= DateTime.Today.DayOfYear && (x.Beneficiary.BirthDate.Year + 90) >= 2024 ? x.Beneficiary.BirthDate.Year + 90 :
                   (x.Beneficiary.BirthDate.Year + 85) < DateTime.Today.Year && (x.Beneficiary.BirthDate.Year + 85) >= 2024 ? x.Beneficiary.BirthDate.Year + 85 :
                   (x.Beneficiary.BirthDate.Year + 85) == DateTime.Today.Year && x.Beneficiary.BirthDate.DayOfYear <= DateTime.Today.DayOfYear && (x.Beneficiary.BirthDate.Year + 85) >= 2024 ? x.Beneficiary.BirthDate.Year + 85 :
                   (x.Beneficiary.BirthDate.Year + 80) < DateTime.Today.Year && (x.Beneficiary.BirthDate.Year + 80) >= 2024 ? x.Beneficiary.BirthDate.Year + 80 :
                   (x.Beneficiary.BirthDate.Year + 80) == DateTime.Today.Year && x.Beneficiary.BirthDate.DayOfYear <= DateTime.Today.DayOfYear && (x.Beneficiary.BirthDate.Year + 80) >= 2024 ? x.Beneficiary.BirthDate.Year + 80 :
                   0,
                    IsIndigenousPeople = x.Beneficiary.IsIndigenousPeople,
                    IsPersonWithDisability = x.Beneficiary.IsPersonWithDisability,
                    CivilStatus = x.Beneficiary.CivilStatus,
                    Citizenship = x.Beneficiary.Citizenship,
                    Sex = x.Beneficiary.Sex,
                    PsgcCodeRegion = x.Beneficiary.Region,
                    Region = x.Region,
                    PsgcCodeProvince = x.Beneficiary.Province,
                    Province = x.Province,
                    PsgcCodeMunicipality = x.Beneficiary.Municipality,
                    Municipality = x.Municipality,
                    PsgcCodeBarangay = x.Beneficiary.Barangay,
                    Barangay = x.Barangay,
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
                    IsDeleted = x.Beneficiary.IsDeleted
                });

        if (filter.SpecificAge.HasValue)
            {
                var cutoffStart = DateTime.Today.AddYears(-filter.SpecificAge.Value - 1).Date;
                var cutoffEnd = DateTime.Today.AddYears(-filter.SpecificAge.Value).Date;

                query = query.Where(x =>
                    x.BirthDate > cutoffStart &&
                    x.BirthDate <= cutoffEnd
                );
            }

            if (filter.MilestoneYear.HasValue)
            {
                query = query.Where(x => x.MilestoneYear == filter.MilestoneYear.Value);
            }

            return query;
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

        private sealed class BeneficiaryQueryModel
        {
            public BeneficiaryInformation Beneficiary { get; set; } = default!;
            public string? Region { get; set; }
            public string? Province { get; set; }
            public string? Municipality { get; set; }
            public string? Barangay { get; set; }
        }

    }
}
