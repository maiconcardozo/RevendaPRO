using System.Text;

namespace RevendaPro.Api.Reports
{
    /// <summary>
    /// Um CSV a partir de linhas e colunas tipadas, escrito à mão.
    ///
    /// Ponto e vírgula como separador, porque o Excel em português usa a vírgula como decimal e
    /// abre um CSV com vírgula como uma coluna só. BOM UTF-8 na frente, porque sem ele o Excel
    /// lê os acentos errado. Aspas dobradas onde o valor tem separador, aspas ou quebra de
    /// linha. São vinte linhas, e uma biblioteca para isso seria dependência sem retorno.
    /// </summary>
    public static class TableCsv
    {
        /// <summary>O media type, com o charset dito por extenso.</summary>
        public const string ContentType = "text/csv; charset=utf-8";

        private const char Separator = ';';

        /// <summary>Gera o CSV.</summary>
        /// <typeparam name="T">A linha.</typeparam>
        /// <param name="rows">As linhas, todas.</param>
        /// <param name="columns">As colunas, na ordem da tela.</param>
        /// <returns>Os bytes do arquivo, com o BOM.</returns>
        public static byte[] Render<T>(IReadOnlyList<T> rows, IReadOnlyList<ReportColumn<T>> columns)
        {
            var text = new StringBuilder();

            text.AppendJoin(Separator, columns.Select(column => Quote(column.Header))).Append("\r\n");

            foreach (var row in rows)
            {
                text.AppendJoin(Separator, columns.Select(column => Quote(DocumentTheme.Cell(column.Value(row)))))
                    .Append("\r\n");
            }

            var body = Encoding.UTF8.GetBytes(text.ToString());
            var bom = Encoding.UTF8.GetPreamble();

            var bytes = new byte[bom.Length + body.Length];
            bom.CopyTo(bytes, 0);
            body.CopyTo(bytes, bom.Length);

            return bytes;
        }

        private static string Quote(string value)
        {
            if (value.IndexOfAny([Separator, '"', '\n', '\r']) < 0)
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
    }
}
