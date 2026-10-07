using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.TenantManagement
{
    /// <inheritdoc />
    public partial class FixSyncColumnNamesToSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SyncApiKeyHash",
                schema: "public",
                table: "tenants",
                newName: "sync_api_key_hash");

            migrationBuilder.RenameColumn(
                name: "LastSyncedAt",
                schema: "public",
                table: "tenants",
                newName: "last_synced_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "sync_api_key_hash",
                schema: "public",
                table: "tenants",
                newName: "SyncApiKeyHash");

            migrationBuilder.RenameColumn(
                name: "last_synced_at",
                schema: "public",
                table: "tenants",
                newName: "LastSyncedAt");
        }
    }
}
