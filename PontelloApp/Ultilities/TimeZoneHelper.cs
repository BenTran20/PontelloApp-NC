namespace PontelloApp.Ultilities
{
    public static class TimeZoneHelper
    {
        private static readonly TimeZoneInfo Eastern =
            TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        // UTC → Eastern (để hiển thị cho khách)
        public static DateTime ToEastern(this DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc), Eastern);
        }

        // Eastern → UTC (để lưu vào DB)
        public static DateTime ToUtc(this DateTime eastern)
        {
            return TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(eastern, DateTimeKind.Unspecified), Eastern);
        }

        // TimeSpan Eastern → UTC (cho TimeOfDay)
        public static TimeSpan TimeOfDayToUtc(this TimeSpan easternTime)
        {
            var offset = Eastern.GetUtcOffset(DateTime.Now);
            return easternTime.Add(-offset); // trừ offset để ra UTC
        }

        // TimeSpan UTC → Eastern (để hiển thị TimeOfDay)
        public static TimeSpan TimeOfDayToEastern(this TimeSpan utcTime)
        {
            var offset = Eastern.GetUtcOffset(DateTime.Now);
            return utcTime.Add(offset);
        }
    }
}