using EcaInformationSystem.Shared.DTOs.UserManagement;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IUserManagementService
    {
        Task<List<UserListDto>> GetAllUsersAsync();
        Task<UserListDto?> GetUserByIdAsync(Guid id);
        Task ApproveAsync(Guid userId, string role, string? remarks, string approvedBy);
        Task RejectAsync(Guid userId, string? remarks, string rejectedBy);
        Task AssignJurisdictionsAsync(Guid userId, List<int> municipalityCodes,
            string assignedBy);
        Task<List<int>> GetJurisdictionCodesAsync(Guid userId);
    }
}