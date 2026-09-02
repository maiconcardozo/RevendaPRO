namespace RevendaPro.Application.Vehicles.DTOs
{
    /// <summary>One line of what was spent on a vehicle (RF-08).</summary>
    /// <param name="Code">Public identifier.</param>
    /// <param name="ExpenseTypeCode">Which kind of expense.</param>
    /// <param name="ExpenseTypeName">Name of the kind, for the list.</param>
    /// <param name="Description">What it was.</param>
    /// <param name="Amount">How much.</param>
    /// <param name="Date">When.</param>
    /// <param name="Notes">Free text: where it was bought, warranty, invoice number.</param>
    /// <param name="IsPaid">False means planned, and out of the real cost.</param>
    public sealed record VehicleExpenseDto(
        Guid Code,
        Guid ExpenseTypeCode,
        string ExpenseTypeName,
        string Description,
        decimal Amount,
        DateOnly Date,
        string? Notes,
        bool IsPaid);

    /// <summary>A kind of expense, maintained by the dealership (RF-09).</summary>
    /// <param name="Code">Public identifier.</param>
    /// <param name="Name">Name shown to the user.</param>
    /// <param name="Keywords">Words that point an expense here.</param>
    /// <param name="Position">Position in the list.</param>
    /// <param name="ExpenseCount">How many expenses use it. Deleting one in use is refused.</param>
    public sealed record ExpenseTypeDto(
        Guid Code,
        string Name,
        string? Keywords,
        int Position,
        int ExpenseCount);

    /// <summary>
    /// What the screen offers while somebody types a description.
    ///
    /// The suggestion comes from what this dealership already wrote, so picking a known
    /// description brings its type along and the entry costs two fields.
    /// </summary>
    /// <param name="Description">The description already used.</param>
    /// <param name="ExpenseTypeCode">The kind it was filed under.</param>
    /// <param name="ExpenseTypeName">Name of that kind.</param>
    public sealed record ExpenseSuggestionDto(
        string Description,
        Guid ExpenseTypeCode,
        string ExpenseTypeName);
}

namespace RevendaPro.Application.Vehicles.Queries
{
    using MediatR;
    using RevendaPro.Application.Vehicles.DTOs;

    /// <summary>Lists what was spent on a vehicle (RF-08).</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    public sealed record ListVehicleExpensesQuery(Guid VehicleCode)
        : IRequest<IReadOnlyList<VehicleExpenseDto>>;

    /// <summary>Lists the kinds of expense of the tenant (RF-09).</summary>
    public sealed record ListExpenseTypesQuery : IRequest<IReadOnlyList<ExpenseTypeDto>>;

    /// <summary>
    /// Suggests a description from what the dealership already used, and the kind that goes
    /// with it. Falls back to matching the keywords of each kind when the words are new.
    /// </summary>
    /// <param name="Term">What the user has typed so far.</param>
    public sealed record SuggestExpenseQuery(string Term) : IRequest<IReadOnlyList<ExpenseSuggestionDto>>;
}

namespace RevendaPro.Application.Vehicles.Commands
{
    using MediatR;
    using RevendaPro.Application.Vehicles.DTOs;

    /// <summary>Records or changes what was spent on a vehicle (RF-08).</summary>
    /// <param name="Code">Null creates; filled updates.</param>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="ExpenseTypeCode">Which kind of expense.</param>
    /// <param name="Description">What it was.</param>
    /// <param name="Amount">How much.</param>
    /// <param name="Date">When.</param>
    /// <param name="Notes">Free text: where it was bought, warranty, invoice number.</param>
    /// <param name="IsPaid">False records it as planned (RF-11).</param>
    public sealed record SaveVehicleExpenseCommand(
        Guid? Code,
        Guid VehicleCode,
        Guid ExpenseTypeCode,
        string Description,
        decimal Amount,
        DateOnly Date,
        string? Notes,
        bool IsPaid) : IRequest<VehicleExpenseDto>;

    /// <summary>Turns a planned expense into a paid one.</summary>
    /// <param name="Code">Public identifier of the expense.</param>
    public sealed record ConfirmExpensePaymentCommand(Guid Code) : IRequest;

    /// <summary>Soft deletes an expense.</summary>
    /// <param name="Code">Public identifier of the expense.</param>
    public sealed record DeleteVehicleExpenseCommand(Guid Code) : IRequest;

    /// <summary>Creates or renames a kind of expense (RF-09).</summary>
    /// <param name="Code">Null creates; filled updates.</param>
    /// <param name="Name">Name shown to the user.</param>
    /// <param name="Keywords">Words that point an expense here.</param>
    /// <param name="Position">Position in the list.</param>
    public sealed record SaveExpenseTypeCommand(
        Guid? Code,
        string Name,
        string? Keywords,
        int Position) : IRequest<ExpenseTypeDto>;

    /// <summary>Soft deletes a kind of expense that no expense uses.</summary>
    /// <param name="Code">Public identifier.</param>
    public sealed record DeleteExpenseTypeCommand(Guid Code) : IRequest;
}
