using EcaInformationSystem.Shared.DTOs.ApplicationTracking;
using EcaInformationSystem.Shared.DTOs.Common;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IApplicationTrackingService
    {
        Task<List<ApplicationBatchDto>> GetAllAsync();
        Task<ApplicationBatchDto?> GetByIdAsync(Guid id);
        Task<List<UserLookupDto>> GetTaggableUsersAsync();

        Task<ApplicationBatchDto> CreateAsync(CreateApplicationBatchDto dto, Guid callerId, string callerName, string? callerRole = null);

        Task<ApplicationBatchDto> AcceptAsync(Guid batchId, Guid callerId, string callerName);

        Task<ApplicationBatchDto> ReturnToViewerAsync(Guid batchId, Guid callerId, string callerName, string? note, bool isFinding = false, string? findingJustification = null, string? callerRole = null);
        Task<ApplicationBatchDto> DistributeToPdoAsync(Guid batchId, Guid callerId, string callerName, RelayApplicationDto dto, string? callerRole = null);
        Task<ApplicationBatchDto> EndorseToFinanceAsync(Guid batchId, Guid callerId, string callerName, RelayApplicationDto dto, string? callerRole = null);
        Task<ApplicationBatchDto> ReturnToPdoForFindingsAsync(Guid batchId, Guid callerId, string callerName, ReturnApplicationForFindingsDto dto);
        Task<ApplicationBatchDto> ForwardToViewerForScanningAsync(Guid batchId, Guid callerId, string callerName, RelayApplicationDto dto, string? callerRole = null);
        Task<ApplicationBatchDto> CompleteAsync(Guid batchId, Guid callerId, string callerName, string? note);

        Task<ApplicationBatchDto> ResolveFindingAsync(Guid batchId, Guid rowId, Guid callerId, string callerName, bool isSuperAdmin = false);

        // ── SuperAdmin-only overrides — bypass the holder/status gating ────
        Task DeleteAsync(Guid batchId, string callerName);
        Task<ApplicationBatchDto> UpdateHeaderAsync(Guid batchId, UpdateApplicationBatchHeaderDto dto, string callerName);
        Task<ApplicationBatchDto> AddRowAsync(Guid batchId, CreateApplicationGranteeRowDto dto, string callerName);
        Task<ApplicationBatchDto> UpdateRowAsync(Guid batchId, Guid rowId, CreateApplicationGranteeRowDto dto, string callerName);
        Task<ApplicationBatchDto> DeleteRowAsync(Guid batchId, Guid rowId, string callerName);

        // ── Admin/SuperAdmin — correct a wrongly-tagged recipient without
        // otherwise disturbing the batch's current workflow stage. ─────────
        Task<ApplicationBatchDto> ReassignRecipientAsync(Guid batchId, Guid newRecipientUserId, Guid callerId, string callerName);

        // Only SuperAdmin can correct any relay history entry's Note/finding;
        // any other role (including plain Admin) can only correct/clear the
        // entry they raised themselves.
        Task<ApplicationBatchDto> UpdateTransferAsync(Guid batchId, Guid transferId, UpdateApplicationTransferNoteDto dto, Guid callerId, string callerName, bool isSuperAdmin, string? callerRole = null);
    }
}
