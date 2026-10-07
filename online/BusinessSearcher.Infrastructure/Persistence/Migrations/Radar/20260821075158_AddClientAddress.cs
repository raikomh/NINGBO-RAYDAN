using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Radar
{
    /// <inheritdoc />
    public partial class AddClientAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "country",
                schema: "public",
                table: "clients",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state",
                schema: "public",
                table: "clients",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "street",
                schema: "public",
                table: "clients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "country",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "state",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "street",
                schema: "public",
                table: "clients");
        }
    }
}
