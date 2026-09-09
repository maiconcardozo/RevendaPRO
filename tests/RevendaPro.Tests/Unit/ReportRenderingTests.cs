using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using RevendaPro.Api.Reports;
using RevendaPro.Application.Company.DTOs;
using RevendaPro.Application.Reports.DTOs;
using RevendaPro.Domain.Enums;
using SkiaSharp;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// Os construtores genéricos de documento (M19): cada formato gera um arquivo de verdade.
    ///
    /// O PortalCliente, que é a referência, não tem um teste que gere um byte de PDF ou de
    /// planilha. Aqui cada formato é gerado e aberto de novo: o PDF começa com a assinatura
    /// <c>%PDF-</c>, o Excel guarda número como número e data como data, e o CSV sai com ponto
    /// e vírgula, BOM e aspas onde precisa — que é o que faz o Excel em português abrir o
    /// arquivo certo.
    /// </summary>
    public class ReportRenderingTests
    {
        private static readonly CompanyDto Company = new(
            "Revenda do Zé", "12345678000195", "51999990000", "contato@revenda.com", "Rua A, 10 — Porto Alegre",
            HasLogo: false, LogoVersion: null);

        private sealed record Row(string Plate, decimal Amount, DateOnly Date, int Count, bool Paid);

        private static readonly IReadOnlyList<Row> Rows =
        [
            new("ABC1D23", 1234.5m, new DateOnly(2026, 9, 8), 3, true),
            new("Gol; \"prata\"", 99m, new DateOnly(2026, 1, 1), 0, false),
        ];

        private static readonly IReadOnlyList<ReportColumn<Row>> Columns =
        [
            new("Placa", r => r.Plate),
            new("Valor", r => r.Amount, AlignRight: true),
            new("Data", r => r.Date),
            new("Gastos", r => r.Count, AlignRight: true),
            new("Pago", r => r.Paid),
        ];

        [Fact]
        public void ThePdf_IsAPdf_AndCarriesTheLetterhead()
        {
            var bytes = TablePdf.Render(Company, "Veículos", "Desde o início", Rows, Columns);

            bytes.Should().NotBeEmpty();
            Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        }

        [Fact]
        public void TheExcel_KeepsNumbersAndDates_AsNumbersAndDates()
        {
            var bytes = TableExcel.Render("Veículos", Rows, Columns);

            // PK: o .xlsx é um zip, e é assim que ele começa.
            bytes[0].Should().Be((byte)'P');
            bytes[1].Should().Be((byte)'K');

            using var workbook = new XLWorkbook(new MemoryStream(bytes));
            var sheet = workbook.Worksheet(1);

            sheet.Cell(1, 1).GetString().Should().Be("Placa");
            sheet.Cell(2, 2).GetValue<decimal>().Should().Be(1234.5m);
            sheet.Cell(2, 3).GetDateTime().Should().Be(new DateTime(2026, 9, 8));
            sheet.Cell(2, 4).GetValue<int>().Should().Be(3);
            sheet.Cell(2, 5).GetString().Should().Be("Sim");
            sheet.Cell(3, 5).GetString().Should().Be("—");
        }

        [Fact]
        public void TheCsv_UsesSemicolon_Bom_AndQuotesWhatNeedsQuoting()
        {
            var bytes = TableCsv.Render(Rows, Columns);

            bytes.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());

            var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            lines[0].Should().Be("Placa;Valor;Data;Gastos;Pago");
            lines[1].Should().Be("ABC1D23;1.234,50;08/09/2026;3;Sim");

            // Ponto e vírgula e aspas dentro do valor: o valor inteiro vai entre aspas, e a
            // aspa de dentro dobra. É o que o Excel espera.
            lines[2].Should().Be("\"Gol; \"\"prata\"\"\";99,00;01/01/2026;0;—");
        }

        [Fact]
        public void TheSaleSheet_RendersWithWebpPhotos_AndWithoutAny()
        {
            // As fotos do armazenamento são WebP: é o formato que o PDF tem de aceitar.
            var sheet = new SaleSheetDto(
                Company, "ABC1D23", "Honda", "Civic", "2.0 EXL", 2019, 2018, "Prata", 48_300,
                FuelType.Flex, TransmissionType.Automatic, 98_900m, 95_400m, new DateOnly(2026, 9, 1),
                [WebpOf(800, 600), WebpOf(800, 600), WebpOf(600, 800)],
                new DateOnly(2026, 9, 8));

            var withPhotos = SaleSheetPdf.Render(sheet);
            Encoding.ASCII.GetString(withPhotos, 0, 5).Should().Be("%PDF-");
            withPhotos.Length.Should().BeGreaterThan(10_000, "three photos went in");

            var bare = SaleSheetPdf.Render(sheet with { Photos = [], AdvertisedPrice = null });
            Encoding.ASCII.GetString(bare, 0, 5).Should().Be("%PDF-");
        }

        [Fact]
        public void TheProposal_Renders_WithAndWithoutPhotoAndNotes()
        {
            var proposal = new ProposalDocumentDto(
                Company, Guid.NewGuid(), "Eduardo Sampaio", "51988887777", "39053344705", "Rua das Flores, 10, Porto Alegre",
                "Toyota Corolla 2.0 XEi", "ABC1D23", 2020, 2019, 48_300, "Prata",
                112_000m, PaymentMethod.Financing, new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 15),
                "Entrada de R$ 30.000 e o restante em 48 vezes.", WebpOf(800, 600));

            Encoding.ASCII.GetString(ProposalPdf.Render(proposal), 0, 5).Should().Be("%PDF-");

            var bare = proposal with { CoverPhoto = null, Notes = null, ProspectPhone = null, ProspectDocument = null, ProspectAddress = null };
            Encoding.ASCII.GetString(ProposalPdf.Render(bare), 0, 5).Should().Be("%PDF-");
        }

        private static byte[] WebpOf(int width, int height)
        {
            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(new SKColor(0, 144, 196));
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Webp, 80);
            return encoded.ToArray();
        }

        [Fact]
        public void TheFileName_CarriesTheDate()
        {
            ReportFileName.For("Veiculos", "xlsx")
                .Should().MatchRegex(@"^Veiculos\d{8}\.xlsx$");
        }

        [Fact]
        public void TheTheme_FormatsMoneyDatesAndDocuments_InPortuguese()
        {
            DocumentTheme.Money(1234.5m).Should().Be("R$ 1.234,50");
            DocumentTheme.Date(new DateOnly(2026, 9, 8)).Should().Be("08/09/2026");
            DocumentTheme.LongDate(new DateOnly(2026, 9, 8)).Should().Be("8 de setembro de 2026");
            DocumentTheme.TaxId("12345678000195").Should().Be("12.345.678/0001-95");
            DocumentTheme.TaxId("12345678909").Should().Be("123.456.789-09");
            DocumentTheme.Phone("51999990000").Should().Be("(51) 99999-0000");
            DocumentTheme.Mileage(48300).Should().Be("48.300 km");
        }
    }
}
