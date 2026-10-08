using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Operations
{
    /// <inheritdoc />
    public partial class RemoveTerminals_RenameSaleWarehouseName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "op_terminals",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "terminal_id",
                schema: "public",
                table: "op_cash_registers");

            migrationBuilder.RenameColumn(
                name: "terminal_name",
                schema: "public",
                table: "op_sales",
                newName: "warehouse_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "warehouse_name",
                schema: "public",
                table: "op_sales",
                newName: "terminal_name");

            migrationBuilder.AddColumn<Guid>(
                name: "terminal_id",
                schema: "public",
                table: "op_cash_registers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "op_terminals",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_terminals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_op_terminals_tenant_id",
                schema: "public",
                table: "op_terminals",
                column: "tenant_id");
        }
    }
}
