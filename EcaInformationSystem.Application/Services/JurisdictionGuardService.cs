using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;

namespace EcaInformationSystem.Application.Services
{
    public class JurisdictionGuardService : IJurisdictionGuardService
    {
        private readonly IPendingUserRegistrationRepository _repo;

        public JurisdictionGuardService(IPendingUserRegistrationRepository repo)
        {
            _repo = repo;
        }
        public async Task<string?> CheckAsync(string userName, string role, int municipalityCode)
        {
            // Admin and SuperAdmin have no restrictions
            if (role == "Admin" || role == "SuperAdmin")
                return null;

            // PDO — check jurisdiction
            if (role == "PDO")
            {
                var user = await _repo.GetByUserNameAsync(userName);
                if (user is null)
                    return "User account not found.";

                var allowed = user.Jurisdictions
                    .Select(j => j.PsgcCodeMunicipality)
                    .ToHashSet();

                if (!allowed.Contains(municipalityCode))
                {
                    var jurisdictionNames = user.Jurisdictions
                        .Select(j => j.MunicipalityName)
                        .OrderBy(n => n);

                    var allowedList = string.Join(", ", jurisdictionNames);

                    return $"Access denied. You are only authorized to manage records " +
                           $"in: {allowedList}.";
                }

                return null;
            }

            // Viewer — read only, block all writes
            return "Your account does not have permission to modify records.";
        }

        public async Task<List<int>> GetAllowedMunicipalityCodesAsync(string userName)
        {
            var user = await _repo.GetByUserNameAsync(userName);
            if (user is null) return new();

            return user.Jurisdictions
                .Select(j => j.PsgcCodeMunicipality)
                .ToList();
        }
    }
}