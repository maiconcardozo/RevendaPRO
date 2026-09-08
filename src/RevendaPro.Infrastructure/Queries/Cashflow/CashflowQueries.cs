using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Cashflow
{
    /// <summary>
    /// Os números do topo do caixa, numa ida ao banco só (M22).
    ///
    /// Sete somas, cada uma numa subconsulta escalar. Poderia ser sete consultas; é uma, porque
    /// a tela pergunta as sete juntas e o banco as responde sem carregar linha nenhuma para cá.
    ///
    /// O que se deve e o que se tem a receber são <b>sem período</b>: "quanto eu devo" é a
    /// pergunta de hoje, e uma janela de datas a transformaria em outra pergunta. O período
    /// delimita só o que se moveu — quanto saiu e quanto entrou.
    /// </summary>
    internal sealed class ReadCashflowSummaryQuery(int idTenant, DateOnly? from, DateOnly? to, DateOnly today)
        : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public DateOnly? From { get; } = from;

        public DateOnly? To { get; } = to;

        public DateOnly Today { get; } = today;

        /// <summary>O horizonte de "vence logo": uma semana, que é o que cabe numa segunda-feira.</summary>
        public DateOnly Soon { get; } = today.AddDays(7);

        public override string GetSql() => """
            SELECT
              (SELECT COALESCE(SUM(e.Amount), 0)
                 FROM VehicleExpense e
                 INNER JOIN Vehicle v ON v.Id = e.IdVehicle AND v.IsActive = 1
                WHERE v.IdTenant = @IdTenant AND e.IsActive = 1 AND e.IsPaid = 0)
            + (SELECT COALESCE(SUM(s.Amount), 0)
                 FROM StoreExpense s
                WHERE s.IdTenant = @IdTenant AND s.IsActive = 1 AND s.IsPaid = 0) AS PayableOpen,

              (SELECT COALESCE(SUM(e.Amount), 0)
                 FROM VehicleExpense e
                 INNER JOIN Vehicle v ON v.Id = e.IdVehicle AND v.IsActive = 1
                WHERE v.IdTenant = @IdTenant AND e.IsActive = 1 AND e.IsPaid = 0 AND e.DueDate < @Today)
            + (SELECT COALESCE(SUM(s.Amount), 0)
                 FROM StoreExpense s
                WHERE s.IdTenant = @IdTenant AND s.IsActive = 1 AND s.IsPaid = 0 AND s.DueDate < @Today) AS PayableOverdue,

              (SELECT COALESCE(SUM(e.Amount), 0)
                 FROM VehicleExpense e
                 INNER JOIN Vehicle v ON v.Id = e.IdVehicle AND v.IsActive = 1
                WHERE v.IdTenant = @IdTenant AND e.IsActive = 1 AND e.IsPaid = 0
                  AND e.DueDate >= @Today AND e.DueDate <= @Soon)
            + (SELECT COALESCE(SUM(s.Amount), 0)
                 FROM StoreExpense s
                WHERE s.IdTenant = @IdTenant AND s.IsActive = 1 AND s.IsPaid = 0
                  AND s.DueDate >= @Today AND s.DueDate <= @Soon) AS PayableDueSoon,

              (SELECT COALESCE(SUM(o.Balance), 0) FROM (
                   SELECT sa.Amount - COALESCE(sa.TradeInValue, 0) - COALESCE(sa.PartnerCutAmount, 0)
                          - COALESCE((SELECT SUM(r.Amount) FROM SaleReceipt r
                                       WHERE r.IdSale = sa.Id AND r.IsActive = 1), 0) AS Balance
                     FROM Sale sa
                     INNER JOIN Vehicle v ON v.Id = sa.IdVehicle AND v.IsActive = 1
                    WHERE v.IdTenant = @IdTenant AND sa.IsActive = 1
               ) o WHERE o.Balance > 0) AS ReceivableOpen,

              (SELECT COALESCE(SUM(o.Balance), 0) FROM (
                   SELECT sa.DueDate,
                          sa.Amount - COALESCE(sa.TradeInValue, 0) - COALESCE(sa.PartnerCutAmount, 0)
                          - COALESCE((SELECT SUM(r.Amount) FROM SaleReceipt r
                                       WHERE r.IdSale = sa.Id AND r.IsActive = 1), 0) AS Balance
                     FROM Sale sa
                     INNER JOIN Vehicle v ON v.Id = sa.IdVehicle AND v.IsActive = 1
                    WHERE v.IdTenant = @IdTenant AND sa.IsActive = 1
               ) o WHERE o.Balance > 0 AND o.DueDate IS NOT NULL AND o.DueDate < @Today) AS ReceivableOverdue,

              (SELECT COALESCE(SUM(e.Amount), 0)
                 FROM VehicleExpense e
                 INNER JOIN Vehicle v ON v.Id = e.IdVehicle AND v.IsActive = 1
                WHERE v.IdTenant = @IdTenant AND e.IsActive = 1 AND e.IsPaid = 1
                  AND (@From IS NULL OR e.PaidDate >= @From) AND (@To IS NULL OR e.PaidDate <= @To))
            + (SELECT COALESCE(SUM(s.Amount), 0)
                 FROM StoreExpense s
                WHERE s.IdTenant = @IdTenant AND s.IsActive = 1 AND s.IsPaid = 1
                  AND (@From IS NULL OR s.PaidDate >= @From) AND (@To IS NULL OR s.PaidDate <= @To)) AS PaidInPeriod,

              (SELECT COALESCE(SUM(r.Amount), 0)
                 FROM SaleReceipt r
                 INNER JOIN Sale sa ON sa.Id = r.IdSale AND sa.IsActive = 1
                 INNER JOIN Vehicle v ON v.Id = sa.IdVehicle AND v.IsActive = 1
                WHERE v.IdTenant = @IdTenant AND r.IsActive = 1
                  AND (@From IS NULL OR r.Date >= @From) AND (@To IS NULL OR r.Date <= @To)) AS ReceivedInPeriod
            """;
    }

    /// <summary>
    /// As contas a pagar: o gasto do carro e a despesa da loja numa lista só (M22).
    ///
    /// Um <c>UNION ALL</c>, e não duas consultas somadas aqui: a ordenação por vencimento é do
    /// extrato inteiro, e ordenar duas listas separadas para intercalar em memória seria
    /// refazer no C# o que o banco faz melhor.
    /// </summary>
    internal sealed class ListPayablesQuery(int idTenant, bool? settled, DateOnly? from, DateOnly? to)
        : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        /// <summary>Nulo traz as duas; 0 só o que falta pagar; 1 só o pago.</summary>
        public int? Settled { get; } = settled is null ? null : settled.Value ? 1 : 0;

        public DateOnly? From { get; } = from;

        public DateOnly? To { get; } = to;

        public override string GetSql() => """
            SELECT e.Code, 1 AS Kind, e.Description, sup.Name AS Party, t.Name AS Category,
                   e.Amount, e.DueDate, e.PaidDate AS SettledDate, e.IsPaid AS IsSettled,
                   v.Code AS VehicleCode, v.Plate
              FROM VehicleExpense e
              INNER JOIN Vehicle v ON v.Id = e.IdVehicle AND v.IsActive = 1
              LEFT JOIN ExpenseType t ON t.Id = e.IdExpenseType AND t.IsActive = 1
              LEFT JOIN Supplier sup ON sup.Id = e.IdSupplier AND sup.IsActive = 1
             WHERE v.IdTenant = @IdTenant
               AND e.IsActive = 1
               AND (@Settled IS NULL OR e.IsPaid = @Settled)
               AND (@From IS NULL OR e.DueDate >= @From)
               AND (@To IS NULL OR e.DueDate <= @To)
            UNION ALL
            SELECT s.Code, 2 AS Kind, s.Description, sup.Name AS Party, t.Name AS Category,
                   s.Amount, s.DueDate, s.PaidDate AS SettledDate, s.IsPaid AS IsSettled,
                   NULL AS VehicleCode, NULL AS Plate
              FROM StoreExpense s
              LEFT JOIN ExpenseType t ON t.Id = s.IdExpenseType AND t.IsActive = 1
              LEFT JOIN Supplier sup ON sup.Id = s.IdSupplier AND sup.IsActive = 1
             WHERE s.IdTenant = @IdTenant
               AND s.IsActive = 1
               AND (@Settled IS NULL OR s.IsPaid = @Settled)
               AND (@From IS NULL OR s.DueDate >= @From)
               AND (@To IS NULL OR s.DueDate <= @To)
             ORDER BY IsSettled, DueDate, Description
            """;
    }

    /// <summary>
    /// O que falta receber de cada venda (M22).
    ///
    /// O saldo é a subtração de sempre — o esperado em dinheiro menos o que entrou —, feita
    /// aqui pelo banco. Venda quitada some da lista sozinha, porque o saldo dela é zero.
    /// </summary>
    internal sealed class ListReceivablesQuery(int idTenant) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public override string GetSql() => """
            SELECT o.Code, 3 AS Kind, o.Description, o.Party, o.Category, o.Balance AS Amount,
                   o.DueDate, NULL AS SettledDate, 0 AS IsSettled, o.VehicleCode, o.Plate
              FROM (
                SELECT sa.Code,
                       CONCAT(v.Brand, ' ', v.Model) AS Description,
                       sa.BuyerName AS Party,
                       CASE sa.PaymentMethod
                            WHEN 1 THEN 'Dinheiro'
                            WHEN 2 THEN 'Transferência ou Pix'
                            WHEN 3 THEN 'Financiamento'
                            WHEN 4 THEN 'Cartão'
                            WHEN 5 THEN 'Troca'
                            WHEN 6 THEN 'Troca com volta'
                            ELSE 'Outro'
                       END AS Category,
                       sa.DueDate,
                       v.Code AS VehicleCode,
                       v.Plate,
                       sa.Amount - COALESCE(sa.TradeInValue, 0) - COALESCE(sa.PartnerCutAmount, 0)
                       - COALESCE((SELECT SUM(r.Amount) FROM SaleReceipt r
                                    WHERE r.IdSale = sa.Id AND r.IsActive = 1), 0) AS Balance
                  FROM Sale sa
                  INNER JOIN Vehicle v ON v.Id = sa.IdVehicle AND v.IsActive = 1
                 WHERE v.IdTenant = @IdTenant
                   AND sa.IsActive = 1
              ) o
             WHERE o.Balance > 0
             ORDER BY o.DueDate IS NULL, o.DueDate, o.Description
            """;
    }
}
