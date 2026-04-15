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
                select new
                {
                    Beneficiary = b,
                    Region = region != null ? region.Name : null,
                    Province = province != null ? province.Name : null,
                    Municipality = municipality != null ? municipality.Name : null,
                    Barangay = barangay != null ? barangay.Name : null
                };

            if (filter.PsgcCodeRegion.HasValue && filter.PsgcCodeRegion.Value > 0)
                query = query.Where(x => x.Beneficiary.Region == filter.PsgcCodeRegion.Value);

            if (filter.PsgcCodeProvince.HasValue && filter.PsgcCodeProvince.Value > 0)
                query = query.Where(x => x.Beneficiary.Province == filter.PsgcCodeProvince.Value);

            if (filter.PsgcCodeMunicipality.HasValue && filter.PsgcCodeMunicipality.Value > 0)
                query = query.Where(x => x.Beneficiary.Municipality == filter.PsgcCodeMunicipality.Value);

            if (filter.PsgcCodeBarangay.HasValue && filter.PsgcCodeBarangay.Value > 0)
                query = query.Where(x => x.Beneficiary.Barangay == filter.PsgcCodeBarangay.Value);

            if (!string.IsNullOrEmpty(filter.LastName))
                query = query.Where(x => x.Beneficiary.LastName!.Contains(filter.LastName));

            if (!string.IsNullOrEmpty(filter.FirstName))
                query = query.Where(x => x.Beneficiary.FirstName.Contains(filter.FirstName));

            if (filter.Sex.HasValue && filter.Sex.Value > 0)
                query = query.Where(x => x.Beneficiary.Sex == filter.Sex.Value);

            if (filter.SpecificBirthday.HasValue)
            {
                var specificBirthday = filter.SpecificBirthday.Value.Date;
                query = query.Where(x => x.Beneficiary.BirthDate.Date == filter.SpecificBirthday.Value.Date);
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

            var projected = await
                query.AsNoTracking()
                .ToListAsync();

            var result = projected.Select(x =>
            {
                var age = DateTime.Today.Year - x.Beneficiary.BirthDate.Year;
                if (x.Beneficiary.BirthDate.Date > DateTime.Today.AddYears(-age))
                    age--;

                return new BeneficiaryInformationDto
                {
                    Id = x.Beneficiary.Id,
                    BatchCode = x.Beneficiary.BatchCode,
                    OscaIdNumber = x.Beneficiary.OscaIdNumber,
                    NcscRrn = x.Beneficiary.NcscRrn,
                    LastName = x.Beneficiary.LastName,
                    FirstName = x.Beneficiary.FirstName,
                    MiddleName = x.Beneficiary.MiddleName,
                    Extension = x.Beneficiary.Extension,
                    BirthDate = x.Beneficiary.BirthDate,
                    Age = age,
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
                    PaymentDate = x.Beneficiary.PaymentDate,
                    IsDeceased = x.Beneficiary.IsDeceased,
                    DateOfDeath = x.Beneficiary.DateOfDeath,
                    IsEligible = x.Beneficiary.IsEligible,
                    RemarkCategory = x.Beneficiary.RemarkCategory,
                    Remarks = x.Beneficiary.Remarks,
                    IsDeleted = x.Beneficiary.IsDeleted
                };
            });

            if (filter.SpecificAge.HasValue)
            {
                result = result.Where(x => x.Age == filter.SpecificAge.Value);
            }
            else
            {
                if (filter.AgeFrom.HasValue)
                    result = result.Where(x => x.Age >= filter.AgeFrom.Value);

                if (filter.AgeTo.HasValue)
                    result = result.Where(x => x.Age <= filter.AgeTo.Value);
            }

            return result.ToList();

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
                 BatchCode = b.BatchCode,
                 OscaIdNumber = b.OscaIdNumber,
                 NcscRrn = b.NcscRrn,
                 LastName = b.LastName,
                 FirstName = b.FirstName,
                 MiddleName = b.MiddleName,
                 Extension = b.Extension,
                 BirthDate = b.BirthDate,
                 Age = DateTime.Today.Year - b.BirthDate.Year -
                 (b.BirthDate.Date > DateTime.Today.AddYears(-(DateTime.Today.Year - b.BirthDate.Year)) ? 1 : 0),
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
                 PaymentDate = b.PaymentDate,
                 IsDeceased = b.IsDeceased,
                 DateOfDeath = b.DateOfDeath,
                 IsEligible = b.IsEligible,
                 RemarkCategory = b.RemarkCategory != null ? b.RemarkCategory : null,
                 Remarks = b.Remarks,
                 IsDeleted = b.IsDeleted
             })
             .AsNoTracking()
             .ToListAsync();

            return result;
        }

        public async Task<BeneficiarySummaryResultDto> GetSummaryAsync(BeneficiaryFilterDto filter)
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
                select new
                {
                    Beneficiary = b,
                    Region = region != null ? region.Name : null,
                    Province = province != null ? province.Name : null,
                    Municipality = municipality != null ? municipality.Name : null,
                    Barangay = barangay != null ? barangay.Name : null
                };

            if (filter.PsgcCodeRegion.HasValue && filter.PsgcCodeRegion.Value > 0)
                query = query.Where(x => x.Beneficiary.Region == filter.PsgcCodeRegion.Value);

            if (filter.PsgcCodeProvince.HasValue && filter.PsgcCodeProvince.Value > 0)
                query = query.Where(x => x.Beneficiary.Province == filter.PsgcCodeProvince.Value);

            if (filter.PsgcCodeMunicipality.HasValue && filter.PsgcCodeMunicipality.Value > 0)
                query = query.Where(x => x.Beneficiary.Municipality == filter.PsgcCodeMunicipality.Value);

            if (filter.PsgcCodeBarangay.HasValue && filter.PsgcCodeBarangay.Value > 0)
                query = query.Where(x => x.Beneficiary.Barangay == filter.PsgcCodeBarangay.Value);

            if (!string.IsNullOrWhiteSpace(filter.LastName))
                query = query.Where(x => x.Beneficiary.LastName.Contains(filter.LastName));

            if (!string.IsNullOrWhiteSpace(filter.FirstName))
                query = query.Where(x => x.Beneficiary.FirstName.Contains(filter.FirstName));

            if (filter.Sex.HasValue && filter.Sex.Value > 0)
                query = query.Where(x => x.Beneficiary.Sex == filter.Sex.Value);

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

            var rawList = await query.AsNoTracking().ToListAsync();

            var beneficiaries = rawList.Select(x =>
            {
                var age = DateTime.Today.Year - x.Beneficiary.BirthDate.Year;
                if (x.Beneficiary.BirthDate.Date > DateTime.Today.AddYears(-age))
                    age--;

                return new BeneficiaryInformationDto
                {
                    Id = x.Beneficiary.Id,
                    BatchCode = x.Beneficiary.BatchCode,
                    OscaIdNumber = x.Beneficiary.OscaIdNumber,
                    NcscRrn = x.Beneficiary.NcscRrn,
                    LastName = x.Beneficiary.LastName,
                    FirstName = x.Beneficiary.FirstName,
                    MiddleName = x.Beneficiary.MiddleName,
                    Extension = x.Beneficiary.Extension,
                    BirthDate = x.Beneficiary.BirthDate,
                    Age = age,
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
                    PaymentDate = x.Beneficiary.PaymentDate,
                    IsDeceased = x.Beneficiary.IsDeceased,
                    DateOfDeath = x.Beneficiary.DateOfDeath,
                    IsEligible = x.Beneficiary.IsEligible,
                    RemarkCategory = x.Beneficiary.RemarkCategory,
                    Remarks = x.Beneficiary.Remarks,
                    IsDeleted = x.Beneficiary.IsDeleted
                };
            }).ToList();

            if (filter.SpecificAge.HasValue)
            {
                beneficiaries = beneficiaries
                    .Where(x => x.Age == filter.SpecificAge.Value)
                    .ToList();
            }
            else
            {
                if (filter.AgeFrom.HasValue)
                    beneficiaries = beneficiaries
                        .Where(x => x.Age >= filter.AgeFrom.Value)
                        .ToList();

                if (filter.AgeTo.HasValue)
                    beneficiaries = beneficiaries
                        .Where(x => x.Age <= filter.AgeTo.Value)
                        .ToList();
            }

            var result = new BeneficiarySummaryResultDto
            {
                Beneficiaries = beneficiaries,
                TotalBeneficiaries = beneficiaries.Count,
                TotalMale = beneficiaries.Count(x => x.Sex == 1),
                TotalFemale = beneficiaries.Count(x => x.Sex == 2),

                Age80Count = beneficiaries.Count(x => x.Age >= 80 && x.Age <= 84),
                Age85Count = beneficiaries.Count(x => x.Age >= 85 && x.Age <= 89),
                Age90Count = beneficiaries.Count(x => x.Age >= 90 && x.Age <= 94),
                Age95Count = beneficiaries.Count(x => x.Age >= 95 && x.Age <= 99),
                Age100Count = beneficiaries.Count(x => x.Age >= 100),

                ProvinceCounts = beneficiaries
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

                MunicipalityCounts = beneficiaries
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
    }
}
