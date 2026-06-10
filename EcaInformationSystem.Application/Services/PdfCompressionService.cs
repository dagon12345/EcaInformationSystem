using EcaInformationSystem.Application.Interfaces.Services;
using iText.Kernel.Pdf;

namespace EcaInformationSystem.Application.Services
{
    public class PdfCompressionService : IPdfCompressionService
    {
        public async Task<byte[]> CompressAsync(Stream inputStream)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var inputMs = new MemoryStream();
                    inputStream.CopyTo(inputMs);
                    inputMs.Position = 0;

                    using var outputMs = new MemoryStream();

                    using var reader = new PdfReader(inputMs);

                    var writerProps = new WriterProperties()
                        .SetCompressionLevel(CompressionConstants.BEST_COMPRESSION)
                        .UseSmartMode();

                        using var writer = new PdfWriter(outputMs, writerProps);
                        using var pdfDoc = new PdfDocument(reader, writer);

                        pdfDoc.Close();

                        return outputMs.ToArray();
                }
                catch
                {
                     // If compression fails for any reason,
                    // return the original stream as-is
                    inputStream.Position = 0;
                    using var fallback = new MemoryStream();
                    inputStream.CopyTo(fallback);
                    return fallback.ToArray();
                }
            });
        }
    }
}