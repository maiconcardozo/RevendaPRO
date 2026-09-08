using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RevendaPro.Application.Company.DTOs;

namespace RevendaPro.Api.Reports
{
    /// <summary>
    /// Uma tabela em PDF a partir de linhas e colunas tipadas: A4 deitado, o cabeçalho com a
    /// revenda, o título e o período, o cabeçalho da tabela repetido em toda página, e o rodapé
    /// com a numeração.
    /// </summary>
    public static class TablePdf
    {
        /// <summary>O media type do PDF.</summary>
        public const string ContentType = "application/pdf";

        /// <summary>Gera o PDF.</summary>
        /// <typeparam name="T">A linha.</typeparam>
        /// <param name="company">A revenda, para o cabeçalho.</param>
        /// <param name="title">O nome do documento.</param>
        /// <param name="subtitle">O período ou o filtro, por extenso.</param>
        /// <param name="rows">As linhas.</param>
        /// <param name="columns">As colunas, na ordem da tela.</param>
        /// <returns>Os bytes do arquivo.</returns>
        public static byte[] Render<T>(
            CompanyDto company,
            string title,
            string? subtitle,
            IReadOnlyList<T> rows,
            IReadOnlyList<ReportColumn<T>> columns)
        {
            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    DocumentTheme.Configure(page, landscape: true);

                    page.Header().Element(header => DocumentTheme.Letterhead(header, company, title, subtitle));

                    page.Content().PaddingTop(5, Unit.Millimetre).Table(table =>
                    {
                        table.ColumnsDefinition(definition =>
                        {
                            foreach (var _ in columns)
                            {
                                definition.RelativeColumn();
                            }
                        });

                        table.Header(header =>
                        {
                            foreach (var column in columns)
                            {
                                var cell = header.Cell()
                                    .Background(DocumentTheme.Wash)
                                    .BorderBottom(0.75f).BorderColor(DocumentTheme.Line)
                                    .PaddingVertical(2).PaddingHorizontal(3);

                                (column.AlignRight ? cell.AlignRight() : cell)
                                    .Text(column.Header).FontSize(8).Bold();
                            }
                        });

                        foreach (var row in rows)
                        {
                            foreach (var column in columns)
                            {
                                var cell = table.Cell()
                                    .BorderBottom(0.25f).BorderColor(DocumentTheme.Line)
                                    .PaddingVertical(1.5f).PaddingHorizontal(3);

                                (column.AlignRight ? cell.AlignRight() : cell)
                                    .Text(DocumentTheme.Cell(column.Value(row))).FontSize(8);
                            }
                        }

                        if (rows.Count == 0)
                        {
                            table.Cell().ColumnSpan((uint)columns.Count)
                                .PaddingVertical(8, Unit.Millimetre)
                                .AlignCenter()
                                .Text("Nenhum registro no período ou nos filtros escolhidos.")
                                .FontColor(DocumentTheme.Muted);
                        }
                    });

                    page.Footer().Element(DocumentTheme.Footer);
                });
            }).WithSettings(DocumentTheme.Settings).GeneratePdf();
        }
    }
}
