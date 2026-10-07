using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Operations
{
    /// <inheritdoc />
    public partial class AddPublicCatalogToOperationsProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_url",
                schema: "public",
                table: "op_products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_publicly_visible",
                schema: "public",
                table: "op_products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "min_order_quantity",
                schema: "public",
                table: "op_products",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ix_op_products_is_publicly_visible_for_sale_is_active",
                schema: "public",
                table: "op_products",
                columns: new[] { "is_publicly_visible", "for_sale", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_op_products_is_publicly_visible_for_sale_is_active",
                schema: "public",
                table: "op_products");

            migrationBuilder.DropColumn(
                name: "image_url",
                schema: "public",
                table: "op_products");

            migrationBuilder.DropColumn(
                name: "is_publicly_visible",
                schema: "public",
                table: "op_products");

            migrationBuilder.DropColumn(
                name: "min_order_quantity",
                schema: "public",
                table: "op_products");
        }
    }
}
