using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Operations
{
    /// <inheritdoc />
    public partial class AddAuditedToInventoryCountItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "audited",
                schema: "public",
                table: "op_inventory_count_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "op_role_salary_configs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    base_salary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    sales_percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_role_salary_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_op_role_salary_configs_tenant_id_role",
                schema: "public",
                table: "op_role_salary_configs",
                columns: new[] { "tenant_id", "role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "op_role_salary_configs",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "audited",
                schema: "public",
                table: "op_inventory_count_items");
        }
    }
}
