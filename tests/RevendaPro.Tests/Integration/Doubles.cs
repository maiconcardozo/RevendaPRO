using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces.Reference;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// A tabela de referência, sem rede.
    ///
    /// Ela responde como uma fonte saudável responderia, com os números reais do levantamento
    /// do M11. Três motivos para o dublê, e todos valem: um teste que depende da internet falha
    /// por motivo errado; um teste que gasta consulta gasta a faixa gratuita que a operação
    /// precisa; e a fonte é um espelho de terceiros, que pode mudar de resposta amanhã.
    /// </summary>
    internal sealed class FipeCatalogDouble : IFipeCatalog
    {
        private static readonly DateOnly Month = new(2026, 9, 1);

        /// <inheritdoc/>
        public Task<FipeResult<FipeReference>> GetCurrentReferenceAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FipeResult<FipeReference>.Found(new FipeReference(337, Month)));

        /// <inheritdoc/>
        public Task<FipeResult<FipePrice>> GetPriceAsync(
            string fipeCode,
            string yearFuel,
            int reference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FipeResult<FipePrice>.Found(new FipePrice(
                fipeCode, yearFuel, Month, 56_530.00m,
                "GM - Chevrolet", "CRUZE LT 1.8 16V FlexPower 4p Aut.", 2014, "Flex")));

        /// <inheritdoc/>
        public Task<FipeResult<IReadOnlyList<FipeYearOption>>> ListYearsAsync(
            string fipeCode,
            int reference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FipeResult<IReadOnlyList<FipeYearOption>>.Found(
                [new FipeYearOption("2014-5", "2014 Flex", 2014)]));

        /// <inheritdoc/>
        public Task<FipeResult<IReadOnlyList<FipeNamed>>> ListBrandsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FipeResult<IReadOnlyList<FipeNamed>>.Found(
                [new FipeNamed("23", "GM - Chevrolet")]));

        /// <inheritdoc/>
        public Task<FipeResult<IReadOnlyList<FipeNamed>>> ListModelsAsync(
            string brandCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FipeResult<IReadOnlyList<FipeNamed>>.Found(
                [new FipeNamed("5635", "CRUZE LT 1.8 16V FlexPower 4p Aut.")]));

        /// <inheritdoc/>
        public Task<FipeResult<IReadOnlyList<FipeYearOption>>> ListModelYearsAsync(
            string brandCode,
            string modelCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FipeResult<IReadOnlyList<FipeYearOption>>.Found(
                [new FipeYearOption("2014-5", "2014 Flex", 2014)]));

        /// <inheritdoc/>
        public Task<FipeResult<FipePrice>> GetPriceOfModelAsync(
            string brandCode,
            string modelCode,
            string yearFuel,
            int reference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FipeResult<FipePrice>.Found(new FipePrice(
                "004380-0", yearFuel, Month, 56_530.00m,
                "GM - Chevrolet", "CRUZE LT 1.8 16V FlexPower 4p Aut.", 2014, "Flex")));
    }

    /// <summary>
    /// O armazenamento, sem bucket.
    ///
    /// Guarda os bytes em memória e devolve um endereço assinado de mentira. A matriz jamais
    /// envia arquivo — ela bate na fechadura —, e o teste de isolamento precisa que a leitura
    /// de um documento de outra empresa seja recusada <b>antes</b> de qualquer arquivo, que é
    /// exatamente o que este dublê deixa visível.
    /// </summary>
    internal sealed class FileStorageDouble : IFileStorage
    {
        private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

        /// <inheritdoc/>
        public long MaxSizeInBytes => 12 * 1024 * 1024;

        /// <inheritdoc/>
        public async Task<StoredFile> SaveAsync(
            Stream content,
            StorageRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(request);

            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);

            var bytes = memory.ToArray();
            files[request.Key] = bytes;

            return new StoredFile(request.Key, request.ContentType, bytes.Length);
        }

        /// <inheritdoc/>
        public Uri GetUrl(string key, FileVisibility visibility, TimeSpan? expiresIn = null) =>
            new($"https://arquivos.teste/{visibility}/{key}?assinatura=teste");

        /// <inheritdoc/>
        public Task DeleteAsync(
            string key,
            FileVisibility visibility,
            CancellationToken cancellationToken = default)
        {
            files.Remove(key);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<Stream?> OpenReadAsync(
            string key,
            FileVisibility visibility,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(
                files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);

        /// <inheritdoc/>
        public Task<int> DeleteByPrefixAsync(
            string prefix,
            FileVisibility visibility,
            CancellationToken cancellationToken = default)
        {
            var removed = files.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            foreach (var key in removed)
            {
                files.Remove(key);
            }

            return Task.FromResult(removed.Count);
        }
    }
}
