using System.Globalization;
using System.Text;
using BusinessSearcher.Application.Commons.Interfaces;
using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>
    /// Lee el catálogo de productos (Excel). Busca la fila de encabezados (puede haber un título encima)
    /// y reconoce las columnas sin importar tildes ni mayúsculas: Código, Producto, Categoría,
    /// Cant. disponible, Precio x unidad (USD). Descripción es opcional. Si el archivo trae fotos
    /// incrustadas (pegadas sobre la fila de cada producto, no en una columna de texto), se extraen y se
    /// asocian por la fila donde está anclada cada imagen.
    /// Valida el archivo completo antes de devolver filas (todo-o-nada).
    /// </summary>
    public class ClosedXmlProductCatalogParser : IExcelProductCatalogParser
    {
        private const int MaxHeaderSearchRows = 20;

        private static readonly Dictionary<XLPictureFormat, string> ImageContentTypes = new()
        {
            [XLPictureFormat.Jpeg] = "image/jpeg",
            [XLPictureFormat.Png]  = "image/png",
            [XLPictureFormat.Webp] = "image/webp",
        };

        public ExcelProductCatalogParseResult Parse(Stream fileStream)
        {
            using var workbook = new XLWorkbook(fileStream);
            var ws = workbook.Worksheets.First();
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

            var imagesByRow = ReadImagesByRow(ws);

            var headerRow = FindHeaderRow(ws, lastRow, lastCol);
            if (headerRow == 0)
                return Fail("No se encontró la columna 'Código' en las primeras filas del archivo.");

            var headers = new Dictionary<string, int>();
            for (var c = 1; c <= lastCol; c++)
            {
                var h = Normalize(ws.Cell(headerRow, c).GetString());
                if (h.Length > 0 && !headers.ContainsKey(h)) headers[h] = c;
            }

            var codeCol     = ColumnOf(headers, h => h == "codigo");
            var productCol  = ColumnOf(headers, h => h.Contains("producto"));
            var categoryCol = ColumnOf(headers, h => h.Contains("categoria"));
            var stockCol    = ColumnOf(headers, h => h.Contains("disponible"));
            var priceCol    = ColumnOf(headers, h => h.Contains("precio") && h.Contains("usd"));
            var descCol     = ColumnOf(headers, h => h.Contains("descripcion"));

            var missing = new List<string>();
            if (productCol == 0)  missing.Add("Producto");
            if (categoryCol == 0) missing.Add("Categoría");
            if (stockCol == 0)    missing.Add("Cant. disponible");
            if (priceCol == 0)    missing.Add("Precio x unidad (USD)");
            if (missing.Count > 0)
                return Fail("Faltan columnas requeridas: " + string.Join(", ", missing) + ".");

            var rows = new List<ProductCatalogRowDto>();
            var errors = new List<string>();
            var seenCodes = new HashSet<string>(StringComparer.Ordinal);

            for (var r = headerRow + 1; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                var code = Text(row.Cell(codeCol));
                var name = Text(row.Cell(productCol));
                var category = Text(row.Cell(categoryCol));
                var stockCell = row.Cell(stockCol);
                var priceCell = row.Cell(priceCol);

                if (code.Length == 0 && name.Length == 0 && category.Length == 0
                    && stockCell.IsEmpty() && priceCell.IsEmpty())
                    continue; // fila vacía

                var rowHasError = false;
                if (code.Length == 0)     { errors.Add($"Fila {r}: falta el Código."); rowHasError = true; }
                if (name.Length == 0)     { errors.Add($"Fila {r}: falta el Producto."); rowHasError = true; }
                if (category.Length == 0) { errors.Add($"Fila {r}: falta la Categoría."); rowHasError = true; }

                if (!TryNumber(stockCell, out var stock) || stock < 0 || stock != Math.Floor(stock))
                {
                    errors.Add($"Fila {r}: 'Cant. disponible' debe ser un entero mayor o igual a 0.");
                    rowHasError = true;
                }

                if (!TryNumber(priceCell, out var price) || price < 0)
                {
                    errors.Add($"Fila {r}: 'Precio x unidad (USD)' debe ser un número mayor o igual a 0.");
                    rowHasError = true;
                }

                if (rowHasError) continue;

                if (!seenCodes.Add(code))
                {
                    errors.Add($"Fila {r}: el código '{code}' está repetido en el archivo.");
                    continue;
                }

                var description = descCol > 0 ? Text(row.Cell(descCol)) : string.Empty;
                imagesByRow.TryGetValue(r, out var image);
                rows.Add(new ProductCatalogRowDto(
                    r, code, name, category, (int)stock, price, description.Length == 0 ? null : description,
                    image.Bytes, image.ContentType));
            }

            if (rows.Count == 0 && errors.Count == 0)
                return Fail("El archivo no contiene productos.");

            return new ExcelProductCatalogParseResult(
                errors.Count == 0, errors.Count == 0 ? rows : Array.Empty<ProductCatalogRowDto>(), errors);
        }

        /// <summary>Lee las fotos pegadas en la hoja y las asocia a la fila donde está anclada cada una
        /// (su esquina superior izquierda). Si dos imágenes caen en la misma fila, se queda con la primera.
        /// Formatos no soportados por el almacenamiento (gif, bmp, tiff, etc.) se ignoran.</summary>
        private static Dictionary<int, (byte[]? Bytes, string? ContentType)> ReadImagesByRow(IXLWorksheet ws)
        {
            var result = new Dictionary<int, (byte[]?, string?)>();
            foreach (var picture in ws.Pictures)
            {
                if (!ImageContentTypes.TryGetValue(picture.Format, out var contentType)) continue;
                var row = picture.TopLeftCell.Address.RowNumber;
                if (result.ContainsKey(row)) continue;

                picture.ImageStream.Position = 0;
                using var ms = new MemoryStream();
                picture.ImageStream.CopyTo(ms);
                result[row] = (ms.ToArray(), contentType);
            }
            return result;
        }

        private static int FindHeaderRow(IXLWorksheet ws, int lastRow, int lastCol)
        {
            for (var r = 1; r <= Math.Min(lastRow, MaxHeaderSearchRows); r++)
                for (var c = 1; c <= lastCol; c++)
                    if (Normalize(ws.Cell(r, c).GetString()) == "codigo")
                        return r;
            return 0;
        }

        private static int ColumnOf(Dictionary<string, int> headers, Func<string, bool> match)
            => headers.FirstOrDefault(kv => match(kv.Key)).Value; // 0 si no existe

        private static bool TryNumber(IXLCell cell, out decimal value)
        {
            if (cell.DataType == XLDataType.Number)
            {
                value = (decimal)cell.GetDouble();
                return true;
            }
            return decimal.TryParse(cell.GetString().Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private static string Text(IXLCell cell) => cell.GetString().Trim();

        private static ExcelProductCatalogParseResult Fail(string error)
            => new(false, Array.Empty<ProductCatalogRowDto>(), new[] { error });

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
    }
}
