using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class FormFolderService : IFormFolderService
    {
        private readonly IFormFolderRepository _folderRepo;
        private readonly IFormDocumentRepository _documentRepo;
        private readonly IFormActivityLogRepository _logRepo;

        public FormFolderService(
            IFormFolderRepository folderRepo,
            IFormDocumentRepository documentRepo,
            IFormActivityLogRepository logRepo)
        {
            _folderRepo = folderRepo;
            _documentRepo = documentRepo;
            _logRepo = logRepo;
        }

        public async Task<List<FormFolderDto>> GetAllAsync()
        {
            var folders = await _folderRepo.GetAllAsync();
            var foldersById = folders.ToDictionary(f => f.Id);
            var result = new List<FormFolderDto>();

            foreach (var f in folders)
            {
                var parentName = f.ParentFolderId.HasValue && foldersById.TryGetValue(f.ParentFolderId.Value, out var parent)
                    ? parent.Name
                    : null;

                result.Add(new FormFolderDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    Description = f.Description,
                    DocumentCount = await _folderRepo.CountDocumentsInFolderAsync(f.Id),
                    ParentFolderId = f.ParentFolderId,
                    ParentFolderName = parentName,
                    CreatedBy = f.CreatedBy,
                    CreatedAt = f.CreatedAt,
                    UpdatedBy = f.UpdatedBy,
                    UpdatedAt = f.UpdatedAt
                });
            }

            return result.OrderBy(f => f.Name).ToList();
        }

        public async Task<FormFolderDto> CreateAsync(FormFolderCreateDto dto, string userName)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new InvalidOperationException("Folder name is required.");

            var name = dto.Name.Trim();

            if (await _folderRepo.ExistsByNameAsync(name))
                throw new InvalidOperationException($"A folder named '{name}' already exists.");

            FormFolder? parent = null;
            if (dto.ParentFolderId.HasValue)
            {
                parent = await _folderRepo.GetByIdAsync(dto.ParentFolderId.Value)
                    ?? throw new KeyNotFoundException("Parent folder not found.");

                // Only one level of nesting — a subfolder can't itself have subfolders.
                if (parent.ParentFolderId.HasValue)
                    throw new InvalidOperationException("A subfolder cannot contain another subfolder.");
            }

            var folder = new FormFolder
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = dto.Description?.Trim(),
                ParentFolderId = dto.ParentFolderId,
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _folderRepo.AddAsync(folder);
            await _folderRepo.SaveChangesAsync();

            await LogAsync(parent is null ? "FolderCreated" : "SubfolderCreated", folder.Id, null,
                folder.Name,
                parent is null
                    ? $"Folder '{folder.Name}' created."
                    : $"Subfolder '{folder.Name}' created under '{parent.Name}'.",
                userName);

            return new FormFolderDto
            {
                Id = folder.Id,
                Name = folder.Name,
                Description = folder.Description,
                DocumentCount = 0,
                ParentFolderId = folder.ParentFolderId,
                ParentFolderName = parent?.Name,
                CreatedBy = folder.CreatedBy,
                CreatedAt = folder.CreatedAt
            };
        }

        public async Task UpdateAsync(Guid id, FormFolderUpdateDto dto, string userName)
        {
            var folder = await _folderRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Folder not found.");

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new InvalidOperationException("Folder name is required.");

            var newName = dto.Name.Trim();

            if (await _folderRepo.ExistsByNameAsync(newName, excludeId: id))
                throw new InvalidOperationException($"A folder named '{newName}' already exists.");

            var oldName = folder.Name;

            folder.Name = newName;
            folder.Description = dto.Description?.Trim();
            folder.UpdatedBy = userName;
            folder.UpdatedAt = DateTime.UtcNow;

            await _folderRepo.UpdateAsync(folder);
            await _folderRepo.SaveChangesAsync();

            await LogAsync("FolderRenamed", folder.Id, null, newName,
                $"Folder renamed from '{oldName}' to '{newName}'.", userName);
        }

        // ✅ CAUTION path — deleting a folder does NOT delete its files. Files are
        // detached (FolderId set to null → "Uncategorized") so nothing is silently
        // lost. This is the safe default for a shared document repository; the
        // person deleting still gets told exactly how many files were affected.
        public async Task<int> DeleteAsync(Guid id, string userName)
        {
            var folder = await _folderRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Folder not found.");

            var affectedCount = await _folderRepo.CountDocumentsInFolderAsync(id);
            var childFolders = await _folderRepo.GetChildFoldersAsync(id);

            folder.IsDeleted = true;
            folder.UpdatedBy = userName;
            folder.UpdatedAt = DateTime.UtcNow;

            await _folderRepo.UpdateAsync(folder);
            await _documentRepo.DetachFromFolderAsync(id); // sets FolderId = null on contained docs

            // ✅ Same "never lose data" principle as documents — a deleted folder's
            // subfolders are promoted to root rather than deleted or orphaned.
            foreach (var child in childFolders)
                child.ParentFolderId = null;

            await _folderRepo.SaveChangesAsync();

            var subfolderNote = childFolders.Count > 0
                ? $" {childFolders.Count} subfolder(s) promoted to top-level."
                : "";
            await LogAsync("FolderDeleted", folder.Id, null, folder.Name,
                $"Folder '{folder.Name}' deleted. {affectedCount} file(s) moved to Uncategorized.{subfolderNote}", userName);

            return affectedCount;
        }

        public async Task<List<FormActivityLogDto>> GetActivityLogAsync(int take = 100)
        {
            var logs = await _logRepo.GetRecentAsync(take);
            return logs.Select(l => new FormActivityLogDto
            {
                Id = l.Id,
                Action = l.Action,
                TargetName = l.TargetName,
                Details = l.Details,
                UserName = l.UserName,
                CreatedAt = l.CreatedAt
            }).ToList();
        }

        private async Task LogAsync(string action, Guid? folderId, Guid? documentId,
            string targetName, string details, string userName)
        {
            await _logRepo.AddAsync(new FormActivityLog
            {
                Id = Guid.NewGuid(),
                FolderId = folderId,
                FormDocumentId = documentId,
                Action = action,
                TargetName = targetName,
                Details = details,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}