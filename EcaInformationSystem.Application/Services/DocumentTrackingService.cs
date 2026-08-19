using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.Common;
using EcaInformationSystem.Shared.DTOs.DocumentTracking;

namespace EcaInformationSystem.Application.Services
{
    public class DocumentTrackingService : IDocumentTrackingService
    {
        private readonly IDocumentTrackingRepository _repo;
        private readonly IUserManagementService _userManagementService;
        private readonly ILogRepository _logRepo;

        public DocumentTrackingService(
            IDocumentTrackingRepository repo,
            IUserManagementService userManagementService,
            ILogRepository logRepo)
        {
            _repo = repo;
            _userManagementService = userManagementService;
            _logRepo = logRepo;
        }

        public async Task<PagedResultDto<TrackedDocumentListItemDto>> GetPagedAsync(int page, int pageSize, string? search)
        {
            var (items, totalCount) = await _repo.GetPagedAsync(page, pageSize, search);
            return new PagedResultDto<TrackedDocumentListItemDto>
            {
                Items = items.Select(MapToListItemDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<TrackedDocumentDto?> GetByIdAsync(Guid id)
        {
            var document = await _repo.GetByIdAsync(id);
            return document is null ? null : MapToDto(document);
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

        public async Task<TrackedDocumentDto> CreateAsync(CreateTrackedDocumentDto dto, Guid callerId, string callerName, string? callerRole)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new InvalidOperationException("Title is required.");

            if (dto.RecipientUserId == Guid.Empty)
                throw new InvalidOperationException("A recipient must be tagged.");

            if (dto.RecipientUserId == callerId)
                throw new InvalidOperationException("You cannot tag a document to yourself.");

            EnsureFindingHasJustification(dto.IsFinding, dto.FindingJustification);

            var recipientName = await ResolveUserNameAsync(dto.RecipientUserId);
            var seq = await _repo.GetNextSerialSequenceAsync();
            var now = DateTime.UtcNow;

            var document = new TrackedDocument
            {
                Id = Guid.NewGuid(),
                SerialNumber = $"DT-{now:yyyyMMdd}-{seq:D6}",
                Title = dto.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                CreatedByUserId = callerId,
                CreatedByName = callerName,
                CreatedAt = now,
                CurrentHolderUserId = dto.RecipientUserId,
                CurrentHolderName = recipientName,
                CurrentLegAcceptedAt = null,
                Status = TrackedDocumentStatus.InTransit
            };

            // Added directly to document.Routes here (rather than via
            // AttachNewRoute) is fine — the parent isn't tracked by the context
            // yet, so there's no already-tracked navigation collection for EF to
            // get confused by. AttachNewRoute only matters once the document is
            // an existing, already-tracked entity (see RelayAsync/ReturnAsync/etc).
            document.Routes.Add(new DocumentRoute
            {
                Id = Guid.NewGuid(),
                TrackedDocumentId = document.Id,
                Action = DocumentRouteAction.Tagged,
                FromUserId = callerId,
                FromUserName = callerName,
                ToUserId = dto.RecipientUserId,
                ToUserName = recipientName,
                RelayedAt = now,
                AcceptedAt = null,
                Note = dto.Note,
                IsFinding = dto.IsFinding,
                FindingJustification = dto.IsFinding ? dto.FindingJustification : null,
                RaisedByRole = dto.IsFinding ? callerRole : null
            });

            await LogActivityAsync(callerName, document, $"Logged a new tracked document and tagged it to {recipientName}");
            await _repo.AddAsync(document);
            return MapToDto(document);
        }

        public async Task<TrackedDocumentDto> AcceptAsync(Guid docId, Guid callerId, string callerName)
        {
            var document = await LoadAsync(docId);

            if (document.CurrentHolderUserId != callerId)
                throw new InvalidOperationException("You are not the current holder of this document.");

            if (document.CurrentLegAcceptedAt is not null)
                throw new InvalidOperationException("This document has already been accepted.");

            var now = DateTime.UtcNow;
            var latestRoute = document.Routes
                .Where(r => r.ToUserId == callerId && r.AcceptedAt == null)
                .OrderByDescending(r => r.RelayedAt)
                .First();
            latestRoute.AcceptedAt = now;

            document.CurrentLegAcceptedAt = now;

            await LogActivityAsync(callerName, document, "Accepted a tracked document");
            await _repo.SaveChangesAsync();
            return MapToDto(document);
        }

        public async Task<TrackedDocumentDto> RelayAsync(Guid docId, RelayDocumentToDto dto, Guid callerId, string callerName, string? callerRole)
        {
            var document = await LoadAsync(docId);
            EnsureNotCompleted(document);
            EnsureHolderAccepted(document, callerId);

            if (dto.ToUserId == Guid.Empty || dto.ToUserId == callerId)
                throw new InvalidOperationException("Select someone else to relay this document to.");

            EnsureFindingHasJustification(dto.IsFinding, dto.FindingJustification);

            var toName = await ResolveUserNameAsync(dto.ToUserId);
            var now = DateTime.UtcNow;

            // Attached directly via the DbSet rather than document.Routes.Add(...) —
            // adding to an already-tracked parent's loaded navigation collection
            // was leaving this new row in a "Modified" (not "Added") state, which
            // made EF issue an UPDATE for a row that doesn't exist yet and throw
            // DbUpdateConcurrencyException ("expected to affect 1 row(s), but
            // actually affected 0").
            _repo.AttachNewRoute(new DocumentRoute
            {
                Id = Guid.NewGuid(),
                TrackedDocumentId = document.Id,
                Action = DocumentRouteAction.Relayed,
                FromUserId = callerId,
                FromUserName = callerName,
                ToUserId = dto.ToUserId,
                ToUserName = toName,
                RelayedAt = now,
                AcceptedAt = null,
                Note = dto.Note,
                IsFinding = dto.IsFinding,
                FindingJustification = dto.IsFinding ? dto.FindingJustification : null,
                RaisedByRole = dto.IsFinding ? callerRole : null
            });

            document.CurrentHolderUserId = dto.ToUserId;
            document.CurrentHolderName = toName;
            document.CurrentLegAcceptedAt = null;

            await LogActivityAsync(callerName, document, $"Relayed a tracked document to {toName}");
            await _repo.SaveChangesAsync();
            return MapToDto(document);
        }

        public async Task<TrackedDocumentDto> ReturnAsync(Guid docId, ReturnDocumentDto dto, Guid callerId, string callerName, string? callerRole)
        {
            var document = await LoadAsync(docId);
            EnsureNotCompleted(document);
            EnsureHolderAccepted(document, callerId);

            var inboundRoute = document.Routes
                .Where(r => r.ToUserId == callerId)
                .OrderByDescending(r => r.RelayedAt)
                .FirstOrDefault();
            if (inboundRoute is null)
                throw new InvalidOperationException("Cannot determine who this document came from.");

            EnsureFindingHasJustification(dto.IsFinding, dto.FindingJustification);

            var targetUserId = inboundRoute.FromUserId;
            var targetUserName = inboundRoute.FromUserName;
            var now = DateTime.UtcNow;

            _repo.AttachNewRoute(new DocumentRoute
            {
                Id = Guid.NewGuid(),
                TrackedDocumentId = document.Id,
                Action = DocumentRouteAction.Returned,
                FromUserId = callerId,
                FromUserName = callerName,
                ToUserId = targetUserId,
                ToUserName = targetUserName,
                RelayedAt = now,
                AcceptedAt = null,
                Note = dto.Note,
                IsFinding = dto.IsFinding,
                FindingJustification = dto.IsFinding ? dto.FindingJustification : null,
                RaisedByRole = dto.IsFinding ? callerRole : null
            });

            document.CurrentHolderUserId = targetUserId;
            document.CurrentHolderName = targetUserName;
            document.CurrentLegAcceptedAt = null;

            await LogActivityAsync(callerName, document, $"Returned a tracked document to {targetUserName}");
            await _repo.SaveChangesAsync();
            return MapToDto(document);
        }

        public async Task<TrackedDocumentDto> CompleteAsync(Guid docId, CompleteDocumentDto dto, Guid callerId, string callerName)
        {
            var document = await LoadAsync(docId);
            EnsureNotCompleted(document);
            EnsureHolderAccepted(document, callerId);

            var now = DateTime.UtcNow;
            document.Status = TrackedDocumentStatus.Completed;

            _repo.AttachNewRoute(new DocumentRoute
            {
                Id = Guid.NewGuid(),
                TrackedDocumentId = document.Id,
                Action = DocumentRouteAction.Completed,
                FromUserId = callerId,
                FromUserName = callerName,
                ToUserId = callerId,
                ToUserName = callerName,
                RelayedAt = now,
                AcceptedAt = now,
                Note = dto.Note
            });

            await LogActivityAsync(callerName, document, "Marked a tracked document as completed");
            await _repo.SaveChangesAsync();
            return MapToDto(document);
        }

        public async Task<TrackedDocumentDto> UpdateAsync(Guid docId, UpdateTrackedDocumentDto dto, Guid callerId, bool isSuperAdmin)
        {
            var document = await LoadAsync(docId);

            if (!isSuperAdmin && document.CreatedByUserId != callerId)
                throw new InvalidOperationException("Only the creator or a SuperAdmin can edit this document.");

            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new InvalidOperationException("Title is required.");

            document.Title = dto.Title.Trim();
            document.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

            if (Enum.IsDefined(typeof(TrackedDocumentStatus), dto.Status))
                document.Status = (TrackedDocumentStatus)dto.Status;

            await LogActivityAsync(document.CreatedByName, document, "Edited a tracked document's details");
            await _repo.SaveChangesAsync();
            return MapToDto(document);
        }

        public async Task DeleteAsync(Guid docId, string callerRole)
        {
            var document = await LoadAsync(docId);

            if (callerRole is not ("Admin" or "SuperAdmin" or "Finance"))
                throw new InvalidOperationException("Only Admin, SuperAdmin, or Finance may delete a tracked document.");

            await LogActivityAsync(document.CreatedByName, document, "Deleted a tracked document");
            await _repo.DeleteAsync(document);
        }

        public async Task<TrackedDocumentDto> UpdateRouteAsync(Guid docId, Guid routeId, UpdateDocumentRouteNoteDto dto, Guid callerId, bool isSuperAdmin)
        {
            var document = await LoadAsync(docId);
            var route = document.Routes.FirstOrDefault(r => r.Id == routeId)
                ?? throw new InvalidOperationException("Routing history entry not found.");

            if (!isSuperAdmin && route.FromUserId != callerId)
                throw new InvalidOperationException("You can only correct your own routing entries.");

            EnsureFindingHasJustification(dto.IsFinding, dto.FindingJustification);

            route.Note = dto.Note;
            route.IsFinding = dto.IsFinding;
            route.FindingJustification = dto.IsFinding ? dto.FindingJustification : null;

            await LogActivityAsync(document.CreatedByName, document, "Corrected a routing history entry's note/finding");
            await _repo.SaveChangesAsync();
            return MapToDto(document);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<TrackedDocument> LoadAsync(Guid docId)
        {
            var document = await _repo.GetByIdAsync(docId);
            if (document is null)
                throw new InvalidOperationException("Document not found.");
            return document;
        }

        private static void EnsureNotCompleted(TrackedDocument document)
        {
            if (document.Status == TrackedDocumentStatus.Completed)
                throw new InvalidOperationException("This document has already been marked completed.");
        }

        private static void EnsureHolderAccepted(TrackedDocument document, Guid callerId)
        {
            if (document.CurrentHolderUserId != callerId)
                throw new InvalidOperationException("This document is not currently tagged to you.");
            if (document.CurrentLegAcceptedAt is null)
                throw new InvalidOperationException("You must accept this document before relaying it.");
        }

        // A finding flag with no explanation is useless to whoever receives it —
        // require a justification whenever "flag as a finding" is checked.
        private static void EnsureFindingHasJustification(bool isFinding, string? justification)
        {
            if (isFinding && string.IsNullOrWhiteSpace(justification))
                throw new InvalidOperationException("A finding requires a justification.");
        }

        private async Task<string> ResolveUserNameAsync(Guid userId)
        {
            var user = await _userManagementService.GetUserByIdAsync(userId);
            if (user is null)
                throw new InvalidOperationException("Tagged user was not found.");
            return user.FullName;
        }

        // Staged onto the same DbContext as the document change it accompanies —
        // callers add this BEFORE their own final _repo.SaveChangesAsync() so
        // everything commits together in one transaction, not a separate round trip.
        private async Task LogActivityAsync(string userName, TrackedDocument document, string action)
        {
            await _logRepo.AddAsync(new Log
            {
                Id = Guid.NewGuid(),
                Category = "DocumentTracking",
                UserName = userName,
                CreatedAt = DateTime.UtcNow,
                Activity = $"{action} — {document.SerialNumber} ({document.Title})"
            });
        }

        private TrackedDocumentDto MapToDto(TrackedDocument document)
        {
            return new TrackedDocumentDto
            {
                Id = document.Id,
                SerialNumber = document.SerialNumber,
                Title = document.Title,
                Description = document.Description,
                CreatedByUserId = document.CreatedByUserId,
                CreatedByName = document.CreatedByName,
                CreatedAt = document.CreatedAt,
                CurrentHolderUserId = document.CurrentHolderUserId,
                CurrentHolderName = document.CurrentHolderName,
                CurrentLegAcceptedAt = document.CurrentLegAcceptedAt,
                Status = (int)document.Status,
                StatusLabel = StatusLabel(document.Status),
                Routes = document.Routes
                    .OrderBy(r => r.RelayedAt)
                    .Select(r => new DocumentRouteDto
                    {
                        Id = r.Id,
                        Action = (int)r.Action,
                        ActionLabel = ActionLabel(r.Action),
                        FromUserId = r.FromUserId,
                        FromUserName = r.FromUserName,
                        ToUserId = r.ToUserId,
                        ToUserName = r.ToUserName,
                        RelayedAt = r.RelayedAt,
                        AcceptedAt = r.AcceptedAt,
                        Note = r.Note,
                        IsFinding = r.IsFinding,
                        FindingJustification = r.FindingJustification,
                        RaisedByRole = r.RaisedByRole
                    }).ToList()
            };
        }

        private TrackedDocumentListItemDto MapToListItemDto(TrackedDocument document)
        {
            return new TrackedDocumentListItemDto
            {
                Id = document.Id,
                SerialNumber = document.SerialNumber,
                Title = document.Title,
                CreatedByName = document.CreatedByName,
                CreatedAt = document.CreatedAt,
                CurrentHolderUserId = document.CurrentHolderUserId,
                CurrentHolderName = document.CurrentHolderName,
                CurrentLegAcceptedAt = document.CurrentLegAcceptedAt,
                Status = (int)document.Status,
                StatusLabel = StatusLabel(document.Status)
            };
        }

        private static string StatusLabel(TrackedDocumentStatus status) => status switch
        {
            TrackedDocumentStatus.InTransit => "In Transit",
            TrackedDocumentStatus.Completed => "Completed",
            _ => status.ToString()
        };

        private static string ActionLabel(DocumentRouteAction action) => action switch
        {
            DocumentRouteAction.Tagged => "Tagged",
            DocumentRouteAction.Relayed => "Relayed",
            DocumentRouteAction.Returned => "Returned",
            DocumentRouteAction.Completed => "Completed",
            _ => action.ToString()
        };
    }
}
