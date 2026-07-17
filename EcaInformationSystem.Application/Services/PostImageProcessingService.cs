using EcaInformationSystem.Application.Interfaces.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace EcaInformationSystem.Application.Services
{
    public class PostImageProcessingService : IPostImageProcessingService
    {
        private const int MaxFullEdge = 1600;
        private const int MaxThumbEdge = 720;
        private const int JpegQuality = 85;

        public async Task<(byte[] fullData, byte[] thumbData, int width, int height)> ProcessAsync(Stream input)
        {
            using var image = await Image.LoadAsync(input);

            // ✅ ImageSharp auto-applies EXIF orientation on load and strips it
            // afterward — same "no more sideways photos" fix you already solved
            // in ImageToPdfService, done here at the library level.
            image.Mutate(x => x.AutoOrient());

            int originalWidth = image.Width;
            int originalHeight = image.Height;

            // Full version — cap the longest edge, don't upscale
            using var full = image.Clone(ctx =>
            {
                if (originalWidth > MaxFullEdge || originalHeight > MaxFullEdge)
                    ctx.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(MaxFullEdge, MaxFullEdge)
                    });
            });

            using var fullStream = new MemoryStream();
            await full.SaveAsync(fullStream, new JpegEncoder { Quality = JpegQuality });

            // Thumbnail — small, for the grid
            using var thumb = image.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxThumbEdge, MaxThumbEdge)
            }));

            using var thumbStream = new MemoryStream();
            await thumb.SaveAsync(thumbStream, new JpegEncoder { Quality = JpegQuality });

            return (fullStream.ToArray(), thumbStream.ToArray(), full.Width, full.Height);
        }
    }
}

