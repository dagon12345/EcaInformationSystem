using System.Globalization;

public static class StringExtensions
{
    public static string ToTitleCase(this string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
    }
}