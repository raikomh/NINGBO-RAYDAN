using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Operations
{
    /// <inheritdoc />
    public partial class AddPurchasingInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "op_inventory_counts",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    adjusted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_inventory_counts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "op_inventory_movements",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    from_warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_inventory_movements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "op_purchase_requests",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_stock = table.Column<int>(type: "integer", nullable: false),
                    min_stock = table.Column<int>(type: "integer", nullable: false),
                    requested_quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_purchase_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "op_purchases",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    associated_expenses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    associated_expenses_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    invoice_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    purchase_request_ids = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_purchases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "op_inventory_count_items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_count_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    system_quantity = table.Column<int>(type: "integer", nullable: false),
                    counted_quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_inventory_count_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_op_inventory_count_items_op_inventory_counts_inventory_coun",
                        column: x => x.inventory_count_id,
                        principalSchema: "public",
                        principalTable: "op_inventory_counts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "op_purchase_items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    cost_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    batch_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_purchase_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_op_purchase_items_op_purchases_purchase_id",
                        column: x => x.purchase_id,
                        principalSchema: "public",
                        principalTable: "op_purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_op_inventory_count_items_inventory_count_id",
                schema: "public",
                table: "op_inventory_count_items",
                column: "inventory_count_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_inventory_count_items_product_id",
                schema: "public",
                table: "op_inventory_count_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_inventory_counts_tenant_id_warehouse_id",
                schema: "public",
                table: "op_inventory_counts",
                columns: new[] { "tenant_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "ix_op_inventory_movements_product_id",
                schema: "public",
                table: "op_inventory_movements",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_inventory_movements_tenant_id_date",
                schema: "public",
                table: "op_inventory_movements",
                columns: new[] { "tenant_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_op_purchase_items_product_id",
                schema: "public",
                table: "op_purchase_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_purchase_items_purchase_id",
                schema: "public",
                table: "op_purchase_items",
                column: "purchase_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_purchase_requests_tenant_id_status",
                schema: "public",
                table: "op_purchase_requests",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_op_purchases_tenant_id",
                schema: "public",
                table: "op_purchases",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_purchases_tenant_id_date",
                schema: "public",
                table: "op_purchases",
                columns: new[] { "tenant_id", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "op_inventory_count_items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_inventory_movements",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_purchase_items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_purchase_requests",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_inventory_counts",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_purchases",
                schema: "public");
        }
    }
}
