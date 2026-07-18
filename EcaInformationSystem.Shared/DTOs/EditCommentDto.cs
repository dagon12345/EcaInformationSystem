namespace EcaInformationSystem.Shared.DTOs
{
    public class EditCommentDto
    {
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(1000)]
        public string Content { get; set; } = string.Empty;
    }
}
