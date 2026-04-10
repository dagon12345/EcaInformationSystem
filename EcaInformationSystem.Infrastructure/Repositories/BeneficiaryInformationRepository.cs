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

        public async Task<IEnumerable<BeneficiaryInformationDto>> GetAllAsync()
        {
            //This is where our joining of tables will be done, we will use the Include method to include the related tables
            var result = await (from b in _context.BeneficiaryInformations
                                join region in _context.Regions on b.Region equals region.PsgcCodeRegion into
                                regionJoin from region in regionJoin.DefaultIfEmpty()
                                where !b.isDeleted select new BeneficiaryInformationDto
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
                                    Sex = b.Sex,
                                    PsgcCodeRegion = b.Region,
                                    Region = region.Name,
                                    Province = b.Province,
                                    Municipality = b.Municipality,
                                    Barangay = b.Barangay,
                                    isCompliant = b.isCompliant,
                                    Validator = b.Validator,
                                    ValidationDate = b.ValidationDate,
                                    PaymentStatus = b.PaymentStatus,
                                    PaymentDate = b.PaymentDate,
                                    isDeceased = b.isDeceased,
                                    DateOfDeath = b.DateOfDeath,
                                    isEligible = b.isEligible,
                                    Remarks = b.Remarks,
                                    isDeleted = b.isDeleted
                                })
                                .AsNoTracking()
                                .ToListAsync();
            return result;
        }

        public async Task<BeneficiaryInformation?> GetByIdAsync(Guid id)
        {
            return await _context.BeneficiaryInformations.FirstOrDefaultAsync(x => x.Id == id);
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
