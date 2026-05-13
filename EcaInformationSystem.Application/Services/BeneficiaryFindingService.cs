using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace EcaInformationSystem.Application.Services
{
    public class BeneficiaryFindingService : IBeneficiaryFindingService
    {
        private readonly IBeneficiaryFindingRepository _beneficiaryFindingRepository;
        private readonly IBeneficiaryInformationRepository _beneficiaryInformationRepository;
        private readonly ILogRepository _logRepository;
        private readonly IMemoryCache _memoryCache;
        public BeneficiaryFindingService(IBeneficiaryFindingRepository beneficiaryFindingRepository, IBeneficiaryInformationRepository beneficiaryInformationRepository, 
            ILogRepository logRepository, IMemoryCache memoryCache)
        {
            _beneficiaryFindingRepository = beneficiaryFindingRepository;
            _beneficiaryInformationRepository = beneficiaryInformationRepository;
            _logRepository = logRepository;
            _memoryCache = memoryCache;
        }

        public async Task<BeneficiaryFindingDto> UpsertAsync(Guid beneficiaryId, UpsertBeneficiaryFindingDto dto, string userName)
        {
            var beneficiary = await _beneficiaryInformationRepository.GetEntityByIdAsync(beneficiaryId)
                ?? throw new Exception(CommonConstants.GranteeNotFound);

            if (dto.FindingStatus == (int)FindingStatusEnum.Unresolved
                && string.IsNullOrWhiteSpace(dto.FindingRemarks))
                throw new Exception("Remarks are required when finding is Unresolved.");

            //Used the tracked version to ensure proper change tracking and logging of updates
            var existing = await _beneficiaryFindingRepository.GetByBeneficiaryIdAsync(beneficiaryId);
            BeneficiaryFinding finding;

            if (existing == null)
            {
                finding = new BeneficiaryFinding
                {
                    Id = Guid.NewGuid(),
                    BeneficiaryInformationId = beneficiaryId,
                    FindingStatus = dto.FindingStatus,
                    FindingRemarks = dto.FindingStatus == (int)FindingStatusEnum.Unresolved
                        ? dto.FindingRemarks
                        : null,
                    CreatedAt = DateTime.UtcNow
                };
                await _beneficiaryFindingRepository.AddAsync(finding);

                await _logRepository.AddAsync(new Log
                {
                    Id = Guid.NewGuid(),
                    BeneficiaryInformationId = beneficiaryId,
                    Activity = $"Finding created — Status: {MapFindingStatusLabel(dto.FindingStatus)}",
                    UserName = userName,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                var oldStatus = existing.FindingStatus;
                var oldRemarks = existing.FindingRemarks;

                existing.Update(dto.FindingStatus, dto.FindingRemarks);

                var changes = new List<string>();
                if (oldStatus != dto.FindingStatus)
                    changes.Add($"{CommonConstants.Status}: '{MapFindingStatusLabel(oldStatus)}' → '{MapFindingStatusLabel(dto.FindingStatus)}'");
                if (oldRemarks != dto.FindingRemarks)
                    changes.Add($"{CommonConstants.Remarks.ToTitleCase()}: '{oldRemarks ?? "—"}' → '{dto.FindingRemarks ?? "—"}'");

                if (changes.Any())
                {
                    await _logRepository.AddAsync(new Log
                    {
                        Id = Guid.NewGuid(),
                        BeneficiaryInformationId = beneficiaryId,
                        Activity = $"Finding updated — {string.Join("; ", changes)}",
                        UserName = userName,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                finding = existing;
            }

            await _beneficiaryFindingRepository.SaveChangesAsync();
            InvalidateSummaryCache();
            return MapToDto(finding);
        }

        private void InvalidateSummaryCache()
        {
            var newVersion = Guid.NewGuid().ToString();
            _memoryCache.Set(CommonConstants.SummaryCacheVersionKey, newVersion);
        }

        private static string MapFindingStatusLabel(int status) => status switch
        {
            0 => FindingStatusEnum.NA.ToString(),
            1 => FindingStatusEnum.Solved.ToString(),
            2 => FindingStatusEnum.Unresolved.ToString(),
            _ => CommonConstants.Unknown
        };
        public async Task<BeneficiaryFindingDto?> GetByBeneficiaryIdAsync(Guid beneficiaryId)
        {
            //As no tracking is used here, it won't interfere with the tracked entity in UpsertAsync and is suitable for read-only retrieval
            var finding = await _beneficiaryFindingRepository.GetByBeneficiaryIdAsNoTrackingAsync(beneficiaryId);
            return finding == null ? null : MapToDto(finding);
        }
        private static BeneficiaryFindingDto MapToDto(BeneficiaryFinding f) => new()
        {
            Id = f.Id,
            BeneficiaryInformationId = f.BeneficiaryInformationId,
            FindingStatus = f.FindingStatus,
            FindingRemarks = f.FindingRemarks,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        };
    }
}
