// Application/Services/SystemUpdateSummaryWordDocumentBuilder.cs
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using EcaInformationSystem.Shared.DTOs.SystemUpdate;

namespace EcaInformationSystem.Application.Services
{
    // Turns the published release notes into an editable .docx summary — one
    // block per release with the exact date/time it was published, so an admin
    // can hand the file to Word and amend the wording if a note was written
    // wrong, instead of only reading the notice in the bell/banner.
    // Same OpenXml approach (and same A4 geometry) as CoeWordDocumentBuilder
    // and LivenessPhotoWordDocumentBuilder.
    public static class SystemUpdateSummaryWordDocumentBuilder
    {
        private const string NavyHex = "042C53";
        private const string GreyHex = "595959";
        private const string RuleHex = "D0D7E5";

        private const int PageWidth = 11906;   // A4, DXA
        private const int PageHeight = 16838;
        private const int MarginTopBottom = 1000;
        private const int MarginLeftRight = 1000;

        // Word needs a numbering definition to render real bullet glyphs;
        // only the single bullet level is defined since the summary is a
        // flat list of changes.
        private const int BulletNumberingId = 1;
        private const int BulletAbstractNumberingId = 1;

        // PublishedAt is written as DateTime.UtcNow and EF hands it back with
        // Kind == Unspecified, so a .ToLocalTime() here would print the
        // hosting server's zone (UTC on a container) rather than the
        // Philippine time the admin actually published at. Convert explicitly,
        // same reasoning as LivenessPhotoWordDocumentBuilder.
        private static readonly TimeZoneInfo PhilippineTimeZone = ResolvePhilippineTimeZone();

        private static TimeZoneInfo ResolvePhilippineTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); // Linux/macOS IANA id
            }
            catch (TimeZoneNotFoundException)
            {
                // Windows id set — the Philippines has no DST, so Singapore
                // Standard Time (fixed UTC+8) is an exact equivalent.
                return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            }
        }

        private static DateTime ToPhilippineTime(DateTime stored)
        {
            var utc = stored.Kind switch
            {
                DateTimeKind.Utc => stored,
                DateTimeKind.Local => stored.ToUniversalTime(),
                // The DB round-trip drops Kind — assume UTC, as the rest of the
                // system update code does, then convert to PHT.
                _ => DateTime.SpecifyKind(stored, DateTimeKind.Utc)
            };

            return TimeZoneInfo.ConvertTimeFromUtc(utc, PhilippineTimeZone);
        }

        public static string SuggestedFileName(DateTime generatedAtUtc) =>
            $"System Updates Summary - {ToPhilippineTime(generatedAtUtc):yyyy-MM-dd}.docx";

        public static byte[] Build(IReadOnlyList<SystemUpdateNoticeDto> notices, DateTime generatedAtUtc, string generatedBy)
        {
            using var stream = new MemoryStream();

            using (var wordDoc = WordprocessingDocument.Create(
                stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = new Body();

                AddBulletNumbering(mainPart);

                body.AppendChild(CenteredParagraph(
                    "System Updates Summary", bold: true, size: 30, colorHex: NavyHex, after: 60));
                body.AppendChild(CenteredParagraph(
                    $"Generated {ToPhilippineTime(generatedAtUtc):MMMM d, yyyy} at {ToPhilippineTime(generatedAtUtc):h:mm tt} (PHT)",
                    bold: false, size: 18, colorHex: GreyHex, after: 0));
                body.AppendChild(CenteredParagraph(
                    $"Prepared by {Safe(generatedBy)} · {CountLabel(notices.Count)}",
                    bold: false, size: 18, colorHex: GreyHex, after: 0));

                if (notices.Count == 0)
                {
                    AppendSpacer(body);
                    body.AppendChild(PlainParagraph("No updates have been published yet.", size: 22));
                }
                else
                {
                    // Newest first, matching the on-screen Update History list —
                    // the date/time of each release is what the reader is here for.
                    foreach (var notice in notices.OrderByDescending(n => n.PublishedAt))
                        AppendNotice(body, notice);

                    AppendSpacer(body);
                    body.AppendChild(ItalicParagraph(
                        "Generated from the published release notes. This document is fully editable in Word — "
                        + "amend any wording that needs correcting before circulating it.",
                        size: 18, colorHex: GreyHex));
                }

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

        private static void AppendNotice(Body body, SystemUpdateNoticeDto notice)
        {
            // "v1.4.0 — Title", underlined by a hairline rule so each release
            // reads as its own block.
            body.AppendChild(new Paragraph(
                new ParagraphProperties(
                    new ParagraphBorders(new BottomBorder
                    {
                        Val = BorderValues.Single,
                        Size = 6,
                        Space = 2,
                        Color = RuleHex
                    }),
                    new SpacingBetweenLines { Before = "360", After = "60" }),
                new Run(
                    new RunProperties(new Bold(), new Color { Val = NavyHex }, new FontSize { Val = "26" }),
                    new Text($"v{Safe(notice.Version)} — {Safe(notice.Title)}")
                    { Space = SpaceProcessingModeValues.Preserve })));

            var publishedAt = ToPhilippineTime(notice.PublishedAt);
            body.AppendChild(ItalicParagraph(
                $"Published {publishedAt:MMMM d, yyyy} at {publishedAt:h:mm tt} (PHT) by {Safe(notice.PublishedByName)}",
                size: 18, colorHex: GreyHex));

            var changes = SplitChanges(notice.Changes);
            if (changes.Count == 0)
            {
                body.AppendChild(BulletParagraph("(No change details were provided for this release.)"));
                return;
            }

            foreach (var line in changes)
                body.AppendChild(BulletParagraph(line));
        }

        private static List<string> SplitChanges(string? changes) =>
            string.IsNullOrWhiteSpace(changes)
                ? new List<string>()
                : changes
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(line => line.TrimStart('-', '*', '•', ' ').Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();

        private static string CountLabel(int count) =>
            count == 1 ? "1 release" : $"{count} releases";

        private static string Safe(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();

        private static void AppendSpacer(Body body) =>
            body.AppendChild(new Paragraph(new Run(new Text(""))));

        private static Paragraph CenteredParagraph(string text, bool bold, int size, string colorHex, int after)
        {
            var props = new ParagraphProperties(
                new SpacingBetweenLines { After = after.ToString() },
                new Justification { Val = JustificationValues.Center });

            var runProps = new RunProperties(new Color { Val = colorHex }, new FontSize { Val = size.ToString() });
            if (bold) runProps.PrependChild(new Bold());

            return new Paragraph(props, new Run(runProps, new Text(text)));
        }

        private static Paragraph PlainParagraph(string text, int size) =>
            new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { After = "0" }),
                new Run(new RunProperties(new FontSize { Val = size.ToString() }), new Text(text)));

        private static Paragraph ItalicParagraph(string text, int size, string colorHex) =>
            new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }),
                new Run(
                    new RunProperties(new Italic(), new Color { Val = colorHex }, new FontSize { Val = size.ToString() }),
                    new Text(text)));

        private static Paragraph BulletParagraph(string text) =>
            new Paragraph(
                new ParagraphProperties(
                    new NumberingProperties(
                        new NumberingLevelReference { Val = 0 },
                        new NumberingId { Val = BulletNumberingId }),
                    new SpacingBetweenLines { After = "0" }),
                new Run(new RunProperties(new FontSize { Val = "22" }), new Text(text)));

        // Minimal numbering definition: one abstract level formatted as a
        // bullet. Without this, numPr in the paragraphs above would have
        // nothing to point at and Word would render no bullet at all.
        private static void AddBulletNumbering(MainDocumentPart mainPart)
        {
            var numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
            numberingPart.Numbering = new Numbering(
                new AbstractNum(
                    new Level(
                        new NumberingFormat { Val = NumberFormatValues.Bullet },
                        new LevelText { Val = "•" },
                        new LevelJustification { Val = LevelJustificationValues.Left },
                        new PreviousParagraphProperties(
                            new Indentation { Left = "720", Hanging = "360" }))
                    { LevelIndex = 0 })
                { AbstractNumberId = BulletAbstractNumberingId },
                new NumberingInstance(new AbstractNumId { Val = BulletAbstractNumberingId })
                { NumberID = BulletNumberingId });

            numberingPart.Numbering.Save();
        }
    }
}
