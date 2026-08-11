using DocumentFormat.OpenXml.Packaging;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using SharpImage = SixLabors.ImageSharp.Image;

namespace EcaInformationSystem.Application.Services
{
    // Lets an over-10MB PDF/DOCX/XLSX get uploaded anyway instead of being
    // flatly rejected, by recompressing embedded raster images. Deliberately
    // pure in-process .NET (iText7 + OpenXML + ImageSharp, all already
    // dependencies) — no external process is spawned, because shared ASP.NET
    // hosts (this app is deployed to MonsterASP.NET) block Process.Start
    // entirely, which ruled out a Ghostscript-based approach for PDFs.
    public class FileShrinkService : IFileShrinkService
    {
        private const string PdfContentType = "application/pdf";
        private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public bool CanShrink(string contentType) => contentType switch
        {
            PdfContentType => true,
            DocxContentType => true,
            XlsxContentType => true,
            _ => false
        };

        public Task<byte[]> ShrinkAsync(byte[] data, string contentType, ShrinkQuality quality, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                return contentType switch
                {
                    PdfContentType => ShrinkPdf(data, quality),
                    DocxContentType => ShrinkOpenXmlImages(data, isWord: true, quality),
                    XlsxContentType => ShrinkOpenXmlImages(data, isWord: false, quality),
                    _ => throw new InvalidOperationException("This file type cannot be automatically shrunk.")
                };
            }, ct);
        }

        // ── PDF — recompress embedded raster images via iText7 ──────────────────
        // Root-caused against a real corrupted-in-production sample
        // (Butuan-City_Payroll_2024.pdf, CamScanner output): the FIRST version
        // of this mutated an existing image XObject's stream in place
        // (Remove/Put dictionary keys + PdfStream.SetData(newJpegBytes)). That
        // let iText's writer silently re-Flate the already-JPEG-encoded bytes
        // while the dictionary still said /DCTDecode — verified via pikepdf:
        // the shipped object ended up with /Filter /FlateDecode and zlib-magic
        // (0x78 0xDA) bytes, which is why it rendered as a garbled strip.
        //
        // Fix: never mutate the old stream. Build a brand-new, self-contained
        // PdfImageXObject from the re-encoded JPEG (iText derives correct
        // /Filter=/DCTDecode, /ColorSpace, /Width, /Height, /BitsPerComponent
        // for it, the same code path proven correct when building a PDF from
        // scratch), make it indirect, and swap the RESOURCE DICTIONARY's
        // pointer to it — no manual byte/dict surgery on the original stream
        // at all. Re-verified end-to-end against all 38 pages of that same
        // sample file (26.75 MB → 1.56/3.28/5.79 MB across High/Medium/Low,
        // every page re-rendered and visually confirmed readable) before
        // being re-enabled here.
        private static byte[] ShrinkPdf(byte[] data, ShrinkQuality quality)
        {
            var (maxDim, jpegQuality) = quality switch
            {
                ShrinkQuality.High => (900, 35),
                ShrinkQuality.Medium => (1300, 55),
                ShrinkQuality.Low => (1700, 70),
                _ => (1300, 55)
            };

            byte[] shrunk;
            try
            {
                using var input = new MemoryStream(data);
                using var output = new MemoryStream();

                var writerProps = new WriterProperties()
                    .SetCompressionLevel(CompressionConstants.BEST_COMPRESSION)
                    .UseSmartMode();

                using (var reader = new PdfReader(input))
                using (var writer = new PdfWriter(output, writerProps))
                using (var pdfDoc = new PdfDocument(reader, writer))
                {
                    var visited = new HashSet<PdfIndirectReference>();
                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        var page = pdfDoc.GetPage(i);
                        ShrinkImagesInResources(pdfDoc, page.GetResources(), maxDim, jpegQuality, visited);
                    }
                }

                shrunk = output.ToArray();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not process this PDF for shrinking: {ex.Message}", ex);
            }

            // Belt-and-suspenders: re-parse before trusting the result. If it
            // doesn't open cleanly, treat the shrink as failed rather than
            // silently persisting a broken PDF.
            try
            {
                using var verifyStream = new MemoryStream(shrunk);
                using var verifyReader = new PdfReader(verifyStream);
                using var verifyDoc = new PdfDocument(verifyReader);
                _ = verifyDoc.GetNumberOfPages();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Shrinking produced an invalid PDF and was discarded: {ex.Message}", ex);
            }

            return shrunk;
        }

        private static void ShrinkImagesInResources(PdfDocument pdfDoc, PdfResources resources, int maxDim, int jpegQuality, HashSet<PdfIndirectReference> visited)
        {
            var xobjectDict = resources.GetResource(PdfName.XObject);
            if (xobjectDict == null) return;

            foreach (var key in xobjectDict.KeySet().ToList())
            {
                var xobjStream = xobjectDict.GetAsStream(key);
                if (xobjStream == null) continue;

                // Skip an XObject shared across multiple pages (e.g. a letterhead
                // logo) if it's already been processed once.
                var indirectRef = xobjStream.GetIndirectReference();
                if (indirectRef != null && !visited.Add(indirectRef)) continue;

                var subtype = xobjStream.GetAsName(PdfName.Subtype);

                if (PdfName.Form.Equals(subtype))
                {
                    var formXObject = new PdfFormXObject(xobjStream);
                    ShrinkImagesInResources(pdfDoc, formXObject.GetResources(), maxDim, jpegQuality, visited);
                    continue;
                }

                if (!PdfName.Image.Equals(subtype)) continue;

                ShrinkSingleImage(pdfDoc, xobjectDict, key, xobjStream, maxDim, jpegQuality);
            }
        }

        // Best-effort per image: if one embedded image can't be processed (odd
        // colorspace, corrupt data), it's left untouched rather than failing the
        // whole document — and a re-encode is only kept if it's actually
        // smaller, so this can never make the file bigger.
        private static void ShrinkSingleImage(PdfDocument pdfDoc, PdfDictionary xobjectDict, PdfName key, PdfStream imageStream, int maxDim, int jpegQuality)
        {
            try
            {
                var imageXObject = new PdfImageXObject(imageStream);
                var originalBytes = imageXObject.GetImageBytes(true);
                if (originalBytes == null || originalBytes.Length == 0) return;

                using var image = SharpImage.Load(originalBytes);

                if (image.Width > maxDim || image.Height > maxDim)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(maxDim, maxDim)
                    }));
                }

                using var outStream = new MemoryStream();
                image.Save(outStream, new JpegEncoder { Quality = jpegQuality });
                var newBytes = outStream.ToArray();

                if (newBytes.LongLength >= originalBytes.LongLength) return; // never make it worse

                var newImageData = ImageDataFactory.Create(newBytes);
                var replacement = new PdfImageXObject(newImageData);
                var replacementStream = replacement.GetPdfObject();
                replacementStream.MakeIndirect(pdfDoc);
                xobjectDict.Put(key, replacementStream);
            }
            catch
            {
                // Leave this image untouched; the rest of the document still shrinks.
            }
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
                        ShrinkOpenXmlImagePart(imagePart, maxDim, jpegQuality);

                    foreach (var headerPart in mainPart.HeaderParts)
                        foreach (var imagePart in headerPart.ImageParts.ToList())
                            ShrinkOpenXmlImagePart(imagePart, maxDim, jpegQuality);

                    foreach (var footerPart in mainPart.FooterParts)
                        foreach (var imagePart in footerPart.ImageParts.ToList())
                            ShrinkOpenXmlImagePart(imagePart, maxDim, jpegQuality);
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
                            ShrinkOpenXmlImagePart(imagePart, maxDim, jpegQuality);
                    }
                }
            }

            return ms.ToArray();
        }

        // Best-effort per image: if one embedded image can't be processed (odd
        // format, corrupt data), it's left untouched rather than failing the
        // whole document's shrink — and a re-encode is only kept if it's
        // actually smaller, so this can never make the file bigger.
        private static void ShrinkOpenXmlImagePart(ImagePart imagePart, int maxDim, int jpegQuality)
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

                using var image = SharpImage.Load(originalBytes);
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
