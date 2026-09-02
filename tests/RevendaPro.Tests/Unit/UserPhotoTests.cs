using FluentAssertions;
using Moq;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Infrastructure.Storage;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// A foto do usuário no bucket: o último arquivo do sistema a sair do disco.
    ///
    /// O que importa aqui é a chave — tenant na frente, usuário no meio, nada endereçável de
    /// outra empresa — e o tamanho: um avatar é desenhado com quarenta pixels, e guardar a
    /// foto de celular inteira atrás dele seria pagar quatro megabytes por sidebar.
    /// </summary>
    public class UserPhotoTests
    {
        private static readonly Guid UserCode = Guid.CreateVersion7();
        private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

        [Fact]
        public async Task ThePhoto_IsStoredUnderTheTenantAndTheUser_AsOneSmallWebP()
        {
            var world = new World();

            var name = await world.Storage.SaveAsync(7, UserCode, new MemoryStream(Jpeg));

            name.Should().EndWith(".webp");

            var saved = world.Saved.Should().ContainSingle().Which;
            saved.Key.Should().Be($"7/users/{UserCode}/{name}");
            saved.ContentType.Should().Be("image/webp");
            saved.Visibility.Should().Be(FileVisibility.Private);

            // Só a menor rendição vai para o bucket. As outras duas jamais são gravadas.
            world.SavedBytes.Should().ContainSingle().Which.Should().Equal(World.Thumbnail);
        }

        [Fact]
        public async Task WhatIsNotAnImage_IsRefusedByItsBytes()
        {
            var world = new World();

            var act = () => world.Storage.SaveAsync(7, UserCode, new MemoryStream([0x4D, 0x5A, 0x90, 0x00]));

            await act.Should().ThrowAsync<BusinessRuleException>();
            world.Saved.Should().BeEmpty();
        }

        [Fact]
        public async Task AnOversizedFile_IsRefusedBeforeAnyProcessing()
        {
            var world = new World();
            var big = new byte[BucketUserPhotoStorage.MaxSizeInBytes + 1];
            Jpeg.CopyTo(big, 0);

            var act = () => world.Storage.SaveAsync(7, UserCode, new MemoryStream(big));

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*2 MB*");
            world.Images.Verify(i => i.ProcessAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ANameThatLooksLikeAPath_IsNeverRead()
        {
            // O nome vem do banco. Se um dia alguém gravar "../../outra-empresa/foto.webp"
            // ali, a leitura tem que falhar sem perguntar ao bucket.
            var world = new World();

            var photo = await world.Storage.ReadAsync(7, UserCode, "../3/users/x/foto.webp");

            photo.Should().BeNull();
            world.Files.Verify(
                f => f.OpenReadAsync(It.IsAny<string>(), It.IsAny<FileVisibility>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task AMissingPhoto_ReadsAsNull()
        {
            var world = new World();

            var photo = await world.Storage.ReadAsync(7, UserCode, "sumiu.webp");

            photo.Should().BeNull();
        }

        private sealed class World
        {
            public static readonly byte[] Thumbnail = [1, 2, 3];

            public World()
            {
                Files = new Mock<IFileStorage>();
                Files
                    .Setup(f => f.SaveAsync(It.IsAny<Stream>(), It.IsAny<StorageRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Stream content, StorageRequest request, CancellationToken _) =>
                    {
                        using var buffer = new MemoryStream();
                        content.CopyTo(buffer);
                        Saved.Add(request);
                        SavedBytes.Add(buffer.ToArray());
                        return new StoredFile(request.Key, request.ContentType, buffer.Length);
                    });
                Files
                    .Setup(f => f.OpenReadAsync(It.IsAny<string>(), It.IsAny<FileVisibility>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Stream?)null);

                Images = new Mock<IImageProcessor>();
                Images
                    .Setup(i => i.ProcessAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ProcessedImage(
                        [
                            new ImageRendition(ImageSize.Thumbnail, Thumbnail, 320, 240),
                            new ImageRendition(ImageSize.Card, [4, 5, 6, 7], 800, 600),
                            new ImageRendition(ImageSize.Full, [8, 9, 10, 11, 12], 1600, 1200)
                        ],
                        1600, 1200));

                Storage = new BucketUserPhotoStorage(Files.Object, Images.Object);
            }

            public Mock<IFileStorage> Files { get; }

            public Mock<IImageProcessor> Images { get; }

            public BucketUserPhotoStorage Storage { get; }

            public List<StorageRequest> Saved { get; } = [];

            public List<byte[]> SavedBytes { get; } = [];
        }
    }
}
