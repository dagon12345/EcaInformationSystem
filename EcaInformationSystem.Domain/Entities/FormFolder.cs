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

        // Self-referencing — folders can be nested arbitrarily deep.
        public Guid? ParentFolderId { get; set; }
        public FormFolder? ParentFolder { get; set; }
        public ICollection<FormFolder> ChildFolders { get; set; } = new List<FormFolder>();

        public ICollection<FormDocument> Documents { get; set; } = new List<FormDocument>();
    }
}

