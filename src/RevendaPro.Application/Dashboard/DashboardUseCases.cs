using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Dashboard.DTOs
{
    /// <summary>One car on a ranking of the dashboard (RF-24).</summary>
    /// <param name="Code">Public identifier.</param>
    /// <param name="Plate">Plate.</param>
    /// <param name="Name">Brand and model, ready to print.</param>
    /// <param name="Status">Where it is in the pipeline.</param>
    /// <param name="Cost">What it cost so far.</param>
    /// <param name="ProjectedProfit">Desired price minus cost, when there is a desired price.</param>
    /// <param name="DaysInStock">How long it has been there.</param>
    /// <param name="CoverThumbnailUrl">Signed address of the cover, smallest rendition.</param>
    public sealed record RankedVehicleDto(
        Guid Code,
        string Plate,
        string Name,
        VehicleStatus Status,
        decimal Cost,
        decimal? ProjectedProfit,
        int? DaysInStock,
        string? CoverThumbnailUrl);

    /// <summary>How many cars sit in one status.</summary>
    /// <param name="Status">The status.</param>
    /// <param name="Count">How many.</param>
    /// <param name="Cost">What they cost together.</param>
    public sealed record StatusCountDto(VehicleStatus Status, int Count, decimal Cost);

    /// <summary>One sale, as the sales listing and the dashboard show it (RF-23).</summary>
    /// <param name="Code">Public identifier of the sale.</param>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="Plate">Plate.</param>
    /// <param name="Name">Brand and model, ready to print.</param>
    /// <param name="Date">When it was sold.</param>
    /// <param name="BuyerName">Who bought.</param>
    /// <param name="Channel">Direct, or through a partner store.</param>
    /// <param name="PartnerStoreName">Which store, when through one.</param>
    /// <param name="PaymentMethod">How the buyer paid.</param>
    /// <param name="Amount">The closed price.</param>
    /// <param name="Cost">What the car had cost.</param>
    /// <param name="NetProfit">What was left.</param>
    /// <param name="Margin">Net profit over the price.</param>
    /// <param name="DaysInStock">From the purchase to the sale.</param>
    /// <param name="HadTradeIn">Whether a car came in as part of the payment.</param>
    public sealed record SaleListingDto(
        Guid Code,
        Guid VehicleCode,
        string Plate,
        string Name,
        DateOnly Date,
        string BuyerName,
        SaleChannel Channel,
        string? PartnerStoreName,
        PaymentMethod PaymentMethod,
        decimal Amount,
        decimal Cost,
        decimal NetProfit,
        decimal? Margin,
        int? DaysInStock,
        bool HadTradeIn);

    /// <summary>
    /// The dashboard (RF-23, RF-24). Every number is summed at the moment of the request:
    /// the money parked in the yard is the cost of every unsold car, and the realized profit
    /// is the net profit of every sale in the period.
    /// </summary>
    /// <param name="From">First day of the period, for what is realized.</param>
    /// <param name="To">Last day of the period.</param>
    /// <param name="InStock">Cars without a sale.</param>
    /// <param name="Invested">What those cars cost together (RF-23).</param>
    /// <param name="ProjectedProfit">Sum of desired price minus cost, over cars with a desired price.</param>
    /// <param name="ByStatus">How many cars in each status, and what they cost.</param>
    /// <param name="SalesInPeriod">How many sales in the period.</param>
    /// <param name="SoldInPeriod">The closed prices together.</param>
    /// <param name="RealizedProfit">The net profits together (RF-23).</param>
    /// <param name="AverageDaysToSell">Mean days from purchase to sale, over the period.</param>
    /// <param name="BiggestInvestments">The cars with the most money in them (RF-24).</param>
    /// <param name="BiggestMargins">The cars promising the most (RF-24).</param>
    /// <param name="LongestInStock">The cars that have sat the longest (RF-24).</param>
    /// <param name="RecentSales">The last sales of the period.</param>
    public sealed record DashboardDto(
        DateOnly? From,
        DateOnly? To,
        int InStock,
        decimal Invested,
        decimal ProjectedProfit,
        IReadOnlyList<StatusCountDto> ByStatus,
        int SalesInPeriod,
        decimal SoldInPeriod,
        decimal RealizedProfit,
        int? AverageDaysToSell,
        IReadOnlyList<RankedVehicleDto> BiggestInvestments,
        IReadOnlyList<RankedVehicleDto> BiggestMargins,
        IReadOnlyList<RankedVehicleDto> LongestInStock,
        IReadOnlyList<SaleListingDto> RecentSales);
}

namespace RevendaPro.Application.Dashboard.Queries
{
    using MediatR;
    using RevendaPro.Application.Dashboard.DTOs;

    /// <summary>The dashboard of the tenant. The period bounds only what is realized.</summary>
    /// <param name="From">First day, inclusive. Null for no lower bound.</param>
    /// <param name="To">Last day, inclusive. Null for no upper bound.</param>
    public sealed record GetDashboardQuery(DateOnly? From, DateOnly? To) : IRequest<DashboardDto>;

    /// <summary>The sales of the tenant in a period, newest first (RF-23).</summary>
    /// <param name="From">First day, inclusive. Null for no lower bound.</param>
    /// <param name="To">Last day, inclusive. Null for no upper bound.</param>
    public sealed record ListSalesQuery(DateOnly? From, DateOnly? To)
        : IRequest<IReadOnlyList<SaleListingDto>>;
}
