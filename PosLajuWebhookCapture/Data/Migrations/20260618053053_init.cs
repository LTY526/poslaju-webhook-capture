using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PosLajuWebhookCapture.Data.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "courier_webhook_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    raw_payload = table.Column<string>(type: "text", nullable: false),
                    headers = table.Column<string>(type: "text", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tracking_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    processed = table.Column<bool>(type: "boolean", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courier_webhook_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "courier_webhook_status_updates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    courier_webhook_event_id = table.Column<int>(type: "integer", nullable: false),
                    tracking_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    event_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    raw_status_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    reason_description = table.Column<string>(type: "text", nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courier_webhook_status_updates", x => x.id);
                    table.ForeignKey(
                        name: "fk_courier_webhook_status_updates_courier_webhook_events_couri",
                        column: x => x.courier_webhook_event_id,
                        principalTable: "courier_webhook_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_courier_webhook_events_processed",
                table: "courier_webhook_events",
                column: "processed");

            migrationBuilder.CreateIndex(
                name: "ix_courier_webhook_events_tracking_number",
                table: "courier_webhook_events",
                column: "tracking_number");

            migrationBuilder.CreateIndex(
                name: "ix_courier_webhook_status_updates_courier_webhook_event_id",
                table: "courier_webhook_status_updates",
                column: "courier_webhook_event_id");

            migrationBuilder.CreateIndex(
                name: "ix_courier_webhook_status_updates_tracking_number",
                table: "courier_webhook_status_updates",
                column: "tracking_number");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "courier_webhook_status_updates");

            migrationBuilder.DropTable(
                name: "courier_webhook_events");
        }
    }
}
