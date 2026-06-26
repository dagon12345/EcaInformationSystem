using EcaInformationSystem.Application.Interfaces.Services;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace EcaInformationSystem.Application.Services
{
    public class ImageToPdfService : IImageToPdfService
    {
        // Hard cap per image, in bytes. 1MB = 1,048,576 bytes; we target
        // slightly under that to leave headroom for JPEG encoding overhead.
        private const long MaxImageBytes = 1_000_000;

        // Don't bother resizing below this — going smaller than this
        // starts visibly hurting legibility of scanned documents/IDs.
        private const int MinDimension = 800;

        public async Task<byte[]> ConvertToPdfAsync(List<byte[]> imageBytesList)
        {
            if (imageBytesList is null || imageBytesList.Count == 0)
                throw new ArgumentException("At least one image is required.");

            using var outputStream = new MemoryStream();
            using var writer = new PdfWriter(outputStream);
            using var pdfDoc = new PdfDocument(writer);

            using var document = new Document(pdfDoc, PageSize.A4);
            document.SetMargins(20, 20, 20, 20);

            for (int i = 0; i < imageBytesList.Count; i++)
            {
                // Compress/resize this image down to under ~1MB before
                // it ever touches the PDF.
                var compressedBytes = await CompressImageAsync(imageBytesList[i]);

                var imageData = iText.IO.Image.ImageDataFactory.Create(compressedBytes);
                var image = new Image(imageData);

                var pageSize = pdfDoc.GetDefaultPageSize();
                float maxWidth = pageSize.GetWidth() - 40;
                float maxHeight = pageSize.GetHeight() - 40;

                image.SetAutoScale(true);
                image.SetMaxWidth(maxWidth);
                image.SetMaxHeight(maxHeight);
                image.SetHorizontalAlignment(HorizontalAlignment.CENTER);

                document.Add(image);

                if (i < imageBytesList.Count - 1)
                    document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
            }

            document.Close();

            return outputStream.ToArray();
        }

        /// <summary>
        /// Re-encodes the image as JPEG, progressively lowering quality and
        /// then dimensions, until it fits under MaxImageBytes — or until
        /// further reduction stops helping, whichever comes first.
        /// </summary>
        private static async Task<byte[]> CompressImageAsync(byte[] originalBytes)
        {
            using var inputStream = new MemoryStream(originalBytes);
            using var img = await SixLabors.ImageSharp.Image.LoadAsync(inputStream);

            // Pass 1 — try quality reduction alone first, since this
            // usually preserves the most legibility for scanned text/IDs.
            var qualitySteps = new[] { 85, 75, 65, 55, 45, 35 };

            foreach (var quality in qualitySteps)
            {
                var bytes = await EncodeJpegAsync(img, quality);
                if (bytes.Length <= MaxImageBytes)
                    return bytes;
            }

            // Pass 2 — quality alone wasn't enough (very large/high-res
            // photo). Progressively shrink dimensions, re-trying a
            // moderate quality at each size, until it fits or we hit the
            // legibility floor.
            var currentImg = img;
            var width = currentImg.Width;
            var height = currentImg.Height;

            while (Math.Min(width, height) > MinDimension)
            {
                width = (int)(width * 0.8);
                height = (int)(height * 0.8);

                using var resized = currentImg.Clone(ctx => ctx.Resize(width, height));
                var bytes = await EncodeJpegAsync(resized, 60);

                if (bytes.Length <= MaxImageBytes)
                    return bytes;
            }

            // Last resort — return the smallest attempt we produced,
            // even if it's still slightly over the cap. This only happens
            // for unusually dense/noisy images; better to upload something
            // close to the target than fail the whole request.
            using var finalResized = currentImg.Clone(ctx => ctx.Resize(width, height));
            return await EncodeJpegAsync(finalResized, 40);
        }

        private static async Task<byte[]> EncodeJpegAsync(SixLabors.ImageSharp.Image image, int quality)
        {
            using var ms = new MemoryStream();
            var encoder = new JpegEncoder { Quality = quality };
            await image.SaveAsync(ms, encoder);
            return ms.ToArray();
        }
    }
}