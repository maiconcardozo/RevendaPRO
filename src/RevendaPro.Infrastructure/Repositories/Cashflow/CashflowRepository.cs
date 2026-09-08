using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Infrastructure.Queries.Cashflow;

namespace RevendaPro.Infrastructure.Repositories.Cashflow
{
    /// <summary>
    /// A leitura do caixa (M22): junta o gasto do carro, a despesa da loja e o que falta receber.
    ///
    /// Herda de <see cref="DapperRepository{TEntity}"/> por <see cref="StoreExpense"/> só para
    /// reaproveitar a execução de consulta; escrita nenhuma passa por aqui — quem grava é o
    /// repositório de cada entidade, onde a regra dela mora.
    /// </summary>
    public class CashflowRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<StoreExpense>(unitOfWork), ICashflowRepository
    {
        /// <inheritdoc/>
        public async Task<CashflowSummary> ReadSummaryAsync(
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            DateOnly today,
            CancellationToken cancellationToken = default)
        {
            var rows = await QueryColumnAsync<SummaryRow>(
                new ReadCashflowSummaryQuery(idTenant, from, to, today), cancellationToken)
                .ConfigureAwait(false);

            var row = rows.FirstOrDefault() ?? new SummaryRow();

            return new CashflowSummary(
                row.PayableOpen,
                row.PayableOverdue,
                row.PayableDueSoon,
                row.ReceivableOpen,
                row.ReceivableOverdue,
                row.PaidInPeriod,
                row.ReceivedInPeriod);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<CashflowLine>> ListPayablesAsync(
            int idTenant,
            bool? settled,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default)
        {
            var rows = await QueryColumnAsync<LineRow>(
                new ListPayablesQuery(idTenant, settled, from, to), cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(ToLine)];
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<CashflowLine>> ListReceivablesAsync(
            int idTenant,
            CancellationToken cancellationToken = default)
        {
            var rows = await QueryColumnAsync<LineRow>(
                new ListReceivablesQuery(idTenant), cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(ToLine)];
        }

        private static CashflowLine ToLine(LineRow row) =>
            new(row.Code,
                (CashflowKind)row.Kind,
                row.Description,
                row.Party,
                row.Category,
                row.Amount,
                Day(row.DueDate),
                Day(row.SettledDate),
                row.IsSettled,
                row.VehicleCode,
                row.Plate);

        private static DateOnly? Day(DateTime? moment) =>
            moment is { } value ? DateOnly.FromDateTime(value) : null;

        // Propriedades graváveis, e não records posicionais: o driver devolve SUM como decimal,
        // DATE como DateTime e a soma de dois SELECT como decimal, e o Dapper converte ao
        // atribuir, jamais ao construir.
        private sealed class SummaryRow
        {
            public decimal PayableOpen { get; set; }

            public decimal PayableOverdue { get; set; }

            public decimal PayableDueSoon { get; set; }

            public decimal ReceivableOpen { get; set; }

            public decimal ReceivableOverdue { get; set; }

            public decimal PaidInPeriod { get; set; }

            public decimal ReceivedInPeriod { get; set; }
        }

        private sealed class LineRow
        {
            public Guid Code { get; set; }

            public int Kind { get; set; }

            public string Description { get; set; } = string.Empty;

            public string? Party { get; set; }

            public string? Category { get; set; }

            public decimal Amount { get; set; }

            public DateTime? DueDate { get; set; }

            public DateTime? SettledDate { get; set; }

            public bool IsSettled { get; set; }

            public Guid? VehicleCode { get; set; }

            public string? Plate { get; set; }
        }
    }
}
