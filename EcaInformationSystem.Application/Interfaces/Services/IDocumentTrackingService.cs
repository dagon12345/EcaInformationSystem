using EcaInformationSystem.Shared.DTOs.DocumentTracking;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IDocumentTrackingService
    {
        Task<List<DocumentBatchDto>> GetAllAsync();
        Task<DocumentBatchDto?> GetByIdAsync(Guid id);
        Task<List<UserLookupDto>> GetTaggableUsersAsync();

        Task<DocumentBatchDto> CreateAsync(CreateDocumentBatchDto dto, Guid callerId, string callerName);

        Task<DocumentBatchDto> AcceptAsync(Guid batchId, Guid callerId, string callerName);

        Task<DocumentBatchDto> ReturnToViewerAsync(Guid batchId, Guid callerId, string callerName, string? note);
        Task<DocumentBatchDto> DistributeToPdoAsync(Guid batchId, Guid callerId, string callerName, RelayDocumentDto dto);
        Task<DocumentBatchDto> EndorseToFinanceAsync(Guid batchId, Guid callerId, string callerName, RelayDocumentDto dto);
        Task<DocumentBatchDto> ReturnToPdoForFindingsAsync(Guid batchId, Guid callerId, string callerName, ReturnForFindingsDto dto);
        Task<DocumentBatchDto> ForwardToViewerForScanningAsync(Guid batchId, Guid callerId, string callerName, RelayDocumentDto dto);
        Task<DocumentBatchDto> CompleteAsync(Guid batchId, Guid callerId, string callerName, string? note);

        Task<DocumentBatchDto> ResolveFindingAsync(Guid batchId, Guid rowId, Guid callerId, string callerName, bool isSuperAdmin = false);

        // ── SuperAdmin-only overrides — bypass the holder/status gating ────
        Task DeleteAsync(Guid batchId, string callerName);
        Task<DocumentBatchDto> UpdateHeaderAsync(Guid batchId, UpdateDocumentBatchHeaderDto dto, string callerName);
        Task<DocumentBatchDto> AddRowAsync(Guid batchId, CreateDocumentGranteeRowDto dto, string callerName);
        Task<DocumentBatchDto> UpdateRowAsync(Guid batchId, Guid rowId, CreateDocumentGranteeRowDto dto, string callerName);
        Task<DocumentBatchDto> DeleteRowAsync(Guid batchId, Guid rowId, string callerName);

        // ── Admin/SuperAdmin — correct a wrongly-tagged recipient without
        // otherwise disturbing the batch's current workflow stage. ─────────
        Task<DocumentBatchDto> ReassignRecipientAsync(Guid batchId, Guid newRecipientUserId, Guid callerId, string callerName);
    }
}
