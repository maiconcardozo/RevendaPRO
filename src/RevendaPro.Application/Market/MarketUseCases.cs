using MediatR;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Market.Queries
{
    /// <summary>
    /// Everything the Mercado screen asks at once (RF-14).
    ///
    /// One question, one round trip: the screen answers five things about the same set of
    /// cars, and asking five times would read the same yard five times.
    /// </summary>
    public sealed record GetMarketOverviewQuery : IRequest<DTOs.MarketOverviewDto>;
}

namespace RevendaPro.Application.Market.DTOs
{
    /// <summary>
    /// The dealership against the reference table.
    /// </summary>
    /// <param name="ReferenceMonth">The table these numbers are compared against.</param>
    /// <param name="Purchases">What was paid against the table of each purchase month.</param>
    /// <param name="Sales">What was closed against the table of each sale month.</param>
    /// <param name="Asking">What is being asked against the table of now.</param>
    /// <param name="LostThisMonth">
    /// How much reference the whole yard lost from last month to this one. It is the cost of
    /// standing still, and it is the number nobody could name before this milestone.
    /// </param>
    /// <param name="LostSincePurchase">How much reference the yard lost since each car came in.</param>
    /// <param name="Yard">The cars still on the lot.</param>
    /// <param name="Sold">The cars already sold, newest first.</param>
    /// <param name="Proposals">The offers still on the table.</param>
    /// <param name="WithoutReference">
    /// How many cars have no comparison at all, because they carry no model code or because
    /// their month was never fetched. The screen says this out loud: a average taken over half
    /// the yard, presented as the yard, is a lie.
    /// </param>
    public sealed record MarketOverviewDto(
        DateOnly ReferenceMonth,
        MarketAverageDto Purchases,
        MarketAverageDto Sales,
        MarketAverageDto Asking,
        decimal LostThisMonth,
        decimal LostSincePurchase,
        IReadOnlyList<MarketLineDto> Yard,
        IReadOnlyList<MarketLineDto> Sold,
        IReadOnlyList<MarketProposalDto> Proposals,
        int WithoutReference);

    /// <summary>
    /// One answer of the screen, averaged over the cars that have a comparison.
    /// </summary>
    /// <param name="Cars">How many cars entered this average.</param>
    /// <param name="Amount">What was paid, closed or asked, added up.</param>
    /// <param name="Reference">What the tables of those months said, added up.</param>
    /// <param name="Difference">The distance in reais.</param>
    /// <param name="Percent">The same distance as a percentage of the table.</param>
    public sealed record MarketAverageDto(
        int Cars,
        decimal Amount,
        decimal Reference,
        decimal Difference,
        decimal? Percent);

    /// <summary>
    /// One car on the screen.
    /// </summary>
    /// <param name="Code">Public identifier, so the screen links to the sheet.</param>
    /// <param name="Plate">Plate.</param>
    /// <param name="Brand">Brand.</param>
    /// <param name="Model">Model.</param>
    /// <param name="Version">Version or trim.</param>
    /// <param name="ModelYear">Model year.</param>
    /// <param name="Status">Where it is in the pipeline.</param>
    /// <param name="DaysInStock">How long it has been on the lot, or was.</param>
    /// <param name="Amount">The amount this line is about: what was paid, closed or asked.</param>
    /// <param name="Reference">The table of that amount's month, or null with no comparison.</param>
    /// <param name="Difference">The distance in reais, or null with no comparison.</param>
    /// <param name="Percent">The distance as a percentage, or null with no comparison.</param>
    /// <param name="PurchaseDifference">What was paid against the table of the purchase month.</param>
    /// <param name="PurchasePercent">The same, as a percentage.</param>
    /// <param name="LostSincePurchase">How much reference it lost since it came in.</param>
    public sealed record MarketLineDto(
        Guid Code,
        string Plate,
        string Brand,
        string Model,
        string? Version,
        short ModelYear,
        VehicleStatus Status,
        int? DaysInStock,
        decimal Amount,
        decimal? Reference,
        decimal? Difference,
        decimal? Percent,
        decimal? PurchaseDifference,
        decimal? PurchasePercent,
        decimal? LostSincePurchase);

    /// <summary>
    /// One offer on the table, against the table of this month.
    /// </summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="Plate">Plate.</param>
    /// <param name="Brand">Brand.</param>
    /// <param name="Model">Model.</param>
    /// <param name="ProspectName">Who offered.</param>
    /// <param name="Amount">What was offered.</param>
    /// <param name="Date">When.</param>
    /// <param name="Reference">The table of this month.</param>
    /// <param name="Difference">The distance in reais.</param>
    /// <param name="Percent">The distance as a percentage.</param>
    public sealed record MarketProposalDto(
        Guid VehicleCode,
        string Plate,
        string Brand,
        string Model,
        string ProspectName,
        decimal Amount,
        DateOnly Date,
        decimal? Reference,
        decimal? Difference,
        decimal? Percent);
}
