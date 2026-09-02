using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Sales
{
    /// <summary>Columns of Proposal, so every query materializes the same shape.</summary>
    internal static class ProposalColumns
    {
        public const string All = """
            Id, Code, IdVehicle, ProspectName, ProspectPhone, Amount, Date, PaymentMethod,
            Channel, PartnerCutPercent, PartnerCutAmount, Status, Notes, IsActive, DtCreated,
            CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;
    }

    /// <summary>Columns of Sale.</summary>
    internal static class SaleColumns
    {
        public const string All = """
            Id, Code, IdVehicle, IdProposal, IdTradeInVehicle, Date, Amount, PaymentMethod,
            Channel, PartnerStoreName, PartnerCutPercent, PartnerCutAmount, Commission,
            CommissionNotes, BuyerName, BuyerDocument, BuyerPhone, TradeInValue, Notes,
            IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;

        /// <summary>Same list, qualified. Written out: text substitution cannot see SQL grammar.</summary>
        public const string Aliased = """
            s.Id, s.Code, s.IdVehicle, s.IdProposal, s.IdTradeInVehicle, s.Date, s.Amount,
            s.PaymentMethod, s.Channel, s.PartnerStoreName, s.PartnerCutPercent,
            s.PartnerCutAmount, s.Commission, s.CommissionNotes, s.BuyerName, s.BuyerDocument,
            s.BuyerPhone, s.TradeInValue, s.Notes, s.IsActive, s.DtCreated, s.CreatedBy,
            s.DtUpdated, s.UpdatedBy, s.DtDeleted, s.DeletedBy
            """;
    }

    /// <summary>Proposals of a vehicle, newest first (RF-18).</summary>
    internal sealed class ListProposalsByVehicleQuery : SqlQuery
    {
        public ListProposalsByVehicleQuery(int idVehicle) => IdVehicle = idVehicle;

        public int IdVehicle { get; }

        public override string GetSql() => $"""
            SELECT {ProposalColumns.All}
            FROM Proposal
            WHERE IdVehicle = @IdVehicle
              AND IsActive = 1
            ORDER BY Date DESC, Id DESC
            """;
    }

    /// <summary>Finds a proposal by its public code.</summary>
    internal sealed class FindProposalByCodeQuery : SqlQuery
    {
        public FindProposalByCodeQuery(Guid code) => Code = code;

        public Guid Code { get; }

        public override string GetSql() => $"""
            SELECT {ProposalColumns.All}
            FROM Proposal
            WHERE Code = @Code
              AND IsActive = 1
            """;
    }

    /// <summary>
    /// The active sale of a vehicle. A cancelled sale keeps its row, soft deleted, which is why
    /// "one sale per car" is this filter plus a test, and never a unique index.
    /// </summary>
    internal sealed class FindSaleByVehicleQuery : SqlQuery
    {
        public FindSaleByVehicleQuery(int idVehicle) => IdVehicle = idVehicle;

        public int IdVehicle { get; }

        public override string GetSql() => $"""
            SELECT {SaleColumns.All}
            FROM Sale
            WHERE IdVehicle = @IdVehicle
              AND IsActive = 1
            """;
    }

    /// <summary>Finds a sale by its public code.</summary>
    internal sealed class FindSaleByCodeQuery : SqlQuery
    {
        public FindSaleByCodeQuery(Guid code) => Code = code;

        public Guid Code { get; }

        public override string GetSql() => $"""
            SELECT {SaleColumns.All}
            FROM Sale
            WHERE Code = @Code
              AND IsActive = 1
            """;
    }

    /// <summary>
    /// Sales of a tenant in a period, newest first (RF-23).
    ///
    /// The tenant lives on the vehicle, so the join is the isolation. Both tables carry their
    /// own soft delete filter: a sale of a deleted vehicle is a sale of a car that no longer
    /// exists for this company.
    /// </summary>
    internal sealed class ListSalesByTenantQuery : SqlQuery
    {
        public ListSalesByTenantQuery(int idTenant, DateOnly? from, DateOnly? to)
        {
            IdTenant = idTenant;
            From = from;
            To = to;
        }

        public int IdTenant { get; }

        public DateOnly? From { get; }

        public DateOnly? To { get; }

        public override string GetSql() => $"""
            SELECT {SaleColumns.Aliased}
            FROM Sale s
            INNER JOIN Vehicle v ON v.Id = s.IdVehicle
            WHERE v.IdTenant = @IdTenant
              AND v.IsActive = 1
              AND s.IsActive = 1
              AND (@From IS NULL OR s.Date >= @From)
              AND (@To IS NULL OR s.Date <= @To)
            ORDER BY s.Date DESC, s.Id DESC
            """;
    }
}
