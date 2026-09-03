using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
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
        ILogger<FipeHttpCatalog> logger) : IFipeCatalog
    {
        private const string TokenHeader = "X-Subscription-Token";

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

            return FipeResult<FipePrice>.Found(new FipePrice(
                // The code the table itself printed wins over the one asked for: a mirror can
                // normalise it, and what gets stored has to be what the table says.
                string.IsNullOrWhiteSpace(row.CodeFipe) ? fipeCode : row.CodeFipe,
                yearFuel,
                month,
                value,
                row.Brand ?? string.Empty,
                row.Model ?? string.Empty,
                row.ModelYear,
                row.Fuel ?? string.Empty));
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
                ? FipeResult<IReadOnlyList<FipeYearOption>>.Missing("A fonte listou zero anos para este código.")
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
