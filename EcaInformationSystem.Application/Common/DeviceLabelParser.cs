namespace EcaInformationSystem.Application.Common
{
    public static class DeviceLabelParser
    {
        public static string Parse(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown device";

            var ua = userAgent;

            string browser =
                ua.Contains("Edg/") ? "Edge" :
                ua.Contains("OPR/") || ua.Contains("Opera") ? "Opera" :
                ua.Contains("Chrome/") ? "Chrome" :
                ua.Contains("Firefox/") ? "Firefox" :
                ua.Contains("Safari/") && ua.Contains("Version/") ? "Safari" :
                "Unknown browser";

            string os =
                ua.Contains("Windows") ? "Windows" :
                ua.Contains("Mac OS") ? "Mac OS" :
                ua.Contains("Android") ? "Android" :
                ua.Contains("iPhone") || ua.Contains("iPad") ? "iOS" :
                ua.Contains("Linux") ? "Linux" :
                "Unknown OS";

            return $"{browser} on {os}";
        }
    }
}
