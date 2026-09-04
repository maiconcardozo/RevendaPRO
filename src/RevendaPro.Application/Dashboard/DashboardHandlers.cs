using MediatR;
using RevendaPro.Application.Dashboard.DTOs;
using RevendaPro.Application.Dashboard.Queries;
using RevendaPro.Application.Vehicles.Handlers;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Application.Dashboard.Handlers
{
    /// <summary>
    /// Everything the dashboard and the sales listing share: the cars, their costs, and the
    /// sales of the period, read in a handful of queries and never one per car.
    ///
    /// Chamava-se <c>Yard</c> até o M14. O nome saiu porque pátio virou uma entidade de
    /// verdade, e duas coisas com o mesmo nome no mesmo arquivo é como se lê errado.
    /// </summary>
    internal sealed class Stock
    {
        private Stock(
            IReadOnlyList<Vehicle> vehicles,
            IReadOnlyDictionary<int, VehicleCost> costs,
            IReadOnlyList<Sale> sales,
            IReadOnlyDictionary<int, VehicleCover> covers,
            IReadOnlyDictionary<int, Yard> yards)
        {
            Vehicles = vehicles;
            Costs = costs;
            Sales = sales;
            Covers = covers;
            Yards = yards;
        }

        public IReadOnlyList<Vehicle> Vehicles { get; }

        public IReadOnlyDictionary<int, VehicleCost> Costs { get; }

        public IReadOnlyList<Sale> Sales { get; }

        public IReadOnlyDictionary<int, VehicleCover> Covers { get; }

        /// <summary>Os pátios do cliente, por Id, para agrupar sem uma consulta por carro.</summary>
        public IReadOnlyDictionary<int, Yard> Yards { get; }

        /// <summary>
        /// Reads the whole yard of the tenant.
        ///
        /// Three queries for the cars and their costs, one for the sales, one for the covers.
        /// A dashboard that asked per car would cost fifty round trips for fifty cars, and it
        /// is opened more often than any other screen.
        /// </summary>
        /// <param name="unitOfWork">Unit of work.</param>
        /// <param name="storage">Where the covers live.</param>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="from">First day of the period of sales.</param>
        /// <param name="to">Last day of the period of sales.</param>
        /// <param name="withCovers">Whether the rankings need a picture.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O estoque.</returns>
        public static async Task<Stock> ReadAsync(
            IUnitOfWork unitOfWork,
            IFileStorage storage,
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            bool withCovers,
            CancellationToken cancellationToken)
        {
            // The whole yard, with no period: capital parked is what is parked today, and a
            // car bought last year still holds money. The period bounds only what was sold.
            var vehicles = await unitOfWork.VehicleRepository
                .ListAsync(idTenant, null, null, null, null, null, null, cancellationToken)
                .ConfigureAwait(false);

            var sales = await unitOfWork.SaleRepository
                .ListByTenantAsync(idTenant, from, to, cancellationToken)
                .ConfigureAwait(false);

            // A sale of the period may belong to a car the listing already holds; the cost
            // is needed for both, so every car that appears anywhere is costed once.
            var ids = vehicles.Select(v => v.Id).Union(sales.Select(s => s.IdVehicle)).ToList();

            var expenses = await unitOfWork.VehicleExpenseRepository
                .ListByVehiclesAsync(ids, cancellationToken)
                .ConfigureAwait(false);

            var byVehicle = expenses.ToLookup(e => e.IdVehicle);

            var costs = vehicles.ToDictionary(
                v => v.Id,
                v => VehicleCost.Of(v, byVehicle[v.Id]));

            var covers = withCovers
                ? await VehicleGalleries.ForAsync(unitOfWork, storage, ids, cancellationToken)
                    .ConfigureAwait(false)
                : new Dictionary<int, VehicleCover>();

            var yards = await VehicleMapper
                .YardsByIdAsync(unitOfWork, idTenant, cancellationToken)
                .ConfigureAwait(false);

            return new Stock(vehicles, costs, sales, covers, yards);
        }

        /// <summary>The car a sale belongs to, when it is still in the listing.</summary>
        /// <param name="sale">The sale.</param>
        /// <returns>The vehicle, or null when it was deleted since.</returns>
        public Vehicle? VehicleOf(Sale sale) => Vehicles.FirstOrDefault(v => v.Id == sale.IdVehicle);

        /// <summary>Turns a sale into what the listing shows.</summary>
        /// <param name="sale">The sale.</param>
        /// <param name="vehicle">Its vehicle.</param>
        /// <returns>The row.</returns>
        public SaleListingDto ToListing(Sale sale, Vehicle vehicle)
        {
            var cost = Costs.TryGetValue(vehicle.Id, out var found)
                ? found
                : VehicleCost.Of(vehicle, []);

            var result = sale.ResultAgainst(cost);

            return new SaleListingDto(
                sale.Code,
                vehicle.Code,
                vehicle.Plate,
                $"{vehicle.Brand} {vehicle.Model}",
                sale.Date,
                sale.BuyerName,
                sale.Channel,
                sale.PartnerStoreName,
                sale.PaymentMethod,
                sale.Amount,
                cost.Total,
                result.NetProfit,
                result.Margin,
                vehicle.DaysInStock(sale.Date, sale.Date),
                sale.TradeInValue is not null);
        }

        /// <summary>
        /// Quanto está parado em cada lugar.
        ///
        /// Os vendidos ficam de fora, pelo mesmo motivo do número do topo: aquele dinheiro
        /// voltou. Um pátio vazio continua na lista, porque "zero carro na Loja do Joãozinho"
        /// é uma resposta, e some da tela seria confundido com um pátio que ninguém cadastrou.
        /// Os carros sem lugar viram uma linha própria, e jamais somem de vista.
        /// </summary>
        /// <param name="inStock">Os carros que ainda estão parados.</param>
        /// <param name="today">Hoje, para a média de dias.</param>
        /// <returns>Uma linha por lugar.</returns>
        public IReadOnlyList<YardStockDto> ByYard(IReadOnlyList<Vehicle> inStock, DateOnly today)
        {
            var rows = new List<YardStockDto>();

            foreach (var yard in Yards.Values.OrderBy(yard => yard.Position).ThenBy(yard => yard.Name))
            {
                rows.Add(Row(
                    yard.Code,
                    yard.Name,
                    yard.Kind,
                    [.. inStock.Where(vehicle => vehicle.IdYard == yard.Id)],
                    today));
            }

            var homeless = inStock.Where(vehicle => vehicle.IdYard is null).ToList();

            if (homeless.Count > 0)
            {
                rows.Add(Row(null, "Sem pátio", null, homeless, today));
            }

            return rows;
        }

        private YardStockDto Row(
            Guid? code,
            string name,
            YardKind? kind,
            IReadOnlyList<Vehicle> vehicles,
            DateOnly today)
        {
            var days = vehicles
                .Select(vehicle => vehicle.DaysInStock(today, soldOn: null))
                .Where(value => value is not null)
                .Select(value => value!.Value)
                .ToList();

            return new YardStockDto(
                code,
                name,
                kind,
                vehicles.Count,
                vehicles.Sum(vehicle => Costs[vehicle.Id].Total),
                days.Count == 0 ? null : (int)Math.Round(days.Average()));
        }

        /// <summary>Turns a car into a row of a ranking.</summary>
        /// <param name="vehicle">The car.</param>
        /// <param name="today">Today, for the days in stock.</param>
        /// <returns>The row.</returns>
        public RankedVehicleDto ToRanked(Vehicle vehicle, DateOnly today)
        {
            var cost = Costs[vehicle.Id];

            return new RankedVehicleDto(
                vehicle.Code,
                vehicle.Plate,
                $"{vehicle.Brand} {vehicle.Model}",
                vehicle.Status,
                cost.Total,
                vehicle.DesiredNetPrice is null ? null : cost.ProfitAt(vehicle.DesiredNetPrice.Value),
                vehicle.DaysInStock(today, soldOn: null),
                Covers.GetValueOrDefault(vehicle.Id)?.ThumbnailUrl);
        }
    }

    /// <summary>Assembles the dashboard (RF-23, RF-24).</summary>
    public class GetDashboardHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage)
        : IRequestHandler<GetDashboardQuery, DashboardDto>
    {
        private const int RankingSize = 5;

        /// <inheritdoc/>
        public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var stock = await Stock
                .ReadAsync(unitOfWork, storage, currentUser.IdTenant, request.From, request.To,
                    withCovers: true, cancellationToken)
                .ConfigureAwait(false);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Sold leaves the parked capital out: that money came back.
            var inStock = stock.Vehicles.Where(v => v.Status != VehicleStatus.Sold).ToList();

            var byStatus = stock.Vehicles
                .GroupBy(v => v.Status)
                .OrderBy(g => g.Key)
                .Select(g => new StatusCountDto(g.Key, g.Count(), g.Sum(v => stock.Costs[v.Id].Total)))
                .ToList();

            var sales = stock.Sales
                .Select(sale => (Sale: sale, Vehicle: stock.VehicleOf(sale)))
                .Where(pair => pair.Vehicle is not null)
                .Select(pair => stock.ToListing(pair.Sale, pair.Vehicle!))
                .ToList();

            var daysToSell = sales.Where(s => s.DaysInStock is not null).Select(s => s.DaysInStock!.Value).ToList();

            return new DashboardDto(
                request.From,
                request.To,
                inStock.Count,
                inStock.Sum(v => stock.Costs[v.Id].Total),
                inStock
                    .Where(v => v.DesiredNetPrice is not null)
                    .Sum(v => stock.Costs[v.Id].ProfitAt(v.DesiredNetPrice!.Value)),
                byStatus,
                stock.ByYard(inStock, today),
                sales.Count,
                sales.Sum(s => s.Amount),
                sales.Sum(s => s.NetProfit),
                daysToSell.Count == 0 ? null : (int)Math.Round(daysToSell.Average()),
                Rank(inStock.OrderByDescending(v => stock.Costs[v.Id].Total), stock, today),
                Rank(
                    inStock
                        .Where(v => v.DesiredNetPrice is not null)
                        .OrderByDescending(v => stock.Costs[v.Id].ProfitAt(v.DesiredNetPrice!.Value)),
                    stock, today),
                Rank(
                    inStock
                        .Where(v => v.PurchaseDate is not null)
                        .OrderByDescending(v => v.DaysInStock(today, soldOn: null)),
                    stock, today),
                [.. sales.Take(RankingSize)]);
        }

        private static IReadOnlyList<RankedVehicleDto> Rank(
            IEnumerable<Vehicle> ordered,
            Stock stock,
            DateOnly today) =>
            [.. ordered.Take(RankingSize).Select(v => stock.ToRanked(v, today))];
    }

    /// <summary>Lists the sales of a period, each with what it left (RF-23).</summary>
    public class ListSalesHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage)
        : IRequestHandler<ListSalesQuery, IReadOnlyList<SaleListingDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<SaleListingDto>> Handle(
            ListSalesQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var stock = await Stock
                .ReadAsync(unitOfWork, storage, currentUser.IdTenant, request.From, request.To,
                    withCovers: false, cancellationToken)
                .ConfigureAwait(false);

            return [.. stock.Sales
                .Select(sale => (Sale: sale, Vehicle: stock.VehicleOf(sale)))
                .Where(pair => pair.Vehicle is not null)
                .Select(pair => stock.ToListing(pair.Sale, pair.Vehicle!))];
        }
    }
}
