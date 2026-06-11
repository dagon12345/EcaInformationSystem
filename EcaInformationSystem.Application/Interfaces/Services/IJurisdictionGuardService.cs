namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IJurisdictionGuardService
    {
        /// <summary>
        /// Returns null if allowed, or an error message if blocked.
        /// </summary>
        Task<string?> CheckAsync(string userName, string role, int municipalityCode);

        Task<List<int>> GetAllowedMunicipalityCodesAsync(string userName);
    }
}