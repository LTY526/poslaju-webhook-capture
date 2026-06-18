namespace PosLajuWebhookCapture.Domains
{
    public enum CourierWebhookStatus
    {
        Ignored,
        Shipped,
        Delivered,

        /// <summary>
        /// A recognized delivery failure/exception (failed attempt, hub/item exception, …). Audited and
        /// linked to the consignment note, but does NOT change <c>CN.Status</c> — the parcel is still in
        /// the courier's hands and is typically reattempted.
        /// </summary>
        Exception
    }

    /// <summary>
    /// A normalized result of parsing one courier webhook entry, produced by the per-provider
    /// <c>Parse</c> during processing and persisted, linked back to the originating
    /// <see cref="CourierWebhookEvent"/>.
    /// </summary>
    public class CourierWebhookStatusUpdate
    {
        public int Id { get; set; }

        /// <summary>FK to the <see cref="CourierWebhookEvent"/> this update was parsed from.</summary>
        public int CourierWebhookEventId { get; private set; }

        public string TrackingNumber { get; private set; } = string.Empty;
        public CourierWebhookStatus Status { get; private set; }
        public DateTime? EventTimeUtc { get; private set; }

        /// <summary>The provider's raw status code/label, retained for diagnostics.</summary>
        public string? RawStatusCode { get; private set; }

        /// <summary>Provider failure/exception code (e.g. PosLaju <c>reason_code</c> AEX23). Null unless <see cref="Status"/> is <see cref="CourierWebhookStatus.Exception"/>.</summary>
        public string? ReasonCode { get; private set; }

        /// <summary>Human-readable failure/exception text (e.g. PosLaju <c>reason_description</c>).</summary>
        public string? ReasonDescription { get; private set; }

        public DateTime CreatedDate { get; private set; }

        private CourierWebhookStatusUpdate() { }

        public CourierWebhookStatusUpdate(
            string trackingNumber,
            CourierWebhookStatus status,
            DateTime? eventTimeUtc,
            string? rawStatusCode,
            string? reasonCode,
            string? reasonDescription)
        {
            TrackingNumber = trackingNumber;
            Status = status;
            EventTimeUtc = eventTimeUtc;
            RawStatusCode = rawStatusCode;
            ReasonCode = reasonCode;
            ReasonDescription = reasonDescription;
            CreatedDate = DateTime.UtcNow;
        }

        /// <summary>Links this update to the webhook event it was parsed from.</summary>
        public void LinkEvent(int courierWebhookEventId) => CourierWebhookEventId = courierWebhookEventId;
    }
}
