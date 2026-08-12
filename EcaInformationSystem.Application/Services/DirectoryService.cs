using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class DirectoryService : IDirectoryService
    {
        private static readonly string[] DirectoryRoles = { "PDO", "Focal" };

        private readonly IPendingUserRegistrationRepository _userRepo;

        public DirectoryService(IPendingUserRegistrationRepository userRepo)
        {
            _userRepo = userRepo;
        }

        public async Task<List<ProvinceDirectoryGroupDto>> GetProvincialDirectoryAsync()
        {
            var users = await _userRepo.GetAllAsync();

            var eligible = users.Where(u =>
                DirectoryRoles.Contains(u.Role) &&
                !u.IsDeactivated &&
                u.IsActivated &&
                u.ApprovalStatus == 1);

            var groups = new Dictionary<string, List<DirectoryPersonDto>>();

            foreach (var user in eligible)
            {
                var provinces = user.Jurisdictions.Select(j => j.ProvinceName).Distinct();
                foreach (var province in provinces)
                {
                    if (string.IsNullOrWhiteSpace(province)) continue;

                    if (!groups.TryGetValue(province, out var people))
                    {
                        people = new List<DirectoryPersonDto>();
                        groups[province] = people;
                    }

                    // A person can have multiple municipalities in the same province —
                    // only list them once per province, but show all of them.
                    if (people.Any(p => p.UserId == user.Id)) continue;

                    var municipalityNames = user.Jurisdictions
                        .Where(j => j.ProvinceName == province && !string.IsNullOrWhiteSpace(j.MunicipalityName))
                        .Select(j => j.MunicipalityName)
                        .Distinct()
                        .ToList();

                    people.Add(new DirectoryPersonDto
                    {
                        UserId = user.Id,
                        FullName = user.FullName,
                        Role = user.Role,
                        IsOnline = false, // filled in by the caller
                        MunicipalityName = municipalityNames.Count > 0 ? string.Join(", ", municipalityNames) : null
                    });
                }
            }

            return groups
                .OrderBy(g => g.Key)
                .Select(g => new ProvinceDirectoryGroupDto
                {
                    ProvinceName = g.Key,
                    People = g.Value.OrderBy(p => p.Role).ThenBy(p => p.FullName).ToList()
                })
                .ToList();
        }
    }
}
