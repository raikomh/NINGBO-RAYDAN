using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Radar
{
    /// <inheritdoc />
    public partial class AddReferrals_Radar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "premium_until",
                schema: "public",
                table: "clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "referral_code",
                schema: "public",
                table: "clients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "referral_month_anchor",
                schema: "public",
                table: "clients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "referred_by_client_id",
                schema: "public",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "valid_referrals_this_month",
                schema: "public",
                table: "clients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "valid_referrals_total",
                schema: "public",
                table: "clients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "referrals",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inviter_client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referrals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clients_referral_code",
                schema: "public",
                table: "clients",
                column: "referral_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_referrals_invited_client_id",
                schema: "public",
                table: "referrals",
                column: "invited_client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_referrals_inviter_client_id",
                schema: "public",
                table: "referrals",
                column: "inviter_client_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referrals",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_clients_referral_code",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "premium_until",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "referral_code",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "referral_month_anchor",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "referred_by_client_id",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "valid_referrals_this_month",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "valid_referrals_total",
                schema: "public",
                table: "clients");
        }
    }
}
