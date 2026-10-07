using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Operations
{
    /// <inheritdoc />
    public partial class AddCatalogImportsAndWarehousePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "sell_price",
                schema: "public",
                table: "op_product_stocks",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "sell_price_usd",
                schema: "public",
                table: "op_product_stocks",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "op_catalog_imports",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_date = table.Column<DateOnly>(type: "date", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    product_count = table.Column<int>(type: "integer", nullable: false),
                    imported_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_catalog_imports", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_op_catalog_imports_tenant_id_warehouse_id_catalog_date",
                schema: "public",
                table: "op_catalog_imports",
                columns: new[] { "tenant_id", "warehouse_id", "catalog_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "op_catalog_imports",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "sell_price",
                schema: "public",
                table: "op_product_stocks");

            migrationBuilder.DropColumn(
                name: "sell_price_usd",
                schema: "public",
                table: "op_product_stocks");
        }
    }
}
