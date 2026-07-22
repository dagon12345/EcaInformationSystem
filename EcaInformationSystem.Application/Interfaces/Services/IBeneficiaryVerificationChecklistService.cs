// Application/Interfaces/Services/IBeneficiaryVerificationChecklistService.cs
using EcaInformationSystem.Shared.DTOs;

public interface IBeneficiaryVerificationChecklistService
{
    Task<BeneficiaryVerificationChecklistDto?> GetByBeneficiaryIdAsync(Guid beneficiaryId);
    Task<(bool Success, string? Error)> UpsertAsync(Guid beneficiaryId, BeneficiaryVerificationChecklistDto dto, string userName);
}