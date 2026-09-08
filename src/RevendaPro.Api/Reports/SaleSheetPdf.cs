using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RevendaPro.Application.Reports.DTOs;

namespace RevendaPro.Api.Reports
{
    /// <summary>
    /// A ficha do carro para venda: uma página, a foto grande, o que o carro tem, a tabela e o
    /// preço — e o contato da revenda embaixo. Pronta para imprimir ou mandar pelo WhatsApp.
    ///
    /// Tudo o que é da casa fica de fora por decisão do handler; aqui só se desenha.
    /// </summary>
    public static class SaleSheetPdf
    {
        /// <summary>O media type do PDF.</summary>
        public const string ContentType = "application/pdf";

        /// <summary>Gera a ficha.</summary>
        /// <param name="sheet">A ficha, como a aplicação a entrega.</param>
        /// <returns>Os bytes do PDF.</returns>
        public static byte[] Render(SaleSheetDto sheet)
        {
            var name = string.IsNullOrWhiteSpace(sheet.Version)
                ? $"{sheet.Brand} {sheet.Model}"
                : $"{sheet.Brand} {sheet.Model} {sheet.Version}";

            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    DocumentTheme.Configure(page);

                    page.Header().Element(header => DocumentTheme.Letterhead(
                        header, sheet.Company, "Ficha do veículo", DocumentTheme.LongDate(sheet.IssuedOn)));

                    page.Content().PaddingTop(5, Unit.Millimetre).Column(column =>
                    {
                        column.Spacing(4, Unit.Millimetre);

                        // Nome e ano, o que a pessoa lê primeiro.
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Column(title =>
                            {
                                title.Item().Text(name).FontSize(20).Bold();
                                title.Item().Text($"{sheet.ModelYear}/{sheet.ManufactureYear} · {DocumentTheme.Mileage(sheet.Mileage)}"
                                        + (string.IsNullOrWhiteSpace(sheet.Color) ? string.Empty : $" · {sheet.Color}"))
                                    .FontSize(10).FontColor(DocumentTheme.Muted);
                            });

                            row.ConstantItem(62, Unit.Millimetre)
                                .Background(DocumentTheme.Wash)
                                .Padding(3, Unit.Millimetre)
                                .Column(price =>
                                {
                                    price.Item().Element(e => DocumentTheme.Label(e, "Preço"));
                                    price.Item().Text(sheet.AdvertisedPrice is { } value
                                            ? DocumentTheme.Money(value)
                                            : "Consulte")
                                        .FontSize(18).Bold().FontColor(DocumentTheme.Signal);
                                });
                        });

                        // A foto de capa, e as outras em fila embaixo.
                        if (sheet.Photos.Count > 0)
                        {
                            column.Item().Height(95, Unit.Millimetre).AlignCenter()
                                .Image(sheet.Photos[0]).FitArea();

                            if (sheet.Photos.Count > 1)
                            {
                                column.Item().Row(row =>
                                {
                                    row.Spacing(2, Unit.Millimetre);

                                    foreach (var photo in sheet.Photos.Skip(1).Take(6))
                                    {
                                        row.RelativeItem().Height(26, Unit.Millimetre).AlignCenter().Image(photo).FitArea();
                                    }
                                });
                            }
                        }
                        else
                        {
                            column.Item().Height(40, Unit.Millimetre)
                                .Background(DocumentTheme.Wash)
                                .AlignCenter().AlignMiddle()
                                .Text("Sem fotos cadastradas").FontColor(DocumentTheme.Muted);
                        }

                        // O que o carro tem, em duas colunas de pares.
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(definition =>
                            {
                                definition.RelativeColumn(1.1f);
                                definition.RelativeColumn(1.6f);
                                definition.RelativeColumn(1.1f);
                                definition.RelativeColumn(1.6f);
                            });

                            var facts = new (string Label, string Value)[]
                            {
                                ("Marca", sheet.Brand),
                                ("Modelo", sheet.Model),
                                ("Versão", sheet.Version ?? "—"),
                                ("Ano", $"{sheet.ModelYear}/{sheet.ManufactureYear}"),
                                ("Quilometragem", DocumentTheme.Mileage(sheet.Mileage)),
                                ("Cor", sheet.Color ?? "—"),
                                ("Combustível", Labels.Of(sheet.FuelType)),
                                ("Câmbio", Labels.Of(sheet.Transmission)),
                                ("Placa", sheet.Plate),
                                ("Tabela FIPE", sheet.FipeValue is { } fipe
                                    ? $"{DocumentTheme.Money(fipe)}"
                                        + (sheet.FipeReferenceDate is { } month ? $" ({DocumentTheme.MonthName(month)})" : string.Empty)
                                    : "—"),
                            };

                            foreach (var (label, value) in facts)
                            {
                                table.Cell().BorderBottom(0.25f).BorderColor(DocumentTheme.Line).PaddingVertical(2)
                                    .Element(e => DocumentTheme.Label(e, label));
                                table.Cell().BorderBottom(0.25f).BorderColor(DocumentTheme.Line).PaddingVertical(2)
                                    .Text(value);
                            }
                        });

                        // O convite: como falar com a revenda.
                        var contact = new[]
                            {
                                DocumentTheme.Phone(sheet.Company.Phone),
                                sheet.Company.Email ?? string.Empty,
                            }
                            .Where(part => part.Length > 0)
                            .ToList();

                        if (contact.Count > 0 || !string.IsNullOrWhiteSpace(sheet.Company.Address))
                        {
                            column.Item().PaddingTop(2, Unit.Millimetre)
                                .Background(DocumentTheme.Wash)
                                .Padding(3, Unit.Millimetre)
                                .Column(box =>
                                {
                                    box.Item().Element(e => DocumentTheme.Label(e, "Fale com a gente"));
                                    box.Item().Text(sheet.Company.Name).Bold();

                                    if (contact.Count > 0)
                                    {
                                        box.Item().Text(string.Join("  ·  ", contact));
                                    }

                                    if (!string.IsNullOrWhiteSpace(sheet.Company.Address))
                                    {
                                        box.Item().Text(sheet.Company.Address).FontColor(DocumentTheme.Muted);
                                    }
                                });
                        }
                    });

                    page.Footer().Element(DocumentTheme.Footer);
                });
            }).GeneratePdf();
        }
    }
}
