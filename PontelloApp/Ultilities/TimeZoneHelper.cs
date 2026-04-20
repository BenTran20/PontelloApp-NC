namespace PontelloApp.Ultilities
{
    public static class TimeZoneHelper
    {
        private static readonly TimeZoneInfo Eastern =
            TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        public static DateTime ToEastern(this DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc), Eastern);
        }

        public static DateTime ToUtc(this DateTime eastern)
        {
            return TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(eastern, DateTimeKind.Unspecified), Eastern);
        }

        public static TimeSpan TimeOfDayToUtc(this TimeSpan easternTime)
        {
            var offset = Eastern.GetUtcOffset(DateTime.UtcNow);
            var utc = easternTime.Add(-offset);

            var totalMinutes = (long)utc.TotalMinutes % (24 * 60);
            if (totalMinutes < 0) totalMinutes += 24 * 60;
            return TimeSpan.FromMinutes(totalMinutes);
        }

        public static TimeSpan TimeOfDayToEastern(this TimeSpan utcTime)
        {
            var offset = Eastern.GetUtcOffset(DateTime.UtcNow);
            var eastern = utcTime.Add(offset);

            var totalMinutes = (long)eastern.TotalMinutes % (24 * 60);
            if (totalMinutes < 0) totalMinutes += 24 * 60;
            return TimeSpan.FromMinutes(totalMinutes);
        }
    }
}