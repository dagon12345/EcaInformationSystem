namespace EcaInformationSystem.Domain.Common.Extensions
{
    public static class DateTimeExtensions
    {
        public static string ToYear(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy");
        }
        public static string ToMonth(this DateTime dateTime)
        {
            return dateTime.ToString("MMMM");
        }
        public static string ToDefaultFormat(this DateTime dateTime)
        {
            return dateTime.ToString("dd/MM/yyyy");
        }
        public static string ToStandardDate(this DateTime dateTime)
        {
            return dateTime.ToString("MM/dd/yyyy");
        }
        public static string ToCompeleteDate(this DateTime dateTime)
        {
            return dateTime.ToString("MMMM dd, yyyy");
        }
        // ✅ Plain DateTime overload — this must exist!
        public static string ToFullDate(this DateTime date)
        {
            return date.ToString("yyyy-MM-dd");
        }

        // Nullable overload — calls the one above via date.Value
        public static string ToFullDate(this DateTime? date, string fallback = "-")
        {
            return date.HasValue ? date.Value.ToFullDate() : fallback;
        }
        public static string ToPaddedDay(this int value)
        {
            return value.ToString("D2");
        }
        public static string ToPaddedPage(this int value)
        {
            return value.ToString("D4");
        }
    }
}
