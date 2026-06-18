namespace PosLajuWebhookCapture.Services
{
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo MalaysiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");

        public static DateTime MalaysiaNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MalaysiaTimeZone);

        /// <summary>
        /// Converts a wall-clock time expressed in Malaysia time (MYT, UTC+8) to UTC.
        /// The input <see cref="DateTime.Kind"/> is ignored — it is always interpreted as Malaysia local time.
        /// </summary>
        public static DateTime MalaysiaToUtc(DateTime malaysiaLocal) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(malaysiaLocal, DateTimeKind.Unspecified), MalaysiaTimeZone);
    }
}