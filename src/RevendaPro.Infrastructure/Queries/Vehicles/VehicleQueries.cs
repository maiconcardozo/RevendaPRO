using Foundation.Dapper.Sql;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Infrastructure.Queries.Vehicles
{
    /// <summary>
    /// Columns of Vehicle, so every query returns the same shape and Dapper can materialize
    /// the entity, audit state included.
    /// </summary>
    internal static class VehicleColumns
    {
        public const string All = """
            Id, Code, IdTenant, Plate, Chassis, Brand, Model, Version, ModelYear,
            ManufactureYear, Color, Mileage, FuelType, Transmission, Renavam, Origin,
            HasDamage, DamageDescription, Status, PurchasePrice, PurchaseDate, SupplierName,
            PurchasePaymentMethod, BudgetCeiling, FipeValue, FipeReferenceDate, FipeCode,
            DesiredNetPrice, MinimumNetPrice, AdvertisedPrice, MarketNotes, Notes,
            IdCoverPhoto, IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted,
            DeletedBy
            """;

        /// <summary>
        /// Same list, qualified. Written out instead of derived by string replacement: text
        /// substitution over SQL cannot see the grammar of SQL.
        /// </summary>
        public const string Aliased = """
            v.Id, v.Code, v.IdTenant, v.Plate, v.Chassis, v.Brand, v.Model, v.Version,
            v.ModelYear, v.ManufactureYear, v.Color, v.Mileage, v.FuelType, v.Transmission,
            v.Renavam, v.Origin, v.HasDamage, v.DamageDescription, v.Status, v.PurchasePrice,
            v.PurchaseDate, v.SupplierName, v.PurchasePaymentMethod, v.BudgetCeiling,
            v.FipeValue, v.FipeReferenceDate, v.FipeCode, v.DesiredNetPrice, v.MinimumNetPrice,
            v.AdvertisedPrice, v.MarketNotes, v.Notes, v.IdCoverPhoto, v.IsActive, v.DtCreated,
            v.CreatedBy, v.DtUpdated, v.UpdatedBy, v.DtDeleted, v.DeletedBy
            """;
    }

    /// <summary>Finds one vehicle of a tenant by its public code.</summary>
    internal sealed class FindVehicleByCodeQuery : SqlQuery
    {
        public FindVehicleByCodeQuery(int idTenant, Guid code)
        {
            IdTenant = idTenant;
            Code = code;
        }

        public int IdTenant { get; }

        public Guid Code { get; }

        public override string GetSql() => $"""
            SELECT {VehicleColumns.All}
            FROM Vehicle
            WHERE Code = @Code
              AND IdTenant = @IdTenant
              AND IsActive = 1
            """;
    }

    /// <summary>
    /// Lists the vehicles of a tenant, filtered by status, origin and free text (RF-25).
    /// </summary>
    internal sealed class ListVehiclesQuery : SqlQuery
    {
        public ListVehiclesQuery(int idTenant, string? search, VehicleStatus? status, VehicleOrigin? origin)
        {
            IdTenant = idTenant;
            Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
            Status = (int?)status;
            Origin = (int?)origin;
        }

        public int IdTenant { get; }

        public string? Search { get; }

        public int? Status { get; }

        public int? Origin { get; }

        public override string GetSql() => $"""
            SELECT {VehicleColumns.Aliased}
            FROM Vehicle v
            WHERE v.IdTenant = @IdTenant
              AND v.IsActive = 1
              AND (@Status IS NULL OR v.Status = @Status)
              AND (@Origin IS NULL OR v.Origin = @Origin)
              AND (@Search IS NULL
                   OR v.Plate LIKE @Search
                   OR v.Brand LIKE @Search
                   OR v.Model LIKE @Search
                   OR v.Version LIKE @Search
                   OR v.Chassis LIKE @Search)
            ORDER BY v.Status, v.DtCreated DESC
            """;
    }

    /// <summary>
    /// Whether a plate or a chassis is already taken inside the tenant.
    ///
    /// The uniqueness is enforced here, and not by a unique index, because a deleted vehicle
    /// keeps its row: an index over the columns would refuse a plate that came back into the
    /// yard, and an index including IsActive would let two active rows share a plate.
    /// </summary>
    internal sealed class VehicleIdentifierExistsQuery : SqlQuery
    {
        public VehicleIdentifierExistsQuery(int idTenant, string plate, string chassis, int? ignoreId)
        {
            IdTenant = idTenant;
            Plate = plate;
            Chassis = chassis;
            IgnoreId = ignoreId;
        }

        public int IdTenant { get; }

        public string Plate { get; }

        public string Chassis { get; }

        public int? IgnoreId { get; }

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM Vehicle
            WHERE IdTenant = @IdTenant
              AND IsActive = 1
              AND (Plate = @Plate OR Chassis = @Chassis)
              AND (@IgnoreId IS NULL OR Id <> @IgnoreId)
            """;
    }

    /// <summary>Expenses of a vehicle, newest first (RF-08).</summary>
    internal sealed class ListVehicleExpensesQuery : SqlQuery
    {
        public ListVehicleExpensesQuery(int idVehicle) => IdVehicle = idVehicle;

        public int IdVehicle { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdVehicle, IdExpenseType, Description, Amount, Date, Notes,
                   IsPaid, IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted,
                   DeletedBy
            FROM VehicleExpense
            WHERE IdVehicle = @IdVehicle
              AND IsActive = 1
            ORDER BY Date DESC, Id DESC
            """;
    }

    /// <summary>
    /// Expenses of several vehicles at once, so a listing computes the cost of every row
    /// without one query per vehicle.
    /// </summary>
    internal sealed class ListExpensesOfVehiclesQuery : SqlQuery
    {
        public ListExpensesOfVehiclesQuery(IReadOnlyCollection<int> idVehicles) =>
            IdVehicles = idVehicles;

        public IReadOnlyCollection<int> IdVehicles { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdVehicle, IdExpenseType, Description, Amount, Date, Notes,
                   IsPaid, IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted,
                   DeletedBy
            FROM VehicleExpense
            WHERE IdVehicle IN @IdVehicles
              AND IsActive = 1
            """;
    }

    /// <summary>Finds one expense of a vehicle by its public code.</summary>
    internal sealed class FindVehicleExpenseByCodeQuery : SqlQuery
    {
        public FindVehicleExpenseByCodeQuery(Guid code) => Code = code;

        public Guid Code { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdVehicle, IdExpenseType, Description, Amount, Date, Notes,
                   IsPaid, IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted,
                   DeletedBy
            FROM VehicleExpense
            WHERE Code = @Code
              AND IsActive = 1
            """;
    }


    /// <summary>The types of expense of a tenant, in the order they are shown (RF-09).</summary>
    internal sealed class ListExpenseTypesQuery : SqlQuery
    {
        public ListExpenseTypesQuery(int idTenant) => IdTenant = idTenant;

        public int IdTenant { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdTenant, Name, Keywords, Position,
                   IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            FROM ExpenseType
            WHERE IdTenant = @IdTenant
              AND IsActive = 1
            ORDER BY Position, Name
            """;
    }

    /// <summary>Finds a type of expense of a tenant by its public code.</summary>
    internal sealed class FindExpenseTypeByCodeQuery : SqlQuery
    {
        public FindExpenseTypeByCodeQuery(int idTenant, Guid code)
        {
            IdTenant = idTenant;
            Code = code;
        }

        public int IdTenant { get; }

        public Guid Code { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdTenant, Name, Keywords, Position,
                   IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            FROM ExpenseType
            WHERE Code = @Code
              AND IdTenant = @IdTenant
              AND IsActive = 1
            """;
    }

    /// <summary>
    /// How many expenses point at a type, so deleting one in use can be refused with a reason
    /// instead of leaving orphaned rows behind.
    /// </summary>
    internal sealed class CountExpensesByTypeQuery : SqlQuery
    {
        public CountExpensesByTypeQuery(int idExpenseType) => IdExpenseType = idExpenseType;

        public int IdExpenseType { get; }

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM VehicleExpense
            WHERE IdExpenseType = @IdExpenseType
              AND IsActive = 1
            """;
    }

    /// <summary>
    /// Expenses of the tenant whose description matches what is being typed, so the screen can
    /// suggest from what this dealership already wrote — and bring the type along with it.
    ///
    /// The grouping happens in memory rather than in SQL because the repository materializes
    /// entities, and the number of rows a prefix matches inside one tenant is small.
    /// </summary>
    internal sealed class ListExpensesForSuggestionQuery : SqlQuery
    {
        public ListExpensesForSuggestionQuery(int idTenant, string term)
        {
            IdTenant = idTenant;
            Term = $"%{term.Trim()}%";
        }

        public int IdTenant { get; }

        public string Term { get; }

        public override string GetSql() => """
            SELECT e.Id, e.Code, e.IdVehicle, e.IdExpenseType, e.Description, e.Amount, e.Date,
                   e.Notes, e.IsPaid, e.IsActive, e.DtCreated, e.CreatedBy, e.DtUpdated,
                   e.UpdatedBy, e.DtDeleted, e.DeletedBy
            FROM VehicleExpense e
            INNER JOIN Vehicle v ON v.Id = e.IdVehicle AND v.IsActive = 1
            WHERE v.IdTenant = @IdTenant
              AND e.IsActive = 1
              AND e.Description LIKE @Term
            ORDER BY e.Id DESC
            LIMIT 200
            """;
    }
    /// <summary>Status history of a vehicle, oldest first (RF-26).</summary>
    internal sealed class ListVehicleStatusHistoryQuery : SqlQuery
    {
        public ListVehicleStatusHistoryQuery(int idVehicle) => IdVehicle = idVehicle;

        public int IdVehicle { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdVehicle, FromStatus, ToStatus, Reason,
                   IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            FROM VehicleStatusHistory
            WHERE IdVehicle = @IdVehicle
              AND IsActive = 1
            ORDER BY Id
            """;
    }
}
