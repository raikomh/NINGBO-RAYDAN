using System.Globalization;
using System.Text;
using BusinessSearcher.Application.Commons.Interfaces;
using ClosedXML.Excel;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>
    /// Lee el Excel de importación de ventas: columnas "Código", "Producto", "Cantidad", "Precio".
    /// Cada fila representa una venta de un solo producto. Valida el archivo completo antes de
    /// reportar éxito (todo-o-nada), igual que el import de catálogo mayorista.
    /// </summary>
    public class ClosedXmlSalesImportParser : IExcelSalesImportParser
    {
        private static readonly string[] RequiredHeaders = { "codigo", "producto", "cantidad", "precio" };

        public ExcelSalesImportParseResult ParseSales(Stream fileStream, CancellationToken cancellationToken = default)
        {
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.First();
            var headerRow = worksheet.Row(1);
            var lastUsedRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            var lastUsedColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 1;

            var columnIndexByHeader = new Dictionary<string, int>();
            for (var col = 1; col <= lastUsedColumn; col++)
            {
                var header = Normalize(headerRow.Cell(col).GetString());
                if (!string.IsNullOrWhiteSpace(header) && !columnIndexByHeader.ContainsKey(header))
                    columnIndexByHeader[header] = col;
            }

            var missingHeaders = RequiredHeaders.Where(h => !columnIndexByHeader.ContainsKey(h)).ToList();
            if (missingHeaders.Count > 0)
            {
                var headerErrors = missingHeaders
                    .Select(h => $"Falta la columna requerida '{ToDisplayName(h)}' en el encabezado.")
                    .ToList();
                return new ExcelSalesImportParseResult(false, Array.Empty<SalesImportRowDto>(), headerErrors);
            }

            var codeCol = columnIndexByHeader["codigo"];
            var productCol = columnIndexByHeader["producto"];
            var quantityCol = columnIndexByHeader["cantidad"];
            var priceCol = columnIndexByHeader["precio"];

            var rows = new List<SalesImportRowDto>();
            var rowErrors = new List<string>();

            for (var rowNum = 2; rowNum <= lastUsedRow; rowNum++)
            {
                var row = worksheet.Row(rowNum);

                var codeRaw = row.Cell(codeCol).GetString().Trim();
                var productRaw = row.Cell(productCol).GetString().Trim();
                var quantityRaw = row.Cell(quantityCol).GetString().Trim();
                var priceRaw = row.Cell(priceCol).GetString().Trim();

                if (string.IsNullOrEmpty(codeRaw) && string.IsNullOrEmpty(productRaw)
                    && string.IsNullOrEmpty(quantityRaw) && string.IsNullOrEmpty(priceRaw))
                    continue; // fila vacía al final del archivo

                if (string.IsNullOrWhiteSpace(codeRaw) && string.IsNullOrWhiteSpace(productRaw))
                    rowErrors.Add($"Fila {rowNum}: debes indicar 'Código' o 'Producto' para identificar el producto.");

                if (!int.TryParse(quantityRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
                    rowErrors.Add($"Fila {rowNum}: 'Cantidad' debe ser un número entero mayor que 0.");

                if (!decimal.TryParse(priceRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price < 0)
                    rowErrors.Add($"Fila {rowNum}: 'Precio' debe ser un número mayor o igual a 0.");

                var hasErrorForThisRow = rowErrors.Any(e => e.StartsWith($"Fila {rowNum}:"));
                if (!hasErrorForThisRow)
                    rows.Add(new SalesImportRowDto(rowNum, string.IsNullOrWhiteSpace(codeRaw) ? null : codeRaw,
                        string.IsNullOrWhiteSpace(productRaw) ? null : productRaw, quantity, price));
            }

            return new ExcelSalesImportParseResult(
                rowErrors.Count == 0, rowErrors.Count == 0 ? rows : Array.Empty<SalesImportRowDto>(), rowErrors);
        }

        private static string Normalize(string value)
        {
            var trimmed = value.Trim().ToLowerInvariant();
            var normalized = trimmed.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string ToDisplayName(string normalizedHeader) => normalizedHeader switch
        {
            "codigo" => "Código",
            "producto" => "Producto",
            "cantidad" => "Cantidad",
            "precio" => "Precio",
            _ => normalizedHeader
        };
    }
}
