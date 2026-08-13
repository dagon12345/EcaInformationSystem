using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;

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
            // Admin, SuperAdmin, and Encoder have no restrictions — Encoders are
            // implicitly assigned every jurisdiction (no PdoJurisdiction rows
            // needed) since they're an office-wide encoding role, not tied to a
            // specific province/municipality like PDO.
            if (role == "Admin" || role == "SuperAdmin" || role == "Encoder")
                return null;

            if (role == "PDO")
            {
                var user = await _repo.GetByUserNameAsync(userName);
                if (user is null)
                    return "User account not found.";

                var jurisdictions = user.Jurisdictions ?? new List<PdoJurisdiction>();

                var allowed = jurisdictions
                    .Select(j => j.PsgcCodeMunicipality)
                    .ToHashSet();

                if (!allowed.Contains(municipalityCode))
                {
                    var jurisdictionNames = jurisdictions
                        .Select(j => j.MunicipalityName)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .OrderBy(n => n);

                    var allowedList = jurisdictionNames.Any()
                        ? string.Join(", ", jurisdictionNames)
                        : "no municipalities";

                    return $"Access denied. You are only authorized to manage records in: {allowedList}.";
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