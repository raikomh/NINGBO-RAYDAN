using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Radar
{
    /// <inheritdoc />
    public partial class AddReferralCupRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "referral_cup_balance",
                schema: "public",
                table: "clients",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "referral_cup_paid_total",
                schema: "public",
                table: "clients",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "referral_cup_transactions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referral_cup_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_referral_cup_transactions_clients_client_id",
                        column: x => x.client_id,
                        principalSchema: "public",
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "referral_monthly_settlements",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    winner_client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prize_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_valid_referrals = table.Column<int>(type: "integer", nullable: false),
                    settled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referral_monthly_settlements", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_referral_cup_transactions_client_id",
                schema: "public",
                table: "referral_cup_transactions",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_referral_monthly_settlements_year_month",
                schema: "public",
                table: "referral_monthly_settlements",
                columns: new[] { "year", "month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referral_cup_transactions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "referral_monthly_settlements",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "referral_cup_balance",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "referral_cup_paid_total",
                schema: "public",
                table: "clients");
        }
    }
}
