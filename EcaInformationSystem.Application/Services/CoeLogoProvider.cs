// Application/Services/CoeLogoProvider.cs
//
// Decodes the SAME Base64 PNGs that PayrollLogos.cs uses for the Excel
// payroll sheets. No duplication, no file paths — just a different
// consumption format (raw bytes for QuestPDF vs. a Stream for ClosedXML).
namespace EcaInformationSystem.Application.Services
{
    public static class CoeLogoProvider
    {
        private static readonly Lazy<byte[]> _ncscLogo = new(() =>
            Convert.FromBase64String(PayrollLogos.NcscBase64));

        private static readonly Lazy<byte[]> _bagongPilipinasLogo = new(() =>
            Convert.FromBase64String(PayrollLogos.BagongBase64));

        public static byte[] NcscLogo => _ncscLogo.Value;
        public static byte[] BagongPilipinasLogo => _bagongPilipinasLogo.Value;
    }
}