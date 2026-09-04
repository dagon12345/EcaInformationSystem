// Application/Services/LivenessPhotoWordDocumentBuilder.cs
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace EcaInformationSystem.Application.Services
{
    // Wraps a submitted liveness selfie in an editable .docx (name/date as a
    // caption above the image) so a PDO can download it straight into Word
    // instead of downloading the raw JPEG and building a document around it
    // by hand. Same OpenXml image-embedding approach as CoeWordDocumentBuilder.
    public static class LivenessPhotoWordDocumentBuilder
    {
        private const int PageWidth = 11906;   // A4, DXA
        private const int PageHeight = 16838;
        private const int MarginTopBottom = 1000;
        private const int MarginLeftRight = 1000;
        private const int ContentWidth = PageWidth - (2 * MarginLeftRight);

        public static byte[] Build(byte[] photoBytes, string granteeName, DateTime? submittedDate, string status)
        {
            using var stream = new MemoryStream();

            using (var wordDoc = WordprocessingDocument.Create(
                stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = new Body();

                body.AppendChild(new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                    new Run(
                        new RunProperties(new Bold(), new FontSize { Val = "28" }),
                        new Text("Liveness Verification Photo"))));

                body.AppendChild(new Paragraph(new Run(new Text(""))));

                body.AppendChild(CenteredLabelValue("Grantee:", granteeName));
                body.AppendChild(CenteredLabelValue("Submitted:",
                    submittedDate.HasValue ? submittedDate.Value.ToLocalTime().ToString("MMMM d, yyyy h:mm tt") : "—"));
                body.AppendChild(CenteredLabelValue("Status:", status));

                body.AppendChild(new Paragraph(new Run(new Text(""))));

                var relId = AddImagePart(mainPart, photoBytes);

                // Fit within the page width/height, preserving aspect ratio —
                // the source photo is a phone selfie, so orientation/dimensions
                // aren't known ahead of time.
                var (widthEmu, heightEmu) = GetScaledImageSize(photoBytes, ContentWidth, PageHeight - (2 * MarginTopBottom) - 4000);

                body.AppendChild(new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                    new Run(BuildImageDrawing(relId, widthEmu, heightEmu))));

                body.AppendChild(new SectionProperties(
                    new PageSize { Width = (UInt32Value)(uint)PageWidth, Height = (UInt32Value)(uint)PageHeight },
                    new PageMargin
                    {
                        Top = MarginTopBottom,
                        Bottom = MarginTopBottom,
                        Left = (UInt32Value)(uint)MarginLeftRight,
                        Right = (UInt32Value)(uint)MarginLeftRight,
                        Header = 720,
                        Footer = 720
                    }));

                mainPart.Document.AppendChild(body);
                mainPart.Document.Save();
            }

            return stream.ToArray();
        }

        private static Paragraph CenteredLabelValue(string label, string value)
        {
            var para = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
            para.AppendChild(new Run(
                new RunProperties(new Bold(), new FontSize { Val = "20" }),
                new Text($"{label} ") { Space = SpaceProcessingModeValues.Preserve }));
            para.AppendChild(new Run(
                new RunProperties(new FontSize { Val = "20" }),
                new Text(value)));
            return para;
        }

        private static string AddImagePart(MainDocumentPart mainPart, byte[] imageBytes)
        {
            var imagePart = mainPart.AddImagePart(ImagePartType.Jpeg);
            using var ms = new MemoryStream(imageBytes);
            imagePart.FeedData(ms);
            return mainPart.GetIdOfPart(imagePart);
        }

        // Reads the JPEG's own pixel dimensions (via ImageSharp, already a
        // dependency in this project) and scales to fit within the page while
        // keeping aspect ratio — a phone selfie's orientation isn't known
        // ahead of time, so a fixed size would either overflow the page or
        // leave it comically small.
        private static (int widthEmu, int heightEmu) GetScaledImageSize(byte[] imageBytes, int maxWidthDxa, int maxHeightDxa)
        {
            const double EmuPerDxa = 635.0; // 1 DXA (1/20 pt) = 635 EMU

            int pixelWidth, pixelHeight;
            using (var image = SixLabors.ImageSharp.Image.Load(imageBytes))
            {
                pixelWidth = image.Width;
                pixelHeight = image.Height;
            }

            var maxWidthEmu = maxWidthDxa * EmuPerDxa;
            var maxHeightEmu = maxHeightDxa * EmuPerDxa;

            // 96 DPI assumption for the source pixel size, converted to EMU (914400 EMU/inch).
            const double EmuPerPixel = 914400.0 / 96.0;
            double widthEmu = pixelWidth * EmuPerPixel;
            double heightEmu = pixelHeight * EmuPerPixel;

            var scale = Math.Min(maxWidthEmu / widthEmu, maxHeightEmu / heightEmu);
            if (scale < 1)
            {
                widthEmu *= scale;
                heightEmu *= scale;
            }

            return ((int)widthEmu, (int)heightEmu);
        }

        private static Drawing BuildImageDrawing(string relId, int widthEmu, int heightEmu)
        {
            var inline = new DW.Inline(
                new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = 1U, Name = "LivenessPhoto" },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new Pic.Picture(
                            new Pic.NonVisualPictureProperties(
                                new Pic.NonVisualDrawingProperties { Id = 0U, Name = "LivenessPhoto" },
                                new Pic.NonVisualPictureDrawingProperties()),
                            new Pic.BlipFill(
                                new A.Blip { Embed = relId },
                                new A.Stretch(new A.FillRectangle())),
                            new Pic.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                new A.PresetGeometry(new A.AdjustValueList())
                                { Preset = A.ShapeTypeValues.Rectangle })
                        )
                    )
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
            )
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            };

            return new Drawing(inline);
        }
    }
}
