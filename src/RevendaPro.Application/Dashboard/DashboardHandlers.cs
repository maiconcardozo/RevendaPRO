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
    /// </summary>
    internal sealed class Yard
    {
        private Yard(
            IReadOnlyList<Vehicle> vehicles,
            IReadOnlyDictionary<int, VehicleCost> costs,
            IReadOnlyList<Sale> sales,
            IReadOnlyDictionary<int, VehicleCover> covers)
        {
            Vehicles = vehicles;
            Costs = costs;
            Sales = sales;
            Covers = covers;
        }

        public IReadOnlyList<Vehicle> Vehicles { get; }

        public IReadOnlyDictionary<int, VehicleCost> Costs { get; }

        public IReadOnlyList<Sale> Sales { get; }

        public IReadOnlyDictionary<int, VehicleCover> Covers { get; }

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
        /// <returns>The yard.</returns>
        public static async Task<Yard> ReadAsync(
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
                .ListAsync(idTenant, null, null, null, null, null, cancellationToken)
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

            return new Yard(vehicles, costs, sales, covers);
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
                vehicle.DaysInStock(sale.Date),
                sale.TradeInValue is not null);
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
                vehicle.DaysInStock(today),
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

            var yard = await Yard
                .ReadAsync(unitOfWork, storage, currentUser.IdTenant, request.From, request.To,
                    withCovers: true, cancellationToken)
                .ConfigureAwait(false);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Sold leaves the parked capital out: that money came back.
            var inStock = yard.Vehicles.Where(v => v.Status != VehicleStatus.Sold).ToList();

            var byStatus = yard.Vehicles
                .GroupBy(v => v.Status)
                .OrderBy(g => g.Key)
                .Select(g => new StatusCountDto(g.Key, g.Count(), g.Sum(v => yard.Costs[v.Id].Total)))
                .ToList();

            var sales = yard.Sales
                .Select(sale => (Sale: sale, Vehicle: yard.VehicleOf(sale)))
                .Where(pair => pair.Vehicle is not null)
                .Select(pair => yard.ToListing(pair.Sale, pair.Vehicle!))
                .ToList();

            var daysToSell = sales.Where(s => s.DaysInStock is not null).Select(s => s.DaysInStock!.Value).ToList();

            return new DashboardDto(
                request.From,
                request.To,
                inStock.Count,
                inStock.Sum(v => yard.Costs[v.Id].Total),
                inStock
                    .Where(v => v.DesiredNetPrice is not null)
                    .Sum(v => yard.Costs[v.Id].ProfitAt(v.DesiredNetPrice!.Value)),
                byStatus,
                sales.Count,
                sales.Sum(s => s.Amount),
                sales.Sum(s => s.NetProfit),
                daysToSell.Count == 0 ? null : (int)Math.Round(daysToSell.Average()),
                Rank(inStock.OrderByDescending(v => yard.Costs[v.Id].Total), yard, today),
                Rank(
                    inStock
                        .Where(v => v.DesiredNetPrice is not null)
                        .OrderByDescending(v => yard.Costs[v.Id].ProfitAt(v.DesiredNetPrice!.Value)),
                    yard, today),
                Rank(
                    inStock
                        .Where(v => v.PurchaseDate is not null)
                        .OrderByDescending(v => v.DaysInStock(today)),
                    yard, today),
                [.. sales.Take(RankingSize)]);
        }

        private static IReadOnlyList<RankedVehicleDto> Rank(
            IEnumerable<Vehicle> ordered,
            Yard yard,
            DateOnly today) =>
            [.. ordered.Take(RankingSize).Select(v => yard.ToRanked(v, today))];
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

            var yard = await Yard
                .ReadAsync(unitOfWork, storage, currentUser.IdTenant, request.From, request.To,
                    withCovers: false, cancellationToken)
                .ConfigureAwait(false);

            return [.. yard.Sales
                .Select(sale => (Sale: sale, Vehicle: yard.VehicleOf(sale)))
                .Where(pair => pair.Vehicle is not null)
                .Select(pair => yard.ToListing(pair.Sale, pair.Vehicle!))];
        }
    }
}
