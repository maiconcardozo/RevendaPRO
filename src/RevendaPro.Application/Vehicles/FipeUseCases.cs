using MediatR;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Vehicles.Commands
{
    /// <summary>
    /// Reads the reference table for one vehicle and writes the answer on its sheet (RF-14).
    ///
    /// Asked by a person, and never by itself: the monthly routine of the yard is another
    /// door. See ADR-0005.
    /// </summary>
    /// <param name="Code">Public identifier of the vehicle.</param>
    public sealed record RefreshVehicleFipeCommand(Guid Code)
        : IRequest<DTOs.FipeReferenceDto>;
}

namespace RevendaPro.Application.Vehicles.DTOs
{
    /// <summary>
    /// What the table answered, as the sheet reads it back.
    /// </summary>
    /// <param name="Value">The reference value.</param>
    /// <param name="ReferenceMonth">Which month it belongs to — always the first day.</param>
    /// <param name="FipeCode">Code of the model in the table.</param>
    /// <param name="YearFuel">Year and fuel of the priced row.</param>
    /// <param name="Source">Where the value came from.</param>
    /// <param name="Brand">Brand as the table writes it.</param>
    /// <param name="Model">Model as the table writes it, version included.</param>
    /// <param name="PreviousValue">
    /// What the sheet said before, or null when it said nothing. It is what lets the screen
    /// say how much the reference moved instead of only showing the new number.
    /// </param>
    public sealed record FipeReferenceDto(
        decimal Value,
        DateOnly ReferenceMonth,
        string FipeCode,
        string YearFuel,
        FipeSource Source,
        string Brand,
        string Model,
        decimal? PreviousValue);
}
