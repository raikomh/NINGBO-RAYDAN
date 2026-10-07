using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Radar
{
    /// <inheritdoc />
    public partial class AddClientLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "city",
                schema: "public",
                table: "clients",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "public",
                table: "clients",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "public",
                table: "clients",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "client_products",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_products", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_client_products_client_id",
                schema: "public",
                table: "client_products",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_products_is_available",
                schema: "public",
                table: "client_products",
                column: "is_available");

            migrationBuilder.CreateIndex(
                name: "ix_client_products_normalized_name",
                schema: "public",
                table: "client_products",
                column: "normalized_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_products",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "city",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "public",
                table: "clients");
        }
    }
}
