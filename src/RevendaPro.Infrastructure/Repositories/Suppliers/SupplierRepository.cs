using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Infrastructure.Queries.Suppliers;

namespace RevendaPro.Infrastructure.Repositories.Suppliers
{
    /// <summary>Dapper repository for <see cref="Supplier"/>.</summary>
    public class SupplierRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<Supplier>(unitOfWork), ISupplierRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<Supplier>> ListByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListSuppliersByTenantQuery(idTenant), cancellationToken);

        /// <inheritdoc/>
        public Task<Supplier?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindSupplierByCodeQuery(idTenant, code), cancellationToken);

        /// <inheritdoc/>
        public Task<int> CountExpensesAsync(
            int idSupplier,
            CancellationToken cancellationToken = default) =>
            ExecuteScalarAsync<int>(new CountExpensesOfSupplierQuery(idSupplier), cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> NameExistsAsync(
            int idTenant,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var count = await ExecuteScalarAsync<int>(
                new SupplierNameExistsQuery(idTenant, name.Trim(), ignoreId), cancellationToken)
                .ConfigureAwait(false);

            return count > 0;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<SupplierSpend>> SumByTenantAsync(
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default)
        {
            var rows = await QueryColumnAsync<SpendRow>(
                new SumExpensesBySupplierQuery(idTenant, from, to), cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(row => new SupplierSpend(
                row.IdSupplier,
                row.PaidTotal,
                row.PlannedTotal,
                (int)row.ExpenseCount,
                Day(row.LastDate)))];
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<SupplierExpenseLine>> ListExpensesAsync(
            int idTenant,
            int idSupplier,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default)
        {
            var rows = await QueryColumnAsync<ExpenseLineRow>(
                new ListExpensesOfSupplierQuery(idTenant, idSupplier, from, to), cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(row => new SupplierExpenseLine(
                row.Code,
                DateOnly.FromDateTime(row.Date),
                row.Description,
                row.Amount,
                row.IsPaid,
                row.IdExpenseType,
                row.VehicleCode,
                row.Plate,
                row.Brand,
                row.Model,
                row.Version,
                row.ModelYear))];
        }

        private static DateOnly? Day(DateTime? moment) =>
            moment is null ? null : DateOnly.FromDateTime(moment.Value);

        /// <summary>
        /// The row as the driver hands it over: <c>COUNT</c> arrives as <c>Int64</c>, <c>MAX</c>
        /// of a date as <c>DateTime</c>, and <c>SUM</c> of a decimal as decimal. Settable
        /// properties rather than a positional record, so Dapper maps by name and converts the
        /// flag and the counts instead of demanding the exact type of each column — the lesson
        /// the timeline of the M10 and the market of the M11 taught, learned the other way.
        /// </summary>
        private sealed class SpendRow
        {
            public int IdSupplier { get; set; }

            public decimal PaidTotal { get; set; }

            public decimal PlannedTotal { get; set; }

            public long ExpenseCount { get; set; }

            public DateTime? LastDate { get; set; }
        }

        /// <summary>One expense line as the driver hands it over; see <see cref="SpendRow"/>.</summary>
        private sealed class ExpenseLineRow
        {
            public Guid Code { get; set; }

            public DateTime Date { get; set; }

            public string Description { get; set; } = string.Empty;

            public decimal Amount { get; set; }

            public bool IsPaid { get; set; }

            public int IdExpenseType { get; set; }

            public Guid VehicleCode { get; set; }

            public string Plate { get; set; } = string.Empty;

            public string Brand { get; set; } = string.Empty;

            public string Model { get; set; } = string.Empty;

            public string? Version { get; set; }

            public short ModelYear { get; set; }
        }
    }

    /// <summary>Dapper repository for <see cref="SupplierSegment"/>.</summary>
    public class SupplierSegmentRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<SupplierSegment>(unitOfWork), ISupplierSegmentRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<SupplierSegment>> ListByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListSupplierSegmentsByTenantQuery(idTenant), cancellationToken);

        /// <inheritdoc/>
        public Task<SupplierSegment?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindSupplierSegmentByCodeQuery(idTenant, code), cancellationToken);

        /// <inheritdoc/>
        public Task<int> CountSuppliersAsync(
            int idSupplierSegment,
            CancellationToken cancellationToken = default) =>
            ExecuteScalarAsync<int>(new CountSuppliersInSegmentQuery(idSupplierSegment), cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> NameExistsAsync(
            int idTenant,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var count = await ExecuteScalarAsync<int>(
                new SupplierSegmentNameExistsQuery(idTenant, name.Trim(), ignoreId), cancellationToken)
                .ConfigureAwait(false);

            return count > 0;
        }
    }
}
