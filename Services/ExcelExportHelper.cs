using ClosedXML.Excel;

namespace AppValetParking.Services
{
    // Aplica un formato visual consistente a los reportes Excel generados con ClosedXML
    // en todos los controladores (encabezado de color, bordes, autoajuste de columnas, etc.)
    public static class ExcelExportHelper
    {
        private static readonly XLColor HeaderBackground = XLColor.FromHtml("#1F3864");
        private static readonly XLColor HeaderText = XLColor.White;
        private static readonly XLColor BandedRowFill = XLColor.FromHtml("#F2F5FA");
        private static readonly XLColor BorderColor = XLColor.FromHtml("#D0D7E5");

        public static IXLWorksheet CreateStyledSheet(XLWorkbook workbook, string sheetName, string[] headers)
        {
            var ws = workbook.Worksheets.Add(sheetName);

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
            }

            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Fill.BackgroundColor = HeaderBackground;
            headerRange.Style.Font.FontColor = HeaderText;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.OutsideBorderColor = BorderColor;
            headerRange.Style.Border.InsideBorderColor = BorderColor;

            ws.SheetView.FreezeRows(1);
            ws.SheetView.Freeze(1, 0);

            return ws;
        }

        // Aplica bordes, bandas alternadas y autoajuste de columnas a las filas de datos ya escritas.
        // lastRow es el número de la última fila con datos (incluyendo encabezado); lastColumn el número de columnas.
        public static void FinalizeStyledSheet(IXLWorksheet ws, int lastDataRow, int columnCount)
        {
            if (lastDataRow > 1)
            {
                var dataRange = ws.Range(2, 1, lastDataRow, columnCount);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.OutsideBorderColor = BorderColor;
                dataRange.Style.Border.InsideBorderColor = BorderColor;
                dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                for (int row = 2; row <= lastDataRow; row++)
                {
                    if ((row - 2) % 2 == 1)
                    {
                        ws.Range(row, 1, row, columnCount).Style.Fill.BackgroundColor = BandedRowFill;
                    }
                }
            }

            ws.Columns(1, columnCount).AdjustToContents();
        }

        // Para hojas creadas con ws.Cell(...).InsertTable(...), donde ClosedXML ya generó la tabla.
        public static void StyleInsertedTable(IXLTable table)
        {
            var ws = table.Worksheet;
            var range = table.RangeUsed();
            if (range == null) return;

            int firstRow = range.FirstCell().Address.RowNumber;
            int lastRow = range.LastCell().Address.RowNumber;
            int firstCol = range.FirstCell().Address.ColumnNumber;
            int lastCol = range.LastCell().Address.ColumnNumber;
            int columnCount = lastCol - firstCol + 1;

            var headerRange = ws.Range(firstRow, firstCol, firstRow, lastCol);
            headerRange.Style.Fill.BackgroundColor = HeaderBackground;
            headerRange.Style.Font.FontColor = HeaderText;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            ws.SheetView.FreezeRows(firstRow);

            if (lastRow > firstRow)
            {
                var dataRange = ws.Range(firstRow + 1, firstCol, lastRow, lastCol);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.OutsideBorderColor = BorderColor;
                dataRange.Style.Border.InsideBorderColor = BorderColor;

                for (int row = firstRow + 1; row <= lastRow; row++)
                {
                    if ((row - (firstRow + 1)) % 2 == 1)
                    {
                        ws.Range(row, firstCol, row, lastCol).Style.Fill.BackgroundColor = BandedRowFill;
                    }
                }
            }

            ws.Columns(firstCol, lastCol).AdjustToContents();
        }
    }
}
