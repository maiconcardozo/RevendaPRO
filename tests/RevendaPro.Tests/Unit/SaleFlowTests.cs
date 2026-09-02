using FluentAssertions;
using Moq;
using RevendaPro.Application.Sales.Commands;
using RevendaPro.Application.Sales.Handlers;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.Handlers;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// A venda de ponta a ponta, no nível dos casos de uso: o que ela grava, o que ela recusa e
    /// o que ela deixa para trás quando é desfeita. Nada aqui toca banco nem rede.
    /// </summary>
    public class SaleFlowTests
    {
        private const int IdTenant = 7;
        private const string Chassis = "9BWZZZ377VT004251";
        private static readonly Guid ActorCode = Guid.CreateVersion7();
        private static readonly DateOnly Today = new(2026, 9, 2);

        [Fact]
        public async Task RegisteringTheSale_MovesTheCarToSold_AndDeclinesEveryOtherOffer()
        {
            var world = new World();
            var accepted = world.GivenProposal(55_000m);
            var other = world.GivenProposal(52_000m);

            await world.RegisterSale(Direct(accepted.Code, 55_000m));

            world.Vehicle.Status.Should().Be(VehicleStatus.Sold);
            accepted.Status.Should().Be(ProposalStatus.Accepted);
            other.Status.Should().Be(ProposalStatus.Declined);

            world.History.Should().ContainSingle(h => h.ToStatus == VehicleStatus.Sold)
                .Which.Reason.Should().Be("Venda registrada");
        }

        [Fact]
        public async Task ASecondSale_IsRefused()
        {
            var world = new World();

            await world.RegisterSale(Direct(null, 55_000m));

            var act = () => world.RegisterSale(Direct(null, 50_000m));

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*já tem uma venda*");
        }

        [Fact]
        public async Task ACarStillInTheWorkshop_IsRefusedToTheBuyer_AndNothingIsWritten()
        {
            var world = new World(status: VehicleStatus.InRepair);

            var act = () => world.RegisterSale(Direct(null, 55_000m));

            await act.Should().ThrowAsync<BusinessRuleException>();

            world.Sales.Verify(repository => repository.Add(It.IsAny<Sale>()), Times.Never);
            world.UnitOfWork.Verify(unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ATrade_PutsTheIncomingCarInStock_ValuedAtTheDeal()
        {
            var world = new World();

            var sale = await world.RegisterSale(Direct(null, 55_000m) with
            {
                PaymentMethod = PaymentMethod.TradeInWithCash,
                TradeInValue = 20_000m,
                TradeIn = new TradeInVehicleInput("XYZ9A88", "9BWZZZ377VT004299", "Fiat", "Argo", 2020, 2019, 64_000),
            });

            var incoming = world.Added.Should().ContainSingle().Which;

            incoming.Origin.Should().Be(VehicleOrigin.TradeIn);
            incoming.PurchasePrice.Should().Be(20_000m);
            incoming.SupplierName.Should().Be("Comprador");
            incoming.Mileage.Should().Be(64_000);

            sale.TradeInVehicleCode.Should().Be(incoming.Code);
            sale.CashAmount.Should().Be(35_000m);
            sale.Result.NetProfit.Should().Be(17_006m);

            world.History.Should().Contain(h => h.IdVehicle == incoming.Id && h.Reason!.Contains("ABC1D23"));
        }

        [Fact]
        public async Task ATradeWhosePlateIsAlreadyInTheYard_IsRefused()
        {
            var world = new World();

            world.Vehicles
                .Setup(repository => repository.IdentifierExistsAsync(
                    IdTenant, "XYZ9A88", It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var act = () => world.RegisterSale(Direct(null, 55_000m) with
            {
                PaymentMethod = PaymentMethod.TradeIn,
                TradeInValue = 55_000m,
                TradeIn = new TradeInVehicleInput("XYZ9A88", "9BWZZZ377VT004299", "Fiat", "Argo", 2020, 2019, 0),
            });

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*XYZ9A88*");
        }

        [Fact]
        public async Task UndoingTheSale_PutsTheCarBack_ReopensTheOffer_AndKeepsTheIncomingCar()
        {
            var world = new World();
            var accepted = world.GivenProposal(55_000m);

            await world.RegisterSale(Direct(accepted.Code, 55_000m) with
            {
                PaymentMethod = PaymentMethod.TradeInWithCash,
                TradeInValue = 20_000m,
                TradeIn = new TradeInVehicleInput("XYZ9A88", "9BWZZZ377VT004299", "Fiat", "Argo", 2020, 2019, 0),
            });

            await world.CancelSale("comprador desistiu");

            world.Vehicle.Status.Should().Be(VehicleStatus.ReadyForSale);
            accepted.Status.Should().Be(ProposalStatus.Open);

            world.Sales.Verify(repository => repository.Remove(It.IsAny<Sale>(), ActorCode.ToString()), Times.Once);

            // O carro da troca existe de verdade. Desfazer a venda jamais o apaga.
            world.Vehicles.Verify(
                repository => repository.Remove(It.IsAny<Vehicle>(), It.IsAny<string>()), Times.Never);

            world.History.Should().Contain(h => h.ToStatus == VehicleStatus.ReadyForSale && h.Reason!.Contains("desistiu"));
        }

        [Fact]
        public async Task SoldByHand_IsRefused_AndTheMessagePointsAtTheSale()
        {
            var world = new World();

            var act = () => new ChangeVehicleStatusHandler(world.UnitOfWork.Object, world.CurrentUser.Object)
                .Handle(new ChangeVehicleStatusCommand(world.Vehicle.Code, VehicleStatus.Sold, null), CancellationToken.None);

            (await act.Should().ThrowAsync<BusinessRuleException>())
                .WithMessage("*registre a venda*");

            world.Vehicle.Status.Should().Be(VehicleStatus.ReadyForSale);
        }

        private static RegisterSaleCommand Direct(Guid? proposalCode, decimal amount) =>
            new(VehicleCode: Guid.Empty,
                ProposalCode: proposalCode,
                Date: Today,
                Amount: amount,
                PaymentMethod: PaymentMethod.Cash,
                Channel: SaleChannel.Direct,
                PartnerStoreName: null,
                PartnerCutPercent: null,
                PartnerCutAmount: null,
                Commission: 0,
                CommissionNotes: null,
                BuyerName: "Comprador",
                BuyerDocument: null,
                BuyerPhone: null,
                TradeInValue: null,
                TradeIn: null,
                Notes: null);

        /// <summary>O Cruze da planilha, seus gastos e os repositórios que a venda toca.</summary>
        private sealed class World
        {
            private readonly List<Vehicle> yard = [];
            private readonly List<Proposal> proposals = [];
            private readonly List<Sale> sales = [];

            public World(VehicleStatus status = VehicleStatus.ReadyForSale)
            {
                Vehicle = Vehicle.Create(IdTenant, "ABC1D23", Chassis, "Chevrolet", "Cruze", 2014, 2013);
                Vehicle.Id = 42;
                Vehicle.SetPurchase(29_450m, new DateOnly(2026, 7, 3), "Leilão", PaymentMethod.BankTransfer);

                foreach (var step in Path(status))
                {
                    Vehicle.ChangeStatus(step);
                }

                yard.Add(Vehicle);

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.Id).Returns(9);
                currentUser.SetupGet(user => user.Code).Returns(ActorCode);
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                CurrentUser = currentUser;

                Vehicles = new Mock<IVehicleRepository>();
                Vehicles
                    .Setup(repository => repository.GetByCodeAsync(IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) => yard.Find(v => v.Code == code));
                Vehicles
                    .Setup(repository => repository.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int id, CancellationToken _) => yard.Find(v => v.Id == id));
                Vehicles
                    .Setup(repository => repository.IdentifierExistsAsync(
                        It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);
                Vehicles
                    .Setup(repository => repository.Add(It.IsAny<Vehicle>()))
                    .Callback((Vehicle vehicle) =>
                    {
                        vehicle.Id = 100 + yard.Count;
                        yard.Add(vehicle);
                        Added.Add(vehicle);
                    });

                // Os 21 gastos da planilha, resumidos em uma linha: 8.544 pagos.
                var expenses = new Mock<IVehicleExpenseRepository>();
                expenses
                    .Setup(repository => repository.ListByVehicleAsync(Vehicle.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyList<VehicleExpense>)[VehicleExpense.Create(Vehicle.Id, "Tudo", 1, 8_544m, Today)]);
                expenses
                    .Setup(repository => repository.ListByVehicleAsync(It.Is<int>(id => id != Vehicle.Id), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyList<VehicleExpense>)[]);

                Proposals = new Mock<IProposalRepository>();
                Proposals
                    .Setup(repository => repository.GetByCodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Guid code, CancellationToken _) => proposals.Find(p => p.Code == code));
                Proposals
                    .Setup(repository => repository.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int id, CancellationToken _) => proposals.Find(p => p.Id == id));
                Proposals
                    .Setup(repository => repository.ListByVehicleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int idVehicle, CancellationToken _) =>
                        (IReadOnlyList<Proposal>)[.. proposals.Where(p => p.IdVehicle == idVehicle)]);

                Sales = new Mock<ISaleRepository>();
                Sales
                    .Setup(repository => repository.GetByVehicleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int idVehicle, CancellationToken _) =>
                        sales.Find(s => s.IdVehicle == idVehicle && s.IsActive));
                Sales
                    .Setup(repository => repository.Add(It.IsAny<Sale>()))
                    .Callback((Sale sale) =>
                    {
                        sale.Id = sales.Count + 1;
                        sales.Add(sale);
                    });
                Sales
                    .Setup(repository => repository.Remove(It.IsAny<Sale>(), It.IsAny<string>()))
                    .Callback((Sale sale, string by) => sale.SoftDelete(by));

                var history = new Mock<IVehicleStatusHistoryRepository>();
                history
                    .Setup(repository => repository.Add(It.IsAny<VehicleStatusHistory>()))
                    .Callback((VehicleStatusHistory entry) => History.Add(entry));

                var auditLogs = new Mock<IAuditLogRepository>();

                UnitOfWork = new Mock<IUnitOfWork>();
                UnitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(Vehicles.Object);
                UnitOfWork.SetupGet(unit => unit.VehicleExpenseRepository).Returns(expenses.Object);
                UnitOfWork.SetupGet(unit => unit.ProposalRepository).Returns(Proposals.Object);
                UnitOfWork.SetupGet(unit => unit.SaleRepository).Returns(Sales.Object);
                UnitOfWork.SetupGet(unit => unit.VehicleStatusHistoryRepository).Returns(history.Object);
                UnitOfWork.SetupGet(unit => unit.AuditLogRepository).Returns(auditLogs.Object);
                UnitOfWork.Setup(unit => unit.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            }

            public Vehicle Vehicle { get; }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<ICurrentUser> CurrentUser { get; }

            public Mock<IVehicleRepository> Vehicles { get; }

            public Mock<IProposalRepository> Proposals { get; }

            public Mock<ISaleRepository> Sales { get; }

            public List<Vehicle> Added { get; } = [];

            public List<VehicleStatusHistory> History { get; } = [];

            /// <summary>Uma proposta em aberto, no dinheiro, direta.</summary>
            /// <param name="amount">O valor oferecido.</param>
            /// <returns>A proposta.</returns>
            public Proposal GivenProposal(decimal amount)
            {
                var proposal = Proposal.Create(
                    Vehicle.Id, $"Interessado {proposals.Count + 1}", null, amount, Today,
                    PaymentMethod.Cash, SaleChannel.Direct, null, null, null);

                proposal.Id = proposals.Count + 1;
                proposals.Add(proposal);

                return proposal;
            }

            /// <summary>Registra a venda do Cruze.</summary>
            /// <param name="command">O comando, com o código do veículo preenchido aqui.</param>
            /// <returns>A venda.</returns>
            public Task<Application.Sales.DTOs.SaleDto> RegisterSale(RegisterSaleCommand command) =>
                new RegisterSaleHandler(UnitOfWork.Object, CurrentUser.Object)
                    .Handle(command with { VehicleCode = Vehicle.Code }, CancellationToken.None);

            /// <summary>Desfaz a venda do Cruze.</summary>
            /// <param name="reason">O motivo.</param>
            /// <returns>Uma tarefa.</returns>
            public Task CancelSale(string reason) =>
                new CancelSaleHandler(UnitOfWork.Object, CurrentUser.Object)
                    .Handle(new CancelSaleCommand(Vehicle.Code, reason), CancellationToken.None);

            /// <summary>A esteira até o status pedido, passo a passo.</summary>
            private static IEnumerable<VehicleStatus> Path(VehicleStatus target) => target switch
            {
                VehicleStatus.InRepair => [VehicleStatus.Purchased, VehicleStatus.InRepair],
                VehicleStatus.ReadyForSale => [VehicleStatus.Purchased, VehicleStatus.ReadyForSale],
                _ => [],
            };
        }
    }
}
