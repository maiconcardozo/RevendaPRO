using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RevendaPro.Application.Company.DTOs;

namespace RevendaPro.Api.Reports
{
    /// <summary>
    /// O que todo PDF deste sistema repete: a cultura, o cabeçalho com a revenda, o rodapé com a
    /// página, as fontes e as cores.
    ///
    /// Vive na camada da API porque é apresentação — a mesma pergunta que a tela responde em
    /// HTML, respondida em papel. A aplicação entrega o DTO e jamais sabe o que é uma página A4.
    /// Ver ADR-0007.
    /// </summary>
    public static class DocumentTheme
    {
        /// <summary>
        /// Toda data e todo valor passam por aqui. O contêiner Linux sobe com cultura invariante,
        /// e um preço em <c>1,234.56</c> no papel da revenda é um erro que ninguém vê antes do
        /// cliente.
        /// </summary>
        public static readonly CultureInfo Culture = new("pt-BR");

        /// <summary>A tinta do sistema, a mesma da tela.</summary>
        public const string Ink = "#0b1e3f";

        /// <summary>O azul de destaque, o mesmo da tela.</summary>
        public const string Signal = "#0090c4";

        /// <summary>O cinza do texto secundário.</summary>
        public const string Muted = "#4a566b";

        /// <summary>Fio de tabela e de separação.</summary>
        public const string Line = "#d3dbe3";

        /// <summary>Fundo de célula de cabeçalho e de caixa de destaque.</summary>
        public const string Wash = "#edf1f4";

        /// <summary>
        /// Agora, no horário de Brasília. O contêiner roda em UTC, e "gerado às 06:20" num papel
        /// impresso às três da tarde é o tipo de erro que faz o cliente desconfiar do resto.
        /// </summary>
        public static DateTime Now()
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
            }
            catch (TimeZoneNotFoundException)
            {
                return DateTime.Now;
            }
        }

        /// <summary>Hoje, no horário de Brasília.</summary>
        public static DateOnly Today() => DateOnly.FromDateTime(Now());

        /// <summary>R$ 12.345,67.</summary>
        public static string Money(decimal value) => value.ToString("C2", Culture);

        /// <summary>R$ 12.345,67, ou o traço quando há valor nenhum.</summary>
        public static string Money(decimal? value) => value is { } v ? Money(v) : "—";

        /// <summary>08/09/2026.</summary>
        public static string Date(DateOnly value) => value.ToString("dd/MM/yyyy", Culture);

        /// <summary>08/09/2026, ou o traço.</summary>
        public static string Date(DateOnly? value) => value is { } v ? Date(v) : "—";

        /// <summary>8 de setembro de 2026.</summary>
        public static string LongDate(DateOnly value) => value.ToString("d 'de' MMMM 'de' yyyy", Culture);

        /// <summary>setembro/2026.</summary>
        public static string MonthName(DateOnly value) => value.ToString("MMMM/yyyy", Culture);

        /// <summary>12.345 km.</summary>
        public static string Mileage(int value) => $"{value.ToString("N0", Culture)} km";

        /// <summary>(51) 99999-0000, a partir dos dígitos.</summary>
        public static string Phone(string? digits)
        {
            if (string.IsNullOrWhiteSpace(digits))
            {
                return string.Empty;
            }

            return digits.Length switch
            {
                11 => $"({digits[..2]}) {digits[2..7]}-{digits[7..]}",
                10 => $"({digits[..2]}) {digits[2..6]}-{digits[6..]}",
                _ => digits,
            };
        }

        /// <summary>12.345.678/0001-95 ou 123.456.789-09, a partir dos dígitos.</summary>
        public static string TaxId(string? digits)
        {
            if (string.IsNullOrWhiteSpace(digits))
            {
                return string.Empty;
            }

            return digits.Length switch
            {
                14 => Convert.ToUInt64(digits, Culture).ToString(@"00\.000\.000\/0000\-00", Culture),
                11 => Convert.ToUInt64(digits, Culture).ToString(@"000\.000\.000\-00", Culture),
                _ => digits,
            };
        }

        /// <summary>
        /// As fotos dentro do PDF: reamostradas para 110 pontos por polegada e comprimidas em
        /// JPEG de qualidade média. Uma ficha com foto fica em torno de cem KB, o que o WhatsApp
        /// manda sem reclamar; no padrão do QuestPDF a mesma ficha passava de um megabyte.
        /// </summary>
        public static readonly DocumentSettings Settings = new()
        {
            ImageRasterDpi = 110,
            ImageCompressionQuality = ImageCompressionQuality.Medium,
        };

        /// <summary>A página como todo documento a configura: A4, margens e a fonte base.</summary>
        /// <param name="page">A página.</param>
        /// <param name="landscape">Deitada, para tabelas largas.</param>
        public static void Configure(PageDescriptor page, bool landscape = false)
        {
            page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.Margin(14, Unit.Millimetre);
            page.DefaultTextStyle(text => text.FontSize(9.5f).FontColor(Ink));
        }

        /// <summary>
        /// O cabeçalho com a revenda: o nome em destaque, e abaixo o CNPJ, o telefone, o e-mail e
        /// o endereço — o que o cliente precisa para responder ao papel. À direita, o título do
        /// documento e a data.
        /// </summary>
        /// <param name="container">Onde desenhar.</param>
        /// <param name="company">A revenda.</param>
        /// <param name="title">O nome do documento.</param>
        /// <param name="subtitle">Uma linha abaixo do título, como a data ou o período.</param>
        /// <summary>A largura da caixa do logotipo no timbre, em milímetros. Duas vezes a altura: a proporção da moldura da tela.</summary>
        public const float LogoWidth = 40f;

        /// <summary>A altura da caixa do logotipo no timbre, em milímetros.</summary>
        public const float LogoHeight = 20f;

        public static void Letterhead(IContainer container, CompanyDto company, string title, string? subtitle = null) =>
            Letterhead(container, company, logo: null, title, subtitle);

        /// <summary>
        /// O mesmo cabeçalho, com o logotipo da revenda à esquerda do nome (M25).
        ///
        /// O logotipo entra numa caixa de <see cref="LogoWidth"/> por <see cref="LogoHeight"/>
        /// milímetros — a mesma proporção da moldura de recorte da tela, para o que a pessoa
        /// enquadrou ser o que sai —, e a imagem é ajustada dentro dela sem esticar. Sem
        /// logotipo, a caixa não existe e o nome ocupa o lugar inteiro, exatamente como no M19:
        /// um marco de aparência jamais pode piorar o papel de quem escolheu não usar a novidade.
        /// </summary>
        /// <param name="container">Onde desenhar.</param>
        /// <param name="company">A revenda.</param>
        /// <param name="logo">O logotipo em PNG, ou nulo.</param>
        /// <param name="title">O nome do documento.</param>
        /// <param name="subtitle">Uma linha abaixo do título, como a data ou o período.</param>
        public static void Letterhead(
            IContainer container,
            CompanyDto company,
            byte[]? logo,
            string title,
            string? subtitle = null)
        {
            container
                .BorderBottom(1.2f).BorderColor(Signal)
                .PaddingBottom(4, Unit.Millimetre)
                .Row(row =>
                {
                    if (logo is { Length: > 0 })
                    {
                        row.ConstantItem(LogoWidth, Unit.Millimetre)
                            .Height(LogoHeight, Unit.Millimetre)
                            .PaddingRight(4, Unit.Millimetre)
                            .AlignLeft()
                            .AlignMiddle()
                            .Image(logo)
                            .FitArea();
                    }

                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text(company.Name).FontSize(16).Bold().FontColor(Ink);

                        var lines = new List<string>();

                        if (!string.IsNullOrWhiteSpace(company.Document))
                        {
                            lines.Add($"CNPJ {TaxId(company.Document)}");
                        }

                        var contact = new[] { Phone(company.Phone), company.Email ?? string.Empty }
                            .Where(part => part.Length > 0);

                        var contactLine = string.Join("  ·  ", contact);

                        if (contactLine.Length > 0)
                        {
                            lines.Add(contactLine);
                        }

                        if (!string.IsNullOrWhiteSpace(company.Address))
                        {
                            lines.Add(company.Address);
                        }

                        foreach (var line in lines)
                        {
                            column.Item().Text(line).FontSize(8.5f).FontColor(Muted);
                        }
                    });

                    row.ConstantItem(60, Unit.Millimetre).AlignRight().Column(column =>
                    {
                        column.Item().AlignRight().Text(title).FontSize(12).Bold().FontColor(Signal);

                        if (!string.IsNullOrWhiteSpace(subtitle))
                        {
                            column.Item().AlignRight().Text(subtitle).FontSize(8.5f).FontColor(Muted);
                        }
                    });
                });
        }

        /// <summary>O rodapé: quem gerou, quando, e "página N de M".</summary>
        /// <param name="container">Onde desenhar.</param>
        public static void Footer(IContainer container)
        {
            container
                .PaddingTop(3, Unit.Millimetre)
                .BorderTop(0.5f).BorderColor(Line)
                .Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(7.5f).FontColor(Muted));
                        text.Span("Gerado pelo Revenda Pro em ");
                        text.Span(Now().ToString("dd/MM/yyyy 'às' HH:mm", Culture));
                    });

                    row.ConstantItem(40, Unit.Millimetre).AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(7.5f).FontColor(Muted));
                        text.Span("página ");
                        text.CurrentPageNumber();
                        text.Span(" de ");
                        text.TotalPages();
                    });
                });
        }

        /// <summary>Um rótulo pequeno em caixa alta, como o da tela.</summary>
        /// <param name="container">Onde desenhar.</param>
        /// <param name="text">O rótulo.</param>
        public static void Label(IContainer container, string text) =>
            container.Text(text.ToUpper(Culture)).FontSize(7).Bold().FontColor(Muted).LetterSpacing(0.08f);

        /// <summary>Um valor formatado para a célula: dinheiro, data, número, texto.</summary>
        /// <param name="value">O valor, tipado.</param>
        /// <returns>O texto.</returns>
        public static string Cell(object? value) => value switch
        {
            null => string.Empty,
            string s => s,
            decimal d => d.ToString("N2", Culture),
            double d => d.ToString("N2", Culture),
            int i => i.ToString("N0", Culture),
            long l => l.ToString("N0", Culture),
            DateOnly d => Date(d),
            DateTime d => d.ToString("dd/MM/yyyy HH:mm", Culture),
            bool b => b ? "Sim" : "—",
            _ => value.ToString() ?? string.Empty,
        };
    }
}
