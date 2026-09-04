using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RevendaPro.Domain.Interfaces.Reference;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Infrastructure.Reference
{
    /// <summary>
    /// Reads the reference table over HTTP from the configured mirror.
    ///
    /// <b>The only class in the system that knows the shape of the source.</b> Which mirror
    /// answers, with which token and in how long is configuration; what the domain receives is
    /// a decimal and a month. Swapping the source is another class beside this one. See
    /// ADR-0005.
    ///
    /// Nothing here throws for a source problem. Out of reach, over the daily allowance,
    /// answering something this class cannot read — all of it comes back as
    /// <see cref="FipeOutcome.Unavailable"/>, logged, and the operation keeps the last known
    /// value. A reference table is not allowed to stop a dealership from working.
    /// </summary>
    public class FipeHttpCatalog(
        HttpClient client,
        IOptions<FipeSettings> options,
        IMemoryCache cache,
        ILogger<FipeHttpCatalog> logger) : IFipeCatalog
    {
        private const string TokenHeader = "X-Subscription-Token";

        /// <summary>
        /// Por quanto tempo uma lista de nomes fica guardada.
        ///
        /// <b>Vale para nomes, e jamais para preço.</b> Marca, modelo e os anos de um modelo
        /// mudam de uma tabela mensal para a outra, e não de um minuto para o outro — enquanto o
        /// preço é pedido com o mês fixado, toda vez, porque ele é dinheiro.
        ///
        /// Sem isto, achar o modelo de um Gol custaria trinta e oito chamadas para conferir o
        /// ano de cada versão, e o Gol seguinte custaria as mesmas trinta e oito.
        /// </summary>
        private static readonly TimeSpan NamesLastFor = TimeSpan.FromHours(12);

        private readonly FipeSettings settings = options.Value;

        /// <inheritdoc/>
        public async Task<FipeResult<FipeReference>> GetCurrentReferenceAsync(
            CancellationToken cancellationToken = default)
        {
            var read = await ReadAsync<List<ReferenceRow>>("references", cancellationToken)
                .ConfigureAwait(false);

            if (!read.Ok)
            {
                return new FipeResult<FipeReference>(read.Outcome, null, read.Detail);
            }

            // The newest table is the one the source lists first, and the code is what pins
            // every other query to it. Sorting by code instead of trusting the order: the
            // codes grow by one a month, and an order is easier to break than an integer.
            var newest = read.Value!
                .Where(row => FipeText.TryParseMonth(row.Month, out _))
                .OrderByDescending(row => row.Code)
                .FirstOrDefault();

            if (newest is null || !FipeText.TryParseMonth(newest.Month, out var month))
            {
                return FipeResult<FipeReference>.Unavailable("A fonte respondeu sem nenhum mês legível.");
            }

            return FipeResult<FipeReference>.Found(new FipeReference(newest.Code, month));
        }

        /// <inheritdoc/>
        public async Task<FipeResult<FipePrice>> GetPriceAsync(
            string fipeCode,
            string yearFuel,
            int reference,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fipeCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(yearFuel);

            var path = $"{settings.VehicleType}/{Escape(fipeCode)}/years/{Escape(yearFuel)}"
                + $"?reference={reference.ToString(CultureInfo.InvariantCulture)}";

            var read = await ReadAsync<PriceRow>(path, cancellationToken).ConfigureAwait(false);

            return ToPrice(read, fipeCode, yearFuel);
        }

        /// <inheritdoc/>
        public async Task<FipeResult<IReadOnlyList<FipeYearOption>>> ListYearsAsync(
            string fipeCode,
            int reference,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fipeCode);

            var path = $"{settings.VehicleType}/{Escape(fipeCode)}/years"
                + $"?reference={reference.ToString(CultureInfo.InvariantCulture)}";

            var read = await ReadAsync<List<YearRow>>(path, cancellationToken).ConfigureAwait(false);

            return ToYears(read);
        }

        /// <inheritdoc/>
        public async Task<FipeResult<IReadOnlyList<FipeNamed>>> ListBrandsAsync(
            CancellationToken cancellationToken = default)
        {
            var read = await ReadAsync<List<NamedRow>>(
                $"{settings.VehicleType}/brands", cancellationToken).ConfigureAwait(false);

            return ToNamed(read, "A fonte listou zero marcas.");
        }

        /// <inheritdoc/>
        public async Task<FipeResult<IReadOnlyList<FipeNamed>>> ListModelsAsync(
            string brandCode,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(brandCode);

            return await RememberAsync(
                $"fipe:models:{settings.VehicleType}:{brandCode}",
                async () =>
                {
                    var read = await ReadAsync<List<NamedRow>>(
                        $"{settings.VehicleType}/brands/{Escape(brandCode)}/models",
                        cancellationToken).ConfigureAwait(false);

                    return ToNamed(read, "A fonte listou zero modelos desta marca.");
                })
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<FipeResult<IReadOnlyList<FipeYearOption>>> ListModelYearsAsync(
            string brandCode,
            string modelCode,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(brandCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(modelCode);

            return await RememberAsync(
                $"fipe:years:{settings.VehicleType}:{brandCode}:{modelCode}",
                async () =>
                {
                    var read = await ReadAsync<List<YearRow>>(
                        $"{settings.VehicleType}/brands/{Escape(brandCode)}"
                        + $"/models/{Escape(modelCode)}/years",
                        cancellationToken).ConfigureAwait(false);

                    return ToYears(read);
                })
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<FipeResult<FipePrice>> GetPriceOfModelAsync(
            string brandCode,
            string modelCode,
            string yearFuel,
            int reference,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(brandCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(modelCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(yearFuel);

            var path = $"{settings.VehicleType}/brands/{Escape(brandCode)}"
                + $"/models/{Escape(modelCode)}/years/{Escape(yearFuel)}"
                + $"?reference={reference.ToString(CultureInfo.InvariantCulture)}";

            // Guardado com o mes DENTRO da chave, e por isso reproduzivel: a mesma pergunta,
            // com o mesmo mes fixado, so tem uma resposta. O que a ADR-0005 recusa e o mes
            // escorregar entre duas leituras, e nao a resposta de um mes ficar guardada.
            return await RememberAsync(
                $"fipe:price:{settings.VehicleType}:{brandCode}:{modelCode}:{yearFuel}:{reference}",
                async () =>
                {
                    var read = await ReadAsync<PriceRow>(path, cancellationToken).ConfigureAwait(false);

                    // No code to fall back on: this call exists precisely because nobody knows
                    // it yet, and an answer without it is an answer this adapter cannot use.
                    return ToPrice(read, string.Empty, yearFuel);
                })
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Turns a priced row into what the domain reads, or into why it could not be read.
        /// </summary>
        /// <param name="read">What the source answered.</param>
        /// <param name="fipeCode">Code to fall back on, when the answer omits it.</param>
        /// <param name="yearFuel">Year and fuel that was asked for.</param>
        /// <returns>The price, or the reason.</returns>
        private FipeResult<FipePrice> ToPrice(
            FipeResult<PriceRow> read,
            string fipeCode,
            string yearFuel)
        {
            if (!read.Ok)
            {
                return new FipeResult<FipePrice>(read.Outcome, null, read.Detail);
            }

            var row = read.Value!;

            if (!FipeText.TryParseMoney(row.Price, out var value))
            {
                logger.LogWarning(
                    "FIPE answered {Code}/{YearFuel} with an unreadable price: {Price}.",
                    fipeCode, yearFuel, row.Price);

                return FipeResult<FipePrice>.Unavailable($"Preço ilegível: \"{row.Price}\".");
            }

            if (!FipeText.TryParseMonth(row.ReferenceMonth, out var month))
            {
                return FipeResult<FipePrice>.Unavailable($"Mês ilegível: \"{row.ReferenceMonth}\".");
            }

            // The code the table itself printed wins over the one asked for: a mirror can
            // normalise it, and what gets stored has to be what the table says.
            var code = string.IsNullOrWhiteSpace(row.CodeFipe) ? fipeCode : row.CodeFipe;

            return string.IsNullOrWhiteSpace(code)
                ? FipeResult<FipePrice>.Unavailable("A fonte respondeu sem o código do modelo.")
                : FipeResult<FipePrice>.Found(new FipePrice(
                    code,
                    yearFuel,
                    month,
                    value,
                    row.Brand ?? string.Empty,
                    row.Model ?? string.Empty,
                    row.ModelYear,
                    row.Fuel ?? string.Empty));
        }

        private static FipeResult<IReadOnlyList<FipeNamed>> ToNamed(
            FipeResult<List<NamedRow>> read,
            string whenEmpty)
        {
            if (!read.Ok)
            {
                return new FipeResult<IReadOnlyList<FipeNamed>>(read.Outcome, null, read.Detail);
            }

            var named = read.Value!
                .Where(row => !string.IsNullOrWhiteSpace(row.Code) && !string.IsNullOrWhiteSpace(row.Name))
                .Select(row => new FipeNamed(row.Code!, row.Name!))
                .ToList();

            return named.Count == 0
                ? FipeResult<IReadOnlyList<FipeNamed>>.Missing(whenEmpty)
                : FipeResult<IReadOnlyList<FipeNamed>>.Found(named);
        }

        private static FipeResult<IReadOnlyList<FipeYearOption>> ToYears(FipeResult<List<YearRow>> read)
        {
            if (!read.Ok)
            {
                return new FipeResult<IReadOnlyList<FipeYearOption>>(read.Outcome, null, read.Detail);
            }

            var options = read.Value!
                .Where(row => !string.IsNullOrWhiteSpace(row.Code))
                .Select(row =>
                {
                    FipeText.TryParseModelYear(row.Code, out var year);

                    return new FipeYearOption(row.Code!, row.Name ?? row.Code!, year);
                })
                .ToList();

            return options.Count == 0
                ? FipeResult<IReadOnlyList<FipeYearOption>>.Missing("A fonte listou zero anos.")
                : FipeResult<IReadOnlyList<FipeYearOption>>.Found(options);
        }

        /// <summary>
        /// One call, with every way it can go wrong turned into an outcome.
        /// </summary>
        /// <typeparam name="T">Shape expected in the body.</typeparam>
        /// <param name="path">Path under the base address.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The body, or why there is none.</returns>
        private async Task<FipeResult<T>> ReadAsync<T>(string path, CancellationToken cancellationToken)
        {
            if (!settings.Enabled)
            {
                return FipeResult<T>.Unavailable("A consulta automática está desligada.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, path);

            if (!string.IsNullOrWhiteSpace(settings.Token))
            {
                request.Headers.Add(TokenHeader, settings.Token);
            }

            try
            {
                using var response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return FipeResult<T>.Missing($"A tabela respondeu 404 para {path}.");
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    logger.LogWarning("FIPE refused the call: daily allowance reached.");

                    return FipeResult<T>.Unavailable("O limite diário de consultas foi alcançado.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "FIPE answered {Status} for {Path}.", (int)response.StatusCode, path);

                    return FipeResult<T>.Unavailable($"A fonte respondeu {(int)response.StatusCode}.");
                }

                var body = await response.Content
                    .ReadFromJsonAsync<T>(cancellationToken)
                    .ConfigureAwait(false);

                return body is null
                    ? FipeResult<T>.Unavailable("A fonte respondeu com corpo vazio.")
                    : FipeResult<T>.Found(body);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Whoever asked gave up: this is not a source failure, and it belongs upwards.
                throw;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("FIPE took longer than {Seconds}s for {Path}.",
                    settings.TimeoutInSeconds, path);

                return FipeResult<T>.Unavailable($"A fonte demorou mais de {settings.TimeoutInSeconds}s.");
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning(exception, "FIPE could not be reached for {Path}.", path);

                return FipeResult<T>.Unavailable("A fonte está fora de alcance.");
            }
            catch (System.Text.Json.JsonException exception)
            {
                // The mirror changed shape. Not the fault of the car, and not a reason to fail
                // an operation: it is a source problem, and it is logged as one.
                logger.LogError(exception, "FIPE answered {Path} in a shape this adapter cannot read.", path);

                return FipeResult<T>.Unavailable("A fonte respondeu num formato inesperado.");
            }
        }

        /// <summary>
        /// Guarda o que a fonte respondeu, quando ela respondeu.
        ///
        /// Recusa e indisponibilidade ficam de fora de proposito: guardar um "fora do ar" por
        /// doze horas transformaria um tropeco de um minuto num dia sem tabela.
        /// </summary>
        private async Task<FipeResult<T>> RememberAsync<T>(
            string key,
            Func<Task<FipeResult<T>>> read)
        {
            if (cache.TryGetValue<FipeResult<T>>(key, out var remembered) && remembered is not null)
            {
                return remembered;
            }

            var fresh = await read().ConfigureAwait(false);

            if (fresh.Ok)
            {
                cache.Set(key, fresh, NamesLastFor);
            }

            return fresh;
        }

        private static string Escape(string value) => Uri.EscapeDataString(value.Trim());

        /// <summary>One row of the list of published tables.</summary>
        private sealed class ReferenceRow
        {
            [JsonPropertyName("code")]
            public int Code { get; set; }

            [JsonPropertyName("month")]
            public string? Month { get; set; }
        }

        /// <summary>The priced row of one model.</summary>
        private sealed class PriceRow
        {
            [JsonPropertyName("price")]
            public string? Price { get; set; }

            [JsonPropertyName("brand")]
            public string? Brand { get; set; }

            [JsonPropertyName("model")]
            public string? Model { get; set; }

            [JsonPropertyName("modelYear")]
            public short ModelYear { get; set; }

            [JsonPropertyName("fuel")]
            public string? Fuel { get; set; }

            [JsonPropertyName("codeFipe")]
            public string? CodeFipe { get; set; }

            [JsonPropertyName("referenceMonth")]
            public string? ReferenceMonth { get; set; }
        }

        /// <summary>Something the table names by a code: a brand, a model.</summary>
        private sealed class NamedRow
        {
            [JsonPropertyName("code")]
            public string? Code { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        /// <summary>One year and fuel combination.</summary>
        private sealed class YearRow
        {
            [JsonPropertyName("code")]
            public string? Code { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }
    }
}
