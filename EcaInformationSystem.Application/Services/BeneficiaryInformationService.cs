using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Services
{
    public class BeneficiaryInformationService : IBeneficiaryInformationService
    {
        private readonly IBeneficiaryInformationRepository _repo;

        public BeneficiaryInformationService(IBeneficiaryInformationRepository repo)
        {
            _repo = repo;
        }

        public async Task<BeneficiaryInformationDto> CreateAsync(CreateBeneficiaryInformationDto dto)
        {
            var beneficiary = new BeneficiaryInformation
            {
                Id = Guid.NewGuid(),
                BatchCode = dto.BatchCode,
                OscaIdNumber = dto.OscaIdNumber,
                NcscRrn = dto.NcscRrn,
                LastName = dto.LastName,
                FirstName = dto.FirstName,
                MiddleName = dto.MiddleName,
                Extension = dto.Extension,
                BirthDate = dto.BirthDate,
                Sex = dto.Sex,
                Region = dto.Region,
                Province = dto.Province,
                Municipality = dto.Municipality,
                Barangay = dto.Barangay,
                isCompliant = dto.isCompliant,
                Validator = dto.Validator,
                ValidationDate = dto.ValidationDate,
                PaymentStatus = dto.PaymentStatus,
                PaymentDate = dto.PaymentDate,
                isDeceased = dto.isDeceased,
                DateOfDeath = dto.DateOfDeath,
                isEligible = dto.isEligible,
                Remarks = dto.Remarks,
                isDeleted = false
            };

            await _repo.AddAsync(beneficiary);
            await _repo.SaveChangesAsync();

            return new BeneficiaryInformationDto
            {
                Id = beneficiary.Id,
                BatchCode = beneficiary.BatchCode,
                OscaIdNumber = beneficiary.OscaIdNumber,
                NcscRrn = beneficiary.NcscRrn,
                LastName = beneficiary.LastName,
                FirstName = beneficiary.FirstName,
                MiddleName = beneficiary.MiddleName,
                Extension = beneficiary.Extension,
                BirthDate = beneficiary.BirthDate,
                Sex = beneficiary.Sex,
                Region = beneficiary.Region,
                Province = beneficiary.Province,
                Municipality = beneficiary.Municipality,
                Barangay = beneficiary.Barangay,
                isCompliant = beneficiary.isCompliant,
                Validator = beneficiary.Validator,
                ValidationDate = beneficiary.ValidationDate,
                PaymentStatus = beneficiary.PaymentStatus,
                PaymentDate = beneficiary.PaymentDate,
                isDeceased = beneficiary.isDeceased,
                DateOfDeath = beneficiary.DateOfDeath,
                isEligible = beneficiary.isEligible,
                Remarks = beneficiary.Remarks,
                isDeleted = beneficiary.isDeleted
            };
        }

        public async Task<IEnumerable<BeneficiaryInformationDto>> GetAllAsync()
        {
            var list = await _repo.GetAllAsync();
            return list.Select(x => new BeneficiaryInformationDto
            {
                Id = x.Id,
                BatchCode = x.BatchCode,
                OscaIdNumber = x.OscaIdNumber,
                NcscRrn = x.NcscRrn,
                LastName = x.LastName,
                FirstName = x.FirstName,
                MiddleName = x.MiddleName,
                Extension = x.Extension,
                BirthDate = x.BirthDate,
                Sex = x.Sex,
                Region = x.Region,
                Province = x.Province,
                Municipality = x.Municipality,
                Barangay = x.Barangay,
                isCompliant = x.isCompliant,
                Validator = x.Validator,
                ValidationDate = x.ValidationDate,
                PaymentStatus = x.PaymentStatus,
                PaymentDate = x.PaymentDate,
                isDeceased = x.isDeceased,
                DateOfDeath = x.DateOfDeath,
                isEligible = x.isEligible,
                Remarks = x.Remarks,
                isDeleted = x.isDeleted
            }).ToList();
        }

        public async Task SoftDeleteAsync(Guid Id)
        {
            var selectedBeneficiary = await _repo.GetByIdAsync(Id);
            if (selectedBeneficiary == null)
                throw new Exception("Beneficiary not found");
            selectedBeneficiary.isDeleted = true;
            await _repo.UpdateAsync(selectedBeneficiary);
            await _repo.SaveChangesAsync();
        }

        public async Task UpdateAsync(Guid Id, BeneficiaryInformationDto dto)
        {
            var beneficiary = await _repo.GetByIdAsync(Id);
            if (beneficiary == null)
                throw new Exception("Beneficiary not found");

            beneficiary.Update(dto.BatchCode, dto.OscaIdNumber, dto.NcscRrn, dto.LastName, dto.FirstName, dto.MiddleName, dto.Extension, dto.BirthDate,
                dto.Sex, dto.Region, dto.Province, dto.Municipality, dto.Barangay, dto.isCompliant, dto.Validator, dto.ValidationDate, dto.PaymentStatus, dto.PaymentDate,
                dto.isDeceased, dto.DateOfDeath, dto.isEligible, dto.Remarks);
            await _repo.UpdateAsync(beneficiary);
            await _repo.SaveChangesAsync();

        }
    }
}
