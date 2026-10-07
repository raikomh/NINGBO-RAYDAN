using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Radar
{
    /// <inheritdoc />
    public partial class AddClientMipymeReferralCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "mipyme_referral_month_anchor",
                schema: "public",
                table: "clients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "mipyme_referrals_this_month",
                schema: "public",
                table: "clients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "mipyme_referrals_total",
                schema: "public",
                table: "clients",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mipyme_referral_month_anchor",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "mipyme_referrals_this_month",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "mipyme_referrals_total",
                schema: "public",
                table: "clients");
        }
    }
}
