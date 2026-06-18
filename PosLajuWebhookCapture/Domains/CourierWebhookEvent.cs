namespace PosLajuWebhookCapture.Domains
{
    public class CourierWebhookEvent
    {
        public int Id { get; set; }
        public string Provider { get; private set; }
        public string RawPayload { get; private set; } = string.Empty;

        /// <summary>Request headers captured at receipt, serialized as JSON (for signature/debugging).</summary>
        public string Headers { get; private set; } = string.Empty;
        public DateTime ReceivedAt { get; private set; }

        /// <summary>The parcel's tracking number, resolved from the payload during processing.</summary>
        public string? TrackingNumber { get; private set; }

        /// <summary>Whether <c>/process</c> has already analyzed this event. Guards against re-parsing.</summary>
        public bool Processed { get; private set; }
        public DateTime? ProcessedAt { get; private set; }

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedDate { get; set; }
        
        private CourierWebhookEvent() { }

        public CourierWebhookEvent(string provider, string rawPayload, string headers)
        {
            Provider = provider;
            RawPayload = rawPayload;
            Headers = headers;
            ReceivedAt = DateTime.UtcNow;
        }

        /// <summary>Records the parcel's tracking number once resolved from the payload during processing.</summary>
        public void SetTrackingNumber(string trackingNumber) => TrackingNumber = trackingNumber;

        /// <summary>Marks this event as analyzed so <c>/process</c> won't pick it up again.</summary>
        public void MarkProcessed()
        {
            Processed = true;
            ProcessedAt = DateTime.UtcNow;
        }
    }
}