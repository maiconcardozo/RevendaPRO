using Moq;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Repositories;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// Um repositório de clientes em memória, para os testes de unidade que passam pela proposta
    /// e pela venda (M21): o handler acha ou cria o cliente, e o dublê responde como o banco
    /// responderia — o cliente adicionado ganha Id e volta pelo código, pelo telefone e pelo
    /// documento.
    /// </summary>
    public static class CustomerRepositoryDouble
    {
        /// <summary>Cria o dublê, com os clientes que já existem.</summary>
        /// <param name="existing">Clientes que já estão "no banco".</param>
        /// <returns>O mock, pronto para <c>UnitOfWork.SetupGet</c>.</returns>
        public static Mock<ICustomerRepository> Build(params Customer[] existing)
        {
            var store = existing.ToList();
            var nextId = store.Count == 0 ? 1 : store.Max(c => c.Id) + 1;

            var mock = new Mock<ICustomerRepository>();

            mock.Setup(r => r.Add(It.IsAny<Customer>()))
                .Callback((Customer customer) =>
                {
                    if (customer.Id == 0)
                    {
                        customer.Id = nextId++;
                    }

                    store.Add(customer);
                });

            mock.Setup(r => r.GetByCodeAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int tenant, Guid code, CancellationToken _) =>
                    store.FirstOrDefault(c => c.IdTenant == tenant && c.Code == code));

            mock.Setup(r => r.ListByIdsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int tenant, IReadOnlyCollection<int> ids, CancellationToken _) =>
                    (IReadOnlyList<Customer>)store.Where(c => c.IdTenant == tenant && ids.Contains(c.Id)).ToList());

            mock.Setup(r => r.FindByDocumentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int tenant, string document, int? ignore, CancellationToken _) =>
                    store.FirstOrDefault(c => c.IdTenant == tenant && c.Document == document && c.Id != ignore));

            mock.Setup(r => r.FindByPhoneAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int tenant, string phone, CancellationToken _) =>
                    (IReadOnlyList<Customer>)store.Where(c => c.IdTenant == tenant && c.Phone == phone).ToList());

            mock.Setup(r => r.FindByNameAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int tenant, string name, CancellationToken _) =>
                    (IReadOnlyList<Customer>)store.Where(c => c.IdTenant == tenant && c.Name == name).ToList());

            mock.Setup(r => r.ListByTenantAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int tenant, string? _, CancellationToken _) =>
                    (IReadOnlyList<Customer>)store.Where(c => c.IdTenant == tenant).ToList());

            mock.Setup(r => r.SummarizeByTenantAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            return mock;
        }
    }
}
