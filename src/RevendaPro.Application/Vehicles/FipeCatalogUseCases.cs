using MediatR;

namespace RevendaPro.Application.Vehicles.Queries
{
    /// <summary>
    /// Every brand the reference table prices, for the car nobody has a code for yet.
    /// </summary>
    public sealed record ListFipeBrandsQuery
        : IRequest<IReadOnlyList<DTOs.FipeOptionDto>>;

    /// <summary>Every model of one brand.</summary>
    /// <param name="BrandCode">Code of the brand, as the table names it.</param>
    public sealed record ListFipeModelsQuery(string BrandCode)
        : IRequest<IReadOnlyList<DTOs.FipeOptionDto>>;

    /// <summary>Every year and fuel combination of one model.</summary>
    /// <param name="BrandCode">Code of the brand.</param>
    /// <param name="ModelCode">Code of the model.</param>
    public sealed record ListFipeModelYearsQuery(string BrandCode, string ModelCode)
        : IRequest<IReadOnlyList<DTOs.FipeOptionDto>>;
}

namespace RevendaPro.Application.Vehicles.Commands
{
    /// <summary>
    /// Points the vehicle at a model chosen from the table, and reads its value.
    ///
    /// It is the door for the car with no code: three choices — brand, model, year — and from
    /// then on every lookup is a direct call. See ADR-0005.
    /// </summary>
    /// <param name="Code">Public identifier of the vehicle.</param>
    /// <param name="BrandCode">Code of the brand that was chosen.</param>
    /// <param name="ModelCode">Code of the model that was chosen.</param>
    /// <param name="YearFuel">Year and fuel that was chosen.</param>
    public sealed record SetVehicleFipeModelCommand(
        Guid Code,
        string BrandCode,
        string ModelCode,
        string YearFuel) : IRequest<DTOs.FipeReferenceDto>;
}

namespace RevendaPro.Application.Vehicles.DTOs
{
    /// <summary>
    /// One choice of the chooser: what the source expects back, and what a person reads.
    /// </summary>
    /// <param name="Code">What goes back to the source (<c>23</c>, <c>5635</c>, <c>2014-5</c>).</param>
    /// <param name="Name">What appears on the screen (<c>GM - Chevrolet</c>, <c>2014 Flex</c>).</param>
    public sealed record FipeOptionDto(string Code, string Name);
}
