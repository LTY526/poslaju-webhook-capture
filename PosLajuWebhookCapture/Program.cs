using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PosLajuWebhookCapture.Data;
using PosLajuWebhookCapture.Domains;
using PosLajuWebhookCapture.Services;

namespace PosLajuWebhookCapture
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddScoped<PosLajuWebhookService>();

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                options
                    .UseSnakeCaseNamingConvention()
                    .UseNpgsql(builder.Configuration["ConnectionString"]);
            });

            // Fail fast: /process is gated behind this shared secret, so refuse to start without it
            // (a missing/blank password would otherwise leave the endpoint unusable or, worse, open).
            var processPassword = builder.Configuration["Password"];
            if (string.IsNullOrWhiteSpace(processPassword))
            {
                throw new InvalidOperationException(
                    "Configuration value 'Password' must be set and non-empty before the service can start.");
            }

            var app = builder.Build();

            // Apply any pending EF Core migrations on startup so the schema is always current.
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.Migrate();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.MapGet("/health", (HttpContext httpContext) =>
                {
                    // Authenticate the caller against the configured shared secret (validated non-empty at
                    // startup). Constant-time compare to avoid leaking the password via response timing.
                    var provided = httpContext.Request.Headers["X-Password"].ToString();
                    if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(processPassword)))
                    {
                        return Results.Unauthorized();
                    }

                    return Results.Ok(DateTime.Now);
                })
                .AllowAnonymous()
                .WithName("HealthCheck");
            
            app.MapPost("/poslaju", async (HttpContext httpContext, ApplicationDbContext db, CancellationToken ct) =>
                {
                    using var reader = new StreamReader(httpContext.Request.Body);
                    var rawBody = await reader.ReadToEndAsync(ct);

                    var headers = httpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
                    var headersJson = JsonSerializer.Serialize(headers);

                    // Capture the webhook verbatim. We always persist and always return 200 — never leak whether
                    // a body was recognized (no-info-leak / always-2xx contract); downstream processing is offline.
                    var record = new CourierWebhookEvent("PosLaju", rawBody, headersJson)
                    {
                        CreatedBy = "poslaju-webhook",
                        CreatedDate = DateTime.UtcNow
                    };

                    db.CourierWebhookEvents.Add(record);
                    await db.SaveChangesAsync(ct);

                    return Results.Ok();
                })
                .AllowAnonymous()
                .WithName("PoslajuWebhook");
            
            app.MapPost("/{bankType}", async (string bankType, HttpContext httpContext, ApplicationDbContext db, CancellationToken ct) =>
                {
                    using var reader = new StreamReader(httpContext.Request.Body);
                    var rawBody = await reader.ReadToEndAsync(ct);

                    var headers = httpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
                    var headersJson = JsonSerializer.Serialize(headers);

                    // Capture the webhook verbatim. We always persist and always return 200 — never leak whether
                    // a body was recognized (no-info-leak / always-2xx contract); downstream processing is offline.
                    var record = new CourierWebhookEvent(bankType, rawBody, headersJson)
                    {
                        CreatedBy = "system",
                        CreatedDate = DateTime.UtcNow
                    };

                    db.CourierWebhookEvents.Add(record);
                    await db.SaveChangesAsync(ct);

                    return Results.Ok();
                })
                .AllowAnonymous()
                .WithName("bankWebhook");

            app.MapPost("/process", async (HttpContext httpContext, ApplicationDbContext db, PosLajuWebhookService service, CancellationToken ct) =>
                {
                    // Authenticate the caller against the configured shared secret (validated non-empty at
                    // startup). Constant-time compare to avoid leaking the password via response timing.
                    var provided = httpContext.Request.Headers["X-Password"].ToString();
                    if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(processPassword)))
                    {
                        return Results.Unauthorized();
                    }

                    var pending = await db.CourierWebhookEvents
                        .Where(e => !e.Processed)
                        .OrderBy(e => e.Id)
                        .ToListAsync(ct);

                    var parsed = 0;
                    foreach (var ev in pending)
                    {
                        // A malformed body throws inside Parse; isolate it so one bad payload doesn't fail the
                        // whole batch. The event is still marked processed so it isn't retried forever.
                        try
                        {
                            var update = service.Parse(ev.RawPayload);
                            if (update is not null)
                            {
                                update.LinkEvent(ev.Id);
                                ev.SetTrackingNumber(update.TrackingNumber);
                                db.CourierWebhookStatusUpdates.Add(update);
                                parsed++;
                            }
                        }
                        catch (JsonException)
                        {
                            // Unparseable payload — capture stays, no status update produced.
                        }

                        ev.MarkProcessed();
                    }

                    await db.SaveChangesAsync(ct);
                    return Results.Ok(new { processed = pending.Count, parsed });
                })
                .AllowAnonymous()
                .WithName("ProcessPoslajuWebhooks");

            app.Run();
        }
    }
}