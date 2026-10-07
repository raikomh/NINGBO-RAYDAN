using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using ClosedXML.Excel;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>Genera los .xlsx de los reportes del TPV/ERP (ventas, inventario, gastos) con ClosedXML.</summary>
    public class OperationsReportExporter : IOperationsReportExporter
    {
        public byte[] ExportSales(SalesReportDto report)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Ventas");
            var headers = new[] { "Fecha", "Caja", "Método de pago", "Moneda", "Total", "Estado" };
            for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            ws.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var r in report.Rows)
            {
                ws.Cell(row, 1).Value = r.Date;
                ws.Cell(row, 2).Value = r.RegisterId.ToString();
                ws.Cell(row, 3).Value = r.PaymentMethod;
                ws.Cell(row, 4).Value = r.Currency;
                ws.Cell(row, 5).Value = r.Total;
                ws.Cell(row, 6).Value = r.Status;
                row++;
            }

            ws.Cell(row + 1, 4).Value = "Total ventas:";
            ws.Cell(row + 1, 5).Value = report.TotalSales;
            ws.Cell(row + 2, 4).Value = "Total reembolsado:";
            ws.Cell(row + 2, 5).Value = report.TotalRefunded;
            ws.Columns().AdjustToContents();
            return ToBytes(wb);
        }

        public byte[] ExportInventory(InventoryReportDto report)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Inventario");
            var headers = new[] { "Producto", "Código", "Stock total", "Stock mínimo", "Precio venta", "Bajo stock" };
            for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            ws.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var r in report.Rows)
            {
                ws.Cell(row, 1).Value = r.ProductName;
                ws.Cell(row, 2).Value = r.Barcode ?? "";
                ws.Cell(row, 3).Value = r.TotalStock;
                ws.Cell(row, 4).Value = r.MinStock;
                ws.Cell(row, 5).Value = r.SellPrice;
                ws.Cell(row, 6).Value = r.LowStock ? "Sí" : "No";
                if (r.LowStock) ws.Row(row).Style.Fill.BackgroundColor = XLColor.LightYellow;
                row++;
            }

            ws.Cell(row + 1, 4).Value = "Valor del inventario:";
            ws.Cell(row + 1, 5).Value = report.InventoryValue;
            ws.Columns().AdjustToContents();
            return ToBytes(wb);
        }

        public byte[] ExportExpenses(ExpensesReportDto report)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Gastos");
            var headers = new[] { "Fecha", "Tipo", "Descripción", "Monto" };
            for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            ws.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var r in report.Rows)
            {
                ws.Cell(row, 1).Value = r.Date;
                ws.Cell(row, 2).Value = r.Type;
                ws.Cell(row, 3).Value = r.Description;
                ws.Cell(row, 4).Value = r.Amount;
                row++;
            }

            ws.Cell(row + 1, 3).Value = "Total:";
            ws.Cell(row + 1, 4).Value = report.Total;
            ws.Columns().AdjustToContents();
            return ToBytes(wb);
        }

        private static byte[] ToBytes(XLWorkbook wb)
        {
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
