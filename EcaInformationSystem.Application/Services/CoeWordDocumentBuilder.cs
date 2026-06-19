// Application/Services/CoeWordDocumentBuilder.cs
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using EcaInformationSystem.Shared.DTOs;
using A = DocumentFormat.OpenXml.Drawing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace EcaInformationSystem.Application.Services
{
    public static class CoeWordDocumentBuilder
    {
        // ── Colors (hex, no '#', as OpenXML expects) ───────────────────────
        private const string NavyHex = "1F3864";
        private const string TitleBlueHex = "0000FF";
        private const string LinkBlueHex = "0563C1";
        private const string FooterAccentHex = "C0143C";
        private const string BlackHex = "000000";

        // ── Page geometry (DXA: 1440 = 1 inch) ──────────────────────────────
        private const int PageWidth = 11906;   // A4
        private const int PageHeight = 16838;  // A4
        private const int MarginTopBottom = 1000;
        private const int MarginLeftRight = 1000;
        private const int ContentWidth = PageWidth - (2 * MarginLeftRight);

        // Application/Services/CoeWordDocumentBuilder.cs — additions/changes only

        public static byte[] Build(List<CoePreviewGroupDto> groups, CoeSettingsDto settings)
        {
            using var stream = new MemoryStream();

            using (var wordDoc = WordprocessingDocument.Create(
                stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = new Body();

                string ncscRelId = AddImagePart(mainPart, CoeLogoProvider.NcscLogo, "ncscLogo");
                string bagongRelId = AddImagePart(mainPart, CoeLogoProvider.BagongPilipinasLogo, "bagongLogo");

                // ✅ Footer is created once and reused — its field codes
                // (PAGE / SECTIONPAGES) are inherently section-relative, so the
                // same footer content correctly restarts per section without
                // needing separate footer parts per municipality.
                var footerPart = mainPart.AddNewPart<FooterPart>();
                var footerRelId = mainPart.GetIdOfPart(footerPart);
                footerPart.Footer = BuildFooterContent();
                footerPart.Footer.Save();

                for (int g = 0; g < groups.Count; g++)
                {
                    var group = groups[g];
                    bool isLastMunicipality = g == groups.Count - 1;

                    AppendHeaderBlock(body, group, settings, ncscRelId, bagongRelId);

                    foreach (var yearGroup in group.YearGroups)
                        AppendYearTable(body, yearGroup);

                    // ✅ Capture the last element appended for THIS municipality's
                    // signatory block, so we can attach the section break to its
                    // final paragraph rather than appending a separate page break.
                    var lastParagraph = AppendSignatoryBlock(body, settings);

                    if (!isLastMunicipality)
                    {
                        // ✅ Section break embedded in the LAST paragraph of this
                        // municipality's content. This is the OOXML convention for
                        // every section boundary except the very last one in the
                        // document — those sectPr elements must live inside
                        // ParagraphProperties (w:pPr), not as standalone body
                        // children, or Word will reject the structure.
                        var sectionBreakProps = BuildSectionProperties(footerRelId, restartNumbering: true);

                        var pPr = lastParagraph.ParagraphProperties ?? new ParagraphProperties();
                        pPr.AppendChild(sectionBreakProps);
                        if (lastParagraph.ParagraphProperties == null)
                            lastParagraph.PrependChild(pPr);
                    }
                }

                // ✅ The FINAL section's sectPr is the only one that lives directly
                // in the body, as the last child — this also needs the footer
                // reference and pgNumType restart so the last municipality's
                // section is configured identically to every earlier one.
                var finalSectPr = BuildSectionProperties(footerRelId, restartNumbering: true);
                body.AppendChild(finalSectPr);

                mainPart.Document.AppendChild(body);
                mainPart.Document.Save();
            }

            return stream.ToArray();
        }

        private static Footer BuildFooterContent()
        {
            var para = new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { Before = "0" }));

            para.AppendChild(new Run(
                new RunProperties(new Bold(), new Italic(), new Color { Val = FooterAccentHex }, new FontSize { Val = "18" }),
                new Text("Pusò para sa Seniors")));

            para.AppendChild(PlainRun("   |   ☎ 09384143439/09553822435   |   ✉ ro13@ncsc.gov.ph   |   🌐 www.ncsc.gov.ph   |   Page ", 16));

            AppendPageField(para, "PAGE");

            para.AppendChild(PlainRun(" of ", 16));

            // ✅ SECTIONPAGES instead of NUMPAGES — counts pages within the
            // current section only, matching "this municipality's page count,"
            // not the whole document's.
            AppendPageField(para, "SECTIONPAGES");

            return new Footer(para);
        }

        // ✅ Correct field-code structure: Begin → instrText → Separate →
        // cached result text → End. The Separate marker and the placeholder
        // result run were MISSING in the previous version, which is exactly
        // why "Page  of " rendered with no numbers — Word had no cached
        // display value to show before recalculating on open.
        private static void AppendPageField(Paragraph para, string fieldName)
        {
            var runProps = new RunProperties(new FontSize { Val = "16" });

            var beginRun = new Run(runProps.CloneNode(true) as RunProperties,
                new FieldChar { FieldCharType = FieldCharValues.Begin });

            var instrRun = new Run(runProps.CloneNode(true) as RunProperties,
                new FieldCode($" {fieldName} ") { Space = SpaceProcessingModeValues.Preserve });

            var separateRun = new Run(runProps.CloneNode(true) as RunProperties,
                new FieldChar { FieldCharType = FieldCharValues.Separate });

            // Placeholder cached value — Word recalculates this on open/print,
            // but a value MUST be present here for the field to render anything
            // before that recalculation happens.
            var resultRun = new Run(runProps.CloneNode(true) as RunProperties,
                new Text("1") { Space = SpaceProcessingModeValues.Preserve });

            var endRun = new Run(runProps.CloneNode(true) as RunProperties,
                new FieldChar { FieldCharType = FieldCharValues.End });

            para.AppendChild(beginRun);
            para.AppendChild(instrRun);
            para.AppendChild(separateRun);
            para.AppendChild(resultRun);
            para.AppendChild(endRun);
        }
        private static SectionProperties BuildSectionProperties(string footerRelId, bool restartNumbering)
        {
            var sectPr = new SectionProperties(
                new FooterReference { Type = HeaderFooterValues.Default, Id = footerRelId },
                new PageSize { Width = (UInt32Value)(uint)PageWidth, Height = (UInt32Value)(uint)PageHeight },
                new PageMargin
                {
                    Top = MarginTopBottom,
                    Bottom = MarginTopBottom,
                    Left = (UInt32Value)(uint)MarginLeftRight,
                    Right = (UInt32Value)(uint)MarginLeftRight,
                    Header = 720,
                    Footer = 720
                });

            if (restartNumbering)
            {
                sectPr.AppendChild(new PageNumberType { Start = 1 });
            }

            return sectPr;
        }
        // ─────────────────────────────────────────────────────────────────
        // IMAGE EMBEDDING
        // ─────────────────────────────────────────────────────────────────
        private static string AddImagePart(MainDocumentPart mainPart, byte[] imageBytes, string name)
        {
            var imagePart = mainPart.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Png);
            using var ms = new MemoryStream(imageBytes);
            imagePart.FeedData(ms);
            return mainPart.GetIdOfPart(imagePart);
        }

        private static Drawing BuildImageDrawing(string relId, int sizeEmu, string name)
        {
            var inline = new DW.Inline(
                new DW.Extent { Cx = sizeEmu, Cy = sizeEmu },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = (UInt32Value)1U, Name = name },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new Pic.Picture(
                            new Pic.NonVisualPictureProperties(
                                new Pic.NonVisualDrawingProperties { Id = 0U, Name = name },
                                new Pic.NonVisualPictureDrawingProperties()),
                            new Pic.BlipFill(
                                new A.Blip { Embed = relId },
                                new A.Stretch(new A.FillRectangle())),
                            new Pic.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = sizeEmu, Cy = sizeEmu }),
                                new A.PresetGeometry(new A.AdjustValueList())
                                { Preset = A.ShapeTypeValues.Rectangle })
                        )
                    )
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
            )
            {
                // ✅ Initializer correctly attached to DW.Inline, which is the
                // type that actually declares these four properties.
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            };

            return new Drawing(inline);
        }

        // ─────────────────────────────────────────────────────────────────
        // HEADER BLOCK (logos, title, certification text) — uses a borderless
        // 3-column table for the logo/title/logo row, since plain paragraphs
        // can't place two images flanking centered text on one line reliably.
        // ─────────────────────────────────────────────────────────────────
        private static void AppendHeaderBlock(
            Body body, CoePreviewGroupDto group, CoeSettingsDto settings,
            string ncscRelId, string bagongRelId)
        {
            const int logoSizeEmu = 600000; // ~0.66 inch square

            var table = new Table();
            table.AppendChild(new TableProperties(
                new TableWidth { Width = ContentWidth.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    NoBorder(), NoBorder(), NoBorder(), NoBorder(), NoBorder(), NoBorder())));

            int logoCol = 1600;
            int centerCol = ContentWidth - (2 * logoCol);

            table.AppendChild(new TableGrid(
                new GridColumn { Width = logoCol.ToString() },
                new GridColumn { Width = centerCol.ToString() },
                new GridColumn { Width = logoCol.ToString() }));

            var row = new TableRow();

            // Left logo cell
            row.AppendChild(CellWithContent(logoCol, JustificationValues.Center,
                new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                    new Run(BuildImageDrawing(ncscRelId, logoSizeEmu, "NcscLogo")))));

            // Center text cell — all centered, multiple paragraphs
            var centerCell = new TableCell();
            centerCell.AppendChild(new TableCellProperties(
                new TableCellWidth { Width = centerCol.ToString(), Type = TableWidthUnitValues.Dxa }));

            centerCell.AppendChild(CenteredRun("Republic of the Philippines", bold: false, size: 20));
            centerCell.AppendChild(CenteredRun("NATIONAL COMMISSION OF SENIOR CITIZENS",
                bold: true, size: 22, colorHex: TitleBlueHex));
            centerCell.AppendChild(CenteredRun("CARAGA REGION", bold: true, size: 22, colorHex: TitleBlueHex));
            centerCell.AppendChild(CenteredRun(settings.OfficeAddress, bold: false, size: 16));

            // Email/website line — mixed runs in one paragraph, centered
            var contactPara = new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
            contactPara.AppendChild(PlainRun("Email: ", 16));
            contactPara.AppendChild(LinkRun(settings.OfficeEmail, 16));
            contactPara.AppendChild(PlainRun("; Official Website: ", 16));
            contactPara.AppendChild(LinkRun(settings.OfficeWebsite, 16));
            centerCell.AppendChild(contactPara);

            row.AppendChild(centerCell);

            // Right logo cell
            row.AppendChild(CellWithContent(logoCol, JustificationValues.Center,
                new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                    new Run(BuildImageDrawing(bagongRelId, logoSizeEmu, "BagongLogo")))));

            table.AppendChild(row);
            body.AppendChild(table);

            // Spacer
            body.AppendChild(new Paragraph(new Run(new Text(""))));

            // Title
            body.AppendChild(new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(
                    new RunProperties(new Bold(), new FontSize { Val = "26" }),
                    new Text("CERTIFICATE OF ELIGIBILITY"))));

            body.AppendChild(new Paragraph(new Run(new Text(""))));

            // Certification paragraph — amount NOT bold, LGU name IS bold
            var certPara = new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Both }));

            certPara.AppendChild(PlainRun(
                "This is to certify that the names of the senior citizens appeared below are eligible beneficiaries to receive the cash gift of the Expanded Centenarian Program amounting to ", 20));
            certPara.AppendChild(PlainRun($"Php {settings.CashGiftAmount:N2} each", 20));
            certPara.AppendChild(PlainRun(
                " based on the approved list from the Central Office, subject to the existing rules and regulations and as endorsed by the Local Chief Executive of ", 20));
            certPara.AppendChild(BoldRun($"{group.MunicipalityName}, {group.ProvinceName}", 20));
            certPara.AppendChild(PlainRun(", to wit:", 20));

            body.AppendChild(certPara);
            body.AppendChild(new Paragraph(new Run(new Text(""))));
        }

        // ─────────────────────────────────────────────────────────────────
        // YEAR TABLE — centered navy banner + full-grid bordered table
        // ─────────────────────────────────────────────────────────────────
        private static void AppendYearTable(Body body, CoeYearGroupDto yearGroup)
        {
            // Banner — centered, navy background, white bold text
            var bannerTable = new Table();
            bannerTable.AppendChild(new TableProperties(
                new TableWidth { Width = ContentWidth.ToString(), Type = TableWidthUnitValues.Dxa }));
            bannerTable.AppendChild(new TableGrid(new GridColumn { Width = ContentWidth.ToString() }));

            var bannerRow = new TableRow();
            var bannerCell = new TableCell();
            bannerCell.AppendChild(new TableCellProperties(
                new TableCellWidth { Width = ContentWidth.ToString(), Type = TableWidthUnitValues.Dxa },
                new Shading { Fill = NavyHex }));
            bannerCell.AppendChild(new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(
                    new RunProperties(new Bold(), new Color { Val = "FFFFFF" }, new FontSize { Val = "18" }),
                    new Text($"{yearGroup.MilestoneYear} Eligible Grantees"))));
            bannerRow.AppendChild(bannerCell);
            bannerTable.AppendChild(bannerRow);
            body.AppendChild(bannerTable);

            // Data table
            var table = new Table();
            table.AppendChild(new TableProperties(
                new TableWidth { Width = ContentWidth.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    SolidBorder("top"), SolidBorder("bottom"), SolidBorder("left"),
                    SolidBorder("right"), SolidBorder("insideH"), SolidBorder("insideV"))));

            int[] widths = {
                (int)(ContentWidth * 0.06), (int)(ContentWidth * 0.18), (int)(ContentWidth * 0.18),
                (int)(ContentWidth * 0.18), (int)(ContentWidth * 0.16), (int)(ContentWidth * 0.08),
                (int)(ContentWidth * 0.16)
            };

            table.AppendChild(new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() }).ToArray()));

            string[] headers = { "No.", "Last Name", "First Name", "Middle Name", "Birthdate", "Age", "Barangay" };
            var headerRow = new TableRow();
            // ✅ keepNext-equivalent for the table header — TableHeader
            // property makes Word repeat this row on continuation pages.
            // Per your earlier instruction (no repeated headers on
            // continuation), this is OMITTED — header appears once only.
            for (int i = 0; i < headers.Length; i++)
                headerRow.AppendChild(HeaderCell(widths[i], headers[i]));
            table.AppendChild(headerRow);

            foreach (var rec in yearGroup.Records)
            {
                var dataRow = new TableRow();
                dataRow.AppendChild(DataCell(widths[0], rec.RowNumber.ToString(), JustificationValues.Center));
                dataRow.AppendChild(DataCell(widths[1], rec.LastName, JustificationValues.Start));
                dataRow.AppendChild(DataCell(widths[2], rec.FirstName, JustificationValues.Start));
                dataRow.AppendChild(DataCell(widths[3], rec.MiddleName, JustificationValues.Start));
                dataRow.AppendChild(DataCell(widths[4],
                    rec.BirthDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture), JustificationValues.Center));
                dataRow.AppendChild(DataCell(widths[5], rec.Age.ToString(), JustificationValues.Center));
                dataRow.AppendChild(DataCell(widths[6], rec.BarangayName, JustificationValues.Start));
                table.AppendChild(dataRow);
            }

            body.AppendChild(table);
            body.AppendChild(new Paragraph(new Run(new Text(""))));
        }

        // ✅ Changed return type from void to Paragraph — returns the LAST
        // paragraph appended, so the caller can embed a section break inside
        // it (required OOXML pattern for non-final section boundaries).
        private static Paragraph AppendSignatoryBlock(Body body, CoeSettingsDto settings)
        {
            var lines = new List<(string text, bool bold, int size)>
    {
        ("This certification is being issued for whatever legal purpose it may serve.", false, 18),
        ("", false, 18),
        ("Prepared by:", false, 18),
        ("", false, 18),
        ("", false, 18),
        (settings.PreparedByName, true, 20),
        (settings.PreparedByPosition, false, 18),
        ("", false, 18),
        ("Approved by:", false, 18),
        ("", false, 18),
        ("", false, 18),
        (settings.ApprovedByName, true, 20),
        (settings.ApprovedByPosition, false, 18),
    };

            Paragraph? last = null;

            for (int i = 0; i < lines.Count; i++)
            {
                var (text, bold, size) = lines[i];
                bool isLast = i == lines.Count - 1;

                var pPr = new ParagraphProperties();
                if (!isLast)
                    pPr.AppendChild(new KeepNext());

                var runProps = new RunProperties(new FontSize { Val = size.ToString() });
                if (bold) runProps.AppendChild(new Bold());

                var para = new Paragraph(pPr,
                    new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

                body.AppendChild(para);
                last = para;
            }

            return last!;
        }

        // ─────────────────────────────────────────────────────────────────
        // FOOTER BAR — contact info + page number, black text, red accent,
        // no background fill ("transparent" per your instruction)
        // ─────────────────────────────────────────────────────────────────
        private static void AppendFooterBar(Body body, CoeSettingsDto settings)
        {
            var para = new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { Before = "400" }));

            para.AppendChild(new Run(
                new RunProperties(new Bold(), new Italic(), new Color { Val = FooterAccentHex }, new FontSize { Val = "18" }),
                new Text("Pusò para sa Seniors")));
            para.AppendChild(PlainRun("   |   ☎ 09384143439/09553822435   |   ✉ ro13@ncsc.gov.ph   |   🌐 www.ncsc.gov.ph   |   Page ", 16));

            var fldChar1 = new Run(new FieldChar { FieldCharType = FieldCharValues.Begin });
            var instrText = new Run(new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve });
            var fldChar2 = new Run(new FieldChar { FieldCharType = FieldCharValues.End });

            para.AppendChild(fldChar1);
            para.AppendChild(instrText);
            para.AppendChild(fldChar2);

            para.AppendChild(PlainRun(" of ", 16));

            var fldChar3 = new Run(new FieldChar { FieldCharType = FieldCharValues.Begin });
            var instrText2 = new Run(new FieldCode(" NUMPAGES ") { Space = SpaceProcessingModeValues.Preserve });
            var fldChar4 = new Run(new FieldChar { FieldCharType = FieldCharValues.End });

            para.AppendChild(fldChar3);
            para.AppendChild(instrText2);
            para.AppendChild(fldChar4);

            body.AppendChild(para);
        }

        // ─────────────────────────────────────────────────────────────────
        // SMALL HELPERS
        // ─────────────────────────────────────────────────────────────────
        private static TableCell HeaderCell(int width, string text)
        {
            var cell = new TableCell();
            cell.AppendChild(new TableCellProperties(
                new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
            // ✅ Correct
            new Shading { Fill = NavyHex, Val = ShadingPatternValues.Clear }));
            cell.AppendChild(new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Left }),
                new Run(
                    new RunProperties(new Bold(), new Color { Val = "FFFFFF" }, new FontSize { Val = "18" }),
                    new Text(text))));
            return cell;
        }

        private static TableCell DataCell(int width, string text, JustificationValues align)
        {
            var cell = new TableCell();
            cell.AppendChild(new TableCellProperties(
                new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa }));
            cell.AppendChild(new Paragraph(
                new ParagraphProperties(new Justification { Val = align }),
                new Run(new RunProperties(new FontSize { Val = "18" }), new Text(text ?? ""))));
            return cell;
        }

        private static TableCell CellWithContent(int width, JustificationValues align, Paragraph content)
        {
            var cell = new TableCell();
            cell.AppendChild(new TableCellProperties(
                new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa }));
            cell.AppendChild(content);
            return cell;
        }

        private static Paragraph CenteredRun(string text, bool bold, int size, string? colorHex = null)
        {
            var rPr = new RunProperties(new FontSize { Val = size.ToString() });
            if (bold) rPr.AppendChild(new Bold());
            if (colorHex != null) rPr.AppendChild(new Color { Val = colorHex });

            return new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(rPr, new Text(text)));
        }

        private static Run PlainRun(string text, int size) =>
            new Run(new RunProperties(new FontSize { Val = size.ToString() }), new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        private static Run BoldRun(string text, int size) =>
            new Run(new RunProperties(new Bold(), new FontSize { Val = size.ToString() }), new Text(text));

        private static Run LinkRun(string text, int size) =>
            new Run(
                new RunProperties(new Color { Val = LinkBlueHex }, new Underline { Val = UnderlineValues.Single }, new FontSize { Val = size.ToString() }),
                new Text(text));

        private static BottomBorder NoBorder() => new BottomBorder { Val = BorderValues.None, Size = 0 };

        private static BorderType SolidBorder(string side)
        {
            BorderType border = side switch
            {
                "top" => new TopBorder(),
                "bottom" => new BottomBorder(),
                "left" => new LeftBorder(),
                "right" => new RightBorder(),
                "insideH" => new InsideHorizontalBorder(),
                "insideV" => new InsideVerticalBorder(),
                _ => throw new ArgumentException(side)
            };
            border.Val = BorderValues.Single;
            border.Size = 4;
            border.Color = BlackHex;
            return border;
        }
    }
}