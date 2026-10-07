using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.TenantManagement
{
    /// <summary>
    /// Índice único sobre la clave de sincronización. Cada push/pull resuelve el negocio buscando
    /// por este hash (ver IngestTenantSyncPushCommand), así que sin índice cada sincronización
    /// recorría entera la tabla de tenants.
    ///
    /// EF generó además el renombrado de ~30 restricciones e índices (PK_/IX_/FK_ a pk_/ix_/fk_),
    /// consecuencia de aplicar UseSnakeCaseNamingConvention a la fábrica de tiempo de diseño. Esos
    /// nombres no intervienen en ninguna consulta, así que se han quitado a propósito de este
    /// archivo para no ejecutar DDL masivo sobre producción a cambio de nada. El snapshot sí los
    /// recoge, de modo que no volverán a aparecer en futuras migraciones.
    /// </summary>
    public partial class AddSyncApiKeyHashUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El hash es SHA-256 en hexadecimal: 64 caracteres. 128 deja margen de sobra.
            migrationBuilder.AlterColumn<string>(
                name: "sync_api_key_hash",
                schema: "public",
                table: "tenants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // Único: una clave identifica a un solo negocio, y es justo lo que impide que una
            // instalación local acabe sincronizando contra la tienda de otro. En PostgreSQL los
            // NULL no colisionan entre sí, así que los negocios sin clave conviven sin problema.
            migrationBuilder.CreateIndex(
                name: "ix_tenants_sync_api_key_hash",
                schema: "public",
                table: "tenants",
                column: "sync_api_key_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenants_sync_api_key_hash",
                schema: "public",
                table: "tenants");

            migrationBuilder.AlterColumn<string>(
                name: "sync_api_key_hash",
                schema: "public",
                table: "tenants",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);
        }
    }
}
