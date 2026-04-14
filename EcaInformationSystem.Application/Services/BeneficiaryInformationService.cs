using ClosedXML.Excel;
using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace EcaInformationSystem.Application.Services
{
    public class BeneficiaryInformationService : IBeneficiaryInformationService
    {
        private readonly IBeneficiaryInformationRepository _repo;
        private const int DefaultRegionCode = 1600000000;
        public BeneficiaryInformationService(IBeneficiaryInformationRepository repo)
        {
            _repo = repo;
        }

        public async Task<BeneficiaryInformationDto> CreateAsync(CreateBeneficiaryInformationDto dto)
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
                Region = DefaultRegionCode,
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
                PsgcCodeRegion = DefaultRegionCode,
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

        public async Task<BeneficiarySummaryResultDto> GetSummaryAsync(BeneficiaryFilterDto filter)
        {
            filter.PsgcCodeRegion = DefaultRegionCode;
            return await _repo.GetSummaryAsync(filter);
        }

        public async Task SoftDeleteAsync(Guid Id)
        {
            var selectedBeneficiary = await _repo.GetByIdAsync(Id);
            if (selectedBeneficiary == null)
                throw new Exception("Grantee not found");
            selectedBeneficiary.isDeleted = true;
            await _repo.UpdateAsync(selectedBeneficiary);
            await _repo.SaveChangesAsync();
        }

        public async Task UpdateAsync(Guid Id, BeneficiaryInformationDto dto)
        {
            var beneficiary = await _repo.GetByIdAsync(Id);
            if (beneficiary == null)
                throw new Exception("Grantee not found");

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

            beneficiary.Update(dto.BatchCode, dto.OscaIdNumber, dto.NcscRrn, dto.LastName, dto.FirstName, dto.MiddleName, dto.Extension, dto.BirthDate,
                dto.Sex, dto.IsIndigenousPeople, dto.IsPersonWithDisability, dto.CivilStatus, dto.Citizenship, DefaultRegionCode, dto.PsgcCodeProvince, dto.PsgcCodeMunicipality, dto.PsgcCodeBarangay,
                dto.isCompliant, dto.Validator, dto.ValidationDate, dto.PaymentStatus, dto.PaymentDate,
                dto.isDeceased, dto.DateOfDeath, dto.isEligible, dto.RemarkCategory, dto.Remarks);
            await _repo.UpdateAsync(beneficiary);
            await _repo.SaveChangesAsync();

        }

        public async Task<BeneficiaryImportResultDto> ImportExcelAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new Exception("Please upload a valid Excel file.");

            var result = new BeneficiaryImportResultDto();

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet("Sheet1");

            var rows = worksheet.RangeUsed()!.RowsUsed().Skip(1);

            foreach (var row in rows)
            {
                result.TotalRows++;

                try
                {
                    var batchCode = row.Cell(1).GetString().Trim();
                    var oscaIdNumber = row.Cell(3).GetString().Trim();
                    var ncscRrnRaw = row.Cell(4).GetString().Trim();
                    var lastName = row.Cell(5).GetString().Trim();
                    var firstName = row.Cell(6).GetString().Trim();
                    var middleName = row.Cell(7).GetString().Trim();
                    var extension = row.Cell(8).GetString().Trim();

                    // Use the complete BirthDate column from Excel
                    var birthDateRaw = row.Cell(13).GetString().Trim();

                    var sexRaw = row.Cell(15).GetString().Trim();
                    var regionName = row.Cell(16).GetString().Trim();
                    var provinceName = row.Cell(17).GetString().Trim();
                    var municipalityName = row.Cell(18).GetString().Trim();
                    var barangayName = row.Cell(19).GetString().Trim();

                    var complianceRaw = row.Cell(20).GetString().Trim();
                    var validator = row.Cell(21).GetString().Trim();
                    var validationDateRaw = row.Cell(22).GetString().Trim();
                    var paymentStatusRaw = row.Cell(23).GetString().Trim();
                    var paymentDateRaw = row.Cell(24).GetString().Trim();
                    var dateOfDeathRaw = row.Cell(25).GetString().Trim();
                    var coAssessmentRaw = row.Cell(26).GetString().Trim();
                    var remarks = row.Cell(27).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(firstName))
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {result.TotalRows}: First Name is required.");
                        continue;
                    }

                    if (!TryParseExcelDate(birthDateRaw, out var birthDate))
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {result.TotalRows}: Invalid Birth Date '{birthDateRaw}'.");
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
                            result.Errors.Add($"Row {result.TotalRows}: Invalid NCSC RRN '{ncscRrnRaw}'.");
                            continue;
                        }
                    }

                    var regionCode = await _repo.GetRegionCodeByNameAsync(regionName);
                    var provinceCode = await _repo.GetProvinceCodeByNameAsync(provinceName);
                    var municipalityCode = await _repo.GetMunicipalityCodeByNameAsync(municipalityName);
                    var barangayCode = await _repo.GetBarangayCodeByNameAsync(barangayName);

                    if (!regionCode.HasValue)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {result.TotalRows}: Region '{regionName}' not found.");
                        continue;
                    }

                    if (!provinceCode.HasValue)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {result.TotalRows}: Province '{provinceName}' not found.");
                        continue;
                    }

                    if (!municipalityCode.HasValue)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {result.TotalRows}: Municipality '{municipalityName}' not found.");
                        continue;
                    }

                    if (!barangayCode.HasValue)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Row {result.TotalRows}: Barangay '{barangayName}' not found.");
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
                        continue;
                    }

                    var beneficiary = new BeneficiaryInformation
                    {
                        Id = Guid.NewGuid(),
                        BatchCode = string.IsNullOrWhiteSpace(batchCode) ? null : batchCode,
                        OscaIdNumber = string.IsNullOrWhiteSpace(oscaIdNumber) ? null : oscaIdNumber,
                        NcscRrn = ncscRrn,
                        LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName,
                        FirstName = firstName,
                        MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName,
                        Extension = string.IsNullOrWhiteSpace(extension) ? null : extension,
                        BirthDate = birthDate,
                        Sex = MapSex(sexRaw),
                        IsIndigenousPeople = false,
                        IsPersonWithDisability = false,
                        CivilStatus = null,
                        Citizenship = null,
                        Region = regionCode.Value,
                        Province = provinceCode.Value,
                        Municipality = municipalityCode.Value,
                        Barangay = barangayCode.Value,
                        isCompliant = MapCompliance(complianceRaw),
                        Validator = string.IsNullOrWhiteSpace(validator) ? "N/A" : validator,
                        ValidationDate = ParseNullableDate(validationDateRaw) ?? DateTime.Today,
                        PaymentStatus = MapPaymentStatus(paymentStatusRaw),
                        PaymentDate = ParseNullableDate(paymentDateRaw),
                        isDeceased = ParseNullableDate(dateOfDeathRaw).HasValue,
                        DateOfDeath = ParseNullableDate(dateOfDeathRaw),
                        isEligible = MapEligibility(coAssessmentRaw),
                        RemarkCategory = null,
                        Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks,
                        isDeleted = false
                    };

                    await _repo.AddAsync(beneficiary);
                    result.ImportedCount++;
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Row {result.TotalRows}: {ex.Message}");
                }
            }

            await _repo.SaveChangesAsync();
            return result;
        }

        #region Private helpers
        private static string NormalizeName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Trim().ToUpperInvariant();

            normalized = normalized.Replace(".", "");
            normalized = normalized.Replace(",", "");
            normalized = normalized.Replace("-", " ");
            normalized = normalized.Replace("_", " ");
            normalized = normalized.Replace("  ", " ");

            // common PH address abbreviations
            normalized = normalized.Replace("BRGY", "BARANGAY");
            normalized = normalized.Replace("BGY", "BARANGAY");
            normalized = normalized.Replace("MUN.", "MUNICIPALITY");
            normalized = normalized.Replace("CITY OF ", "");
            normalized = normalized.Replace("CITY", "CITY");
            normalized = normalized.Replace("MUNICIPALITY OF ", "");

            // collapse repeated spaces
            while (normalized.Contains("  "))
                normalized = normalized.Replace("  ", " ");

            return normalized.Trim();
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
        "MM-d-yyyy"
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
