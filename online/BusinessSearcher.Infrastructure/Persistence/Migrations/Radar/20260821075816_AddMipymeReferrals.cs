using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Radar
{
    /// <inheritdoc />
    public partial class AddMipymeReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mipyme_referral_monthly_settlements",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    winner_client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prize_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_valid_mipyme_referrals = table.Column<int>(type: "integer", nullable: false),
                    settled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mipyme_referral_monthly_settlements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mipyme_referrals",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referrer_client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referred_tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mipyme_referrals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mipyme_referral_monthly_settlements_year_month",
                schema: "public",
                table: "mipyme_referral_monthly_settlements",
                columns: new[] { "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mipyme_referrals_referred_tenant_id",
                schema: "public",
                table: "mipyme_referrals",
                column: "referred_tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mipyme_referrals_referrer_client_id",
                schema: "public",
                table: "mipyme_referrals",
                column: "referrer_client_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mipyme_referral_monthly_settlements",
                schema: "public");

            migrationBuilder.DropTable(
                name: "mipyme_referrals",
                schema: "public");
        }
    }
}
