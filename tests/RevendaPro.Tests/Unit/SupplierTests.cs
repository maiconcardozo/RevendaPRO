using FluentAssertions;
using Moq;
using RevendaPro.Application.Suppliers.Commands;
using RevendaPro.Application.Suppliers.Handlers;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O cadastro de quem a revenda paga.
    ///
    /// <i>"Preciso fazer a implementação de fornecedor e quanto você já gastou em cada
    /// fornecedor. Vai ser oficina, pintura, autopeças, essas coisas."</i>
    ///
    /// Fornecedor diz de quem; tipo de gasto diz o quê. O que se prova aqui é o contorno do
    /// cadastro: o ramo é da revenda, o documento é CPF ou CNPJ ou nada, e quem tem gasto no
    /// nome fica — porque apagá-lo apagaria a resposta para "quanto já foi para ele".
    /// </summary>
    public class SupplierTests
    {
        private const int IdTenant = 7;

        [Fact]
        public void ASupplierWithoutAName_IsRefused()
        {
            var act = () => Supplier.Create(IdTenant, "  ", idSupplierSegment: 1);

            act.Should().Throw<BusinessRuleException>().WithMessage("*nome do fornecedor*");
        }

        [Fact]
        public void ASupplierWithoutASegment_IsRefused()
        {
            var act = () => Supplier.Create(IdTenant, "Auto Mecânica Silva", idSupplierSegment: 0);

            act.Should().Throw<BusinessRuleException>().WithMessage("*ramo*");
        }

        [Theory]
        [InlineData("12.345.678/0001-95", "12345678000195")]
        [InlineData("123.456.789-09", "12345678909")]
        [InlineData("", null)]
        public void TheDocument_IsKeptAsDigits_OrLeftEmpty(string typed, string? stored)
        {
            var supplier = Supplier.Create(IdTenant, "Autopeças Central", 3);

            supplier.SetContact(null, "(51) 99999-0000", typed, null);

            supplier.Document.Should().Be(stored);
            supplier.ContactPhone.Should().Be("51999990000");
        }

        [Fact]
        public void ADocumentOfTheWrongSize_IsRefused()
        {
            var supplier = Supplier.Create(IdTenant, "Autopeças Central", 3);

            var act = () => supplier.SetContact(null, null, "1234567", null);

            act.Should().Throw<BusinessRuleException>().WithMessage("*11 dígitos*14*");
        }

        [Fact]
        public async Task ASegmentOfAnotherDealership_IsRefused()
        {
            var world = new World();

            var act = () => world.Save("Auto Mecânica Silva", Guid.NewGuid());

            // O código de ramo que esta revenda desconhece lê como inexistente, e jamais vira
            // ligação cruzada entre empresas.
            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*ramo desta revenda*");
        }

        [Fact]
        public async Task ASupplierWithExpenses_RefusesDeletion_AndSaysHowMany()
        {
            var world = new World();
            var supplier = world.Given("Funilaria do Zé", expenses: 12);

            var act = () => world.Delete(supplier.Code);

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*12 gastos*");
        }

        [Fact]
        public async Task ASupplierWithoutExpenses_IsDeletedLogically_AndTheDeletionIsRecorded()
        {
            var world = new World();
            var supplier = world.Given("Guincho Antigo", expenses: 0);

            await world.Delete(supplier.Code);

            world.Suppliers.Verify(
                repository => repository.Remove(supplier, It.IsAny<string>()), Times.Once);

            world.Audit.Verify(
                repository => repository.Add(It.Is<AuditLog>(log =>
                    log.EntityName == nameof(Supplier)
                    && log.RecordCode == supplier.Code
                    && log.Action == AuditAction.Delete)),
                Times.Once);
        }

        [Fact]
        public async Task ANameAlreadyInUse_IsRefused()
        {
            var world = new World();
            world.Given("Auto Mecânica Silva", expenses: 0);
            world.TheNameIsTaken();

            var act = () => world.Save("Auto Mecânica Silva", world.Workshop.Code);

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*já tem um fornecedor*");
        }

        [Fact]
        public async Task ANewSupplier_ComesBackWithItsSegmentName()
        {
            var world = new World();

            var saved = await world.Save("Auto Mecânica Silva", world.Workshop.Code);

            saved.SegmentName.Should().Be("Oficina mecânica");
            saved.ExpenseCount.Should().Be(0);
        }

        [Fact]
        public async Task ASegmentWithSuppliersInIt_RefusesDeletion()
        {
            var world = new World();
            world.Given("Auto Mecânica Silva", expenses: 0);

            var act = () => world.DeleteSegment(world.Workshop.Code);

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*1 fornecedor*");
        }

        private sealed class World
        {
            private readonly List<Supplier> suppliers = [];
            private readonly Dictionary<int, int> expensesOf = [];
            private bool nameTaken;
            private int nextId = 1;

            public World()
            {
                Workshop = SupplierSegment.Create(IdTenant, "Oficina mecânica");
                Workshop.Id = 1;

                Segments = new Mock<ISupplierSegmentRepository>();

                Segments.Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) =>
                        code == Workshop.Code ? Workshop : null);

                Segments.Setup(repository => repository.ListByTenantAsync(
                        IdTenant, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([Workshop]);

                Segments.Setup(repository => repository.CountSuppliersAsync(
                        It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int id, CancellationToken _) =>
                        suppliers.Count(supplier => supplier.IdSupplierSegment == id));

                Suppliers = new Mock<ISupplierRepository>();

                Suppliers.Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) =>
                        suppliers.FirstOrDefault(supplier => supplier.Code == code));

                Suppliers.Setup(repository => repository.CountExpensesAsync(
                        It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int id, CancellationToken _) =>
                        expensesOf.TryGetValue(id, out var count) ? count : 0);

                Suppliers.Setup(repository => repository.NameExistsAsync(
                        It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => nameTaken);

                Audit = new Mock<IAuditLogRepository>();

                UnitOfWork = new Mock<IUnitOfWork>();
                UnitOfWork.SetupGet(unit => unit.SupplierRepository).Returns(Suppliers.Object);
                UnitOfWork.SetupGet(unit => unit.SupplierSegmentRepository).Returns(Segments.Object);
                UnitOfWork.SetupGet(unit => unit.AuditLogRepository).Returns(Audit.Object);

                // A exclusão do fornecedor enxerga os dois lados desde o M22: o gasto do carro e
                // a despesa da loja. Aqui a loja está sempre vazia; o que se prova é o gasto.
                var storeExpenses = new Mock<IStoreExpenseRepository>();
                storeExpenses
                    .Setup(repository => repository.CountBySupplierAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(0);
                UnitOfWork.SetupGet(unit => unit.StoreExpenseRepository).Returns(storeExpenses.Object);

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.Id).Returns(9);
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                currentUser.SetupGet(user => user.Code).Returns(Guid.NewGuid());

                Saver = new SaveSupplierHandler(UnitOfWork.Object, currentUser.Object);
                Remover = new DeleteSupplierHandler(UnitOfWork.Object, currentUser.Object);
                SegmentRemover = new DeleteSupplierSegmentHandler(UnitOfWork.Object, currentUser.Object);
            }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<ISupplierRepository> Suppliers { get; }

            public Mock<ISupplierSegmentRepository> Segments { get; }

            public Mock<IAuditLogRepository> Audit { get; }

            public SupplierSegment Workshop { get; }

            private SaveSupplierHandler Saver { get; }

            private DeleteSupplierHandler Remover { get; }

            private DeleteSupplierSegmentHandler SegmentRemover { get; }

            public Supplier Given(string name, int expenses)
            {
                var supplier = Supplier.Create(IdTenant, name, Workshop.Id);
                supplier.Id = nextId++;

                suppliers.Add(supplier);
                expensesOf[supplier.Id] = expenses;

                return supplier;
            }

            public void TheNameIsTaken() => nameTaken = true;

            public Task<Application.Suppliers.DTOs.SupplierDto> Save(string name, Guid segmentCode) =>
                Saver.Handle(
                    new SaveSupplierCommand(null, name, segmentCode, null, null, null, null),
                    CancellationToken.None);

            public Task Delete(Guid code) =>
                Remover.Handle(new DeleteSupplierCommand(code), CancellationToken.None);

            public Task DeleteSegment(Guid code) =>
                SegmentRemover.Handle(new DeleteSupplierSegmentCommand(code), CancellationToken.None);
        }
    }
}
