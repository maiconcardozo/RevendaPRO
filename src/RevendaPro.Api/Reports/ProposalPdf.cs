using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RevendaPro.Application.Reports.DTOs;

namespace RevendaPro.Api.Reports
{
    /// <summary>
    /// A proposta em papel timbrado: a revenda em cima, o cliente, o carro com a foto, o valor
    /// em destaque, a forma de pagamento, a validade, as condições e duas linhas para assinar.
    /// Uma página, para imprimir ou mandar.
    /// </summary>
    public static class ProposalPdf
    {
        /// <summary>O media type do PDF.</summary>
        public const string ContentType = "application/pdf";

        /// <summary>Gera a proposta.</summary>
        /// <param name="proposal">A proposta, como a aplicação a entrega.</param>
        /// <returns>Os bytes do PDF.</returns>
        public static byte[] Render(ProposalDocumentDto proposal)
        {
            var reference = proposal.ProposalCode.ToString("N")[..8].ToUpperInvariant();

            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    DocumentTheme.Configure(page);

                    page.Header().Element(header => DocumentTheme.Letterhead(
                        header, proposal.Company, "Proposta comercial",
                        $"Nº {reference} · {DocumentTheme.LongDate(proposal.Date)}"));

                    page.Content().PaddingTop(6, Unit.Millimetre).Column(column =>
                    {
                        column.Spacing(5, Unit.Millimetre);

                        // Para quem.
                        column.Item().Column(to =>
                        {
                            to.Item().Element(e => DocumentTheme.Label(e, "Para"));
                            to.Item().Text(proposal.ProspectName).FontSize(13).Bold();

                            if (!string.IsNullOrWhiteSpace(proposal.ProspectPhone))
                            {
                                to.Item().Text(DocumentTheme.Phone(proposal.ProspectPhone)).FontColor(DocumentTheme.Muted);
                            }
                        });

                        column.Item().Text(text =>
                        {
                            text.Span("Apresentamos a proposta para a venda do veículo abaixo, nas condições descritas. ");
                            text.Span("Esta proposta vale até ");
                            text.Span(DocumentTheme.LongDate(proposal.ValidUntil)).Bold();
                            text.Span(".");
                        });

                        // O carro, com a foto ao lado.
                        column.Item().Row(row =>
                        {
                            row.Spacing(5, Unit.Millimetre);

                            if (proposal.CoverPhoto is not null)
                            {
                                row.ConstantItem(70, Unit.Millimetre).Height(48, Unit.Millimetre)
                                    .AlignCenter().Image(proposal.CoverPhoto).FitArea();
                            }

                            row.RelativeItem().Column(car =>
                            {
                                car.Item().Element(e => DocumentTheme.Label(e, "Veículo"));
                                car.Item().Text(proposal.VehicleName).FontSize(14).Bold();
                                car.Item().Text(
                                        $"{proposal.ModelYear}/{proposal.ManufactureYear} · {DocumentTheme.Mileage(proposal.Mileage)}"
                                        + (string.IsNullOrWhiteSpace(proposal.Color) ? string.Empty : $" · {proposal.Color}"))
                                    .FontColor(DocumentTheme.Muted);
                                car.Item().PaddingTop(1, Unit.Millimetre).Text($"Placa {proposal.Plate}").FontSize(8.5f).FontColor(DocumentTheme.Muted);
                            });
                        });

                        // O valor e a forma de pagamento, lado a lado.
                        column.Item().Row(row =>
                        {
                            row.Spacing(4, Unit.Millimetre);

                            row.RelativeItem().Background(DocumentTheme.Wash).Padding(4, Unit.Millimetre).Column(box =>
                            {
                                box.Item().Element(e => DocumentTheme.Label(e, "Valor proposto"));
                                box.Item().Text(DocumentTheme.Money(proposal.Amount))
                                    .FontSize(22).Bold().FontColor(DocumentTheme.Signal);
                            });

                            row.RelativeItem().Background(DocumentTheme.Wash).Padding(4, Unit.Millimetre).Column(box =>
                            {
                                box.Item().Element(e => DocumentTheme.Label(e, "Forma de pagamento"));
                                box.Item().PaddingTop(1, Unit.Millimetre).Text(Labels.Of(proposal.PaymentMethod)).FontSize(12).Bold();
                                box.Item().Text($"Validade: {DocumentTheme.Date(proposal.ValidUntil)}").FontSize(8.5f).FontColor(DocumentTheme.Muted);
                            });
                        });

                        // As condições, quando escritas.
                        if (!string.IsNullOrWhiteSpace(proposal.Notes))
                        {
                            column.Item().Column(notes =>
                            {
                                notes.Item().Element(e => DocumentTheme.Label(e, "Condições"));
                                notes.Item().PaddingTop(1, Unit.Millimetre).Text(proposal.Notes);
                            });
                        }

                        column.Item().Text(
                                "Valores sujeitos a confirmação de disponibilidade do veículo na data da assinatura. "
                                + "Documentação e transferência conforme combinado entre as partes.")
                            .FontSize(8).FontColor(DocumentTheme.Muted);

                        // As assinaturas.
                        column.Item().PaddingTop(14, Unit.Millimetre).Row(row =>
                        {
                            row.Spacing(12, Unit.Millimetre);

                            foreach (var who in new[] { proposal.Company.Name, proposal.ProspectName })
                            {
                                row.RelativeItem().Column(signature =>
                                {
                                    signature.Item().BorderTop(0.6f).BorderColor(DocumentTheme.Ink)
                                        .PaddingTop(1.5f, Unit.Millimetre)
                                        .AlignCenter().Text(who).FontSize(8.5f);
                                });
                            }
                        });
                    });

                    page.Footer().Element(DocumentTheme.Footer);
                });
            }).WithSettings(DocumentTheme.Settings).GeneratePdf();
        }
    }
}
