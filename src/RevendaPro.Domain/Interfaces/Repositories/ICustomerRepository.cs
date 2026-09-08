using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Quem a revenda conhece: quem ofereceu, quem comprou, quem volta (M21).
    /// </summary>
    public interface ICustomerRepository : IDapperRepository<Customer>
    {
        /// <summary>
        /// Os clientes de uma revenda, por nome, filtrados por um trecho do nome, do telefone ou
        /// do documento. O filtro é do banco: uma loja com mil clientes digita "Mar" e recebe
        /// os Marcos, e não a lista inteira para peneirar aqui.
        /// </summary>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="search">Trecho a procurar. Nulo ou vazio traz todos.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os clientes.</returns>
        Task<IReadOnlyList<Customer>> ListByTenantAsync(
            int idTenant,
            string? search,
            CancellationToken cancellationToken = default);

        /// <summary>Acha um cliente pelo código público.</summary>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O cliente, ou nulo.</returns>
        Task<Customer?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default);

        /// <summary>Vários clientes de uma vez, pelos Ids, para a listagem desenhar o nome sem uma ida por linha.</summary>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="ids">Os Ids.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os clientes ativos entre esses Ids.</returns>
        Task<IReadOnlyList<Customer>> ListByIdsAsync(
            int idTenant,
            IReadOnlyCollection<int> ids,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// O cliente com esse documento, se houver. Documento igual é a única duplicidade que o
        /// sistema recusa: dois CPFs iguais são a mesma pessoa.
        /// </summary>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="document">CPF ou CNPJ, só dígitos.</param>
        /// <param name="ignoreId">Cliente a deixar de fora, ao editar.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O cliente, ou nulo.</returns>
        Task<Customer?> FindByDocumentAsync(
            int idTenant,
            string document,
            int? ignoreId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Os clientes com esse telefone. É lista porque o telefone da loja parceira pode estar
        /// em mais de um comprador; quem chama decide o que fazer com mais de um.
        /// </summary>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="phone">Telefone, só dígitos.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os clientes, do mais antigo para o mais novo.</returns>
        Task<IReadOnlyList<Customer>> FindByPhoneAsync(
            int idTenant,
            string phone,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Os clientes com exatamente esse nome. Existe para o aproveitamento da primeira subida
        /// casar a proposta sem telefone com quem já existe, e para nada mais: nome igual jamais
        /// é recusa, porque há muitos Joões.
        /// </summary>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="name">O nome, como foi digitado.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os clientes, do mais antigo para o mais novo.</returns>
        Task<IReadOnlyList<Customer>> FindByNameAsync(
            int idTenant,
            string name,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Quantas propostas e quantas compras cada cliente da revenda tem, e quanto comprou,
        /// somado pelo banco. Uma linha por cliente com história; nenhuma para quem só foi
        /// cadastrado.
        /// </summary>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O resumo por cliente.</returns>
        Task<IReadOnlyList<CustomerSummary>> SummarizeByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default);

        /// <summary>As propostas de um cliente, com o carro de cada uma, da mais recente para a mais antiga.</summary>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="idCustomer">O cliente.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>As linhas.</returns>
        Task<IReadOnlyList<CustomerProposalLine>> ListProposalsAsync(
            int idTenant,
            int idCustomer,
            CancellationToken cancellationToken = default);

        /// <summary>As compras de um cliente, com o carro de cada uma, da mais recente para a mais antiga.</summary>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="idCustomer">O cliente.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>As linhas.</returns>
        Task<IReadOnlyList<CustomerSaleLine>> ListSalesAsync(
            int idTenant,
            int idCustomer,
            CancellationToken cancellationToken = default);
    }

    /// <summary>O resumo de um cliente: quantas propostas, quantas compras, quanto comprou.</summary>
    /// <param name="IdCustomer">O cliente.</param>
    /// <param name="ProposalCount">Quantas propostas ele fez, de toda situação.</param>
    /// <param name="SaleCount">Quantos carros comprou.</param>
    /// <param name="BoughtTotal">Quanto pagou pelos carros, somado.</param>
    /// <param name="LastDate">A data da proposta ou compra mais recente.</param>
    public sealed record CustomerSummary(
        int IdCustomer,
        int ProposalCount,
        int SaleCount,
        decimal BoughtTotal,
        DateOnly? LastDate);

    /// <summary>Uma proposta na ficha do cliente.</summary>
    /// <param name="Code">Código da proposta.</param>
    /// <param name="Date">Quando.</param>
    /// <param name="Amount">Quanto ofereceu.</param>
    /// <param name="Status">A situação, como o enum de proposta.</param>
    /// <param name="PaymentMethod">Como pagaria.</param>
    /// <param name="VehicleCode">Código do carro.</param>
    /// <param name="Plate">Placa.</param>
    /// <param name="Brand">Marca.</param>
    /// <param name="Model">Modelo.</param>
    /// <param name="Version">Versão, quando há.</param>
    /// <param name="ModelYear">Ano do modelo.</param>
    public sealed record CustomerProposalLine(
        Guid Code,
        DateOnly Date,
        decimal Amount,
        int Status,
        int PaymentMethod,
        Guid VehicleCode,
        string Plate,
        string Brand,
        string Model,
        string? Version,
        int ModelYear);

    /// <summary>Uma compra na ficha do cliente.</summary>
    /// <param name="Code">Código da venda.</param>
    /// <param name="Date">Quando.</param>
    /// <param name="Amount">Por quanto saiu.</param>
    /// <param name="PaymentMethod">Como pagou.</param>
    /// <param name="HadTradeIn">Se entrou carro na troca.</param>
    /// <param name="VehicleCode">Código do carro.</param>
    /// <param name="Plate">Placa.</param>
    /// <param name="Brand">Marca.</param>
    /// <param name="Model">Modelo.</param>
    /// <param name="Version">Versão, quando há.</param>
    /// <param name="ModelYear">Ano do modelo.</param>
    public sealed record CustomerSaleLine(
        Guid Code,
        DateOnly Date,
        decimal Amount,
        int PaymentMethod,
        bool HadTradeIn,
        Guid VehicleCode,
        string Plate,
        string Brand,
        string Model,
        string? Version,
        int ModelYear);
}
