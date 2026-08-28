using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.ApplicationTracking;
using EcaInformationSystem.Shared.DTOs.Common;

namespace EcaInformationSystem.Application.Services
{
    public class ApplicationTrackingService : IApplicationTrackingService
    {
        private readonly IApplicationTrackingRepository _repo;
        private readonly IPsgcNameCache _psgcNameCache;
        private readonly IUserManagementService _userManagementService;
        private readonly ILogRepository _logRepo;

        public ApplicationTrackingService(
            IApplicationTrackingRepository repo,
            IPsgcNameCache psgcNameCache,
            IUserManagementService userManagementService,
            ILogRepository logRepo)
        {
            _repo = repo;
            _psgcNameCache = psgcNameCache;
            _userManagementService = userManagementService;
            _logRepo = logRepo;
        }

        public async Task<List<ApplicationBatchDto>> GetAllAsync()
        {
            var batches = await _repo.GetAllAsync();
            return batches.Select(MapToDto).ToList();
        }

        public async Task<ApplicationBatchDto?> GetByIdAsync(Guid id)
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

        public async Task<ApplicationBatchDto> CreateAsync(CreateApplicationBatchDto dto, Guid callerId, string callerName, string? callerRole = null)
        {
            EnsureFindingHasJustification(dto.IsFinding, dto.FindingJustification);

            if (dto.Rows is null || dto.Rows.Count == 0)
                throw new InvalidOperationException("At least one grantee row is required.");

            if (dto.RecipientUserId == Guid.Empty)
                throw new InvalidOperationException("A recipient must be tagged.");

            if (dto.RecipientUserId == callerId)
                throw new InvalidOperationException("You cannot endorse a application batch to yourself.");

            var recipientName = await ResolveUserNameAsync(dto.RecipientUserId);
            var now = DateTime.UtcNow;

            var batch = new ApplicationBatch
            {
                Id = Guid.NewGuid(),
                PsgcCodeProvince = dto.PsgcCodeProvince,
                PsgcCodeMunicipality = dto.PsgcCodeMunicipality,
                MilestoneYear = dto.MilestoneYear,
                DateReceived = dto.DateReceived,
                CreatedByUserId = callerId,
                CreatedByName = callerName,
                CreatedByRole = callerRole,
                CreatedAt = now,
                CurrentStatus = ApplicationTrackingStatus.EndorsedByViewer,
                CurrentHolderUserId = dto.RecipientUserId,
                CurrentHolderName = recipientName,
                CurrentLegAcceptedAt = null,
                // Only the sender (whoever logs the batch) decides its urgency —
                // set once here at creation, never editable afterward by anyone,
                // including SuperAdmin's header-edit override.
                Priority = Enum.IsDefined(typeof(ApplicationPriority), dto.Priority)
                    ? (ApplicationPriority)dto.Priority
                    : ApplicationPriority.Normal
            };

            var sort = 0;
            foreach (var row in dto.Rows)
            {
                batch.Rows.Add(new ApplicationGranteeRow
                {
                    Id = Guid.NewGuid(),
                    ApplicationBatchId = batch.Id,
                    FirstName = row.FirstName.Trim(),
                    MiddleName = string.IsNullOrWhiteSpace(row.MiddleName) ? null : row.MiddleName.Trim(),
                    LastName = row.LastName.Trim(),
                    Extension = string.IsNullOrWhiteSpace(row.Extension) ? null : row.Extension.Trim(),
                    SortOrder = sort++
                });
            }

            batch.Transfers.Add(new ApplicationTransfer
            {
                Id = Guid.NewGuid(),
                ApplicationBatchId = batch.Id,
                Status = ApplicationTrackingStatus.EndorsedByViewer,
                FromUserId = callerId,
                FromUserName = callerName,
                ToUserId = dto.RecipientUserId,
                ToUserName = recipientName,
                RelayedAt = now,
                AcceptedAt = null,
                Note = dto.Note,
                IsFinding = dto.IsFinding,
                FindingJustification = dto.IsFinding ? dto.FindingJustification : null,
                RaisedByRole = dto.IsFinding ? callerRole : null,
                ActorRole = callerRole
            });

            // One Log row per grantee (not one per batch) — ranking/leaderboard
            // counts Log rows directly (see LogRepository.CountUserTransactionsAsync),
            // so a batch of 20 grantees should score the Viewer 20, not 1.
            foreach (var row in batch.Rows)
            {
                await LogActivityAsync(callerName, batch,
                    $"Logged a new application for {row.FirstName} {row.LastName} and endorsed it to {recipientName}");
            }
            await _repo.AddAsync(batch);
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> AcceptAsync(Guid batchId, Guid callerId, string callerName)
        {
            var batch = await LoadAsync(batchId);

            if (batch.CurrentStatus == ApplicationTrackingStatus.Completed)
                throw new InvalidOperationException("This application batch has already been completed.");

            if (batch.CurrentHolderUserId != callerId)
                throw new InvalidOperationException("This application was not tagged to you.");

            if (batch.CurrentLegAcceptedAt is not null)
                throw new InvalidOperationException("You have already accepted this application.");

            var now = DateTime.UtcNow;
            batch.CurrentLegAcceptedAt = now;

            var latestTransfer = batch.Transfers
                .Where(t => t.Status == batch.CurrentStatus && t.ToUserId == callerId)
                .OrderByDescending(t => t.RelayedAt)
                .FirstOrDefault();
            if (latestTransfer is not null)
                latestTransfer.AcceptedAt = now;

            await LogActivityAsync(callerName, batch, "Accepted a application batch");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> ReturnToViewerAsync(Guid batchId, Guid callerId, string callerName, string? note, bool isFinding = false, string? findingJustification = null, string? callerRole = null)
        {
            EnsureFindingHasJustification(isFinding, findingJustification);

            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            EnsureStatus(batch, ApplicationTrackingStatus.EndorsedByViewer);

            Advance(batch, ApplicationTrackingStatus.ReturnedToViewer, callerId, callerName,
                batch.CreatedByUserId, batch.CreatedByName, note, isFinding, findingJustification, callerRole);

            await LogActivityAsync(callerName, batch, "Returned a application batch to the Viewer");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> DistributeToPdoAsync(Guid batchId, Guid callerId, string callerName, RelayApplicationDto dto, string? callerRole = null)
        {
            EnsureFindingHasJustification(dto.IsFinding, dto.FindingJustification);

            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            EnsureStatus(batch, ApplicationTrackingStatus.ReturnedToViewer);

            var toName = await ResolveUserNameAsync(dto.ToUserId);
            Advance(batch, ApplicationTrackingStatus.DistributedToPdo, callerId, callerName, dto.ToUserId, toName, dto.Note, dto.IsFinding, dto.FindingJustification, callerRole);

            await LogActivityAsync(callerName, batch, $"Distributed a application batch to PDO {toName}");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> EndorseToFinanceAsync(Guid batchId, Guid callerId, string callerName, RelayApplicationDto dto, string? callerRole = null)
        {
            EnsureFindingHasJustification(dto.IsFinding, dto.FindingJustification);

            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            if (batch.CurrentStatus != ApplicationTrackingStatus.DistributedToPdo
                && batch.CurrentStatus != ApplicationTrackingStatus.ReturnedToPdoForFindings)
            {
                throw new InvalidOperationException("This application is not currently held by a PDO.");
            }

            var toName = await ResolveUserNameAsync(dto.ToUserId);
            Advance(batch, ApplicationTrackingStatus.EndorsedToFinance, callerId, callerName, dto.ToUserId, toName, dto.Note, dto.IsFinding, dto.FindingJustification, callerRole);

            await LogActivityAsync(callerName, batch, $"Endorsed a application batch to Finance {toName}");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> ReturnToPdoForFindingsAsync(Guid batchId, Guid callerId, string callerName, ReturnApplicationForFindingsDto dto)
        {
            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            EnsureStatus(batch, ApplicationTrackingStatus.EndorsedToFinance);

            if (dto.GranteeRowIds is null || dto.GranteeRowIds.Count == 0)
                throw new InvalidOperationException("Select at least one grantee to tag with a finding.");

            var toName = await ResolveUserNameAsync(dto.ToUserId);
            Advance(batch, ApplicationTrackingStatus.ReturnedToPdoForFindings, callerId, callerName, dto.ToUserId, toName, dto.Note);

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

            await LogActivityAsync(callerName, batch, $"Returned a application batch to PDO {toName} with {dto.GranteeRowIds.Count} finding(s) flagged");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> ForwardToViewerForScanningAsync(Guid batchId, Guid callerId, string callerName, RelayApplicationDto dto, string? callerRole = null)
        {
            EnsureFindingHasJustification(dto.IsFinding, dto.FindingJustification);

            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            EnsureStatus(batch, ApplicationTrackingStatus.EndorsedToFinance);

            var toName = await ResolveUserNameAsync(dto.ToUserId);
            Advance(batch, ApplicationTrackingStatus.ForwardedToViewerForScanning, callerId, callerName, dto.ToUserId, toName, dto.Note, dto.IsFinding, dto.FindingJustification, callerRole);

            await LogActivityAsync(callerName, batch, $"Forwarded a application batch to Viewer {toName} for scanning");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> CompleteAsync(Guid batchId, Guid callerId, string callerName, string? note)
        {
            var batch = await LoadAsync(batchId);
            EnsureHolderAccepted(batch, callerId);
            EnsureStatus(batch, ApplicationTrackingStatus.ForwardedToViewerForScanning);

            var now = DateTime.UtcNow;
            batch.CurrentStatus = ApplicationTrackingStatus.Completed;
            batch.CurrentLegAcceptedAt = now;

            _repo.AttachNewTransfer(new ApplicationTransfer
            {
                Id = Guid.NewGuid(),
                ApplicationBatchId = batch.Id,
                Status = ApplicationTrackingStatus.Completed,
                FromUserId = callerId,
                FromUserName = callerName,
                ToUserId = callerId,
                ToUserName = callerName,
                RelayedAt = now,
                AcceptedAt = now,
                Note = note
            });

            await LogActivityAsync(callerName, batch, "Marked a application batch as completed / scanned");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> ResolveFindingAsync(Guid batchId, Guid rowId, Guid callerId, string callerName, bool isSuperAdmin = false)
        {
            var batch = await LoadAsync(batchId);

            if (!isSuperAdmin && batch.CurrentHolderUserId != callerId)
                throw new InvalidOperationException("Only the current holder of this application batch can resolve a finding.");

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
            await LogActivityAsync(callerName, batch, "Deleted a application batch");
            await _repo.DeleteAsync(batch);
        }

        public async Task<ApplicationBatchDto> UpdateHeaderAsync(Guid batchId, UpdateApplicationBatchHeaderDto dto, string callerName)
        {
            var batch = await LoadAsync(batchId);

            batch.PsgcCodeProvince = dto.PsgcCodeProvince;
            batch.PsgcCodeMunicipality = dto.PsgcCodeMunicipality;
            batch.MilestoneYear = dto.MilestoneYear;
            batch.DateReceived = dto.DateReceived;

            if (Enum.IsDefined(typeof(ApplicationPriority), dto.Priority))
                batch.Priority = (ApplicationPriority)dto.Priority;

            await LogActivityAsync(callerName, batch, "Edited a application batch's details");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> AddRowAsync(Guid batchId, CreateApplicationGranteeRowDto dto, string callerName)
        {
            if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
                throw new InvalidOperationException("First and Last name are required.");

            var batch = await LoadAsync(batchId);
            var nextSort = batch.Rows.Any() ? batch.Rows.Max(r => r.SortOrder) + 1 : 0;

            _repo.AttachNewRow(new ApplicationGranteeRow
            {
                Id = Guid.NewGuid(),
                ApplicationBatchId = batch.Id,
                FirstName = dto.FirstName.Trim(),
                MiddleName = string.IsNullOrWhiteSpace(dto.MiddleName) ? null : dto.MiddleName.Trim(),
                LastName = dto.LastName.Trim(),
                Extension = string.IsNullOrWhiteSpace(dto.Extension) ? null : dto.Extension.Trim(),
                SortOrder = nextSort
            });

            await LogActivityAsync(callerName, batch, $"Added grantee row '{dto.FirstName.Trim()} {dto.LastName.Trim()}' to a application batch");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> UpdateRowAsync(Guid batchId, Guid rowId, CreateApplicationGranteeRowDto dto, string callerName)
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

            await LogActivityAsync(callerName, batch, $"Edited grantee row '{row.FirstName} {row.LastName}' in a application batch");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> DeleteRowAsync(Guid batchId, Guid rowId, string callerName)
        {
            var batch = await LoadAsync(batchId);
            var row = batch.Rows.FirstOrDefault(r => r.Id == rowId);
            if (row is null)
                throw new InvalidOperationException("Grantee row not found.");

            if (batch.Rows.Count == 1)
                throw new InvalidOperationException("A application batch must have at least one grantee row.");

            batch.Rows.Remove(row);

            await LogActivityAsync(callerName, batch, $"Removed grantee row '{row.FirstName} {row.LastName}' from a application batch");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        public async Task<ApplicationBatchDto> ReassignRecipientAsync(Guid batchId, Guid newRecipientUserId, Guid callerId, string callerName)
        {
            if (newRecipientUserId == Guid.Empty)
                throw new InvalidOperationException("Select who this application should be tagged to instead.");

            var batch = await LoadAsync(batchId);

            if (batch.CurrentStatus == ApplicationTrackingStatus.Completed)
                throw new InvalidOperationException("This application batch has already been completed.");

            if (newRecipientUserId == batch.CurrentHolderUserId)
                throw new InvalidOperationException("That user is already tagged on this application.");

            var newRecipientName = await ResolveUserNameAsync(newRecipientUserId);
            var previousHolderName = batch.CurrentHolderName;
            var now = DateTime.UtcNow;

            // Same CurrentStatus — this is a correction, not a workflow advance —
            // but still logged as a transfer entry so the relay history shows who
            // corrected the tagging and when.
            _repo.AttachNewTransfer(new ApplicationTransfer
            {
                Id = Guid.NewGuid(),
                ApplicationBatchId = batch.Id,
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

            await LogActivityAsync(callerName, batch, $"Corrected the recipient on a application batch from {previousHolderName} to {newRecipientName}");

            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        // Only SuperAdmin can correct ANY relay history entry's Note/finding.
        // Every other role (including plain Admin) can only correct/clear the
        // entry THEY raised (their own FromUserId) — e.g. fixing a typo, or
        // unchecking "finding" to withdraw one they flagged by mistake.
        // Doesn't touch who it was sent to/from or the batch's workflow stage.
        // RaisedByRole is only (re)stamped when this edit is what newly turns
        // the entry into a finding — otherwise the original raiser's role is
        // preserved.
        public async Task<ApplicationBatchDto> UpdateTransferAsync(Guid batchId, Guid transferId, UpdateApplicationTransferNoteDto dto, Guid callerId, string callerName, bool isSuperAdmin, string? callerRole = null)
        {
            EnsureFindingHasJustification(dto.IsFinding, dto.FindingJustification);

            var batch = await LoadAsync(batchId);
            var transfer = batch.Transfers.FirstOrDefault(t => t.Id == transferId)
                ?? throw new InvalidOperationException("Relay history entry not found.");

            if (!isSuperAdmin && transfer.FromUserId != callerId)
                throw new InvalidOperationException("You can only correct a note or finding that you raised yourself.");

            transfer.Note = dto.Note;

            if (dto.IsFinding)
            {
                if (!transfer.IsFinding)
                    transfer.RaisedByRole = callerRole;
                transfer.FindingJustification = dto.FindingJustification;
            }
            else
            {
                transfer.FindingJustification = null;
                transfer.RaisedByRole = null;
            }
            transfer.IsFinding = dto.IsFinding;

            await LogActivityAsync(callerName, batch, "Corrected a relay history entry's note/finding");
            await _repo.SaveChangesAsync();
            return MapToDto(batch);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<ApplicationBatch> LoadAsync(Guid batchId)
        {
            var batch = await _repo.GetByIdAsync(batchId);
            if (batch is null)
                throw new InvalidOperationException("Application batch not found.");
            return batch;
        }

        private static void EnsureHolderAccepted(ApplicationBatch batch, Guid callerId)
        {
            if (batch.CurrentHolderUserId != callerId)
                throw new InvalidOperationException("This application is not currently tagged to you.");
            if (batch.CurrentLegAcceptedAt is null)
                throw new InvalidOperationException("Accept the application before relaying it further.");
        }

        private static void EnsureStatus(ApplicationBatch batch, ApplicationTrackingStatus expected)
        {
            if (batch.CurrentStatus != expected)
                throw new InvalidOperationException("This application is not in the right stage for that action.");
        }

        // A finding flag with no explanation is useless to whoever receives it —
        // require a justification whenever "flag as a finding" is checked.
        private static void EnsureFindingHasJustification(bool isFinding, string? note)
        {
            if (isFinding && string.IsNullOrWhiteSpace(note))
                throw new InvalidOperationException("Please explain the finding — a justification is required when flagging one.");
        }

        private void Advance(
            ApplicationBatch batch,
            ApplicationTrackingStatus newStatus,
            Guid fromUserId, string fromUserName,
            Guid toUserId, string toUserName,
            string? note,
            bool isFinding = false, string? findingJustification = null, string? raisedByRole = null)
        {
            var now = DateTime.UtcNow;

            // Attached directly via the DbSet rather than batch.Transfers.Add(...) —
            // adding to an already-tracked parent's loaded navigation collection
            // was leaving this new row in a "Modified" (not "Added") state, which
            // made EF issue an UPDATE for a row that doesn't exist yet and throw
            // DbUpdateConcurrencyException ("expected to affect 1 row(s), but
            // actually affected 0").
            _repo.AttachNewTransfer(new ApplicationTransfer
            {
                Id = Guid.NewGuid(),
                ApplicationBatchId = batch.Id,
                Status = newStatus,
                FromUserId = fromUserId,
                FromUserName = fromUserName,
                ToUserId = toUserId,
                ToUserName = toUserName,
                RelayedAt = now,
                AcceptedAt = null,
                Note = note,
                IsFinding = isFinding,
                FindingJustification = isFinding ? findingJustification : null,
                RaisedByRole = isFinding ? raisedByRole : null,
                ActorRole = raisedByRole
            });

            batch.CurrentStatus = newStatus;
            batch.CurrentHolderUserId = toUserId;
            batch.CurrentHolderName = toUserName;
            batch.CurrentLegAcceptedAt = null;
        }

        // Staged onto the same DbContext as the batch change it accompanies —
        // callers add this BEFORE their own final _repo.SaveChangesAsync() so
        // everything commits together in one transaction, not a separate round trip.
        private async Task LogActivityAsync(string userName, ApplicationBatch batch, string action)
        {
            var provinceName = _psgcNameCache.GetProvinceName(batch.PsgcCodeProvince) ?? $"Province {batch.PsgcCodeProvince}";
            var municipalityName = _psgcNameCache.GetMunicipalityName(batch.PsgcCodeMunicipality) ?? $"Municipality {batch.PsgcCodeMunicipality}";

            await _logRepo.AddAsync(new Log
            {
                Id = Guid.NewGuid(),
                Category = "ApplicationTracking",
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

        private ApplicationBatchDto MapToDto(ApplicationBatch batch)
        {
            return new ApplicationBatchDto
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
                CreatedByRole = batch.CreatedByRole,
                CreatedAt = batch.CreatedAt,
                CurrentStatus = (int)batch.CurrentStatus,
                CurrentStatusLabel = StatusLabel(batch.CurrentStatus,
                    batch.Transfers.Where(t => t.Status == batch.CurrentStatus).OrderByDescending(t => t.RelayedAt).FirstOrDefault()?.ActorRole,
                    batch.CreatedByRole),
                Priority = (int)batch.Priority,
                PriorityLabel = PriorityLabel(batch.Priority),
                CurrentHolderUserId = batch.CurrentHolderUserId,
                CurrentHolderName = batch.CurrentHolderName,
                CurrentLegAcceptedAt = batch.CurrentLegAcceptedAt,
                RowCount = batch.Rows.Count,
                FindingCount = batch.Rows.Count(r => r.HasFinding),
                Rows = batch.Rows
                    .OrderBy(r => r.SortOrder)
                    .Select(r => new ApplicationGranteeRowDto
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
                    .Select(t => new ApplicationTransferDto
                    {
                        Id = t.Id,
                        Status = (int)t.Status,
                        StatusLabel = StatusLabel(t.Status, t.ActorRole, batch.CreatedByRole),
                        FromUserId = t.FromUserId,
                        FromUserName = t.FromUserName,
                        ToUserId = t.ToUserId,
                        ToUserName = t.ToUserName,
                        RelayedAt = t.RelayedAt,
                        AcceptedAt = t.AcceptedAt,
                        Note = t.Note,
                        IsFinding = t.IsFinding,
                        FindingJustification = t.FindingJustification,
                        RaisedByRole = t.RaisedByRole
                    }).ToList()
            };
        }

        private static string PriorityLabel(ApplicationPriority priority) => priority switch
        {
            ApplicationPriority.Normal => "Normal",
            ApplicationPriority.Priority => "Priority",
            ApplicationPriority.Urgent => "Urgent",
            _ => priority.ToString()
        };

        // actorRole = the role of whoever performed THIS specific hand-off (used
        // for "Endorsed by ___", since a Viewer, PDO, Admin, or SuperAdmin can
        // all log a batch now). creatorRole = the original batch creator's role
        // (used for "Returned to ___", since that hand-off always targets
        // batch.CreatedByUserId, whoever they turned out to be). Both fall back
        // to "Viewer" for historical rows recorded before these role columns
        // existed, matching the label these statuses always used to show.
        private static string StatusLabel(ApplicationTrackingStatus status, string? actorRole, string? creatorRole) => status switch
        {
            ApplicationTrackingStatus.EndorsedByViewer => $"Endorsed by {actorRole ?? "Viewer"}",
            ApplicationTrackingStatus.ReturnedToViewer => $"Returned to {creatorRole ?? "Viewer"}",
            ApplicationTrackingStatus.DistributedToPdo => "Distributed to PDO",
            ApplicationTrackingStatus.EndorsedToFinance => "Endorsed to Finance",
            ApplicationTrackingStatus.ReturnedToPdoForFindings => "Returned to PDO — Findings",
            ApplicationTrackingStatus.ForwardedToViewerForScanning => "Forwarded to Viewer — Scanning",
            ApplicationTrackingStatus.Completed => "Completed",
            _ => status.ToString()
        };
    }
}
