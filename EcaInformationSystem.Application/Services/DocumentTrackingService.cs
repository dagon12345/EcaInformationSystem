using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.DocumentTracking;

namespace EcaInformationSystem.Application.Services
{
    public class DocumentTrackingService : IDocumentTrackingService
    {
        private readonly IDocumentTrackingRepository _repo;
        private readonly IPsgcNameCache _psgcNameCache;
        private readonly IUserManagementService _userManagementService;
        private readonly ILogRepository _logRepo;

        public DocumentTrackingService(
            IDocumentTrackingRepository repo,
            IPsgcNameCache psgcNameCache,
            IUserManagementService userManagementService,
            ILogRepository logRepo)
        {
            _repo = repo;
            _psgcNameCache = psgcNameCache;
            _userManagementService = userManagementService;
            _logRepo = logRepo;
        }

        public async Task<List<DocumentBatchDto>> GetAllAsync()
        {
            var batches = await _repo.GetAllAsync();
            return batches.Select(MapToDto).ToList();
        }

        public async Task<DocumentBatchDto?> GetByIdAsync(Guid id)
        {
            var batch = await _repo.GetByIdAsync(id);
            return batch is null ? null : MapToDto(batch);
        }

        public async Task<List<UserLookupDto>> GetTaggableUsersAsync()
        {
            var users = await _userManagementService.GetAllUsersAsync();
            return users
                .Where(u => u.ApprovalStatus == (int)Domain.Common.Enum.ApprovalStatus.Approved && u.IsActivated)
                .OrderBy(u => u.FullName)
                .Select(u => new UserLookupDto { Id = u.Id, FullName = u.FullName, Role = u.Role })
                .ToList();
        }

        public async Task<DocumentBatchDto> CreateAsync(CreateDocumentBatchDto dto, Guid callerId, string callerName)
        {
            if (dto.Rows is null || dto.Rows.Count == 0)
                throw new InvalidOperationException("At least one grantee row is required.");

            if (dto.RecipientUserId == Guid.Empty)
                throw new InvalidOperationException("A recipient must be tagged.");

            if (dto.RecipientUserId == callerId)
                throw new InvalidOperationException("You cannot endorse a document batch to yourself.");

            var recipientName = await ResolveUserNameAsync(dto.RecipientUserId);
            var now = DateTime.UtcNow;

            var batch = new DocumentBatch
            {
                Id = Guid.NewGuid(),
                PsgcCodeProvince = dto.PsgcCodeProvince,
                PsgcCodeMunicipality = dto.PsgcCodeMunicipality,
                MilestoneYear = dto.MilestoneYear,
                DateReceived = dto.DateReceived,
                CreatedByUserId = callerId,
                CreatedByName = callerName,
                CreatedAt = now,
                CurrentStatus = DocumentTrackingStatus.EndorsedByViewer,
                CurrentHolderUserId = dto.RecipientUserId,
                CurrentHolderName = recipientName,
                CurrentLegAcceptedAt = null
            };

            var sort = 0;
            foreach (var row in dto.Rows)
            {
                batch.Rows.Add(new DocumentGranteeRow
                {
                    Id = Guid.NewGuid(),
                    DocumentBatchId = batch.Id,
                    FirstName = row.FirstName.Trim(),
                    MiddleName = string.IsNullOrWhiteSpace(row.MiddleName) ? null : row.MiddleName.Trim(),
                    LastName = row.LastName.Trim(),
                    Extension = string.IsNullOrWhiteSpace(row.Extension) ? null : row.Extension.Trim(),
                    SortOrder = sort++
                });
            }

            batch.Transfers.Add(new DocumentTransfer
            {
                Id = Guid.NewGuid(),
                DocumentBatchId = batch.Id,
                Status = DocumentTrackingStatus.EndorsedByViewer,
                FromUserId = callerId,
                FromUserName = callerName,
                ToUserId = dto.RecipientUserId,
                ToUserName = recipientName,
                RelayedAt = now,
                AcceptedAt = null,
                Note = dto.Note
            });

            await LogActivityAsync(callerName, batch, $"Logged a new document batch and endorsed it to {recipientName}");
            await _repo.AddAsync(batch);
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> AcceptAsync(Guid batchId, Guid callerId, string callerName)
        {
            var batch = await LoadAsync(batchId);

            if (batch.CurrentStatus == DocumentTrackingStatus.Completed)
                throw new InvalidOperationException("This document batch has already been completed.");

            if (batch.CurrentHolderUserId != callerId)
                throw new InvalidOperationException("This document was not tagged to you.");

            if (batch.CurrentLegAcceptedAt is not null)
                throw new InvalidOperationException("You have already accepted this document.");

            var now = DateTime.UtcNow;
            batch.CurrentLegAcceptedAt = now;

            var latestTransfer = batch.Transfers
                .Where(t => t.Status == batch.CurrentStatus && t.ToUserId == callerId)
                .OrderByDescending(t => t.RelayedAt)
                .FirstOrDefault();
            if (latestTransfer is not null)
                latestTransfer.AcceptedAt = now;

            await LogActivityAsync(callerName, batch, "Accepted a document batch");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> ReturnToViewerAsync(Guid batchId, Guid callerId, string callerName, string? note)
        {
            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            EnsureStatus(batch, DocumentTrackingStatus.EndorsedByViewer);

            Advance(batch, DocumentTrackingStatus.ReturnedToViewer, callerId, callerName,
                batch.CreatedByUserId, batch.CreatedByName, note);

            await LogActivityAsync(callerName, batch, "Returned a document batch to the Viewer");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> DistributeToPdoAsync(Guid batchId, Guid callerId, string callerName, RelayDocumentDto dto)
        {
            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            EnsureStatus(batch, DocumentTrackingStatus.ReturnedToViewer);

            var toName = await ResolveUserNameAsync(dto.ToUserId);
            Advance(batch, DocumentTrackingStatus.DistributedToPdo, callerId, callerName, dto.ToUserId, toName, dto.Note);

            await LogActivityAsync(callerName, batch, $"Distributed a document batch to PDO {toName}");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> EndorseToFinanceAsync(Guid batchId, Guid callerId, string callerName, RelayDocumentDto dto)
        {
            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            if (batch.CurrentStatus != DocumentTrackingStatus.DistributedToPdo
                && batch.CurrentStatus != DocumentTrackingStatus.ReturnedToPdoForFindings)
            {
                throw new InvalidOperationException("This document is not currently held by a PDO.");
            }

            var toName = await ResolveUserNameAsync(dto.ToUserId);
            Advance(batch, DocumentTrackingStatus.EndorsedToFinance, callerId, callerName, dto.ToUserId, toName, dto.Note);

            await LogActivityAsync(callerName, batch, $"Endorsed a document batch to Finance {toName}");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> ReturnToPdoForFindingsAsync(Guid batchId, Guid callerId, string callerName, ReturnForFindingsDto dto)
        {
            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            EnsureStatus(batch, DocumentTrackingStatus.EndorsedToFinance);

            if (dto.GranteeRowIds is null || dto.GranteeRowIds.Count == 0)
                throw new InvalidOperationException("Select at least one grantee to tag with a finding.");

            var toName = await ResolveUserNameAsync(dto.ToUserId);
            Advance(batch, DocumentTrackingStatus.ReturnedToPdoForFindings, callerId, callerName, dto.ToUserId, toName, dto.Note);

            var now = DateTime.UtcNow;
            foreach (var rowId in dto.GranteeRowIds)
            {
                var row = batch.Rows.FirstOrDefault(r => r.Id == rowId);
                if (row is null) continue;

                row.HasFinding = true;
                row.FindingNote = dto.Note;
                row.FindingSetAt = now;
                row.FindingSetByName = callerName;
                row.FindingResolvedAt = null;
            }

            await LogActivityAsync(callerName, batch, $"Returned a document batch to PDO {toName} with {dto.GranteeRowIds.Count} finding(s) flagged");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> ForwardToViewerForScanningAsync(Guid batchId, Guid callerId, string callerName, RelayDocumentDto dto)
        {
            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            EnsureStatus(batch, DocumentTrackingStatus.EndorsedToFinance);

            var toName = await ResolveUserNameAsync(dto.ToUserId);
            Advance(batch, DocumentTrackingStatus.ForwardedToViewerForScanning, callerId, callerName, dto.ToUserId, toName, dto.Note);

            await LogActivityAsync(callerName, batch, $"Forwarded a document batch to Viewer {toName} for scanning");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> CompleteAsync(Guid batchId, Guid callerId, string callerName, string? note)
        {
            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            EnsureStatus(batch, DocumentTrackingStatus.ForwardedToViewerForScanning);

            var now = DateTime.UtcNow;
            batch.CurrentStatus = DocumentTrackingStatus.Completed;
            batch.CurrentLegAcceptedAt = now;

            _repo.AttachNewTransfer(new DocumentTransfer
            {
                Id = Guid.NewGuid(),
                DocumentBatchId = batch.Id,
                Status = DocumentTrackingStatus.Completed,
                FromUserId = callerId,
                FromUserName = callerName,
                ToUserId = callerId,
                ToUserName = callerName,
                RelayedAt = now,
                AcceptedAt = now,
                Note = note
            });

            await LogActivityAsync(callerName, batch, "Marked a document batch as completed / scanned");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> ResolveFindingAsync(Guid batchId, Guid rowId, Guid callerId, string callerName, bool isSuperAdmin = false)
        {
            var batch = await LoadAsync(batchId);

            if (!isSuperAdmin && batch.CurrentHolderUserId != callerId)
                throw new InvalidOperationException("Only the current holder of this document batch can resolve a finding.");

            var row = batch.Rows.FirstOrDefault(r => r.Id == rowId);
            if (row is null)
                throw new InvalidOperationException("Grantee row not found.");

            row.HasFinding = false;
            row.FindingResolvedAt = DateTime.UtcNow;

            await LogActivityAsync(callerName, batch, $"Resolved a finding on grantee row '{row.FirstName} {row.LastName}'");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        // ── SuperAdmin-only overrides ────────────────────────────────────────

        public async Task DeleteAsync(Guid batchId, string callerName)
        {
            var batch = await LoadAsync(batchId);
            await LogActivityAsync(callerName, batch, "Deleted a document batch");
            await _repo.DeleteAsync(batch);
        }

        public async Task<DocumentBatchDto> UpdateHeaderAsync(Guid batchId, UpdateDocumentBatchHeaderDto dto, string callerName)
        {
            var batch = await LoadAsync(batchId);

            batch.PsgcCodeProvince = dto.PsgcCodeProvince;
            batch.PsgcCodeMunicipality = dto.PsgcCodeMunicipality;
            batch.MilestoneYear = dto.MilestoneYear;
            batch.DateReceived = dto.DateReceived;

            await LogActivityAsync(callerName, batch, "Edited a document batch's details");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> AddRowAsync(Guid batchId, CreateDocumentGranteeRowDto dto, string callerName)
        {
            if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
                throw new InvalidOperationException("First and Last name are required.");

            var batch = await LoadAsync(batchId);
            var nextSort = batch.Rows.Any() ? batch.Rows.Max(r => r.SortOrder) + 1 : 0;

            _repo.AttachNewRow(new DocumentGranteeRow
            {
                Id = Guid.NewGuid(),
                DocumentBatchId = batch.Id,
                FirstName = dto.FirstName.Trim(),
                MiddleName = string.IsNullOrWhiteSpace(dto.MiddleName) ? null : dto.MiddleName.Trim(),
                LastName = dto.LastName.Trim(),
                Extension = string.IsNullOrWhiteSpace(dto.Extension) ? null : dto.Extension.Trim(),
                SortOrder = nextSort
            });

            await LogActivityAsync(callerName, batch, $"Added grantee row '{dto.FirstName.Trim()} {dto.LastName.Trim()}' to a document batch");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> UpdateRowAsync(Guid batchId, Guid rowId, CreateDocumentGranteeRowDto dto, string callerName)
        {
            if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
                throw new InvalidOperationException("First and Last name are required.");

            var batch = await LoadAsync(batchId);
            var row = batch.Rows.FirstOrDefault(r => r.Id == rowId);
            if (row is null)
                throw new InvalidOperationException("Grantee row not found.");

            row.FirstName = dto.FirstName.Trim();
            row.MiddleName = string.IsNullOrWhiteSpace(dto.MiddleName) ? null : dto.MiddleName.Trim();
            row.LastName = dto.LastName.Trim();
            row.Extension = string.IsNullOrWhiteSpace(dto.Extension) ? null : dto.Extension.Trim();

            await LogActivityAsync(callerName, batch, $"Edited grantee row '{row.FirstName} {row.LastName}' in a document batch");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> DeleteRowAsync(Guid batchId, Guid rowId, string callerName)
        {
            var batch = await LoadAsync(batchId);
            var row = batch.Rows.FirstOrDefault(r => r.Id == rowId);
            if (row is null)
                throw new InvalidOperationException("Grantee row not found.");

            if (batch.Rows.Count == 1)
                throw new InvalidOperationException("A document batch must have at least one grantee row.");

            batch.Rows.Remove(row);

            await LogActivityAsync(callerName, batch, $"Removed grantee row '{row.FirstName} {row.LastName}' from a document batch");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<DocumentBatchDto> ReassignRecipientAsync(Guid batchId, Guid newRecipientUserId, Guid callerId, string callerName)
        {
            if (newRecipientUserId == Guid.Empty)
                throw new InvalidOperationException("Select who this document should be tagged to instead.");

            var batch = await LoadAsync(batchId);

            if (batch.CurrentStatus == DocumentTrackingStatus.Completed)
                throw new InvalidOperationException("This document batch has already been completed.");

            if (newRecipientUserId == batch.CurrentHolderUserId)
                throw new InvalidOperationException("That user is already tagged on this document.");

            var newRecipientName = await ResolveUserNameAsync(newRecipientUserId);
            var previousHolderName = batch.CurrentHolderName;
            var now = DateTime.UtcNow;

            // Same CurrentStatus — this is a correction, not a workflow advance —
            // but still logged as a transfer entry so the relay history shows who
            // corrected the tagging and when.
            _repo.AttachNewTransfer(new DocumentTransfer
            {
                Id = Guid.NewGuid(),
                DocumentBatchId = batch.Id,
                Status = batch.CurrentStatus,
                FromUserId = callerId,
                FromUserName = callerName,
                ToUserId = newRecipientUserId,
                ToUserName = newRecipientName,
                RelayedAt = now,
                AcceptedAt = null,
                Note = $"Recipient corrected from {previousHolderName} to {newRecipientName}"
            });

            batch.CurrentHolderUserId = newRecipientUserId;
            batch.CurrentHolderName = newRecipientName;
            batch.CurrentLegAcceptedAt = null;

            await LogActivityAsync(callerName, batch, $"Corrected the recipient on a document batch from {previousHolderName} to {newRecipientName}");

            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<DocumentBatch> LoadAsync(Guid batchId)
        {
            var batch = await _repo.GetByIdAsync(batchId);
            if (batch is null)
                throw new InvalidOperationException("Document batch not found.");
            return batch;
        }

        private static void EnsureHolderAccepted(DocumentBatch batch, Guid callerId)
        {
            if (batch.CurrentHolderUserId != callerId)
                throw new InvalidOperationException("This document is not currently tagged to you.");
            if (batch.CurrentLegAcceptedAt is null)
                throw new InvalidOperationException("Accept the document before relaying it further.");
        }

        private static void EnsureStatus(DocumentBatch batch, DocumentTrackingStatus expected)
        {
            if (batch.CurrentStatus != expected)
                throw new InvalidOperationException("This document is not in the right stage for that action.");
        }

        private void Advance(
            DocumentBatch batch,
            DocumentTrackingStatus newStatus,
            Guid fromUserId, string fromUserName,
            Guid toUserId, string toUserName,
            string? note)
        {
            var now = DateTime.UtcNow;

            // Attached directly via the DbSet rather than batch.Transfers.Add(...) —
            // adding to an already-tracked parent's loaded navigation collection
            // was leaving this new row in a "Modified" (not "Added") state, which
            // made EF issue an UPDATE for a row that doesn't exist yet and throw
            // DbUpdateConcurrencyException ("expected to affect 1 row(s), but
            // actually affected 0").
            _repo.AttachNewTransfer(new DocumentTransfer
            {
                Id = Guid.NewGuid(),
                DocumentBatchId = batch.Id,
                Status = newStatus,
                FromUserId = fromUserId,
                FromUserName = fromUserName,
                ToUserId = toUserId,
                ToUserName = toUserName,
                RelayedAt = now,
                AcceptedAt = null,
                Note = note
            });

            batch.CurrentStatus = newStatus;
            batch.CurrentHolderUserId = toUserId;
            batch.CurrentHolderName = toUserName;
            batch.CurrentLegAcceptedAt = null;
        }

        // Staged onto the same DbContext as the batch change it accompanies —
        // callers add this BEFORE their own final _repo.SaveChangesAsync() so
        // everything commits together in one transaction, not a separate round trip.
        private async Task LogActivityAsync(string userName, DocumentBatch batch, string action)
        {
            var provinceName = _psgcNameCache.GetProvinceName(batch.PsgcCodeProvince) ?? $"Province {batch.PsgcCodeProvince}";
            var municipalityName = _psgcNameCache.GetMunicipalityName(batch.PsgcCodeMunicipality) ?? $"Municipality {batch.PsgcCodeMunicipality}";

            await _logRepo.AddAsync(new Log
            {
                Id = Guid.NewGuid(),
                Category = "DocumentTracking",
                UserName = userName,
                CreatedAt = DateTime.UtcNow,
                Activity = $"{action} — {municipalityName}, {provinceName} (Milestone {batch.MilestoneYear})"
            });
        }

        private async Task<string> ResolveUserNameAsync(Guid userId)
        {
            var user = await _userManagementService.GetUserByIdAsync(userId);
            if (user is null)
                throw new InvalidOperationException("Tagged user was not found.");
            return user.FullName;
        }

        private DocumentBatchDto MapToDto(DocumentBatch batch)
        {
            return new DocumentBatchDto
            {
                Id = batch.Id,
                PsgcCodeProvince = batch.PsgcCodeProvince,
                ProvinceName = _psgcNameCache.GetProvinceName(batch.PsgcCodeProvince) ?? $"Province {batch.PsgcCodeProvince}",
                PsgcCodeMunicipality = batch.PsgcCodeMunicipality,
                MunicipalityName = _psgcNameCache.GetMunicipalityName(batch.PsgcCodeMunicipality) ?? $"Municipality {batch.PsgcCodeMunicipality}",
                MilestoneYear = batch.MilestoneYear,
                DateReceived = batch.DateReceived,
                CreatedByUserId = batch.CreatedByUserId,
                CreatedByName = batch.CreatedByName,
                CreatedAt = batch.CreatedAt,
                CurrentStatus = (int)batch.CurrentStatus,
                CurrentStatusLabel = StatusLabel(batch.CurrentStatus),
                CurrentHolderUserId = batch.CurrentHolderUserId,
                CurrentHolderName = batch.CurrentHolderName,
                CurrentLegAcceptedAt = batch.CurrentLegAcceptedAt,
                RowCount = batch.Rows.Count,
                FindingCount = batch.Rows.Count(r => r.HasFinding),
                Rows = batch.Rows
                    .OrderBy(r => r.SortOrder)
                    .Select(r => new DocumentGranteeRowDto
                    {
                        Id = r.Id,
                        FirstName = r.FirstName,
                        MiddleName = r.MiddleName,
                        LastName = r.LastName,
                        Extension = r.Extension,
                        SortOrder = r.SortOrder,
                        HasFinding = r.HasFinding,
                        FindingNote = r.FindingNote,
                        FindingSetAt = r.FindingSetAt,
                        FindingSetByName = r.FindingSetByName,
                        FindingResolvedAt = r.FindingResolvedAt
                    }).ToList(),
                Transfers = batch.Transfers
                    .OrderBy(t => t.RelayedAt)
                    .Select(t => new DocumentTransferDto
                    {
                        Id = t.Id,
                        Status = (int)t.Status,
                        StatusLabel = StatusLabel(t.Status),
                        FromUserId = t.FromUserId,
                        FromUserName = t.FromUserName,
                        ToUserId = t.ToUserId,
                        ToUserName = t.ToUserName,
                        RelayedAt = t.RelayedAt,
                        AcceptedAt = t.AcceptedAt,
                        Note = t.Note
                    }).ToList()
            };
        }

        private static string StatusLabel(DocumentTrackingStatus status) => status switch
        {
            DocumentTrackingStatus.EndorsedByViewer => "Endorsed by Viewer",
            DocumentTrackingStatus.ReturnedToViewer => "Returned to Viewer",
            DocumentTrackingStatus.DistributedToPdo => "Distributed to PDO",
            DocumentTrackingStatus.EndorsedToFinance => "Endorsed to Finance",
            DocumentTrackingStatus.ReturnedToPdoForFindings => "Returned to PDO — Findings",
            DocumentTrackingStatus.ForwardedToViewerForScanning => "Forwarded to Viewer — Scanning",
            DocumentTrackingStatus.Completed => "Completed",
            _ => status.ToString()
        };
    }
}
