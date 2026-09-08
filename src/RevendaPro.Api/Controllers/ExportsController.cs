using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Reports;
using RevendaPro.Application.Customers.DTOs;
using RevendaPro.Application.Customers.Queries;
using RevendaPro.Application.Dashboard.DTOs;
using RevendaPro.Application.Dashboard.Queries;
using RevendaPro.Application.Reports.DTOs;
using RevendaPro.Application.Reports.Queries;
using RevendaPro.Application.Suppliers.DTOs;
using RevendaPro.Application.Suppliers.Queries;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Queries;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// As planilhas: o que cada tela mostra, com os filtros da tela, em Excel ou CSV (M19).
    ///
    /// Cada ação é guardada pela tela que mostra a mesma lista: quem vê a listagem pode levá-la.
    /// A consulta é a mesma da tela; só o formato muda, e ele é decidido aqui — o handler jamais
    /// sabe o que é uma célula (ADR-0007). Sem paginação: planilha é para levar tudo.
    /// </summary>
    [ApiController]
    [Route("api/exports")]
    [Authorize]
    public sealed class ExportsController(IMediator mediator) : ControllerBase
    {
        /// <summary>Os veículos, com os mesmos filtros da listagem.</summary>
        /// <param name="format">xlsx ou csv. Excel é o padrão.</param>
        /// <param name="search">Busca por placa, marca ou modelo.</param>
        /// <param name="status">Situação na esteira.</param>
        /// <param name="origin">De onde veio.</param>
        /// <param name="from">Comprado a partir de.</param>
        /// <param name="to">Comprado até.</param>
        /// <param name="yard">O pátio, pelo código público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A planilha, como anexo.</returns>
        [HttpGet("vehicles")]
        [RequireScreen("vehicles")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Vehicles(
            [FromQuery] string? format,
            [FromQuery] string? search,
            [FromQuery] VehicleStatus? status,
            [FromQuery] VehicleOrigin? origin,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] Guid? yard,
            CancellationToken cancellationToken)
        {
            var vehicles = await mediator.Send(
                new ListVehiclesQuery(search, status, origin, from, to, yard), cancellationToken);

            var columns = new ReportColumn<VehicleDto>[]
            {
                new("Placa", v => v.Plate),
                new("Marca", v => v.Brand),
                new("Modelo", v => v.Model),
                new("Versão", v => v.Version),
                new("Ano", v => $"{v.ModelYear}/{v.ManufactureYear}"),
                new("Km", v => v.Mileage, AlignRight: true),
                new("Cor", v => v.Color),
                new("Situação", v => Labels.Of(v.Status)),
                new("Pátio", v => v.Yard?.Name),
                new("Origem", v => Labels.Of(v.Origin)),
                new("Compra", v => v.PurchaseDate),
                new("Preço de compra", v => v.PurchasePrice, AlignRight: true),
                new("Gastos pagos", v => v.Cost.PaidExpenses, AlignRight: true),
                new("Gastos previstos", v => v.Cost.PlannedExpenses, AlignRight: true),
                new("Custo total", v => v.Cost.Total, AlignRight: true),
                new("FIPE", v => v.FipeValue, AlignRight: true),
                new("Quero receber", v => v.DesiredNetPrice, AlignRight: true),
                new("Preço anunciado", v => v.AdvertisedPrice, AlignRight: true),
                new("Sobra prevista", v => v.Cost.ProfitAtDesired, AlignRight: true),
                new("Dias parado", v => v.DaysInStock, AlignRight: true),
            };

            return Spreadsheet(format, "Veiculos", "Veículos", vehicles, columns);
        }

        /// <summary>Todos os gastos do período, com o carro, o tipo e o fornecedor.</summary>
        /// <param name="format">xlsx ou csv.</param>
        /// <param name="from">Primeiro dia, inclusive.</param>
        /// <param name="to">Último dia, inclusive.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A planilha, como anexo.</returns>
        [HttpGet("expenses")]
        [RequireScreen("vehicles")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Expenses(
            [FromQuery] string? format,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var lines = await mediator.Send(new ListExpenseLinesQuery(from, to), cancellationToken);

            var columns = new ReportColumn<ExpenseLineDto>[]
            {
                new("Data", e => e.Date),
                new("Placa", e => e.Plate),
                new("Veículo", e => e.VehicleName),
                new("Gasto", e => e.Description),
                new("Tipo", e => e.ExpenseTypeName),
                new("Fornecedor", e => e.SupplierName),
                new("Valor", e => e.Amount, AlignRight: true),
                new("Situação", e => e.IsPaid ? "Pago" : "Previsto"),
            };

            return Spreadsheet(format, "Gastos", "Gastos", lines, columns);
        }

        /// <summary>As vendas do período, cada uma com o que deixou.</summary>
        /// <param name="format">xlsx ou csv.</param>
        /// <param name="from">Primeiro dia, inclusive.</param>
        /// <param name="to">Último dia, inclusive.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A planilha, como anexo.</returns>
        [HttpGet("sales")]
        [RequireScreen("sales")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Sales(
            [FromQuery] string? format,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var sales = await mediator.Send(new ListSalesQuery(from, to), cancellationToken);

            var columns = new ReportColumn<SaleListingDto>[]
            {
                new("Data", s => s.Date),
                new("Placa", s => s.Plate),
                new("Veículo", s => s.Name),
                new("Comprador", s => s.BuyerName),
                new("Canal", s => Labels.Of(s.Channel)),
                new("Loja parceira", s => s.PartnerStoreName),
                new("Pagamento", s => Labels.Of(s.PaymentMethod)),
                new("Valor", s => s.Amount, AlignRight: true),
                new("Custo", s => s.Cost, AlignRight: true),
                new("Sobra", s => s.NetProfit, AlignRight: true),
                new("Margem %", s => s.Margin, AlignRight: true),
                new("Dias parado", s => s.DaysInStock, AlignRight: true),
                new("Troca", s => s.HadTradeIn ? "Com troca" : "Sem troca"),
            };

            return Spreadsheet(format, "Vendas", "Vendas", sales, columns);
        }

        /// <summary>Quanto foi para cada fornecedor no período.</summary>
        /// <param name="format">xlsx ou csv.</param>
        /// <param name="from">Primeiro dia, inclusive.</param>
        /// <param name="to">Último dia, inclusive.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A planilha, como anexo.</returns>
        [HttpGet("suppliers")]
        [RequireScreen("suppliers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Suppliers(
            [FromQuery] string? format,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var spending = await mediator.Send(new ListSupplierSpendingQuery(from, to), cancellationToken);

            var columns = new ReportColumn<SupplierSpendDto>[]
            {
                new("Fornecedor", s => s.Name),
                new("Ramo", s => s.SegmentName),
                new("Pago", s => s.PaidTotal, AlignRight: true),
                new("Previsto", s => s.PlannedTotal, AlignRight: true),
                new("Gastos", s => s.ExpenseCount, AlignRight: true),
                new("Último gasto", s => s.LastDate),
            };

            return Spreadsheet(format, "Fornecedores", "Fornecedores", spending, columns);
        }

        /// <summary>
        /// Os clientes da revenda, com o que cada um já fez (M21). Telefone e documento saem
        /// formatados, porque a planilha é para ler e ligar, e não para importar.
        /// </summary>
        /// <param name="format">"xlsx" ou "csv". Excel quando falta.</param>
        /// <param name="search">O mesmo filtro da tela.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A planilha, como anexo.</returns>
        [HttpGet("customers")]
        [RequireScreen("customers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Customers(
            [FromQuery] string? format,
            [FromQuery] string? search,
            CancellationToken cancellationToken)
        {
            var customers = await mediator.Send(new ListCustomersQuery(search), cancellationToken);

            var columns = new ReportColumn<CustomerDto>[]
            {
                new("Cliente", c => c.Name),
                new("Telefone", c => DocumentTheme.Phone(c.Phone)),
                new("CPF/CNPJ", c => DocumentTheme.TaxId(c.Document)),
                new("E-mail", c => c.Email),
                new("Endereço", c => c.Address),
                new("Propostas", c => c.ProposalCount, AlignRight: true),
                new("Compras", c => c.SaleCount, AlignRight: true),
                new("Total comprado", c => c.BoughtTotal, AlignRight: true),
                new("Último contato", c => c.LastDate),
            };

            return Spreadsheet(format, "Clientes", "Clientes", customers, columns);
        }

        /// <summary>
        /// A planilha no formato pedido. Excel é o padrão; qualquer coisa que comece com "csv"
        /// vira CSV. Um formato desconhecido cai no Excel em vez de responder erro: a pessoa
        /// pediu a planilha, e a planilha é o que ela leva.
        /// </summary>
        private FileContentResult Spreadsheet<T>(
            string? format,
            string fileName,
            string sheetName,
            IReadOnlyList<T> rows,
            IReadOnlyList<ReportColumn<T>> columns)
        {
            var csv = format?.StartsWith("csv", StringComparison.OrdinalIgnoreCase) == true;

            return csv
                ? File(TableCsv.Render(rows, columns), TableCsv.ContentType, ReportFileName.For(fileName, "csv"))
                : File(TableExcel.Render(sheetName, rows, columns), TableExcel.ContentType, ReportFileName.For(fileName, "xlsx"));
        }
    }
}
