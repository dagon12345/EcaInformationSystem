namespace EcaInformationSystem.Domain.Entities
{
    public class FormFolder
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        // One level of nesting only — a subfolder's ParentFolderId points at a
        // root folder; a subfolder may not itself have children (enforced in
        // FormFolderService, not here).
        public Guid? ParentFolderId { get; set; }
        public FormFolder? ParentFolder { get; set; }
        public ICollection<FormFolder> ChildFolders { get; set; } = new List<FormFolder>();

        public ICollection<FormDocument> Documents { get; set; } = new List<FormDocument>();
    }
}

