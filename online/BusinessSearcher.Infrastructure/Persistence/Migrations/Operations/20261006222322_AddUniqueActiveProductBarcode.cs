using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Operations
{
    /// <inheritdoc />
    public partial class AddUniqueActiveProductBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_op_products_tenant_id_barcode",
                schema: "public",
                table: "op_products");

            migrationBuilder.CreateIndex(
                name: "ix_op_products_tenant_id_barcode",
                schema: "public",
                table: "op_products",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "is_active AND barcode IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_op_products_tenant_id_barcode",
                schema: "public",
                table: "op_products");

            migrationBuilder.CreateIndex(
                name: "ix_op_products_tenant_id_barcode",
                schema: "public",
                table: "op_products",
                columns: new[] { "tenant_id", "barcode" });
        }
    }
}
