using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Operations
{
    /// <inheritdoc />
    public partial class AddXminConcurrencyToProductStock : Migration
    {
        // "xmin" ya existe en toda tabla de Postgres como columna de sistema (no se crea/borra:
        // el generador de EF Core no distingue eso de una columna real). Up/Down quedan vacíos
        // a propósito: esta migración solo sincroniza el snapshot del modelo de EF con la nueva
        // anotación de token de concurrencia (ProductStockConfiguration.UseXminAsConcurrencyToken),
        // sin ningún cambio real de esquema en la base de datos.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
