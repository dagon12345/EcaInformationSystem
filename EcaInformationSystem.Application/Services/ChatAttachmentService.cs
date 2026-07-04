using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Domain.Entities.ChatEntities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace EcaInformationSystem.Application.Services
{
    public class ChatAttachmentService : IChatAttachmentService
    {
        private readonly IChatRepository _repo;
        private readonly IPdfCompressionService _pdfCompression; // ✅ reuse your existing service

        private const long MaxImageSizeBytes = 5 * 1024 * 1024;   // 5MB
        private const long MaxPdfSizeBytes = 10 * 1024 * 1024;    // 10MB

        private const int MaxImageDimension = 960;
        private const int ThumbnailDimension = 150;
        private const int JpegQuality = 65;

        private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };
        private const string AllowedPdfType = "application/pdf";

        public ChatAttachmentService(IChatRepository repo, IPdfCompressionService pdfCompression)
        {
            _repo = repo;
            _pdfCompression = pdfCompression;
        }

        public async Task<Guid> UploadAttachmentAsync(
     Stream fileStream, string fileName, string contentType, Guid uploaderId)
        {
            var normalizedType = contentType.ToLowerInvariant();
            var isImage = AllowedImageTypes.Contains(normalizedType);
            var isPdf = normalizedType == "application/pdf";
            var isOtherDocument = !isPdf && AllowedDocumentTypes.ContainsKey(normalizedType);

            if (!isImage && !isPdf && !isOtherDocument)
                throw new InvalidOperationException(
                    "Unsupported file type. Allowed: images (JPEG/PNG/WEBP), PDF, Word, Excel, PowerPoint, TXT, and CSV files.");

            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var originalSize = memoryStream.Length;

            ChatAttachment attachment;

            if (isImage)
            {
                if (originalSize > MaxImageSizeBytes)
                    throw new InvalidOperationException("Image exceeds the 5MB size limit.");

                attachment = await CompressImageAsync(memoryStream, fileName);
            }
            else if (isPdf)
            {
                if (originalSize > MaxPdfSizeBytes)
                    throw new InvalidOperationException("PDF exceeds the 10MB size limit.");

                attachment = await CompressPdfAsync(memoryStream, fileName, originalSize);
            }
            else
            {
                // ✅ Other documents — same 10MB ceiling as PDF, no compression
                // attempted (Office formats are already compressed internally;
                // trying to re-squeeze them offers negligible benefit for real risk
                // of corrupting the file).
                if (originalSize > MaxPdfSizeBytes)
                    throw new InvalidOperationException("Document exceeds the 10MB size limit.");

                attachment = new ChatAttachment
                {
                    FileData = memoryStream.ToArray(),
                    ThumbnailData = null,
                    ContentType = contentType,
                    OriginalFileName = fileName,
                    FileSizeBytes = originalSize,
                    Type = ChatAttachmentType.Document // see enum update below
                };
            }

            attachment.Id = Guid.NewGuid();
            attachment.ChatMessageId = null;

            await _repo.AddChatAttachmentAsync(attachment);
            await _repo.SaveChangesAsync();

            return attachment.Id;
        }
        private async Task<ChatAttachment> CompressImageAsync(MemoryStream input, string fileName)
        {
            using var image = await Image.LoadAsync(input);
            image.Mutate(x => x.AutoOrient());

            if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxImageDimension, MaxImageDimension)
                }));
            }

            using var fullOutput = new MemoryStream();
            await image.SaveAsync(fullOutput, new JpegEncoder { Quality = JpegQuality });

            using var thumbClone = image.Clone(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(ThumbnailDimension, ThumbnailDimension)
            }));
            using var thumbOutput = new MemoryStream();
            await thumbClone.SaveAsync(thumbOutput, new JpegEncoder { Quality = 60 });

            return new ChatAttachment
            {
                FileData = fullOutput.ToArray(),
                ThumbnailData = thumbOutput.ToArray(),
                ContentType = "image/jpeg",
                OriginalFileName = fileName,
                FileSizeBytes = fullOutput.Length,
                Type = ChatAttachmentType.Image
            };
        }

        private async Task<ChatAttachment> CompressPdfAsync(MemoryStream input, string fileName, long originalSize)
        {
            input.Position = 0;

            // ✅ Same exact service/logic your BeneficiaryDocumentService already
            // uses — genuinely mirrored, not reimplemented.
            var compressedBytes = await _pdfCompression.CompressAsync(input);

            // ✅ Same "keep whichever is smaller" safety check as BeneficiaryDocumentService —
            // compression can occasionally produce a LARGER file for already-optimized PDFs.
            byte[] finalBytes;
            if (compressedBytes.Length < originalSize)
            {
                finalBytes = compressedBytes;
            }
            else
            {
                input.Position = 0;
                finalBytes = input.ToArray();
            }

            return new ChatAttachment
            {
                FileData = finalBytes,
                ThumbnailData = null,
                ContentType = AllowedPdfType,
                OriginalFileName = fileName,
                FileSizeBytes = finalBytes.Length,
                Type = ChatAttachmentType.Pdf
            };
        }

        public async Task<(byte[] Data, string ContentType, string FileName)?> GetFullAttachmentAsync(Guid attachmentId)
        {
            var attachment = await _repo.GetChatAttachmentByIdAsync(attachmentId);
            if (attachment == null) return null;

            return (attachment.FileData, attachment.ContentType, attachment.OriginalFileName);
        }
        // ✅ NEW — common office document types
        private static readonly Dictionary<string, string> AllowedDocumentTypes = new()
        {
            ["application/pdf"] = ".pdf",
            ["application/msword"] = ".doc",
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
            ["application/vnd.ms-excel"] = ".xls",
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
            ["application/vnd.ms-powerpoint"] = ".ppt",
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = ".pptx",
            ["text/plain"] = ".txt",
            ["text/csv"] = ".csv"
        };
    }
}