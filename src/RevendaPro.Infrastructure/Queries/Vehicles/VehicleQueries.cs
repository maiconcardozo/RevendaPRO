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
            FipeYearFuel, FipeSource, DesiredNetPrice, MinimumNetPrice, AdvertisedPrice,
            MarketNotes, Notes,
            IdCoverPhoto, IdYard, IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy,
            DtDeleted, DeletedBy
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
            v.FipeValue, v.FipeReferenceDate, v.FipeCode, v.FipeYearFuel, v.FipeSource,
            v.DesiredNetPrice, v.MinimumNetPrice, v.AdvertisedPrice, v.MarketNotes, v.Notes,
            v.IdCoverPhoto,
            v.IdYard, v.IsActive, v.DtCreated, v.CreatedBy, v.DtUpdated, v.UpdatedBy,
            v.DtDeleted, v.DeletedBy
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
        public ListVehiclesQuery(
            int idTenant,
            string? search,
            VehicleStatus? status,
            VehicleOrigin? origin,
            DateOnly? purchasedFrom = null,
            DateOnly? purchasedTo = null,
            int? idYard = null)
        {
            IdTenant = idTenant;
            Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
            Status = (int?)status;
            Origin = (int?)origin;
            PurchasedFrom = purchasedFrom;
            PurchasedTo = purchasedTo;
            IdYard = idYard;
        }

        public int IdTenant { get; }

        public string? Search { get; }

        public int? Status { get; }

        public int? Origin { get; }

        /// <summary>
        /// The period is read over the purchase date, which is the day the car entered
        /// the yard. A vehicle with no purchase date stays out of any period.
        /// </summary>
        public DateOnly? PurchasedFrom { get; }

        public DateOnly? PurchasedTo { get; }

        /// <summary>
        /// O pátio, quando a pergunta é sobre um lugar só.
        ///
        /// A filtragem é do banco, e jamais uma peneira em memória: pedir o pátio inteiro para
        /// jogar fora o que não interessa é o mesmo erro que o período já evita, e cresce com
        /// o estoque.
        /// </summary>
        public int? IdYard { get; }

        public override string GetSql() => $"""
            SELECT {VehicleColumns.Aliased}
            FROM Vehicle v
            WHERE v.IdTenant = @IdTenant
              AND v.IsActive = 1
              AND (@IdYard IS NULL OR v.IdYard = @IdYard)
              AND (@Status IS NULL OR v.Status = @Status)
              AND (@Origin IS NULL OR v.Origin = @Origin)
              AND (@PurchasedFrom IS NULL OR v.PurchaseDate >= @PurchasedFrom)
              AND (@PurchasedTo IS NULL OR v.PurchaseDate <= @PurchasedTo)
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
    /// The cars of the yard whose reference is older than the published table.
    ///
    /// <b>Crosses every company on purpose, and it is the only reading that does.</b> This is
    /// the monthly routine, and not a person: nobody is logged in, so there is no tenant to
    /// filter by. The rows never leave the routine — what it does with each one is write that
    /// vehicle's own reference. The isolation of RNF-04 lives in every path a person can take,
    /// and a scheduled job is not one of them.
    ///
    /// Sold cars stay out: the comparison of a closed deal is against the table of the month
    /// it closed, which the quotes already keep. Cars with no code stay out because there is
    /// nothing to ask the table. Whether a typed value may be overwritten is a rule of the
    /// vehicle, and it is asked there.
    /// </summary>
    internal sealed class ListVehiclesBehindFipeQuery : SqlQuery
    {
        public ListVehiclesBehindFipeQuery(DateOnly publishedMonth, int limit)
        {
            PublishedMonth = new DateOnly(publishedMonth.Year, publishedMonth.Month, 1);
            Limit = limit;
        }

        public DateOnly PublishedMonth { get; }

        public int Limit { get; }

        public override string GetSql() => $"""
            SELECT {VehicleColumns.All}
            FROM Vehicle
            WHERE IsActive = 1
              AND Status <> {(int)VehicleStatus.Sold}
              AND FipeCode IS NOT NULL
              AND FipeCode <> ''
              AND (FipeReferenceDate IS NULL OR FipeReferenceDate < @PublishedMonth)
            ORDER BY FipeCode, FipeYearFuel, Id
            LIMIT @Limit
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

    /// <summary>Photos of a vehicle, in gallery order (RF-12).</summary>
    internal sealed class ListVehiclePhotosQuery : SqlQuery
    {
        public ListVehiclePhotosQuery(int idVehicle) => IdVehicle = idVehicle;

        public int IdVehicle { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdVehicle, Kind, StorageKey, ContentType, SizeInBytes, Width,
                   Height, Position, IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy,
                   DtDeleted, DeletedBy
            FROM VehiclePhoto
            WHERE IdVehicle = @IdVehicle
              AND IsActive = 1
            ORDER BY Position, Id
            """;
    }

    /// <summary>Finds a photo by its public code.</summary>
    internal sealed class FindVehiclePhotoByCodeQuery : SqlQuery
    {
        public FindVehiclePhotoByCodeQuery(Guid code) => Code = code;

        public Guid Code { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdVehicle, Kind, StorageKey, ContentType, SizeInBytes, Width,
                   Height, Position, IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy,
                   DtDeleted, DeletedBy
            FROM VehiclePhoto
            WHERE Code = @Code
              AND IsActive = 1
            """;
    }

    /// <summary>
    /// How many photos each vehicle has, and the key of its cover, for a whole listing at once.
    ///
    /// The join to Vehicle exists only to read <c>IdCoverPhoto</c>: which photo is the cover
    /// is a decision of the vehicle, so that one row can never disagree with another.
    /// </summary>
    internal sealed class SummarizeVehicleGalleriesQuery : SqlQuery
    {
        public SummarizeVehicleGalleriesQuery(IReadOnlyCollection<int> idVehicles) =>
            IdVehicles = idVehicles;

        public IReadOnlyCollection<int> IdVehicles { get; }

        public override string GetSql() => """
            SELECT p.IdVehicle,
                   COUNT(*) AS PhotoCount,
                   MAX(CASE WHEN p.Id = v.IdCoverPhoto THEN p.StorageKey END) AS CoverStorageKey
            FROM VehiclePhoto p
            INNER JOIN Vehicle v ON v.Id = p.IdVehicle
            WHERE p.IdVehicle IN @IdVehicles
              AND p.IsActive = 1
              AND v.IsActive = 1
            GROUP BY p.IdVehicle
            """;
    }

    /// <summary>Documents of a vehicle, newest first (RF-13).</summary>
    internal sealed class ListVehicleDocumentsQuery : SqlQuery
    {
        public ListVehicleDocumentsQuery(int idVehicle) => IdVehicle = idVehicle;

        public int IdVehicle { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdVehicle, Kind, StorageKey, FileName, ContentType, SizeInBytes,
                   IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            FROM VehicleDocument
            WHERE IdVehicle = @IdVehicle
              AND IsActive = 1
            ORDER BY Id DESC
            """;
    }

    /// <summary>Finds a document by its public code.</summary>
    internal sealed class FindVehicleDocumentByCodeQuery : SqlQuery
    {
        public FindVehicleDocumentByCodeQuery(Guid code) => Code = code;

        public Guid Code { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdVehicle, Kind, StorageKey, FileName, ContentType, SizeInBytes,
                   IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            FROM VehicleDocument
            WHERE Code = @Code
              AND IsActive = 1
            """;
    }
    /// <summary>As passagens de um veículo pelos pátios, da mais antiga para a mais nova.</summary>
    internal sealed class ListVehicleYardHistoryQuery : SqlQuery
    {
        public ListVehicleYardHistoryQuery(int idVehicle) => IdVehicle = idVehicle;

        public int IdVehicle { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdVehicle, IdFromYard, IdToYard, Reason,
                   IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            FROM VehicleYardHistory
            WHERE IdVehicle = @IdVehicle
              AND IsActive = 1
            ORDER BY Id
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
    /// <summary>
    /// Everything that happened to a vehicle, in one reading (RF-26).
    ///
    /// Eight statements over the tables that already hold the operation, projected to the same
    /// shape and ordered by the database. One trip: the file opens with a single query instead
    /// of eight, and the ordering is never rebuilt in memory.
    ///
    /// The integer and NULL columns are cast on purpose. A UNION resolves the type of each
    /// column across every branch, and a bare NULL leaves that to the driver; naming the type
    /// makes what Dapper receives the same no matter which branch produced the row.
    ///
    /// Attachments are grouped by day and by author: sending the photos of a car is one act,
    /// done in a minute, and twenty identical lines would drown the history. Everything else
    /// comes one by one, because each one is a decision taken at its own time.
    /// </summary>
    internal sealed class ListVehicleTimelineQuery : SqlQuery
    {
        public ListVehicleTimelineQuery(int idVehicle) => IdVehicle = idVehicle;

        public int IdVehicle { get; }

        public override string GetSql() => """
            SELECT COALESCE(CAST(v.PurchaseDate AS DATETIME), v.DtCreated) AS Moment,
                   CAST(1 AS SIGNED) AS Kind,
                   v.Code AS Code,
                   v.SupplierName AS Title,
                   CAST(NULL AS CHAR) AS FromTitle,
                   CAST(NULL AS CHAR) AS Detail,
                   v.PurchasePrice AS Amount,
                   CAST(1 AS SIGNED) AS Quantity,
                   CAST(NULL AS SIGNED) AS FromStatus,
                   CAST(NULL AS SIGNED) AS ToStatus,
                   CAST(NULL AS SIGNED) AS ProposalStatus,
                   CAST(NULL AS SIGNED) AS IsPaid,
                   v.CreatedBy AS ActorCode
            FROM Vehicle v
            WHERE v.Id = @IdVehicle
              AND v.IsActive = 1

            UNION ALL

            SELECT h.DtCreated,
                   CAST(2 AS SIGNED),
                   h.Code,
                   CAST(NULL AS CHAR),
                   CAST(NULL AS CHAR),
                   h.Reason,
                   CAST(NULL AS DECIMAL(12, 2)),
                   CAST(1 AS SIGNED),
                   h.FromStatus,
                   h.ToStatus,
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   h.CreatedBy
            FROM VehicleStatusHistory h
            WHERE h.IdVehicle = @IdVehicle
              AND h.IsActive = 1

            UNION ALL

            SELECT COALESCE(CAST(e.Date AS DATETIME), e.DtCreated),
                   CAST(3 AS SIGNED),
                   e.Code,
                   e.Description,
                   CAST(NULL AS CHAR),
                   e.Notes,
                   e.Amount,
                   CAST(1 AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   e.IsPaid,
                   e.CreatedBy
            FROM VehicleExpense e
            WHERE e.IdVehicle = @IdVehicle
              AND e.IsActive = 1

            UNION ALL

            SELECT MAX(p.DtCreated),
                   CAST(4 AS SIGNED),
                   CASE WHEN COUNT(*) = 1 THEN MIN(p.Code) END,
                   CAST(NULL AS CHAR),
                   CAST(NULL AS CHAR),
                   CAST(NULL AS CHAR),
                   CAST(NULL AS DECIMAL(12, 2)),
                   COUNT(*),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   p.CreatedBy
            FROM VehiclePhoto p
            WHERE p.IdVehicle = @IdVehicle
              AND p.IsActive = 1
            GROUP BY DATE(p.DtCreated), p.CreatedBy

            UNION ALL

            SELECT MAX(d.DtCreated),
                   CAST(5 AS SIGNED),
                   CASE WHEN COUNT(*) = 1 THEN MIN(d.Code) END,
                   CASE WHEN COUNT(*) = 1 THEN MIN(d.FileName) END,
                   CAST(NULL AS CHAR),
                   CAST(NULL AS CHAR),
                   CAST(NULL AS DECIMAL(12, 2)),
                   COUNT(*),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   d.CreatedBy
            FROM VehicleDocument d
            WHERE d.IdVehicle = @IdVehicle
              AND d.IsActive = 1
            GROUP BY DATE(d.DtCreated), d.CreatedBy

            UNION ALL

            SELECT COALESCE(CAST(pr.Date AS DATETIME), pr.DtCreated),
                   CAST(6 AS SIGNED),
                   pr.Code,
                   pr.ProspectName,
                   CAST(NULL AS CHAR),
                   pr.Notes,
                   pr.Amount,
                   CAST(1 AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   pr.Status,
                   CAST(NULL AS SIGNED),
                   pr.CreatedBy
            FROM Proposal pr
            WHERE pr.IdVehicle = @IdVehicle
              AND pr.IsActive = 1

            UNION ALL

            SELECT COALESCE(CAST(s.Date AS DATETIME), s.DtCreated),
                   CAST(7 AS SIGNED),
                   s.Code,
                   s.BuyerName,
                   CAST(NULL AS CHAR),
                   s.Notes,
                   s.Amount,
                   CAST(1 AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   s.CreatedBy
            FROM Sale s
            WHERE s.IdVehicle = @IdVehicle
              AND s.IsActive = 1

            UNION ALL

            SELECT y.DtCreated,
                   CAST(8 AS SIGNED),
                   y.Code,
                   yt.Name,
                   yf.Name,
                   y.Reason,
                   CAST(NULL AS DECIMAL(12, 2)),
                   CAST(1 AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   CAST(NULL AS SIGNED),
                   y.CreatedBy
            FROM VehicleYardHistory y
            LEFT JOIN Yard yf ON yf.Id = y.IdFromYard AND yf.IsActive = 1
            LEFT JOIN Yard yt ON yt.Id = y.IdToYard AND yt.IsActive = 1
            WHERE y.IdVehicle = @IdVehicle
              AND y.IsActive = 1

            ORDER BY Moment, Kind
            """;
    }
    /// <summary>
    /// Documents that were taken out of the file of a vehicle, newest deletion first.
    ///
    /// One of the two statements in the system that read a deleted row on purpose. Since
    /// the M6 the DELETE of a document keeps the object in the bucket, by requirement: what
    /// this query does is give back the door to it. The vehicle it hangs from is still
    /// filtered normally — a document of a deleted car stays out, because the car is out.
    /// </summary>
    internal sealed class ListDeletedVehicleDocumentsQuery : SqlQuery
    {
        public ListDeletedVehicleDocumentsQuery(int idTenant) => IdTenant = idTenant;

        public int IdTenant { get; }

        public override string GetSql() => """
            SELECT d.Code, d.Kind, d.FileName, d.ContentType, d.SizeInBytes, d.StorageKey,
                   d.DtCreated AS UploadedAt, d.DtDeleted AS DeletedAt,
                   d.DeletedBy AS DeletedByCode,
                   v.Code AS VehicleCode, v.Plate, v.Brand, v.Model
            FROM VehicleDocument d
            JOIN Vehicle v ON v.Id = d.IdVehicle
            WHERE v.IdTenant = @IdTenant
              AND v.IsActive = 1
              AND d.IsActive = 0
            ORDER BY d.DtDeleted DESC, d.Id DESC
            """;
    }

    /// <summary>
    /// Finds a document by code even when it was deleted. Only the administrative screen of
    /// deleted documents calls it; every other reading leaves deleted rows out.
    /// </summary>
    internal sealed class FindVehicleDocumentByCodeIncludingDeletedQuery : SqlQuery
    {
        public FindVehicleDocumentByCodeIncludingDeletedQuery(Guid code) => Code = code;

        public Guid Code { get; }

        public override string GetSql() => """
            SELECT Id, Code, IdVehicle, Kind, StorageKey, FileName, ContentType, SizeInBytes,
                   IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            FROM VehicleDocument
            WHERE Code = @Code
            """;
    }
}
