// Application/Services/CoeService.cs
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class CoeService : ICoeService
    {
        private readonly IBeneficiaryInformationRepository _repo;
        private readonly ILogRepository _logRepository;

        public CoeService(IBeneficiaryInformationRepository repo, ILogRepository logRepository)
        {
            _repo = repo;
            _logRepository = logRepository;
        }

        public async Task<List<CoePreviewGroupDto>> BuildCoePreviewAsync(CoeSettingsDto settings)
        {
            if (settings.Ids == null || !settings.Ids.Any())
                throw new InvalidOperationException("No records selected.");

            var allData = (await _repo.GetByIdsAsync(settings.Ids))
                .DistinctBy(x => x.Id)
                .ToList();

            if (!allData.Any())
                throw new InvalidOperationException("None of the selected records were found.");

            var municipalityGroups = allData
                .GroupBy(x => x.MunicipalityName ?? "Unknown Municipality")
                .OrderBy(g => g.Key);

            var result = new List<CoePreviewGroupDto>();

            foreach (var muniGroup in municipalityGroups)
            {
                var province = muniGroup.First().ProvinceName ?? string.Empty;

                var yearGroups = muniGroup
                    .GroupBy(x => x.MilestoneYear)
                    .Where(g => g.Key > 0)
                    .OrderBy(g => g.Key)
                    .Select(g => new CoeYearGroupDto
                    {
                        MilestoneYear = g.Key,
                        Records = g
                            .OrderBy(x => x.LastName)
                            .ThenBy(x => x.FirstName)
                            .Select((x, idx) => new CoeRecordRowDto
                            {
                                RowNumber = idx + 1,
                                LastName = x.LastName?.ToUpperInvariant() ?? "",
                                FirstName = x.FirstName?.ToUpperInvariant() ?? "",
                                MiddleName = x.MiddleName?.ToUpperInvariant() ?? "",
                                Extension = x.Extension?.ToUpperInvariant() ?? "",
                                BirthDate = x.BirthDate,
                                Age = x.Age,
                                BarangayName = x.BarangayName?.ToUpperInvariant() ?? ""
                            })
                            .ToList()
                    })
                    .ToList();

                result.Add(new CoePreviewGroupDto
                {
                    MunicipalityName = muniGroup.Key,
                    ProvinceName = province,
                    YearGroups = yearGroups
                });
            }

            return result;
        }

        public async Task<byte[]> GenerateCoeAsync(CoeSettingsDto settings, string userName)
        {
            var groups = await BuildCoePreviewAsync(settings);
            var result = CoeWordDocumentBuilder.Build(groups, settings);

            await _logRepository.AddAsync(new Log
            {
                Id = Guid.NewGuid(),
                BeneficiaryInformationId = null,
                Activity = $"Downloaded COE (Certificate of Eligibility) for {settings.Ids.Count} record(s)",
                UserName = userName,
                CreatedAt = DateTime.UtcNow,
                Category = "Download"
            });
            await _logRepository.SaveChangesAsync();

            return result;
        }
    }
}