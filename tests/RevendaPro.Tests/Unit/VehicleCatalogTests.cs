using FluentAssertions;
using Moq;
using RevendaPro.Application.Authentication.Services;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.Handlers;
using RevendaPro.Application.Vehicles.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O que a listagem entrega e o que o cadastro recusa.
    ///
    /// São as duas coisas que a tela de estoque depende para existir: a capa que ela desenha
    /// em cada cartão, e a recusa da placa repetida — que é o erro mais provável de quem
    /// cadastra o mesmo carro duas vezes num pátio movimentado.
    /// </summary>
    public class VehicleCatalogTests
    {
        private const int IdTenant = 7;
        private static readonly Guid ActorCode = Guid.CreateVersion7();

        [Fact]
        public async Task ARepeatedPlate_IsRefusedBeforeAnythingIsWritten()
        {
            var world = new World();

            world.Vehicles
                .Setup(repository => repository.IdentifierExistsAsync(
                    IdTenant, "ABC1D23", It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var act = () => world.Save(NewCruze());

            (await act.Should().ThrowAsync<BusinessRuleException>())
                .WithMessage("*ABC1D23*");

            world.Vehicles.Verify(repository => repository.Add(It.IsAny<Vehicle>()), Times.Never);
            world.UnitOfWork.Verify(unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AFreePlate_IsRegistered()
        {
            var world = new World();

            var vehicle = await world.Save(NewCruze());

            vehicle.Plate.Should().Be("ABC1D23");
            vehicle.Status.Should().Be(VehicleStatus.UnderReview);

            world.Vehicles.Verify(repository => repository.Add(It.IsAny<Vehicle>()), Times.Once);
        }

        [Fact]
        public async Task TheListing_CarriesTheCoverThumbnail_AndNeverTheFullSize()
        {
            // O critério de aceite do front: uma tela de pátio com cinquenta carros não pode
            // puxar a foto cheia para preencher quadrados pequenos.
            var world = new World();
            world.GivenVehicle(id: 1, plate: "ABC1D23");
            world.GivenGallery(idVehicle: 1, photoCount: 20, coverKey: "7/vehicles/abc/foto");

            var listed = await world.List();

            listed.Should().ContainSingle();
            listed[0].PhotoCount.Should().Be(20);

            listed[0].CoverThumbnailUrl.Should().Contain("-thumbnail.webp");
            listed[0].CoverThumbnailUrl.Should().NotContain("-full.webp");
        }

        [Fact]
        public async Task ThePeriod_IsAskedOfTheDatabase_AndNeverSiftedInMemory()
        {
            // Filtrar depois de ler traz o pátio inteiro para a memória a cada consulta, e
            // ainda mente na contagem quando a listagem crescer e ganhar paginação.
            var world = new World();
            world.GivenVehicle(id: 1, plate: "ABC1D23");

            var august = new DateOnly(2026, 8, 1);
            var endOfAugust = new DateOnly(2026, 8, 31);

            await world.List(august, endOfAugust);

            world.Vehicles.Verify(
                repository => repository.ListAsync(
                    IdTenant, null, null, null, august, endOfAugust, null,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task TheYard_IsAskedOfTheDatabase_AndNeverSiftedInMemory()
        {
            var world = new World();
            world.GivenVehicle(id: 1, plate: "ABC1D23");
            var loja = world.GivenYard(id: 4, name: "Loja do Joãozinho");

            await world.List(yardCode: loja.Code);

            // Trazer o pátio inteiro para jogar fora o que não interessa é o mesmo erro que o
            // período já evita, e cresce com o estoque.
            world.Vehicles.Verify(
                repository => repository.ListAsync(
                    IdTenant, null, null, null, null, null, 4,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task AYardOfAnotherDealership_ListsNothing()
        {
            var world = new World();
            world.GivenVehicle(id: 1, plate: "ABC1D23");
            world.GivenYard(id: 4, name: "Loja do Joãozinho");

            var listed = await world.List(yardCode: Guid.NewGuid());

            // Um filtro que o cliente desconhece jamais vira "sem filtro": isso devolveria o
            // estoque inteiro para quem pediu o pátio de outra revenda.
            listed.Should().BeEmpty();

            world.Vehicles.Verify(
                repository => repository.ListAsync(
                    IdTenant, null, null, null, null, null, 0,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task AVehicleWithoutPhotos_HasNoCover()
        {
            var world = new World();
            world.GivenVehicle(id: 1, plate: "ABC1D23");

            var listed = await world.List();

            listed[0].PhotoCount.Should().Be(0);
            listed[0].CoverThumbnailUrl.Should().BeNull();
        }

        [Fact]
        public async Task TheSession_CarriesTheUploadLimit()
        {
            // A API recusa o arquivo grande por conta própria, e essa é a guarda de verdade. O
            // número viaja para que a tela recuse antes de gastar o upload inteiro.
            var world = new World();

            var session = await world.Session();

            session.Limits.MaxUploadSizeInBytes.Should().Be(7 * 1024 * 1024);
        }

        private static SaveVehicleCommand NewCruze() =>
            new(Code: null,
                Plate: "abc-1d23",
                Chassis: "9BWZZZ377VT004251",
                Brand: "Chevrolet",
                Model: "Cruze",
                Version: "LT 1.8 Hatch",
                ModelYear: 2014,
                ManufactureYear: 2013,
                Color: "Branco",
                Mileage: 118_000,
                MileageCorrection: false,
                FuelType: FuelType.Flex,
                Transmission: TransmissionType.Automatic,
                Renavam: null,
                Origin: VehicleOrigin.Auction,
                HasDamage: false,
                DamageDescription: null,
                PurchasePrice: 29_450m,
                PurchaseDate: new DateOnly(2026, 7, 3),
                SupplierName: "Leilão Copart",
                PurchasePaymentMethod: PaymentMethod.BankTransfer,
                BudgetCeiling: 40_000m,
                FipeValue: null,
                FipeReferenceDate: null,
                FipeCode: null,
                DesiredNetPrice: 58_000m,
                MinimumNetPrice: 55_000m,
                AdvertisedPrice: null,
                MarketNotes: null,
                Notes: null);

        /// <summary>
        /// Os repositórios que cercam o cadastro e a listagem, e um armazenamento de mentira
        /// que devolve endereço previsível. Nada aqui toca banco nem rede.
        /// </summary>
        private sealed class World
        {
            private readonly List<Sale> sales = [];

            public World()
            {
                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.Id).Returns(9);
                currentUser.SetupGet(user => user.Code).Returns(ActorCode);
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);

                CurrentUser = currentUser;

                Vehicles = new Mock<IVehicleRepository>();

                Yards = new Mock<IYardRepository>();

                // Quase sempre vazio: estes testes falam de custo, capa e periodo. O
                // repositorio existe aqui porque a listagem le o lugar de cada carro numa
                // consulta so, e os testes de patio enchem a lista com GivenYard.
                Yards.Setup(repository => repository.ListByTenantAsync(
                        It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => yards);

                Sales = new Mock<ISaleRepository>();

                Sales.Setup(repository => repository.ListByVehiclesAsync(
                        It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                        [.. sales.Where(sale => ids.Contains(sale.IdVehicle))]);
                Vehicles
                    .Setup(repository => repository.ListAsync(
                        IdTenant, It.IsAny<string?>(), It.IsAny<VehicleStatus?>(),
                        It.IsAny<VehicleOrigin?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                        It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                    // O duplo filtra pelo patio como o banco filtra: sem isso, um teste sobre
                    // o filtro passaria mesmo com a consulta ignorando o parametro.
                    .ReturnsAsync((
                        int _, string? _, VehicleStatus? _, VehicleOrigin? _,
                        DateOnly? _, DateOnly? _, int? idYard, CancellationToken _) =>
                        idYard is null
                            ? yard
                            : [.. yard.Where(vehicle => vehicle.IdYard == idYard)]);

                Vehicles
                    .Setup(repository => repository.IdentifierExistsAsync(
                        It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                Vehicles
                    .Setup(repository => repository.Add(It.IsAny<Vehicle>()))
                    .Callback((Vehicle vehicle) =>
                    {
                        vehicle.Id = yard.Count + 1;
                        yard.Add(vehicle);
                    });

                // O cadastro relê o veículo depois de gravar, para conhecer o Id que o banco
                // atribuiu antes de escrever o histórico.
                Vehicles
                    .Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) =>
                        yard.Find(vehicle => vehicle.Code == code));

                var expenses = new Mock<IVehicleExpenseRepository>();
                expenses
                    .Setup(repository => repository.ListByVehiclesAsync(
                        It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyList<VehicleExpense>)[]);

                expenses
                    .Setup(repository => repository.ListByVehicleAsync(
                        It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyList<VehicleExpense>)[]);

                Photos = new Mock<IVehiclePhotoRepository>();
                Photos
                    .Setup(repository => repository.SummarizeAsync(
                        It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                        (IReadOnlyList<VehicleGallery>)
                            [.. galleries.Where(g => ids.Contains(g.IdVehicle))]);

                var history = new Mock<IVehicleStatusHistoryRepository>();
                var auditLogs = new Mock<IAuditLogRepository>();

                Storage = new Mock<IFileStorage>();
                // Um número que não é o padrão de propósito: assim o teste da sessão prova
                // que o limite veio do armazenamento, e não de uma constante escondida.
                Storage.SetupGet(storage => storage.MaxSizeInBytes).Returns(7 * 1024 * 1024);
                Storage
                    .Setup(storage => storage.GetUrl(
                        It.IsAny<string>(), It.IsAny<FileVisibility>(), It.IsAny<TimeSpan?>()))
                    .Returns((string key, FileVisibility _, TimeSpan? _) =>
                        new Uri($"https://exemplo.invalid/{key}?assinatura=x"));

                UnitOfWork = new Mock<IUnitOfWork>();
                UnitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(Vehicles.Object);
                UnitOfWork.SetupGet(unit => unit.SaleRepository).Returns(Sales.Object);
                UnitOfWork.SetupGet(unit => unit.YardRepository).Returns(Yards.Object);
                UnitOfWork.SetupGet(unit => unit.VehicleExpenseRepository).Returns(expenses.Object);
                UnitOfWork.SetupGet(unit => unit.VehiclePhotoRepository).Returns(Photos.Object);
                UnitOfWork.SetupGet(unit => unit.VehicleStatusHistoryRepository).Returns(history.Object);
                UnitOfWork.SetupGet(unit => unit.AuditLogRepository).Returns(auditLogs.Object);
                UnitOfWork
                    .Setup(unit => unit.CommitAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);
            }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<ICurrentUser> CurrentUser { get; }

            public Mock<IVehicleRepository> Vehicles { get; }

            public Mock<ISaleRepository> Sales { get; }

            public Mock<IYardRepository> Yards { get; }

            public Mock<IVehiclePhotoRepository> Photos { get; }

            public Mock<IFileStorage> Storage { get; }

            private readonly List<Vehicle> yard = [];

            private readonly List<Yard> yards = [];

            private readonly List<VehicleGallery> galleries = [];

            /// <summary>Põe um veículo no pátio.</summary>
            /// <param name="id">Identificador interno.</param>
            /// <param name="plate">Placa.</param>
            public void GivenVehicle(int id, string plate)
            {
                var vehicle = Vehicle.Create(
                    IdTenant, plate, "9BWZZZ377VT004251", "Chevrolet", "Cruze", 2014, 2013);

                vehicle.Id = id;
                yard.Add(vehicle);
            }

            /// <summary>Cadastra um pátio.</summary>
            /// <param name="id">Identificador interno.</param>
            /// <param name="name">Nome do pátio.</param>
            /// <returns>O pátio.</returns>
            public Yard GivenYard(int id, string name)
            {
                var yard = Yard.Create(IdTenant, name, YardKind.Own);
                yard.Id = id;

                yards.Add(yard);

                return yard;
            }

            /// <summary>Diz quantas fotos o veículo tem, e qual é a capa.</summary>
            /// <param name="idVehicle">O veículo.</param>
            /// <param name="photoCount">Quantidade de fotos.</param>
            /// <param name="coverKey">Prefixo da capa.</param>
            public void GivenGallery(int idVehicle, int photoCount, string coverKey) =>
                galleries.Add(new VehicleGallery(idVehicle, photoCount, coverKey));

            /// <summary>Cadastra ou edita um veículo.</summary>
            /// <param name="command">O comando.</param>
            /// <returns>O veículo salvo.</returns>
            public Task<Application.Vehicles.DTOs.VehicleDto> Save(SaveVehicleCommand command) =>
                new SaveVehicleHandler(UnitOfWork.Object, CurrentUser.Object, Storage.Object)
                    .Handle(command, CancellationToken.None);

            /// <summary>Lê a listagem.</summary>
            /// <returns>Os veículos.</returns>
            public Task<IReadOnlyList<Application.Vehicles.DTOs.VehicleDto>> List(
                DateOnly? from = null,
                DateOnly? to = null,
                Guid? yardCode = null) =>
                new ListVehiclesHandler(UnitOfWork.Object, CurrentUser.Object, Storage.Object)
                    .Handle(
                        new ListVehiclesQuery(null, null, null, from, to, yardCode),
                        CancellationToken.None);

            /// <summary>Monta a sessão de quem está autenticado.</summary>
            /// <returns>A sessão.</returns>
            public Task<Application.Authentication.DTOs.SessionDto> Session()
            {
                var user = User.Create(IdTenant, "Administrador", "admin@revendapro.local", "hash");
                user.Id = 9;

                var users = new Mock<IUserRepository>();
                users
                    .Setup(repository => repository.GetByIdAsync(9, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(user);

                users
                    .Setup(repository => repository.GetScreenKeysAsync(9, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyList<string>)[]);

                users
                    .Setup(repository => repository.GetRoleIdsAsync(9, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyList<int>)[]);

                var screens = new Mock<IScreenRepository>();
                screens
                    .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyList<Screen>)[]);

                var roles = new Mock<IRoleRepository>();
                roles
                    .Setup(repository => repository.GetByIdsAsync(
                        It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyList<Role>)[]);

                UnitOfWork.SetupGet(unit => unit.UserRepository).Returns(users.Object);
                UnitOfWork.SetupGet(unit => unit.ScreenRepository).Returns(screens.Object);
                UnitOfWork.SetupGet(unit => unit.RoleRepository).Returns(roles.Object);

                return new SessionBuilder(UnitOfWork.Object, Storage.Object).BuildAsync(9);
            }
        }
    }
}
