using Microsoft.EntityFrameworkCore;
using PosLajuWebhookCapture.Domains;

namespace PosLajuWebhookCapture.Data
{
    /// <summary>
    /// Minimal persistence context for captured courier webhooks. Snake-case column/table naming is applied
    /// globally via <c>UseSnakeCaseNamingConvention()</c> at registration time, so no explicit column mapping
    /// is needed here.
    /// </summary>
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<CourierWebhookEvent> CourierWebhookEvents => Set<CourierWebhookEvent>();
        public DbSet<CourierWebhookStatusUpdate> CourierWebhookStatusUpdates => Set<CourierWebhookStatusUpdate>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CourierWebhookEvent>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Captured verbatim from the request; payloads can be large, so leave them unbounded (text).
                entity.Property(e => e.RawPayload).HasColumnType("text");
                entity.Property(e => e.Headers).HasColumnType("text");

                entity.Property(e => e.Provider).HasMaxLength(64);
                entity.Property(e => e.TrackingNumber).HasMaxLength(128);

                // Speeds up lookups when correlating a webhook back to a parcel.
                entity.HasIndex(e => e.TrackingNumber);

                // /process scans by this flag, so index it.
                entity.HasIndex(e => e.Processed);
            });

            modelBuilder.Entity<CourierWebhookStatusUpdate>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.TrackingNumber).HasMaxLength(128);
                entity.Property(e => e.RawStatusCode).HasMaxLength(64);
                entity.Property(e => e.ReasonCode).HasMaxLength(64);

                entity.HasIndex(e => e.TrackingNumber);

                // One update is parsed from exactly one event; many updates may reference an event over time.
                entity.HasOne<CourierWebhookEvent>()
                    .WithMany()
                    .HasForeignKey(e => e.CourierWebhookEventId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
