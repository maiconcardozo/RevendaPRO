using ClosedXML.Excel;

namespace RevendaPro.Api.Reports
{
    /// <summary>
    /// Uma planilha Excel a partir de linhas e colunas tipadas.
    ///
    /// A célula guarda o tipo de verdade: número com duas casas, data como data, inteiro como
    /// inteiro. É o que faz a soma e a ordenação funcionarem no Excel sem que ninguém precise
    /// converter texto. A primeira linha fica congelada e em negrito, e as colunas se ajustam ao
    /// conteúdo — o mesmo desenho do PortalCliente.
    /// </summary>
    public static class TableExcel
    {
        /// <summary>O media type do .xlsx.</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>Gera a planilha.</summary>
        /// <typeparam name="T">A linha.</typeparam>
        /// <param name="sheetName">O nome da aba, até 31 caracteres.</param>
        /// <param name="rows">As linhas, todas: planilha é para levar tudo.</param>
        /// <param name="columns">As colunas, na ordem da tela.</param>
        /// <returns>Os bytes do arquivo.</returns>
        public static byte[] Render<T>(
            string sheetName,
            IReadOnlyList<T> rows,
            IReadOnlyList<ReportColumn<T>> columns)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(sheetName.Length > 31 ? sheetName[..31] : sheetName);

            for (var c = 0; c < columns.Count; c++)
            {
                sheet.Cell(1, c + 1).Value = columns[c].Header;
            }

            sheet.Row(1).Style.Font.Bold = true;
            sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml(DocumentTheme.Wash);

            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < columns.Count; c++)
                {
                    Set(sheet.Cell(r + 2, c + 1), columns[c].Value(rows[r]));
                }
            }

            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return stream.ToArray();
        }

        private static void Set(IXLCell cell, object? value)
        {
            switch (value)
            {
                case null:
                    break;
                case DateOnly d:
                    cell.Value = d.ToDateTime(TimeOnly.MinValue);
                    cell.Style.DateFormat.Format = "dd/mm/yyyy";
                    break;
                case DateTime d:
                    cell.Value = d;
                    cell.Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
                    break;
                case decimal d:
                    cell.Value = d;
                    cell.Style.NumberFormat.Format = "#,##0.00";
                    break;
                case double d:
                    cell.Value = d;
                    cell.Style.NumberFormat.Format = "#,##0.00";
                    break;
                case int i:
                    cell.Value = i;
                    break;
                case long l:
                    cell.Value = l;
                    break;
                case bool b:
                    cell.Value = b ? "Sim" : "—";
                    break;
                default:
                    cell.Value = value.ToString();
                    break;
            }
        }
    }
}
