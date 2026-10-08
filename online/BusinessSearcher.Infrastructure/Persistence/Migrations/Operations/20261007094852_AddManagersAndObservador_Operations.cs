using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Operations
{
    /// <inheritdoc />
    public partial class AddManagersAndObservador_Operations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "manager_code",
                schema: "public",
                table: "op_sales",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "op_managers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    id_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    municipality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_op_managers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_op_managers_tenant_id_code",
                schema: "public",
                table: "op_managers",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "op_managers",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "manager_code",
                schema: "public",
                table: "op_sales");
        }
    }
}
