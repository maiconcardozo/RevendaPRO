using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Customers
{
    /// <summary>
    /// Colunas de Customer, para toda consulta devolver a mesma forma e o Dapper materializar a
    /// entidade, auditoria incluída.
    /// </summary>
    internal static class CustomerColumns
    {
        public const string All = """
            Id, Code, IdTenant, Name, Document, Phone, Email, Address, Notes,
            IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;
    }

    /// <summary>
    /// Os clientes de uma revenda, por nome, filtrados por um trecho do nome, do telefone ou do
    /// documento. O trecho chega já sem máscara para telefone e documento: quem digita
    /// "(51) 9" procura "519".
    /// </summary>
    internal sealed class ListCustomersByTenantQuery(int idTenant, string? search) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public string? Search { get; } = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";

        public string? Digits { get; } =
            Digitize(search) is { Length: > 0 } digits ? $"%{digits}%" : null;

        public override string GetSql() => $"""
            SELECT {CustomerColumns.All}
            FROM Customer
            WHERE IdTenant = @IdTenant
              AND IsActive = 1
              AND (@Search IS NULL
                   OR Name LIKE @Search
                   OR (@Digits IS NOT NULL AND (Phone LIKE @Digits OR Document LIKE @Digits)))
            ORDER BY Name
            """;

        private static string Digitize(string? value) =>
            new((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    /// <summary>Um cliente de uma revenda, pelo código público.</summary>
    internal sealed class FindCustomerByCodeQuery(int idTenant, Guid code) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public Guid Code { get; } = code;

        public override string GetSql() => $"""
            SELECT {CustomerColumns.All}
            FROM Customer
            WHERE Code = @Code
              AND IdTenant = @IdTenant
              AND IsActive = 1
            """;
    }

    /// <summary>Vários clientes de uma vez, pelos Ids.</summary>
    internal sealed class ListCustomersByIdsQuery(int idTenant, IReadOnlyCollection<int> ids) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public IReadOnlyCollection<int> Ids { get; } = ids;

        public override string GetSql() => $"""
            SELECT {CustomerColumns.All}
            FROM Customer
            WHERE IdTenant = @IdTenant
              AND Id IN @Ids
              AND IsActive = 1
            """;
    }

    /// <summary>O cliente com esse documento, fora o que se está editando.</summary>
    internal sealed class FindCustomerByDocumentQuery(int idTenant, string document, int? ignoreId) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public string Document { get; } = document;

        public int? IgnoreId { get; } = ignoreId;

        public override string GetSql() => $"""
            SELECT {CustomerColumns.All}
            FROM Customer
            WHERE IdTenant = @IdTenant
              AND Document = @Document
              AND IsActive = 1
              AND (@IgnoreId IS NULL OR Id <> @IgnoreId)
            ORDER BY Id
            LIMIT 1
            """;
    }

    /// <summary>Os clientes com esse telefone, do mais antigo para o mais novo.</summary>
    internal sealed class FindCustomersByPhoneQuery(int idTenant, string phone) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public string Phone { get; } = phone;

        public override string GetSql() => $"""
            SELECT {CustomerColumns.All}
            FROM Customer
            WHERE IdTenant = @IdTenant
              AND Phone = @Phone
              AND IsActive = 1
            ORDER BY Id
            """;
    }

    /// <summary>Os clientes com exatamente esse nome, do mais antigo para o mais novo.</summary>
    internal sealed class FindCustomersByNameQuery(int idTenant, string name) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public string Name { get; } = name;

        public override string GetSql() => $"""
            SELECT {CustomerColumns.All}
            FROM Customer
            WHERE IdTenant = @IdTenant
              AND Name = @Name
              AND IsActive = 1
            ORDER BY Id
            """;
    }

    /// <summary>
    /// Quantas propostas e compras cada cliente tem, e quanto comprou, somado pelo banco.
    ///
    /// Duas subconsultas agrupadas e juntadas pelo cliente, em vez de um JOIN das duas tabelas
    /// de uma vez, que multiplicaria as linhas — três propostas e duas compras virariam seis
    /// linhas e uma soma errada. Passa pelo veículo para conferir a revenda, como toda linha
    /// que pertence a um.
    /// </summary>
    internal sealed class SummarizeCustomersQuery(int idTenant) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public override string GetSql() => """
            SELECT c.Id AS IdCustomer,
                   COALESCE(p.ProposalCount, 0) AS ProposalCount,
                   COALESCE(s.SaleCount, 0) AS SaleCount,
                   COALESCE(s.BoughtTotal, 0) AS BoughtTotal,
                   CASE
                       WHEN p.LastDate IS NULL THEN s.LastDate
                       WHEN s.LastDate IS NULL THEN p.LastDate
                       WHEN p.LastDate > s.LastDate THEN p.LastDate
                       ELSE s.LastDate
                   END AS LastDate
            FROM Customer c
            LEFT JOIN (
                SELECT pr.IdCustomer, COUNT(1) AS ProposalCount, MAX(pr.Date) AS LastDate
                FROM Proposal pr
                INNER JOIN Vehicle v ON v.Id = pr.IdVehicle AND v.IsActive = 1
                WHERE v.IdTenant = @IdTenant
                  AND pr.IsActive = 1
                  AND pr.IdCustomer IS NOT NULL
                GROUP BY pr.IdCustomer
            ) p ON p.IdCustomer = c.Id
            LEFT JOIN (
                SELECT sa.IdCustomer, COUNT(1) AS SaleCount, SUM(sa.Amount) AS BoughtTotal, MAX(sa.Date) AS LastDate
                FROM Sale sa
                INNER JOIN Vehicle v ON v.Id = sa.IdVehicle AND v.IsActive = 1
                WHERE v.IdTenant = @IdTenant
                  AND sa.IsActive = 1
                  AND sa.IdCustomer IS NOT NULL
                GROUP BY sa.IdCustomer
            ) s ON s.IdCustomer = c.Id
            WHERE c.IdTenant = @IdTenant
              AND c.IsActive = 1
              AND (p.IdCustomer IS NOT NULL OR s.IdCustomer IS NOT NULL)
            """;
    }

    /// <summary>As propostas de um cliente, com o carro de cada uma, da mais recente para a mais antiga.</summary>
    internal sealed class ListProposalsOfCustomerQuery(int idTenant, int idCustomer) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public int IdCustomer { get; } = idCustomer;

        public override string GetSql() => """
            SELECT p.Code, p.Date, p.Amount, p.Status, p.PaymentMethod,
                   v.Code AS VehicleCode, v.Plate, v.Brand, v.Model, v.Version, v.ModelYear
            FROM Proposal p
            INNER JOIN Vehicle v ON v.Id = p.IdVehicle AND v.IsActive = 1
            WHERE v.IdTenant = @IdTenant
              AND p.IdCustomer = @IdCustomer
              AND p.IsActive = 1
            ORDER BY p.Date DESC, p.Id DESC
            """;
    }

    /// <summary>As compras de um cliente, com o carro de cada uma, da mais recente para a mais antiga.</summary>
    internal sealed class ListSalesOfCustomerQuery(int idTenant, int idCustomer) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public int IdCustomer { get; } = idCustomer;

        public override string GetSql() => """
            SELECT s.Code, s.Date, s.Amount, s.PaymentMethod,
                   CASE WHEN s.TradeInValue IS NULL THEN 0 ELSE 1 END AS HadTradeIn,
                   v.Code AS VehicleCode, v.Plate, v.Brand, v.Model, v.Version, v.ModelYear
            FROM Sale s
            INNER JOIN Vehicle v ON v.Id = s.IdVehicle AND v.IsActive = 1
            WHERE v.IdTenant = @IdTenant
              AND s.IdCustomer = @IdCustomer
              AND s.IsActive = 1
            ORDER BY s.Date DESC, s.Id DESC
            """;
    }
}
