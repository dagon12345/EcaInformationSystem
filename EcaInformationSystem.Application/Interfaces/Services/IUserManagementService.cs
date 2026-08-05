using EcaInformationSystem.Shared.DTOs.UserManagement;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IUserManagementService
    {
        Task<List<UserListDto>> GetAllUsersAsync();

        // Region-scoped variant — used by the Attendance Management biometric
        // assignment screen, so a region's admins only see (and can only link
        // biometric IDs for) their own region's users. The unfiltered
        // overload above stays as-is for other callers (general User
        // Management, Document Tracking) that aren't region-scoped today.
        Task<List<UserListDto>> GetAllUsersAsync(int regionCode);
        Task<UserListDto?> GetUserByIdAsync(Guid id);
        Task ApproveAsync(Guid userId, string role, string? remarks, string approvedBy);
        Task RejectAsync(Guid userId, string? remarks, string rejectedBy);
        Task AssignJurisdictionsAsync(Guid userId, List<int> municipalityCodes,
            string assignedBy);
        Task<List<int>> GetJurisdictionCodesAsync(Guid userId);
        Task SetBiometricUserIdAsync(Guid userId, string? biometricUserId, string setBy);
    }
}