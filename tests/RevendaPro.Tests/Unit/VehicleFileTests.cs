using FluentAssertions;
using MediatR;
using Moq;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.Handlers;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// Fotos e documentos: o que entra no bucket, como é endereçado, e o que acontece com os
    /// bytes quando a linha é excluída.
    ///
    /// A assimetria entre foto e documento é a regra mais importante deste arquivo, e a mais
    /// fácil de alguém desfazer sem perceber ao "uniformizar" a exclusão. Por isso ela tem
    /// teste dos dois lados.
    /// </summary>
    public class VehicleFileTests
    {
        private const int IdTenant = 7;
        private const int IdVehicle = 42;
        private static readonly Guid ActorCode = Guid.CreateVersion7();

        [Theory]
        [InlineData("application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 })]
        [InlineData("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 })]
        [InlineData("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
        public void DocumentFormats_TheRnf09Accepts(string expected, byte[] content) =>
            ImageFormats.DetectDocument(content).Should().Be(expected);

        [Fact]
        public void AWebPIsAnImage_AndStillNoDocument()
        {
            // RIFF....WEBP
            byte[] webp =
            [
                0x52, 0x49, 0x46, 0x46, 0x20, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50
            ];

            ImageFormats.Detect(webp).Should().Be("image/webp");
            ImageFormats.DetectDocument(webp).Should().BeEmpty();
        }

        [Fact]
        public async Task DeletingAPhoto_TakesTheBytesWithIt()
        {
            var world = new World();
            var photo = world.GivenPhoto();

            await world.Send(new DeleteVehiclePhotoCommand(world.Vehicle.Code, photo.Code));

            world.Photos.Verify(
                repository => repository.Remove(photo, ActorCode.ToString()), Times.Once);

            world.Storage.Verify(
                storage => storage.DeleteByPrefixAsync(
                    photo.StorageKey, FileVisibility.Private, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeletingADocument_LeavesTheFileInTheStoreForever()
        {
            // Nota fiscal, CRV, papel de leilão e recibo são prova. Quem arruma uma tela não é
            // quem decide destruir prova, e os dois jamais podem ser o mesmo clique.
            var world = new World();
            var document = world.GivenDocument();

            await world.Send(new DeleteVehicleDocumentCommand(world.Vehicle.Code, document.Code));

            world.Documents.Verify(
                repository => repository.Remove(document, ActorCode.ToString()), Times.Once);

            world.Storage.Verify(
                storage => storage.DeleteByPrefixAsync(
                    It.IsAny<string>(), It.IsAny<FileVisibility>(), It.IsAny<CancellationToken>()),
                Times.Never);

            world.Storage.Verify(
                storage => storage.DeleteAsync(
                    It.IsAny<string>(), It.IsAny<FileVisibility>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task DeletingADocument_LeavesATrailInTheAudit()
        {
            var world = new World();
            var document = world.GivenDocument();

            await world.Send(new DeleteVehicleDocumentCommand(world.Vehicle.Code, document.Code));

            world.AuditLogs.Should().ContainSingle()
                .Which.Action.Should().Be(AuditAction.Delete);
        }

        [Fact]
        public async Task TheKeyOfADocument_StartsWithTheTenant()
        {
            // O tenant vem primeiro para que apagar tudo de uma empresa, ou aplicar uma regra
            // de ciclo de vida a ela, seja operação de prefixo. RNF-04 e ADR-0004.
            var world = new World();

            await world.Send(new UploadVehicleDocumentCommand(
                world.Vehicle.Code, VehicleDocumentKind.SaleInvoice, "nota.pdf", Pdf()));

            world.SavedKeys.Should().ContainSingle()
                .Which.Should().StartWith($"{IdTenant}/vehicles/{world.Vehicle.Code}/documents/")
                .And.EndWith(".pdf");
        }

        [Fact]
        public async Task ADocumentGoesToThePrivateBucket()
        {
            var world = new World();

            await world.Send(new UploadVehicleDocumentCommand(
                world.Vehicle.Code, VehicleDocumentKind.PersonalDocument, "cnh.pdf", Pdf()));

            world.SavedVisibilities.Should().AllBeEquivalentTo(FileVisibility.Private);
        }

        [Fact]
        public async Task WhatIsNeitherPdfNorImage_NeverReachesTheStore()
        {
            var world = new World();

            // "MZ": um executável renomeado para .pdf continua sendo um executável.
            var content = new MemoryStream([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00]);

            var act = () => world.Send(new UploadVehicleDocumentCommand(
                world.Vehicle.Code, VehicleDocumentKind.Other, "contrato.pdf", content));

            await act.Should().ThrowAsync<BusinessRuleException>();

            world.SavedKeys.Should().BeEmpty();
        }

        [Fact]
        public async Task AnEmptyFile_IsRefused()
        {
            var world = new World();

            var act = () => world.Send(new UploadVehicleDocumentCommand(
                world.Vehicle.Code, VehicleDocumentKind.Other, "vazio.pdf", new MemoryStream()));

            await act.Should().ThrowAsync<BusinessRuleException>();
        }

        [Fact]
        public async Task AVeryLongName_IsCutToWhatTheColumnHolds()
        {
            // A coluna guarda 160. Um nome maior tem que virar rótulo legível, e jamais um
            // erro de banco no meio do upload.
            var world = new World();
            var name = new string('a', 400) + ".pdf";

            await world.Send(new UploadVehicleDocumentCommand(
                world.Vehicle.Code, VehicleDocumentKind.Other, name, Pdf()));

            world.AddedDocuments.Should().ContainSingle()
                .Which.FileName.Length.Should().Be(160);
        }

        [Fact]
        public async Task AWholePath_BecomesJustTheFileName()
        {
            var world = new World();

            await world.Send(new UploadVehicleDocumentCommand(
                world.Vehicle.Code, VehicleDocumentKind.SaleInvoice,
                @"C:\Users\revenda\Documentos\nota fiscal.pdf", Pdf()));

            world.AddedDocuments.Should().ContainSingle()
                .Which.FileName.Should().Be("nota fiscal.pdf");
        }

        [Fact]
        public async Task ADocumentOfAnotherVehicle_IsOutOfReach()
        {
            var world = new World();
            var document = world.GivenDocument(idVehicle: IdVehicle + 1);

            var act = () => world.Send(
                new DeleteVehicleDocumentCommand(world.Vehicle.Code, document.Code));

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task APhotoOfAnotherVehicle_IsOutOfReach()
        {
            var world = new World();
            var photo = world.GivenPhoto(idVehicle: IdVehicle + 1);

            var act = () => world.Send(
                new DeleteVehiclePhotoCommand(world.Vehicle.Code, photo.Code));

            await act.Should().ThrowAsync<NotFoundException>();

            world.Storage.Verify(
                storage => storage.DeleteByPrefixAsync(
                    It.IsAny<string>(), It.IsAny<FileVisibility>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task DeletingTheCover_PromotesTheNextPhoto()
        {
            var world = new World();
            var cover = world.GivenPhoto(id: 1);
            var other = world.GivenPhoto(id: 2);

            world.Vehicle.SetCoverPhoto(cover.Id);

            await world.Send(new DeleteVehiclePhotoCommand(world.Vehicle.Code, cover.Code));

            world.Vehicle.IdCoverPhoto.Should().Be(other.Id);
        }

        [Fact]
        public async Task TheVehicleNeverSitsWithoutACoverWhileAPhotoExists()
        {
            var world = new World();

            world.Vehicle.IdCoverPhoto.Should().BeNull();

            world.Images
                .Setup(processor => processor.ProcessAsync(
                    It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ThreeRenditions());

            await world.Send(new UploadVehiclePhotoCommand(
                world.Vehicle.Code, VehiclePhotoKind.Finished, new MemoryStream([1, 2, 3])));

            world.Vehicle.IdCoverPhoto.Should().NotBeNull();
        }

        [Fact]
        public async Task OnePhoto_TakesThreeAddresses()
        {
            var world = new World();

            world.Images
                .Setup(processor => processor.ProcessAsync(
                    It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ThreeRenditions());

            await world.Send(new UploadVehiclePhotoCommand(
                world.Vehicle.Code, VehiclePhotoKind.Damage, new MemoryStream([1, 2, 3])));

            world.SavedKeys.Should().HaveCount(3);
            world.SavedKeys.Should().OnlyContain(key => key.EndsWith(".webp", StringComparison.Ordinal));

            world.SavedKeys.Should().Contain(key => key.EndsWith("-thumbnail.webp", StringComparison.Ordinal));
            world.SavedKeys.Should().Contain(key => key.EndsWith("-card.webp", StringComparison.Ordinal));
            world.SavedKeys.Should().Contain(key => key.EndsWith("-full.webp", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ReorderingTheGallery_SavesTheOrderThatArrived()
        {
            var world = new World();
            var first = world.GivenPhoto(id: 1);
            var second = world.GivenPhoto(id: 2);
            var third = world.GivenPhoto(id: 3);

            await world.Send(new ReorderVehiclePhotosCommand(
                world.Vehicle.Code, [third.Code, first.Code, second.Code]));

            third.Position.Should().Be(0);
            first.Position.Should().Be(1);
            second.Position.Should().Be(2);
        }

        private static MemoryStream Pdf() =>
            new([0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37]);

        private static ProcessedImage ThreeRenditions() =>
            new(
                [
                    new ImageRendition(ImageSize.Thumbnail, [1, 2], 160, 120),
                    new ImageRendition(ImageSize.Card, [1, 2, 3], 640, 480),
                    new ImageRendition(ImageSize.Full, [1, 2, 3, 4], 1600, 1200)
                ],
                1600,
                1200);

        /// <summary>
        /// Um veículo, os repositórios que o cercam e um bucket de mentira que apenas anota o
        /// que recebeu. Nada aqui toca banco nem rede.
        /// </summary>
        private sealed class World
        {
            public World()
            {
                Vehicle = Vehicle.Create(
                    IdTenant, "ABC1D23", "9BWZZZ377VT004251", "Chevrolet", "Cruze", 2014, 2013);

                Vehicle.Id = IdVehicle;

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.Id).Returns(9);
                currentUser.SetupGet(user => user.Code).Returns(ActorCode);
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);

                Vehicles = new Mock<IVehicleRepository>();
                Vehicles
                    .Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) =>
                        code == Vehicle.Code ? Vehicle : null);

                Photos = new Mock<IVehiclePhotoRepository>();
                Photos
                    .Setup(repository => repository.ListByVehicleAsync(
                        It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int idVehicle, CancellationToken _) =>
                        (IReadOnlyList<VehiclePhoto>)
                            [.. gallery.Where(photo => photo.IdVehicle == idVehicle)]);

                Photos
                    .Setup(repository => repository.GetByCodeAsync(
                        It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Guid code, CancellationToken _) =>
                        gallery.Find(photo => photo.Code == code));

                Photos
                    .Setup(repository => repository.Add(It.IsAny<VehiclePhoto>()))
                    .Callback((VehiclePhoto photo) =>
                    {
                        photo.Id = gallery.Count + 1;
                        gallery.Add(photo);
                    });

                Documents = new Mock<IVehicleDocumentRepository>();
                Documents
                    .Setup(repository => repository.GetByCodeAsync(
                        It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Guid code, CancellationToken _) =>
                        archive.Find(document => document.Code == code));

                Documents
                    .Setup(repository => repository.Add(It.IsAny<VehicleDocument>()))
                    .Callback((VehicleDocument document) =>
                    {
                        AddedDocuments.Add(document);
                        archive.Add(document);
                    });

                var auditLogs = new Mock<IAuditLogRepository>();
                auditLogs
                    .Setup(repository => repository.Add(It.IsAny<AuditLog>()))
                    .Callback((AuditLog log) => AuditLogs.Add(log));

                Storage = new Mock<IFileStorage>();
                Storage
                    .Setup(storage => storage.SaveAsync(
                        It.IsAny<Stream>(),
                        It.IsAny<StorageRequest>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Stream content, StorageRequest request, CancellationToken _) =>
                    {
                        SavedKeys.Add(request.Key);
                        SavedVisibilities.Add(request.Visibility);

                        return new StoredFile(request.Key, request.ContentType, content.Length);
                    });

                Storage
                    .Setup(storage => storage.GetUrl(
                        It.IsAny<string>(), It.IsAny<FileVisibility>(), It.IsAny<TimeSpan?>()))
                    .Returns((string key, FileVisibility _, TimeSpan? _) =>
                        new Uri($"https://exemplo.invalid/{key}?assinatura=x"));

                Images = new Mock<IImageProcessor>();

                UnitOfWork = new Mock<IUnitOfWork>();
                UnitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(Vehicles.Object);
                UnitOfWork.SetupGet(unit => unit.VehiclePhotoRepository).Returns(Photos.Object);
                UnitOfWork.SetupGet(unit => unit.VehicleDocumentRepository).Returns(Documents.Object);
                UnitOfWork.SetupGet(unit => unit.AuditLogRepository).Returns(auditLogs.Object);
                UnitOfWork
                    .Setup(unit => unit.CommitAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);

                CurrentUser = currentUser;
            }

            public Vehicle Vehicle { get; }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<ICurrentUser> CurrentUser { get; }

            public Mock<IVehicleRepository> Vehicles { get; }

            public Mock<IVehiclePhotoRepository> Photos { get; }

            public Mock<IVehicleDocumentRepository> Documents { get; }

            public Mock<IFileStorage> Storage { get; }

            public Mock<IImageProcessor> Images { get; }

            public List<string> SavedKeys { get; } = [];

            public List<FileVisibility> SavedVisibilities { get; } = [];

            public List<VehicleDocument> AddedDocuments { get; } = [];

            public List<AuditLog> AuditLogs { get; } = [];

            private readonly List<VehiclePhoto> gallery = [];

            private readonly List<VehicleDocument> archive = [];

            /// <summary>Coloca uma foto já armazenada na galeria.</summary>
            /// <param name="id">Identificador interno.</param>
            /// <param name="idVehicle">Veículo dono da foto.</param>
            /// <returns>A foto.</returns>
            public VehiclePhoto GivenPhoto(int id = 1, int idVehicle = IdVehicle)
            {
                var photo = VehiclePhoto.Create(
                    idVehicle, VehiclePhotoKind.Finished,
                    $"{IdTenant}/vehicles/{Vehicle.Code}/{Guid.CreateVersion7()}",
                    "image/webp", 1024, 1600, 1200, gallery.Count);

                photo.Id = id;
                gallery.Add(photo);

                return photo;
            }

            /// <summary>Coloca um documento já armazenado no arquivo.</summary>
            /// <param name="idVehicle">Veículo dono do documento.</param>
            /// <returns>O documento.</returns>
            public VehicleDocument GivenDocument(int idVehicle = IdVehicle)
            {
                var document = VehicleDocument.Create(
                    idVehicle, VehicleDocumentKind.SaleInvoice,
                    $"{IdTenant}/vehicles/{Vehicle.Code}/documents/{Guid.CreateVersion7()}.pdf",
                    "nota.pdf", "application/pdf", 2048);

                document.Id = archive.Count + 1;
                archive.Add(document);

                return document;
            }

            /// <summary>Executa o handler que atende ao comando.</summary>
            /// <param name="request">O comando.</param>
            /// <returns>Uma tarefa.</returns>
            public Task Send(IRequest request) => request switch
            {
                DeleteVehiclePhotoCommand command =>
                    new DeleteVehiclePhotoHandler(
                        UnitOfWork.Object, CurrentUser.Object, Storage.Object)
                        .Handle(command, CancellationToken.None),

                DeleteVehicleDocumentCommand command =>
                    new DeleteVehicleDocumentHandler(UnitOfWork.Object, CurrentUser.Object)
                        .Handle(command, CancellationToken.None),

                ReorderVehiclePhotosCommand command =>
                    new ReorderVehiclePhotosHandler(UnitOfWork.Object, CurrentUser.Object)
                        .Handle(command, CancellationToken.None),

                _ => throw new InvalidOperationException($"Comando sem handler: {request.GetType().Name}")
            };

            /// <summary>Executa o upload de um documento.</summary>
            /// <param name="command">O comando.</param>
            /// <returns>Uma tarefa.</returns>
            public Task Send(UploadVehicleDocumentCommand command) =>
                new UploadVehicleDocumentHandler(
                    UnitOfWork.Object, CurrentUser.Object, Storage.Object)
                    .Handle(command, CancellationToken.None);

            /// <summary>Executa o upload de uma foto.</summary>
            /// <param name="command">O comando.</param>
            /// <returns>Uma tarefa.</returns>
            public Task Send(UploadVehiclePhotoCommand command) =>
                new UploadVehiclePhotoHandler(
                    UnitOfWork.Object, CurrentUser.Object, Storage.Object, Images.Object)
                    .Handle(command, CancellationToken.None);
        }
    }
}
