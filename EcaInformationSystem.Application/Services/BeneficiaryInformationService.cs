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
                IsIndigenousPeople = dto.IsIndigenousPeople,
                IsPersonWithDisability = dto.IsPersonWithDisability,
                CivilStatus = dto.CivilStatus,
                Citizenship = dto.Citizenship,
                Sex = dto.Sex,
                Region = dto.PsgcCodeRegion,
                Province = dto.PsgcCodeProvince,
                Municipality = dto.PsgcCodeMunicipality,
                Barangay = dto.PsgcCodeBarangay,
                isCompliant = dto.isCompliant,
                Validator = dto.Validator,
                ValidationDate = dto.ValidationDate,
                PaymentStatus = dto.PaymentStatus,
                PaymentDate = dto.PaymentDate,
                isDeceased = dto.isDeceased,
                DateOfDeath = dto.DateOfDeath,
                isEligible = dto.isEligible,
                RemarkCategory = dto.RemarkCategory,
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
                IsIndigenousPeople = beneficiary.IsIndigenousPeople,
                IsPersonWithDisability = beneficiary.IsPersonWithDisability,
                CivilStatus = beneficiary.CivilStatus,
                Citizenship = beneficiary.Citizenship,
                PsgcCodeRegion = beneficiary.Region,
                PsgcCodeProvince = beneficiary.Province,
                PsgcCodeMunicipality = beneficiary.Municipality,
                PsgcCodeBarangay = beneficiary.Barangay,
                isCompliant = beneficiary.isCompliant,
                Validator = beneficiary.Validator,
                ValidationDate = beneficiary.ValidationDate,
                PaymentStatus = beneficiary.PaymentStatus,
                PaymentDate = beneficiary.PaymentDate,
                isDeceased = beneficiary.isDeceased,
                DateOfDeath = beneficiary.DateOfDeath,
                isEligible = beneficiary.isEligible,
                RemarkCategory = beneficiary.RemarkCategory,
                Remarks = beneficiary.Remarks,
                isDeleted = beneficiary.isDeleted
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
                dto.Sex, dto.IsIndigenousPeople, dto.IsPersonWithDisability, dto.CivilStatus, dto.Citizenship, dto.PsgcCodeRegion, dto.PsgcCodeProvince, dto.PsgcCodeMunicipality, dto.PsgcCodeBarangay,
                dto.isCompliant, dto.Validator, dto.ValidationDate, dto.PaymentStatus, dto.PaymentDate,
                dto.isDeceased, dto.DateOfDeath, dto.isEligible, dto.RemarkCategory, dto.Remarks);
            await _repo.UpdateAsync(beneficiary);
            await _repo.SaveChangesAsync();

        }
    }
}
