using FluentAssertions;
using Moq;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.Handlers;
using RevendaPro.Application.Vehicles.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// A porta de volta do documento excluído.
    ///
    /// Desde o M6 o DELETE de um documento tira ele da ficha e mantém o arquivo no bucket, por
    /// requisito: uma revenda responde pelo que vendeu anos depois. O que se prova aqui é que
    /// a devolução existe, que ela recusa o documento de outra empresa (RNF-04) e que ela fica
    /// registrada — e que exclusão definitiva continua sem existir.
    /// </summary>
    public class DeletedDocumentTests
    {
        private const int IdTenant = 7;
        private const int OtherTenant = 8;

        [Fact]
        public async Task ADeletedDocument_GoesBackToTheFile_AndTheReturnIsRecorded()
        {
            var world = new World();
            var document = world.GivenDeletedDocument();

            await world.Restore(document.Code);

            document.IsActive.Should().BeTrue();

            world.Documents.Verify(
                repository => repository.Update(document), Times.Once);

            world.Audit.Verify(
                repository => repository.Add(It.Is<AuditLog>(log =>
                    log.EntityName == nameof(VehicleDocument)
                    && log.RecordCode == document.Code
                    && log.Action == AuditAction.Activate)),
                Times.Once);

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ADocumentOfAnotherDealership_IsRefused_AndNothingIsWritten()
        {
            var world = new World(tenantOfTheVehicle: OtherTenant);
            var document = world.GivenDeletedDocument();

            var act = () => world.Restore(document.Code);

            // O documento não carrega empresa: ele pende do veículo, e é o veículo que diz de
            // quem ele é. Ler pelo tenant é o que mantém a ficha de uma revenda fora do
            // alcance da outra.
            await act.Should().ThrowAsync<NotFoundException>();

            document.IsActive.Should().BeFalse();

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ADocumentThatIsAlreadyInTheFile_IsRefused()
        {
            var world = new World();
            var document = world.GivenDeletedDocument();

            await world.Restore(document.Code);

            var act = () => world.Restore(document.Code);

            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("*já está na ficha*");
        }

        [Fact]
        public async Task TheListing_NamesWhoDeletedIt_AndSignsTheAddressOfTheFileKept()
        {
            var world = new World();
            var ana = world.GivenUser("Ana");

            world.GivenDeletedRow(deletedBy: ana.Code.ToString());

            var listed = await world.List();

            listed.Should().ContainSingle();
            listed[0].DeletedBy.Should().Be("Ana");
            listed[0].Plate.Should().Be("ABC1D23");

            // O arquivo nunca saiu do bucket: o endereço é assinado, e jamais público (RNF-06).
            listed[0].Url.Should().StartWith("https://bucket/");
        }

        private sealed class World
        {
            private readonly List<User> people = [];
            private readonly List<DeletedVehicleDocument> deleted = [];
            private readonly Vehicle vehicle;
            private VehicleDocument? document;

            public World(int tenantOfTheVehicle = IdTenant)
            {
                vehicle = Vehicle.Create(
                    tenantOfTheVehicle, "ABC1D23", "9BWZZZ377VT004251",
                    "Chevrolet", "Cruze", 2014, 2013);
                vehicle.Id = 42;

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.Id).Returns(9);
                currentUser.SetupGet(user => user.Code).Returns(Guid.CreateVersion7());
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                CurrentUser = currentUser;

                var vehicles = new Mock<IVehicleRepository>();
                vehicles
                    .Setup(repository => repository.GetByIdAsync(42, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(vehicle);

                Documents = new Mock<IVehicleDocumentRepository>();
                Documents
                    .Setup(repository => repository.GetByCodeIncludingDeletedAsync(
                        It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Guid code, CancellationToken _) =>
                        document?.Code == code ? document : null);
                Documents
                    .Setup(repository => repository.ListDeletedByTenantAsync(
                        IdTenant, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => deleted);

                Users = new Mock<IUserRepository>();
                Users
                    .Setup(repository => repository.ListByTenantAsync(
                        IdTenant, It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => people);

                Audit = new Mock<IAuditLogRepository>();

                Storage = new Mock<IFileStorage>();
                Storage
                    .Setup(storage => storage.GetUrl(
                        It.IsAny<string>(), FileVisibility.Private, It.IsAny<TimeSpan?>()))
                    .Returns((string key, FileVisibility _, TimeSpan? _) =>
                        new Uri($"https://bucket/{key}?assinatura=abc"));

                var unitOfWork = new Mock<IUnitOfWork>();
                unitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(vehicles.Object);
                unitOfWork.SetupGet(unit => unit.VehicleDocumentRepository).Returns(Documents.Object);
                unitOfWork.SetupGet(unit => unit.UserRepository).Returns(Users.Object);
                unitOfWork.SetupGet(unit => unit.AuditLogRepository).Returns(Audit.Object);
                UnitOfWork = unitOfWork;
            }

            public Mock<IVehicleDocumentRepository> Documents { get; }

            public Mock<IUserRepository> Users { get; }

            public Mock<IAuditLogRepository> Audit { get; }

            public Mock<IFileStorage> Storage { get; }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<ICurrentUser> CurrentUser { get; }

            public VehicleDocument GivenDeletedDocument()
            {
                document = VehicleDocument.Create(
                    vehicle.Id, VehicleDocumentKind.SaleInvoice,
                    "7/vehicles/abc/nota.pdf", "nota-fiscal.pdf", "application/pdf", 193);

                document.SoftDelete("quem apagou");

                return document;
            }

            public User GivenUser(string name)
            {
                var user = User.Create(IdTenant, name, $"{name}@revenda.com.br", "hash");
                people.Add(user);

                return user;
            }

            public void GivenDeletedRow(string deletedBy) =>
                deleted.Add(new DeletedVehicleDocument(
                    Guid.CreateVersion7(),
                    VehicleDocumentKind.SaleInvoice,
                    "nota-fiscal.pdf",
                    "application/pdf",
                    193,
                    "7/vehicles/abc/nota.pdf",
                    new DateTime(2026, 8, 15, 9, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 8, 16, 9, 0, 0, DateTimeKind.Utc),
                    deletedBy,
                    vehicle.Code,
                    vehicle.Plate,
                    vehicle.Brand,
                    vehicle.Model));

            public Task Restore(Guid code) =>
                new RestoreVehicleDocumentHandler(UnitOfWork.Object, CurrentUser.Object)
                    .Handle(new RestoreVehicleDocumentCommand(code), CancellationToken.None);

            public Task<IReadOnlyList<Application.Vehicles.DTOs.DeletedDocumentDto>> List() =>
                new ListDeletedDocumentsHandler(
                        UnitOfWork.Object, CurrentUser.Object, Storage.Object)
                    .Handle(new ListDeletedDocumentsQuery(), CancellationToken.None);
        }
    }
}
