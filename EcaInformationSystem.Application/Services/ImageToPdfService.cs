using EcaInformationSystem.Application.Interfaces.Services;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Properties;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;
using AreaBreak = iText.Layout.Element.AreaBreak;
using PdfImage = iText.Layout.Element.Image;
// ── Explicit aliases to resolve the Image name collision between
// iText.Layout.Element.Image and SixLabors.ImageSharp.Image ──────────────────
using SharpImage = SixLabors.ImageSharp.Image;

namespace EcaInformationSystem.Application.Services
{
    public class ImageToPdfService : IImageToPdfService
    {
        private const long MaxImageBytes = 1_000_000;
        private const int MinDimension = 800;

        public async Task<byte[]> ConvertToPdfAsync(List<byte[]> imageBytesList)
        {
            if (imageBytesList is null || imageBytesList.Count == 0)
                throw new ArgumentException("At least one image is required.");

            using var outputStream = new MemoryStream();
            using var writer = new PdfWriter(outputStream);
            using var pdfDoc = new PdfDocument(writer);
            using var document = new iText.Layout.Document(pdfDoc, PageSize.A4);
            document.SetMargins(20, 20, 20, 20);

            for (int i = 0; i < imageBytesList.Count; i++)
            {
                // Step 1: auto-rotate based on EXIF orientation tag
                var rotatedBytes = await AutoRotateAsync(imageBytesList[i]);

                // Step 2: compress to soft size target
                var compressedBytes = await CompressImageAsync(rotatedBytes);

                // Step 3: embed into PDF page
                var imageData = iText.IO.Image.ImageDataFactory.Create(compressedBytes);
                var pdfImg = new PdfImage(imageData);

                var pageSize = pdfDoc.GetDefaultPageSize();
                float maxWidth = pageSize.GetWidth() - 40;
                float maxHeight = pageSize.GetHeight() - 40;

                pdfImg.SetAutoScale(true);
                pdfImg.SetMaxWidth(maxWidth);
                pdfImg.SetMaxHeight(maxHeight);
                pdfImg.SetHorizontalAlignment(HorizontalAlignment.CENTER);

                document.Add(pdfImg);

                if (i < imageBytesList.Count - 1)
                    document.Add(new AreaBreak(iText.Layout.Properties.AreaBreakType.NEXT_PAGE));
            }

            document.Close();
            return outputStream.ToArray();
        }

        private static async Task<byte[]> AutoRotateAsync(byte[] imageBytes)
        {
            using var ms = new MemoryStream(imageBytes);
            using var img = await SharpImage.LoadAsync(ms);

            if (img.Metadata.ExifProfile is null)
                return imageBytes;

            if (!img.Metadata.ExifProfile.TryGetValue(ExifTag.Orientation, out var orientation))
                return imageBytes;

            ushort value = orientation.Value;

            img.Mutate(ctx =>
            {
                switch (value)
                {
                    case 2: ctx.Flip(FlipMode.Horizontal); break;
                    case 3: ctx.Rotate(RotateMode.Rotate180); break;
                    case 4: ctx.Flip(FlipMode.Vertical); break;
                    case 5: ctx.Rotate(RotateMode.Rotate90); ctx.Flip(FlipMode.Horizontal); break;
                    case 6: ctx.Rotate(RotateMode.Rotate90); break;
                    case 7: ctx.Rotate(RotateMode.Rotate270); ctx.Flip(FlipMode.Horizontal); break;
                    case 8: ctx.Rotate(RotateMode.Rotate270); break;
                }
            });

            img.Metadata.ExifProfile?.RemoveValue(ExifTag.Orientation);

            using var outStream = new MemoryStream();
            await img.SaveAsJpegAsync(outStream, new JpegEncoder { Quality = 90 });
            return outStream.ToArray();
        }

        private static async Task<byte[]> CompressImageAsync(byte[] originalBytes)
        {
            using var inputStream = new MemoryStream(originalBytes);
            using var originalImg = await SharpImage.LoadAsync(inputStream);

            // Pass 1: quality reduction
            var qualitySteps = new[] { 85, 75, 65, 55, 45, 35 };
            foreach (var quality in qualitySteps)
            {
                var bytes = await EncodeJpegAsync(originalImg, quality);
                if (bytes.Length <= MaxImageBytes)
                    return bytes;
            }

            // Pass 2: progressive resize, each iteration from the previous result
            SharpImage? previousImage = null;
            SharpImage? currentImage = null;

            try
            {
                int width = originalImg.Width;
                int height = originalImg.Height;

                while (Math.Min(width, height) > MinDimension)
                {
                    width = (int)(width * 0.8);
                    height = (int)(height * 0.8);

                    var source = previousImage ?? originalImg;
                    currentImage = source.Clone(ctx => ctx.Resize(width, height));

                    var bytes = await EncodeJpegAsync(currentImage, 60);
                    if (bytes.Length <= MaxImageBytes)
                        return bytes;

                    previousImage?.Dispose();
                    previousImage = currentImage;
                    currentImage = null;
                }

                var lastSource = previousImage ?? originalImg;
                return await EncodeJpegAsync(lastSource, 40);
            }
            finally
            {
                currentImage?.Dispose();
                previousImage?.Dispose();
            }
        }

        private static async Task<byte[]> EncodeJpegAsync(SharpImage image, int quality)
        {
            using var ms = new MemoryStream();
            var encoder = new JpegEncoder { Quality = quality };
            await image.SaveAsync(ms, encoder);
            return ms.ToArray();
        }
    }
}