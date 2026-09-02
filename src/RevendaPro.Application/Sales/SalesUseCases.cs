using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Sales.DTOs
{
    /// <summary>
    /// What a deal leaves in hand. The same shape for a proposal being weighed and a sale
    /// already closed, because it is the same arithmetic — see <c>DealResult</c>.
    /// </summary>
    /// <param name="Amount">The price the buyer pays.</param>
    /// <param name="PartnerCut">What the partner store keeps, zero on a direct deal.</param>
    /// <param name="Commission">Commission paid to a person.</param>
    /// <param name="Cost">What the vehicle cost.</param>
    /// <param name="Received">Price minus the store's cut.</param>
    /// <param name="GrossProfit">Price minus cost.</param>
    /// <param name="NetProfit">What is actually left.</param>
    /// <param name="Margin">Net profit over the price, as a percentage.</param>
    public sealed record DealResultDto(
        decimal Amount,
        decimal PartnerCut,
        decimal Commission,
        decimal Cost,
        decimal Received,
        decimal GrossProfit,
        decimal NetProfit,
        decimal? Margin);

    /// <summary>A proposal, with how much is left if it is accepted (RF-18, RF-19).</summary>
    /// <param name="Code">Public identifier.</param>
    /// <param name="ProspectName">Who offered.</param>
    /// <param name="ProspectPhone">Their phone, digits only.</param>
    /// <param name="Amount">What they offered.</param>
    /// <param name="Date">When.</param>
    /// <param name="PaymentMethod">How they would pay.</param>
    /// <param name="Channel">Direct, or through a partner store.</param>
    /// <param name="PartnerCutPercent">The store's percentage, when agreed that way.</param>
    /// <param name="PartnerCutAmount">The store's amount, when agreed that way.</param>
    /// <param name="Status">Open, accepted or declined.</param>
    /// <param name="Notes">Anything else.</param>
    /// <param name="Result">What is left if accepted. Calculated, never stored.</param>
    public sealed record ProposalDto(
        Guid Code,
        string ProspectName,
        string? ProspectPhone,
        decimal Amount,
        DateOnly Date,
        PaymentMethod PaymentMethod,
        SaleChannel Channel,
        decimal? PartnerCutPercent,
        decimal? PartnerCutAmount,
        ProposalStatus Status,
        string? Notes,
        DealResultDto Result);

    /// <summary>A closed sale, with what was actually left (RF-20, RF-21).</summary>
    /// <param name="Code">Public identifier.</param>
    /// <param name="ProposalCode">The proposal it closed, if any.</param>
    /// <param name="Date">When.</param>
    /// <param name="Amount">The closed price, car included when there is a trade.</param>
    /// <param name="CashAmount">The money part of the price.</param>
    /// <param name="PaymentMethod">How the buyer paid.</param>
    /// <param name="Channel">Direct, or through a partner store.</param>
    /// <param name="PartnerStoreName">Which store.</param>
    /// <param name="PartnerCutPercent">The store's percentage, when agreed that way.</param>
    /// <param name="PartnerCutAmount">What the store kept, in money.</param>
    /// <param name="Commission">Commission paid to a person.</param>
    /// <param name="CommissionNotes">To whom, and why.</param>
    /// <param name="BuyerName">Who bought.</param>
    /// <param name="BuyerDocument">Their CPF or CNPJ. Personal data.</param>
    /// <param name="BuyerPhone">Their phone. Personal data.</param>
    /// <param name="TradeInValue">What the incoming car was valued at, null without a trade.</param>
    /// <param name="TradeInVehicleCode">The incoming car, once in stock.</param>
    /// <param name="Notes">Anything else.</param>
    /// <param name="DaysInStock">From the purchase to the sale.</param>
    /// <param name="Result">What was left. Calculated, never stored.</param>
    public sealed record SaleDto(
        Guid Code,
        Guid? ProposalCode,
        DateOnly Date,
        decimal Amount,
        decimal CashAmount,
        PaymentMethod PaymentMethod,
        SaleChannel Channel,
        string? PartnerStoreName,
        decimal? PartnerCutPercent,
        decimal? PartnerCutAmount,
        decimal Commission,
        string? CommissionNotes,
        string BuyerName,
        string? BuyerDocument,
        string? BuyerPhone,
        decimal? TradeInValue,
        Guid? TradeInVehicleCode,
        string? Notes,
        int? DaysInStock,
        DealResultDto Result);
}

namespace RevendaPro.Application.Sales.Queries
{
    using MediatR;
    using RevendaPro.Application.Sales.DTOs;

    /// <summary>Proposals of a vehicle, newest first, each with its projected profit.</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    public sealed record ListProposalsQuery(Guid VehicleCode)
        : IRequest<IReadOnlyList<ProposalDto>>;

    /// <summary>
    /// What a deal would leave, before anything is saved: the number the screen shows while
    /// the person is still typing the amount (RF-19).
    ///
    /// Served by the API, and never computed in the browser, so the promise on screen and the
    /// report after the sale come out of the same arithmetic.
    /// </summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="Amount">The price under consideration.</param>
    /// <param name="Channel">Direct, or through a partner store.</param>
    /// <param name="PartnerCutPercent">The store's percentage, when agreed that way.</param>
    /// <param name="PartnerCutAmount">The store's amount, when agreed that way.</param>
    /// <param name="Commission">Commission to a person, if any.</param>
    public sealed record PreviewDealQuery(
        Guid VehicleCode,
        decimal Amount,
        SaleChannel Channel,
        decimal? PartnerCutPercent,
        decimal? PartnerCutAmount,
        decimal Commission) : IRequest<DealResultDto>;

    /// <summary>The sale of a vehicle, or null while it is on the lot.</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    public sealed record GetSaleQuery(Guid VehicleCode) : IRequest<SaleDto?>;
}

namespace RevendaPro.Application.Sales.Commands
{
    using MediatR;
    using RevendaPro.Application.Sales.DTOs;

    /// <summary>Records what somebody offered (RF-18).</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="ProspectName">Who offered.</param>
    /// <param name="ProspectPhone">Their phone.</param>
    /// <param name="Amount">What they offered.</param>
    /// <param name="Date">When.</param>
    /// <param name="PaymentMethod">How they would pay.</param>
    /// <param name="Channel">Direct, or through a partner store.</param>
    /// <param name="PartnerCutPercent">The store's percentage, when agreed that way.</param>
    /// <param name="PartnerCutAmount">The store's amount, when agreed that way.</param>
    /// <param name="Notes">Anything else.</param>
    public sealed record RegisterProposalCommand(
        Guid VehicleCode,
        string ProspectName,
        string? ProspectPhone,
        decimal Amount,
        DateOnly Date,
        PaymentMethod PaymentMethod,
        SaleChannel Channel,
        decimal? PartnerCutPercent,
        decimal? PartnerCutAmount,
        string? Notes) : IRequest<ProposalDto>;

    /// <summary>Declines a proposal. It stays on record.</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="ProposalCode">Public identifier of the proposal.</param>
    public sealed record DeclineProposalCommand(Guid VehicleCode, Guid ProposalCode) : IRequest;

    /// <summary>Soft deletes a proposal that was recorded by mistake.</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="ProposalCode">Public identifier of the proposal.</param>
    public sealed record DeleteProposalCommand(Guid VehicleCode, Guid ProposalCode) : IRequest;

    /// <summary>The car that came in as part of the payment.</summary>
    /// <param name="Plate">Plate.</param>
    /// <param name="Chassis">Chassis.</param>
    /// <param name="Brand">Brand.</param>
    /// <param name="Model">Model.</param>
    /// <param name="ModelYear">Model year.</param>
    /// <param name="ManufactureYear">Manufacture year.</param>
    /// <param name="Mileage">Kilometres on the dashboard.</param>
    public sealed record TradeInVehicleInput(
        string Plate,
        string Chassis,
        string Brand,
        string Model,
        short ModelYear,
        short ManufactureYear,
        int Mileage);

    /// <summary>
    /// Registers the sale (RF-20). The only way a vehicle reaches "sold".
    ///
    /// When the buyer paid partly with a car, <see cref="TradeIn"/> describes it and the
    /// handler registers it in stock, valued at <see cref="TradeInValue"/>.
    /// </summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="ProposalCode">The proposal being accepted, if any.</param>
    /// <param name="Date">When.</param>
    /// <param name="Amount">The closed price, car included when there is a trade.</param>
    /// <param name="PaymentMethod">How the buyer paid.</param>
    /// <param name="Channel">Direct, or through a partner store.</param>
    /// <param name="PartnerStoreName">Which store.</param>
    /// <param name="PartnerCutPercent">The store's percentage, when agreed that way.</param>
    /// <param name="PartnerCutAmount">The store's amount, when agreed that way.</param>
    /// <param name="Commission">Commission to a person, zero when none.</param>
    /// <param name="CommissionNotes">To whom, and why.</param>
    /// <param name="BuyerName">Who bought.</param>
    /// <param name="BuyerDocument">Their CPF or CNPJ.</param>
    /// <param name="BuyerPhone">Their phone.</param>
    /// <param name="TradeInValue">What the incoming car was valued at.</param>
    /// <param name="TradeIn">The incoming car, when there is one.</param>
    /// <param name="Notes">Anything else.</param>
    public sealed record RegisterSaleCommand(
        Guid VehicleCode,
        Guid? ProposalCode,
        DateOnly Date,
        decimal Amount,
        PaymentMethod PaymentMethod,
        SaleChannel Channel,
        string? PartnerStoreName,
        decimal? PartnerCutPercent,
        decimal? PartnerCutAmount,
        decimal Commission,
        string? CommissionNotes,
        string BuyerName,
        string? BuyerDocument,
        string? BuyerPhone,
        decimal? TradeInValue,
        TradeInVehicleInput? TradeIn,
        string? Notes) : IRequest<SaleDto>;

    /// <summary>
    /// Undoes a sale: the record is soft deleted, the car goes back to the lot, and the
    /// proposal it closed, if any, reopens. The car that came in as a trade stays in stock —
    /// it is really there, and whoever cancelled decides what to do with it.
    /// </summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="Reason">Why, for the history.</param>
    public sealed record CancelSaleCommand(Guid VehicleCode, string? Reason) : IRequest;
}
