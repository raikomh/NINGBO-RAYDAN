using System.Globalization;
using System.Text;
using BusinessSearcher.Application.Commons.Interfaces;
using ClosedXML.Excel;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>
    /// Lee el Excel de catálogo mayorista: columnas "Producto", "Cantidad", "Precio", "Venta Minima".
    /// Valida el archivo completo antes de reportar éxito (todo-o-nada): si hay una sola fila
    /// inválida, IsValid queda en false y Rows viene vacío, para que el handler nunca reemplace
    /// el catálogo existente con datos parciales.
    /// </summary>
    public class ClosedXmlCatalogParser : IExcelCatalogParser
    {
        private static readonly string[] RequiredHeaders = { "producto", "cantidad", "precio", "venta minima" };

        public ExcelCatalogParseResult ParseWholesaleCatalog(Stream fileStream, CancellationToken cancellationToken = default)
        {
            using var workbook  = new XLWorkbook(fileStream);
            var worksheet       = workbook.Worksheets.First();
            var headerRow       = worksheet.Row(1);
            var lastUsedRow     = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            var lastUsedColumn  = worksheet.LastColumnUsed()?.ColumnNumber() ?? 1;

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
                var errors = missingHeaders
                    .Select(h => $"Falta la columna requerida '{ToDisplayName(h)}' en el encabezado.")
                    .ToList();
                return new ExcelCatalogParseResult(false, Array.Empty<CatalogRowDto>(), errors);
            }

            var productCol = columnIndexByHeader["producto"];
            var quantityCol = columnIndexByHeader["cantidad"];
            var priceCol = columnIndexByHeader["precio"];
            var minOrderCol = columnIndexByHeader["venta minima"];

            var rows       = new List<CatalogRowDto>();
            var rowErrors  = new List<string>();

            for (var rowNum = 2; rowNum <= lastUsedRow; rowNum++)
            {
                var row = worksheet.Row(rowNum);

                var productRaw  = row.Cell(productCol).GetString().Trim();
                var quantityRaw = row.Cell(quantityCol).GetString().Trim();
                var priceRaw    = row.Cell(priceCol).GetString().Trim();
                var minOrderRaw = row.Cell(minOrderCol).GetString().Trim();

                if (string.IsNullOrEmpty(productRaw) && string.IsNullOrEmpty(quantityRaw)
                    && string.IsNullOrEmpty(priceRaw) && string.IsNullOrEmpty(minOrderRaw))
                    continue; // fila vacía al final del archivo

                if (string.IsNullOrWhiteSpace(productRaw))
                    rowErrors.Add($"Fila {rowNum}: 'Producto' es requerido.");
                else if (productRaw.Length > 200)
                    rowErrors.Add($"Fila {rowNum}: 'Producto' no puede exceder 200 caracteres.");

                if (!int.TryParse(quantityRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) || quantity < 0)
                    rowErrors.Add($"Fila {rowNum}: 'Cantidad' debe ser un número entero mayor o igual a 0.");

                if (!decimal.TryParse(priceRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price < 0)
                    rowErrors.Add($"Fila {rowNum}: 'Precio' debe ser un número mayor o igual a 0.");

                if (!int.TryParse(minOrderRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minOrder) || minOrder < 1)
                    rowErrors.Add($"Fila {rowNum}: 'Venta Minima' debe ser un número entero mayor o igual a 1.");

                var hasErrorForThisRow = rowErrors.Any(e => e.StartsWith($"Fila {rowNum}:"));
                if (!hasErrorForThisRow)
                    rows.Add(new CatalogRowDto(rowNum, productRaw, quantity, price, minOrder));
            }

            return new ExcelCatalogParseResult(rowErrors.Count == 0, rowErrors.Count == 0 ? rows : Array.Empty<CatalogRowDto>(), rowErrors);
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
            "producto"      => "Producto",
            "cantidad"      => "Cantidad",
            "precio"        => "Precio",
            "venta minima"  => "Venta Minima",
            _ => normalizedHeader
        };
    }
}
