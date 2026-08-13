using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class DirectoryService : IDirectoryService
    {
        // ✅ "Admin" included so a SuperAdmin can optionally assign an Admin
        // province-level jurisdictions (via UserManagementService.AssignJurisdictionsAsync)
        // purely to segregate them into this directory by province — it does NOT
        // restrict what an Admin can actually access (JurisdictionGuardService still
        // gives Admin unrestricted write access regardless of this assignment).
        private static readonly string[] DirectoryRoles = { "PDO", "Focal", "Admin" };
        private const string AdminInvitedLabel = "Other Invites (Admin-Invited)";

        private readonly IPendingUserRegistrationRepository _userRepo;
        private readonly IFocalInviteRepository _focalInviteRepo;

        public DirectoryService(IPendingUserRegistrationRepository userRepo, IFocalInviteRepository focalInviteRepo)
        {
            _userRepo = userRepo;
            _focalInviteRepo = focalInviteRepo;
        }

        public async Task<List<ProvinceDirectoryGroupDto>> GetProvincialDirectoryAsync()
        {
            var users = await _userRepo.GetAllAsync();

            var eligible = users.Where(u =>
                DirectoryRoles.Contains(u.Role) &&
                !u.IsDeactivated &&
                u.IsActivated &&
                u.ApprovalStatus == 1).ToList();

            var usersById = users.ToDictionary(u => u.Id);

            // Every accepted focal invite's inviter — used to branch each
            // Focal under the PDO (or "Admin-Invited" bucket) that brought
            // them in, since that link isn't stored anywhere on the focal's
            // own account/jurisdiction rows.
            var inviterMap = (await _focalInviteRepo.GetAcceptedInviterMapAsync())
                .GroupBy(x => x.ResultingUserId)
                .ToDictionary(g => g.Key, g => g.First().InvitedByUserId);

            var groups = new Dictionary<string, List<DirectoryPersonDto>>();
            // ✅ FIXED — keyed by plain Guid, not Guid?. Dictionary<Guid?, T>
            // throws ArgumentNullException on a null key even though Guid? is
            // a nullable VALUE type — a well-known .NET gotcha (Dictionary
            // rejects a null key argument outright, regardless of TKey's
            // nullability). Guid.Empty is the "Admin-Invited" bucket sentinel
            // instead; PdoDirectoryBranchDto.PdoUserId is still null in that
            // case for the DTO consumers.
            var pdoBranchesByProvince = new Dictionary<string, Dictionary<Guid, PdoDirectoryBranchDto>>();

            Dictionary<Guid, PdoDirectoryBranchDto> BranchesFor(string province)
            {
                if (!pdoBranchesByProvince.TryGetValue(province, out var branches))
                {
                    branches = new Dictionary<Guid, PdoDirectoryBranchDto>();
                    pdoBranchesByProvince[province] = branches;
                }
                return branches;
            }

            DirectoryPersonDto ToPersonDto(EcaInformationSystem.Domain.Entities.PendingUserRegistration user, string province)
            {
                var municipalityNames = user.Jurisdictions
                    .Where(j => j.ProvinceName == province && !string.IsNullOrWhiteSpace(j.MunicipalityName))
                    .Select(j => j.MunicipalityName)
                    .Distinct()
                    .ToList();

                return new DirectoryPersonDto
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Role = user.Role,
                    IsOnline = false, // filled in by the caller
                    MunicipalityName = municipalityNames.Count > 0 ? string.Join(", ", municipalityNames) : null
                };
            }

            // ── Pass 1: flat People list (unchanged) + a branch entry for every PDO ──
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
                    if (!people.Any(p => p.UserId == user.Id))
                        people.Add(ToPersonDto(user, province));

                    if (user.Role == "PDO")
                    {
                        var branches = BranchesFor(province);
                        if (!branches.ContainsKey(user.Id))
                        {
                            branches[user.Id] = new PdoDirectoryBranchDto
                            {
                                PdoUserId = user.Id,
                                PdoName = user.FullName,
                                IsOnline = false // filled in by the caller
                            };
                        }
                    }
                }
            }

            // ── Pass 2: nest every Focal under their inviting PDO's branch
            // (or the "Admin-Invited" bucket) within the Focal's OWN province. ──
            foreach (var focal in eligible.Where(u => u.Role == "Focal"))
            {
                var province = focal.Jurisdictions.Select(j => j.ProvinceName)
                    .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
                if (string.IsNullOrWhiteSpace(province)) continue;

                var branches = BranchesFor(province);
                var focalDto = ToPersonDto(focal, province);

                var branchKey = Guid.Empty; // default: Admin-Invited bucket
                Guid? pdoUserId = null;
                string branchName = AdminInvitedLabel;

                if (inviterMap.TryGetValue(focal.Id, out var inviterId) &&
                    usersById.TryGetValue(inviterId, out var inviter) &&
                    inviter.Role == "PDO")
                {
                    branchKey = inviter.Id;
                    pdoUserId = inviter.Id;
                    branchName = inviter.FullName;
                }

                if (!branches.TryGetValue(branchKey, out var branch))
                {
                    branch = new PdoDirectoryBranchDto
                    {
                        PdoUserId = pdoUserId,
                        PdoName = branchName,
                        IsOnline = false
                    };
                    branches[branchKey] = branch;
                }

                branch.Focals.Add(focalDto);
            }

            return groups
                .OrderBy(g => g.Key)
                .Select(g => new ProvinceDirectoryGroupDto
                {
                    ProvinceName = g.Key,
                    People = g.Value.OrderBy(p => p.Role).ThenBy(p => p.FullName).ToList(),
                    PdoBranches = pdoBranchesByProvince.TryGetValue(g.Key, out var branches)
                        ? branches.Values
                            // Real PDOs first (alphabetical), "Admin-Invited" bucket last.
                            .OrderBy(b => b.PdoUserId == null)
                            .ThenBy(b => b.PdoName)
                            .Select(b => { b.Focals = b.Focals.OrderBy(f => f.FullName).ToList(); return b; })
                            .ToList()
                        : new List<PdoDirectoryBranchDto>()
                })
                .ToList();
        }
    }
}
