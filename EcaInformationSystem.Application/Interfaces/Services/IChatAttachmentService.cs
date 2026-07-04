namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IChatAttachmentService
    {
        Task<Guid> UploadAttachmentAsync(Stream fileStream, string fileName, string contentType, Guid uploaderId);
        Task<(byte[] Data, string ContentType, string FileName)?> GetFullAttachmentAsync(Guid attachmentId);
    }
}
