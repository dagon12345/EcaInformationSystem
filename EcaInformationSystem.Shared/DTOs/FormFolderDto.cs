namespace EcaInformationSystem.Shared.DTOs
{
    public class FormFolderDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DocumentCount { get; set; }
        public Guid? ParentFolderId { get; set; }
        public string? ParentFolderName { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class FormFolderCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? ParentFolderId { get; set; }
    }

    public class FormFolderUpdateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class FormActivityLogDto
    {
        public Guid Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string TargetName { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}