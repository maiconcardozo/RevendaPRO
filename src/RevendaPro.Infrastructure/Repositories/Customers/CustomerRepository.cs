using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Infrastructure.Queries.Customers;

namespace RevendaPro.Infrastructure.Repositories.Customers
{
    /// <summary>Dapper repository for <see cref="Customer"/>.</summary>
    public class CustomerRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<Customer>(unitOfWork), ICustomerRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<Customer>> ListByTenantAsync(
            int idTenant,
            string? search,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListCustomersByTenantQuery(idTenant, search), cancellationToken);

        /// <inheritdoc/>
        public Task<Customer?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindCustomerByCodeQuery(idTenant, code), cancellationToken);

        /// <inheritdoc/>
        public Task<IReadOnlyList<Customer>> ListByIdsAsync(
            int idTenant,
            IReadOnlyCollection<int> ids,
            CancellationToken cancellationToken = default) =>
            ids.Count == 0
                ? Task.FromResult<IReadOnlyList<Customer>>([])
                : QueryAsync(new ListCustomersByIdsQuery(idTenant, ids), cancellationToken);

        /// <inheritdoc/>
        public Task<Customer?> FindByDocumentAsync(
            int idTenant,
            string document,
            int? ignoreId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(document);

            return QuerySingleAsync(
                new FindCustomerByDocumentQuery(idTenant, document, ignoreId), cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<Customer>> FindByPhoneAsync(
            int idTenant,
            string phone,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(phone);

            return QueryAsync(new FindCustomersByPhoneQuery(idTenant, phone), cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<Customer>> FindByNameAsync(
            int idTenant,
            string name,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return QueryAsync(new FindCustomersByNameQuery(idTenant, name.Trim()), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<CustomerSummary>> SummarizeByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default)
        {
            var rows = await QueryColumnAsync<SummaryRow>(
                new SummarizeCustomersQuery(idTenant), cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(row => new CustomerSummary(
                row.IdCustomer,
                (int)row.ProposalCount,
                (int)row.SaleCount,
                row.BoughtTotal,
                Day(row.LastDate)))];
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<CustomerProposalLine>> ListProposalsAsync(
            int idTenant,
            int idCustomer,
            CancellationToken cancellationToken = default)
        {
            var rows = await QueryColumnAsync<ProposalRow>(
                new ListProposalsOfCustomerQuery(idTenant, idCustomer), cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(row => new CustomerProposalLine(
                row.Code,
                DateOnly.FromDateTime(row.Date),
                row.Amount,
                row.Status,
                row.PaymentMethod,
                row.VehicleCode,
                row.Plate,
                row.Brand,
                row.Model,
                row.Version,
                row.ModelYear))];
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<CustomerSaleLine>> ListSalesAsync(
            int idTenant,
            int idCustomer,
            CancellationToken cancellationToken = default)
        {
            var rows = await QueryColumnAsync<SaleRow>(
                new ListSalesOfCustomerQuery(idTenant, idCustomer), cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(row => new CustomerSaleLine(
                row.Code,
                DateOnly.FromDateTime(row.Date),
                row.Amount,
                row.PaymentMethod,
                row.HadTradeIn,
                row.VehicleCode,
                row.Plate,
                row.Brand,
                row.Model,
                row.Version,
                row.ModelYear))];
        }

        private static DateOnly? Day(DateTime? moment) =>
            moment is { } value ? DateOnly.FromDateTime(value) : null;

        // Linhas com propriedades graváveis, e não records posicionais: o driver devolve COUNT
        // como long e DATE como DateTime, e o Dapper converte ao atribuir, jamais ao construir.

        private sealed class SummaryRow
        {
            public int IdCustomer { get; set; }

            public long ProposalCount { get; set; }

            public long SaleCount { get; set; }

            public decimal BoughtTotal { get; set; }

            public DateTime? LastDate { get; set; }
        }

        private sealed class ProposalRow
        {
            public Guid Code { get; set; }

            public DateTime Date { get; set; }

            public decimal Amount { get; set; }

            public int Status { get; set; }

            public int PaymentMethod { get; set; }

            public Guid VehicleCode { get; set; }

            public string Plate { get; set; } = string.Empty;

            public string Brand { get; set; } = string.Empty;

            public string Model { get; set; } = string.Empty;

            public string? Version { get; set; }

            public int ModelYear { get; set; }
        }

        private sealed class SaleRow
        {
            public Guid Code { get; set; }

            public DateTime Date { get; set; }

            public decimal Amount { get; set; }

            public int PaymentMethod { get; set; }

            public bool HadTradeIn { get; set; }

            public Guid VehicleCode { get; set; }

            public string Plate { get; set; } = string.Empty;

            public string Brand { get; set; } = string.Empty;

            public string Model { get; set; } = string.Empty;

            public string? Version { get; set; }

            public int ModelYear { get; set; }
        }
    }
}
