using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Radar
{
    /// <inheritdoc />
    public partial class InitialCreate_Radar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "availability_alerts",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name_filter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    center_latitude = table.Column<double>(type: "double precision", nullable: true),
                    center_longitude = table.Column<double>(type: "double precision", nullable: true),
                    radius_km = table.Column<double>(type: "double precision", nullable: true),
                    max_price = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_triggered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_availability_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "availability_reports",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: true),
                    place_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    municipality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    photo_url = table.Column<string>(type: "text", nullable: true),
                    reporter_client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_reputation_snapshot = table.Column<int>(type: "integer", nullable: false),
                    reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_availability_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    fcm_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reports_submitted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    confirmations_received = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    reputation_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_confirmations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agrees = table.Column<bool>(type: "boolean", nullable: false),
                    reported_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_confirmations", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_confirmations_availability_reports_report_id",
                        column: x => x.report_id,
                        principalSchema: "public",
                        principalTable: "availability_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_email_verification_tokens",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_email_verification_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_client_email_verification_tokens_clients_client_id",
                        column: x => x.client_id,
                        principalSchema: "public",
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_password_reset_tokens",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_client_password_reset_tokens_clients_client_id",
                        column: x => x.client_id,
                        principalSchema: "public",
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_refresh_tokens",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_by_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_client_refresh_tokens_clients_client_id",
                        column: x => x.client_id,
                        principalSchema: "public",
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_availability_alerts_client_id",
                schema: "public",
                table: "availability_alerts",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_availability_alerts_is_active",
                schema: "public",
                table: "availability_alerts",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_availability_reports_city",
                schema: "public",
                table: "availability_reports",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "ix_availability_reports_normalized_product_name",
                schema: "public",
                table: "availability_reports",
                column: "normalized_product_name");

            migrationBuilder.CreateIndex(
                name: "ix_availability_reports_reported_at",
                schema: "public",
                table: "availability_reports",
                column: "reported_at");

            migrationBuilder.CreateIndex(
                name: "ix_client_email_verification_tokens_client_id",
                schema: "public",
                table: "client_email_verification_tokens",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_email_verification_tokens_token",
                schema: "public",
                table: "client_email_verification_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_client_password_reset_tokens_client_id",
                schema: "public",
                table: "client_password_reset_tokens",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_password_reset_tokens_token",
                schema: "public",
                table: "client_password_reset_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_client_refresh_tokens_client_id",
                schema: "public",
                table: "client_refresh_tokens",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_refresh_tokens_token",
                schema: "public",
                table: "client_refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_email",
                schema: "public",
                table: "clients",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_confirmations_report_id",
                schema: "public",
                table: "report_confirmations",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_confirmations_report_id_client_id",
                schema: "public",
                table: "report_confirmations",
                columns: new[] { "report_id", "client_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "availability_alerts",
                schema: "public");

            migrationBuilder.DropTable(
                name: "client_email_verification_tokens",
                schema: "public");

            migrationBuilder.DropTable(
                name: "client_password_reset_tokens",
                schema: "public");

            migrationBuilder.DropTable(
                name: "client_refresh_tokens",
                schema: "public");

            migrationBuilder.DropTable(
                name: "report_confirmations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "clients",
                schema: "public");

            migrationBuilder.DropTable(
                name: "availability_reports",
                schema: "public");
        }
    }
}
