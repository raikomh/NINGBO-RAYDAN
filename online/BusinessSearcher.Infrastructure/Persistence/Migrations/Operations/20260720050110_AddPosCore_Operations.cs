using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Operations
{
    /// <inheritdoc />
    public partial class AddPosCore_Operations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "op_cash_movements",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    amount_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_cash_movements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "op_cash_registers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    open_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    close_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    initial_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    expected_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    actual_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    difference = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    initial_amount_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    expected_amount_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    actual_amount_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    difference_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    opened_by = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    sales_count = table.Column<int>(type: "integer", nullable: false),
                    total_sales = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_expenses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_cash_in = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_cash_out = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_cash_registers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "op_categories",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "op_product_price_history",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_cost_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    new_cost_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    old_sell_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    new_sell_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    old_cost_price_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    new_cost_price_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    old_sell_price_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    new_sell_price_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    change_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_product_price_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "op_products",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    barcode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    sell_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    cost_price_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    sell_price_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    min_stock = table.Column<int>(type: "integer", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    batch_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    for_sale = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "op_sales",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    discount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    subtotal_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cashier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    payment_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_sales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "op_terminals",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_terminals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "op_product_stocks",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    average_cost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    average_cost_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_product_stocks", x => x.id);
                    table.ForeignKey(
                        name: "fk_op_product_stocks_op_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "public",
                        principalTable: "op_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "op_sale_items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    discount_type = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    discount_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_sale_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_op_sale_items_op_sales_sale_id",
                        column: x => x.sale_id,
                        principalSchema: "public",
                        principalTable: "op_sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "op_sale_payments",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cash_tendered = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    change = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    change_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_sale_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_op_sale_payments_op_sales_sale_id",
                        column: x => x.sale_id,
                        principalSchema: "public",
                        principalTable: "op_sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_op_cash_movements_tenant_id_register_id",
                schema: "public",
                table: "op_cash_movements",
                columns: new[] { "tenant_id", "register_id" });

            migrationBuilder.CreateIndex(
                name: "ix_op_cash_registers_tenant_id",
                schema: "public",
                table: "op_cash_registers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_cash_registers_tenant_id_status",
                schema: "public",
                table: "op_cash_registers",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_op_categories_tenant_id",
                schema: "public",
                table: "op_categories",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_product_price_history_tenant_id_product_id",
                schema: "public",
                table: "op_product_price_history",
                columns: new[] { "tenant_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "ix_op_product_stocks_product_id_warehouse_id",
                schema: "public",
                table: "op_product_stocks",
                columns: new[] { "product_id", "warehouse_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_op_product_stocks_warehouse_id",
                schema: "public",
                table: "op_product_stocks",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_products_tenant_id",
                schema: "public",
                table: "op_products",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_products_tenant_id_barcode",
                schema: "public",
                table: "op_products",
                columns: new[] { "tenant_id", "barcode" });

            migrationBuilder.CreateIndex(
                name: "ix_op_sale_items_product_id",
                schema: "public",
                table: "op_sale_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_sale_items_sale_id",
                schema: "public",
                table: "op_sale_items",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_sale_payments_sale_id",
                schema: "public",
                table: "op_sale_payments",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_sales_register_id",
                schema: "public",
                table: "op_sales",
                column: "register_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_sales_tenant_id",
                schema: "public",
                table: "op_sales",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_sales_tenant_id_date",
                schema: "public",
                table: "op_sales",
                columns: new[] { "tenant_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_op_terminals_tenant_id",
                schema: "public",
                table: "op_terminals",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "op_cash_movements",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_cash_registers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_categories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_product_price_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_product_stocks",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_sale_items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_sale_payments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_terminals",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_products",
                schema: "public");

            migrationBuilder.DropTable(
                name: "op_sales",
                schema: "public");
        }
    }
}
