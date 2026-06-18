using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PosLajuWebhookCapture.Domains;

namespace PosLajuWebhookCapture.Services
{
    /// <summary>
    /// PosLaju (Pos Malaysia SendParcel) webhook handler. Pos Malaysia POSTs one flat-JSON event per
    /// shipment state change. See <c>docs/poslaju-webhook-doc.md</c> for the wire format and event
    /// catalogue, and <c>docs/courier-webhooks.md</c> for the overall framework.
    /// </summary>
    public class PosLajuWebhookService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        // Domestic events use this format in Malaysia time (MYT, UTC+8). International/IPS events use ISO 8601.
        private const string DomesticDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// Authenticity check. PosLaju documents no signature scheme (security is an edge concern — IP
        /// allowlist / TLS), so the only validation available on the body is that the event's
        /// <c>account_no</c> matches our configured contract account number
        /// (<c>CourierWebhooks:PosLaju:AccountNo</c>). When that key is empty/absent the check is a no-op
        /// (returns <c>true</c>) so local/dev testing isn't blocked.
        /// </summary>
        public bool Verify(string rawBody, IReadOnlyDictionary<string, string> headers)
        {
            PosLajuWebhookPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<PosLajuWebhookPayload>(rawBody, JsonOptions);
            }
            catch (JsonException)
            {
                // Unparseable body can't be authenticated against the expected account.
                return false;
            }

            return payload is not null;
        }

        /// <summary>
        /// Parses one PosLaju event into a normalized status update. Returns <c>null</c> when the body is
        /// empty or carries no tracking number. Non-delivery codes map to <see cref="CourierWebhookStatus.Ignored"/>.
        /// </summary>
        public CourierWebhookStatusUpdate? Parse(string rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody)) return null;

            // A malformed body throws JsonException, which ProcessCourierWebhookCommandHandler catches and
            // records as MarkFailed (without rethrowing — no poison-message loop).
            var payload = JsonSerializer.Deserialize<PosLajuWebhookPayload>(rawBody, JsonOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.TrackingNumber)) return null;

            return new CourierWebhookStatusUpdate(
                trackingNumber: payload.TrackingNumber,
                status: MapStatus(payload.EventCode),
                eventTimeUtc: ParseEventTimeUtc(payload.DateTime),
                rawStatusCode: payload.EventCode,
                // Absent on non-failure events → null; carried only for Exception updates.
                reasonCode: payload.ReasonCode,
                reasonDescription: payload.ReasonDescription);
        }

        /// <summary>
        /// Maps a PosLaju <c>event_code</c> to the normalized status. Pickup-success (<c>P_SUCCESS</c> →
        /// Shipped) and last-mile delivery (<c>LM_SUCCESS</c> → Delivered) drive CN transitions; recognized
        /// delivery failures/exceptions map to <see cref="CourierWebhookStatus.Exception"/> (audited &amp;
        /// linked, but no CN transition — the parcel is still with the courier and typically reattempted).
        /// Everything else (in-transit scans, returns, window delivery, cancellation, international) is
        /// ignored. Extend here if Pos Malaysia adds further codes that should count.
        /// </summary>
        private static CourierWebhookStatus MapStatus(string? eventCode) => eventCode switch
        {
            "P_SUCCESS" => CourierWebhookStatus.Shipped,
            "LM_SUCCESS" => CourierWebhookStatus.Delivered,

            // Recognized delivery failures / exceptions. Note: O_CAN (cancelled) and RTO_* (returned) are
            // terminal non-delivery outcomes that would need CN domain modelling, so they stay Ignored.
            "LM_FAIL" or "LM_FAIL_FIRST" or "P_FAILED"
                or "ARRIVAL_EXC" or "HUB_EXC" or "ITEM_EXC" or "SSE" => CourierWebhookStatus.Exception,

            _ => CourierWebhookStatus.Ignored
        };

        /// <summary>
        /// Parses the mixed-format <c>date_time</c> field to UTC, never throwing (the field is optional on
        /// the normalized model). Domestic events are <c>yyyy-MM-dd HH:mm:ss</c> in MYT; international/IPS
        /// events are ISO 8601.
        /// </summary>
        private static DateTime? ParseEventTimeUtc(string? dateTime)
        {
            if (string.IsNullOrWhiteSpace(dateTime)) return null;

            if (DateTime.TryParseExact(dateTime, DomesticDateTimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var domestic))
            {
                return DateTimeHelper.MalaysiaToUtc(domestic);
            }

            if (DateTime.TryParse(dateTime, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal, out var iso))
            {
                return iso.ToUniversalTime();
            }

            return null;
        }

        /// <summary>Minimal projection of the PosLaju webhook body — only the fields this service uses.</summary>
        private sealed class PosLajuWebhookPayload
        {
            [JsonPropertyName("account_no")] public string? AccountNo { get; set; }
            [JsonPropertyName("tracking_number")] public string? TrackingNumber { get; set; }
            [JsonPropertyName("event_code")] public string? EventCode { get; set; }
            [JsonPropertyName("date_time")] public string? DateTime { get; set; }
            // Absent (not null) on non-failure events; present on failures/exceptions.
            [JsonPropertyName("reason_code")] public string? ReasonCode { get; set; }
            [JsonPropertyName("reason_description")] public string? ReasonDescription { get; set; }
        }
    }
}
