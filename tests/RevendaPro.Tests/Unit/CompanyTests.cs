using FluentAssertions;
using Moq;
using RevendaPro.Application.Company.Commands;
using RevendaPro.Application.Company.Handlers;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// Os dados da revenda, que os documentos imprimem em cima (M19).
    ///
    /// Um papel que sai da loja precisa dizer de quem é. O que se prova aqui é o contorno: o
    /// documento é CPF ou CNPJ ou nada, telefone e documento ficam só com dígitos, e o
    /// handler só toca na revenda de quem está logado.
    /// </summary>
    public class CompanyTests
    {
        [Theory]
        [InlineData("12.345.678/0001-95", "12345678000195")]
        [InlineData("123.456.789-09", "12345678909")]
        [InlineData("", null)]
        public void TheDocument_IsKeptAsDigits_OrLeftEmpty(string typed, string? stored)
        {
            var tenant = Tenant.Create("Revenda do Zé");

            tenant.SetDetails(typed, "(51) 3333-4444", "Contato@Revenda.com", "  Rua A, 10  ");

            tenant.Document.Should().Be(stored);
            tenant.Phone.Should().Be("5133334444");
            tenant.Email.Should().Be("contato@revenda.com");
            tenant.Address.Should().Be("Rua A, 10");
        }

        [Fact]
        public void ADocumentOfTheWrongSize_IsRefused()
        {
            var tenant = Tenant.Create("Revenda do Zé");

            var act = () => tenant.SetDetails("1234", null, null, null);

            act.Should().Throw<BusinessRuleException>().WithMessage("*11 dígitos*14*");
        }

        [Fact]
        public async Task SavingTheCompany_WritesTheTenantOfTheToken_AndNothingElse()
        {
            var tenant = Tenant.Create("Revenda Piloto");
            tenant.Id = 7;

            var tenants = new Mock<ITenantRepository>();
            tenants.Setup(r => r.FindAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.SetupGet(u => u.TenantRepository).Returns(tenants.Object);
            unitOfWork.SetupGet(u => u.AuditLogRepository).Returns(new Mock<IAuditLogRepository>().Object);

            var currentUser = new Mock<ICurrentUser>();
            currentUser.SetupGet(u => u.IdTenant).Returns(7);
            currentUser.SetupGet(u => u.Id).Returns(1);
            currentUser.SetupGet(u => u.Code).Returns(Guid.NewGuid());

            var handler = new SaveCompanyHandler(unitOfWork.Object, currentUser.Object);

            var saved = await handler.Handle(
                new SaveCompanyCommand("Revenda do Zé", "12.345.678/0001-95", "51 99999-0000", null, "Rua A, 10"),
                CancellationToken.None);

            saved.Name.Should().Be("Revenda do Zé");
            saved.Document.Should().Be("12345678000195");
            tenants.Verify(r => r.Update(tenant), Times.Once);
            unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
