using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.UserManagement;

namespace EcaInformationSystem.Application.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly IPendingUserRegistrationRepository _repo;
        private readonly IMunicipalityRepository _municipalityRepo;
        private readonly IRegionService _regionService;
        public UserManagementService(IPendingUserRegistrationRepository repo,
        IMunicipalityRepository municipalityRepo,
        IRegionService regionService)
        {
            _repo = repo;
            _municipalityRepo = municipalityRepo;
            _regionService = regionService;
        }
        public async Task<List<UserListDto>> GetAllUsersAsync()
        {
            var users = await _repo.GetAllAsync();
            // ✅ NEW — resolve all region names once, avoid N+1 lookups per user
            var regions = await _regionService.GetAllAsync();
            var regionNameLookup = regions.ToDictionary(r => r.PsgcCodeRegion, r => r.Name);

            return users.Select(u => MapToDto(u, regionNameLookup)).ToList();
        }

        public async Task<List<UserListDto>> GetAllUsersAsync(int regionCode)
        {
            var users = await _repo.GetAllAsync();
            var regions = await _regionService.GetAllAsync();
            var regionNameLookup = regions.ToDictionary(r => r.PsgcCodeRegion, r => r.Name);

            return users.Where(u => u.Region == regionCode)
                .Select(u => MapToDto(u, regionNameLookup)).ToList();
        }
        public async Task<UserListDto?> GetUserByIdAsync(Guid id)
        {
            var user = await _repo.GetByIdAsync(id);
            if (user is null) return null;

            var regions = await _regionService.GetAllAsync();
            var regionNameLookup = regions.ToDictionary(r => r.PsgcCodeRegion, r => r.Name);

            return MapToDto(user, regionNameLookup);
        }
        public async Task ApproveAsync(
            Guid userId, string role, string? remarks, string approvedBy)
        {
            var user = await _repo.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            var validRoles = new[] { "SuperAdmin", "Admin", "PDO", "Finance", "Viewer" };
            if (!validRoles.Contains(role))
                throw new ArgumentException($"Invalid role: {role}");

            user.ApprovalStatus = 1;
            user.IsActivated = true;
            user.Role = role;
            user.ReviewedAt = DateTime.UtcNow;
            user.ReviewedBy = approvedBy;
            user.Remarks = remarks;

            await _repo.SaveChangesAsync();
        }

        public async Task SetBiometricUserIdAsync(Guid userId, string? biometricUserId, string setBy)
        {
            var user = await _repo.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            user.BiometricUserId = string.IsNullOrWhiteSpace(biometricUserId) ? null : biometricUserId.Trim();
            await _repo.SaveChangesAsync();
        }

        public async Task RejectAsync(Guid userId, string? remarks, string rejectedBy)
        {
            var user = await _repo.GetByIdAsync(userId)
             ?? throw new KeyNotFoundException("User not found.");

            user.ApprovalStatus = 2;
            user.IsActivated = false;
            user.ReviewedAt = DateTime.UtcNow;
            user.ReviewedBy = rejectedBy;
            user.Remarks = remarks;

            await _repo.SaveChangesAsync();
        }
        public async Task DeactivateAsync(Guid userId, string? remarks, string deactivatedBy)
        {
            var user = await _repo.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            user.IsDeactivated = true;
            user.DeactivatedAt = DateTime.UtcNow;
            user.DeactivatedBy = deactivatedBy;
            if (!string.IsNullOrWhiteSpace(remarks))
                user.Remarks = remarks;

            await _repo.SaveChangesAsync();
        }

        public async Task ReactivateAsync(Guid userId, string reactivatedBy)
        {
            var user = await _repo.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            user.IsDeactivated = false;
            user.DeactivatedAt = null;
            user.DeactivatedBy = null;

            await _repo.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid userId)
        {
            _ = await _repo.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            await _repo.DeleteAsync(userId);
        }

        public async Task AssignJurisdictionsAsync(Guid userId, List<int> municipalityCodes, string assignedBy)
        {
            var user = await _repo.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            if (user.Role != "PDO")
                throw new InvalidOperationException("Jurisdiction assignment is only applicable to PDO accounts.");

            var allMunicipalities = await _municipalityRepo.GetAllMunicipalityAsync();
            var allProvinces = await GetProvinceNamesAsync(municipalityCodes, allMunicipalities);

            var newJurisdictions = municipalityCodes.Distinct()
            .Select(code =>
            {
                var muni = allMunicipalities
                 .FirstOrDefault(m => m.PsgcCodeMunicipality == code);

                return new PdoJurisdiction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PsgcCodeMunicipality = code,
                    MunicipalityName = muni?.Name ?? string.Empty,
                    ProvinceName = allProvinces.GetValueOrDefault(code, string.Empty),
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = assignedBy
                };
            }).ToList();

            await _repo.ReplaceJurisdictionsAsync(userId, newJurisdictions);
        }

        public async Task<List<int>> GetJurisdictionCodesAsync(Guid userId)
         => await _repo.GetAssignedMunicipalityCodesAsync(userId);
        #region Private Helpers
        // ── Private helpers ───────────────────────────────────────────────────
        private async Task<Dictionary<int, string>> GetProvinceNamesAsync(
            List<int> municipalityCodes,
            IEnumerable<Municipality> municipalities)
        {
            // Municipality entity has PsgcCodeProvince
            // We need province names — resolve via province codes from the PSGC data
            var result = new Dictionary<int, string>();
            var allProvinces = await _municipalityRepo.GetProvinceNamesForCodesAsync(
                municipalities
                    .Where(m => municipalityCodes.Contains(m.PsgcCodeMunicipality))
                    .Select(m => m.PsgcCodeProvince)
                    .Distinct()
                    .ToList());

            foreach (var code in municipalityCodes)
            {
                var muni = municipalities
                    .FirstOrDefault(m => m.PsgcCodeMunicipality == code);
                if (muni != null && allProvinces.TryGetValue(muni.PsgcCodeProvince, out var pName))
                    result[code] = pName;
            }

            return result;
        }

        private static UserListDto MapToDto(
             PendingUserRegistration u,
             Dictionary<int, string?> regionNameLookup) => new()
             {
                 Id = u.Id,
                 FullName = u.FullName,
                 UserName = u.UserName,
                 Position = u.Position,
                 Role = u.Role,
                 ApprovalStatus = u.ApprovalStatus,
                 IsActivated = u.IsActivated,
                 RequestedAt = u.RequestedAt,
                 ReviewedBy = u.ReviewedBy,
                 Remarks = u.Remarks,
                 Region = u.Region, // ✅ NEW
                 RegionName = u.Region.HasValue && regionNameLookup.TryGetValue(u.Region.Value, out var name)
                 ? name
                 : (u.Region.HasValue ? $"Region {u.Region.Value}" : "Not set"),
                 IsMfaEnabled = u.IsMfaEnabled,
                 BiometricUserId = u.BiometricUserId,
                 IsDeactivated = u.IsDeactivated,
                 DeactivatedAt = u.DeactivatedAt,
                 DeactivatedBy = u.DeactivatedBy,
                 Jurisdictions = u.Jurisdictions.Select(j => new JurisdictionDto
                 {
                     Id = j.Id,
                     PsgcCodeMunicipality = j.PsgcCodeMunicipality,
                     MunicipalityName = j.MunicipalityName,
                     ProvinceName = j.ProvinceName
                 }).ToList()
             };
        #endregion Private Helpers - End
    }
}