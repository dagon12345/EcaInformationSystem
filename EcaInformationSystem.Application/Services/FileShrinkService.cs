using System.Diagnostics;
using DocumentFormat.OpenXml.Packaging;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace EcaInformationSystem.Application.Services
{
    // Lets an over-10MB PDF/DOCX/XLSX get uploaded anyway instead of being
    // flatly rejected — PDFs are shrunk via Ghostscript (downsamples/recompresses
    // embedded images; there's no solid pure-.NET way to recompress an
    // already-produced PDF), DOCX/XLSX are shrunk in-process by recompressing
    // their embedded raster images via OpenXML + ImageSharp.
    public class FileShrinkService : IFileShrinkService
    {
        private const string PdfContentType = "application/pdf";
        private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly string _ghostscriptPath;

        public FileShrinkService(IConfiguration configuration)
        {
            _ghostscriptPath = configuration["Ghostscript:ExecutablePath"] ?? "gswin64c";
        }

        public bool CanShrink(string contentType) => contentType switch
        {
            PdfContentType => true,
            DocxContentType => true,
            XlsxContentType => true,
            _ => false
        };

        public async Task<byte[]> ShrinkAsync(byte[] data, string contentType, ShrinkQuality quality, CancellationToken ct = default)
        {
            return contentType switch
            {
                PdfContentType => await ShrinkPdfAsync(data, quality, ct),
                DocxContentType => ShrinkOpenXmlImages(data, isWord: true, quality),
                XlsxContentType => ShrinkOpenXmlImages(data, isWord: false, quality),
                _ => throw new InvalidOperationException("This file type cannot be automatically shrunk.")
            };
        }

        // ── PDF — Ghostscript ────────────────────────────────────────────────
        private async Task<byte[]> ShrinkPdfAsync(byte[] data, ShrinkQuality quality, CancellationToken ct)
        {
            var pdfSetting = quality switch
            {
                ShrinkQuality.High => "/screen",
                ShrinkQuality.Medium => "/ebook",
                ShrinkQuality.Low => "/printer",
                _ => "/ebook"
            };

            var workDir = Path.Combine(Path.GetTempPath(), "eca-pdf-shrink");
            Directory.CreateDirectory(workDir);
            var inPath = Path.Combine(workDir, $"{Guid.NewGuid():N}.pdf");
            var outPath = Path.Combine(workDir, $"{Guid.NewGuid():N}.pdf");

            try
            {
                await File.WriteAllBytesAsync(inPath, data, ct);

                var psi = new ProcessStartInfo
                {
                    FileName = _ghostscriptPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-sDEVICE=pdfwrite");
                psi.ArgumentList.Add("-dCompatibilityLevel=1.4");
                psi.ArgumentList.Add($"-dPDFSETTINGS={pdfSetting}");
                psi.ArgumentList.Add("-dNOPAUSE");
                psi.ArgumentList.Add("-dBATCH");
                psi.ArgumentList.Add("-dQUIET");
                psi.ArgumentList.Add($"-sOutputFile={outPath}");
                psi.ArgumentList.Add(inPath);

                using var process = new Process { StartInfo = psi };

                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Could not start Ghostscript ('{_ghostscriptPath}'). Make sure it's installed on the server " +
                        "and the configured Ghostscript:ExecutablePath is correct.", ex);
                }

                var stderrTask = process.StandardError.ReadToEndAsync(ct);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(90));

                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    TryKill(process);
                    throw new InvalidOperationException(
                        "Shrinking this PDF took too long and was cancelled. Try a lower-resolution scan or a smaller file.");
                }

                if (process.ExitCode != 0 || !File.Exists(outPath))
                {
                    var stderr = await stderrTask;
                    throw new InvalidOperationException(
                        $"Ghostscript could not shrink this PDF: {(string.IsNullOrWhiteSpace(stderr) ? "unknown error" : stderr.Trim())}");
                }

                return await File.ReadAllBytesAsync(outPath, ct);
            }
            finally
            {
                TryDelete(inPath);
                TryDelete(outPath);
            }
        }

        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* best effort */ }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }

        // ── DOCX/XLSX — recompress embedded raster images in place ─────────────
        private static byte[] ShrinkOpenXmlImages(byte[] data, bool isWord, ShrinkQuality quality)
        {
            var (maxDim, jpegQuality) = quality switch
            {
                ShrinkQuality.High => (1000, 40),
                ShrinkQuality.Medium => (1400, 60),
                ShrinkQuality.Low => (1800, 75),
                _ => (1400, 60)
            };

            using var ms = new MemoryStream();
            ms.Write(data, 0, data.Length);
            ms.Position = 0;

            if (isWord)
            {
                using var doc = WordprocessingDocument.Open(ms, true);
                var mainPart = doc.MainDocumentPart;
                if (mainPart != null)
                {
                    foreach (var imagePart in mainPart.ImageParts.ToList())
                        ShrinkImagePart(imagePart, maxDim, jpegQuality);

                    foreach (var headerPart in mainPart.HeaderParts)
                        foreach (var imagePart in headerPart.ImageParts.ToList())
                            ShrinkImagePart(imagePart, maxDim, jpegQuality);

                    foreach (var footerPart in mainPart.FooterParts)
                        foreach (var imagePart in footerPart.ImageParts.ToList())
                            ShrinkImagePart(imagePart, maxDim, jpegQuality);
                }
            }
            else
            {
                using var doc = SpreadsheetDocument.Open(ms, true);
                var workbookPart = doc.WorkbookPart;
                if (workbookPart != null)
                {
                    foreach (var worksheetPart in workbookPart.WorksheetParts)
                    {
                        var drawingsPart = worksheetPart.DrawingsPart;
                        if (drawingsPart == null) continue;

                        foreach (var imagePart in drawingsPart.ImageParts.ToList())
                            ShrinkImagePart(imagePart, maxDim, jpegQuality);
                    }
                }
            }

            return ms.ToArray();
        }

        // Best-effort per image: if one embedded image can't be processed (odd
        // format, corrupt data), it's left untouched rather than failing the
        // whole document's shrink — and a re-encode is only kept if it's
        // actually smaller, so this can never make the file bigger.
        private static void ShrinkImagePart(ImagePart imagePart, int maxDim, int jpegQuality)
        {
            try
            {
                byte[] originalBytes;
                using (var partStream = imagePart.GetStream(FileMode.Open, FileAccess.Read))
                using (var buffer = new MemoryStream())
                {
                    partStream.CopyTo(buffer);
                    originalBytes = buffer.ToArray();
                }

                using var image = Image.Load(originalBytes);
                var format = image.Metadata.DecodedImageFormat;

                if (image.Width > maxDim || image.Height > maxDim)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(maxDim, maxDim)
                    }));
                }

                using var outStream = new MemoryStream();
                if (format is JpegFormat)
                {
                    image.Save(outStream, new JpegEncoder { Quality = jpegQuality });
                }
                else if (format is PngFormat)
                {
                    image.Save(outStream, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression });
                }
                else
                {
                    // Rare embedded format (bmp/tiff/gif/etc.) — skip rather than
                    // risk a content-type mismatch inside the OPC package.
                    return;
                }

                if (outStream.Length < originalBytes.LongLength)
                {
                    outStream.Position = 0;
                    imagePart.FeedData(outStream);
                }
            }
            catch
            {
                // Leave this image untouched; the rest of the document still shrinks.
            }
        }
    }
}
