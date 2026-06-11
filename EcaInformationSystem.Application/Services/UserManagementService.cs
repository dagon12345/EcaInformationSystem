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

        public UserManagementService(IPendingUserRegistrationRepository repo,
        IMunicipalityRepository municipalityRepo)
        {
            _repo = repo;
            _municipalityRepo = municipalityRepo;
        }
        public async Task<List<UserListDto>> GetAllUsersAsync()
        {
            var users = await _repo.GetAllAsync();
            return users.Select(MapToDto).ToList();
        }
        public async Task<UserListDto?> GetUserByIdAsync(Guid id)
        {
            var user = await _repo.GetByIdAsync(id);
            return user is null ? null : MapToDto(user);
        }
        public async Task ApproveAsync(
            Guid userId, string role, string? remarks, string approvedBy)
        {
            var user = await _repo.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            var validRoles = new[] { "SuperAdmin", "Admin", "PDO", "Viewer" };
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

        private static UserListDto MapToDto(PendingUserRegistration u) => new()
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