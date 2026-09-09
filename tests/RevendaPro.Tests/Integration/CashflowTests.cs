using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// O caixa (M22), contra o MariaDB de verdade.
    ///
    /// O que se prova: as três origens aparecem numa lista só, ordenadas por vencimento; a
    /// baixa muda o total; o que se recebe sai do saldo da venda e some da lista quando ela
    /// quita; e a outra revenda enxerga nada — nem nos números, nem nas linhas.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class CashflowTests(ApiFixture api) : IAsyncLifetime
    {
        private HttpClient mine = default!;
        private SecondDealership other = default!;
        private Guid storeExpenseCode;
        private Guid vehicleCode;
        private string marker = string.Empty;

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            mine = await api.ClientOfAsync("admin@revendapro.local");
            other = await api.OtherDealershipAsync();
            marker = $"Caixa {Guid.NewGuid():N}"[..20];

            var types = await ReadDataAsync(await mine.GetAsync(Url("/api/expense-types?scope=2")));
            var aluguel = types.EnumerateArray().First(t => t.GetProperty("name").GetString() == "Aluguel");

            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

            var created = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/store-expenses"), new
            {
                description = marker,
                expenseTypeCode = aluguel.GetProperty("code").GetGuid(),
                amount = 1_000,
                date = hoje.AddDays(-10).ToString("yyyy-MM-dd"),
                dueDate = hoje.AddDays(-5).ToString("yyyy-MM-dd"),
                isPaid = false,
            }));

            storeExpenseCode = created.GetProperty("code").GetGuid();

            // Um carro com um gasto previsto: o caixa junta os dois lados, e provar isso pede
            // que os dois existam, sem depender do pátio de demonstração estar ligado.
            var suffix = Random.Shared.Next(100, 999);

            var vehicle = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/vehicles"), new
            {
                plate = $"CXA{suffix % 10}D{suffix % 100:00}",
                chassis = $"9BWX1R{suffix:000}D0000{Random.Shared.Next(100, 999):000}",
                brand = "Fiat",
                model = "Uno",
                modelYear = 2015,
                manufactureYear = 2014,
                mileage = 90_000,
                fuelType = 1,
                transmission = 1,
                origin = 1,
                purchasePrice = 15_000,
                purchaseDate = "2026-08-01",
            }));

            vehicleCode = vehicle.GetProperty("code").GetGuid();

            var carTypes = await ReadDataAsync(await mine.GetAsync(Url("/api/expense-types?scope=1")));

            await mine.PostAsJsonAsync(Url($"/api/vehicles/{vehicleCode}/expenses"), new
            {
                vehicleCode,
                expenseTypeCode = carTypes[0].GetProperty("code").GetGuid(),
                description = "Retífica prevista",
                amount = 2_000,
                date = hoje.AddDays(-2).ToString("yyyy-MM-dd"),
                dueDate = hoje.AddDays(20).ToString("yyyy-MM-dd"),
                isPaid = false,
            });
        }

        /// <inheritdoc/>
        public async Task DisposeAsync()
        {
            if (storeExpenseCode != Guid.Empty)
            {
                await mine.DeleteAsync(Url($"/api/store-expenses/{storeExpenseCode}"));
            }

            if (vehicleCode != Guid.Empty)
            {
                await mine.DeleteAsync(Url($"/api/vehicles/{vehicleCode}"));
            }
        }

        [Fact]
        public async Task ADespesaVencida_EntraNoQueSeDeve_NoQueVenceu_ENaListaComOSinalDeAtraso()
        {
            var cashflow = await ReadDataAsync(await mine.GetAsync(Url("/api/cashflow")));

            cashflow.GetProperty("payableOpen").GetDecimal().Should().BeGreaterThanOrEqualTo(1_000m);
            cashflow.GetProperty("payableOverdue").GetDecimal().Should().BeGreaterThanOrEqualTo(1_000m);

            var line = cashflow.GetProperty("payables").EnumerateArray()
                .Single(l => l.GetProperty("code").GetGuid() == storeExpenseCode);

            line.GetProperty("kind").GetInt32().Should().Be(2, "veio da loja");
            line.GetProperty("isOverdue").GetBoolean().Should().BeTrue();
            line.GetProperty("category").GetString().Should().Be("Aluguel");
            line.GetProperty("amount").GetDecimal().Should().Be(1_000m);
            line.GetProperty("vehicleCode").ValueKind.Should().Be(JsonValueKind.Null, "a loja tem carro nenhum");
        }

        [Fact]
        public async Task AListaVemOrdenadaPeloVencimento_EMisturaOCarroComALoja()
        {
            var cashflow = await ReadDataAsync(await mine.GetAsync(Url("/api/cashflow")));

            var dates = cashflow.GetProperty("payables").EnumerateArray()
                .Select(l => l.GetProperty("dueDate").GetString())
                .ToList();

            dates.Should().BeInAscendingOrder("o que vence primeiro é o que a pessoa vê primeiro");

            var kinds = cashflow.GetProperty("payables").EnumerateArray()
                .Select(l => l.GetProperty("kind").GetInt32())
                .Distinct()
                .ToList();

            kinds.Should().Contain(1, "o gasto do carro entra na mesma lista");
            kinds.Should().Contain(2, "e a despesa da loja também");
        }

        [Fact]
        public async Task ABaixaPelaTelaDoCaixa_TiraAContaDaLista_ESomaNoQueSaiuNoPeriodo()
        {
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            var janela = $"?from={hoje.AddDays(-1):yyyy-MM-dd}&to={hoje:yyyy-MM-dd}";

            var before = await ReadDataAsync(await mine.GetAsync(Url($"/api/cashflow{janela}")));
            var paidBefore = before.GetProperty("paidInPeriod").GetDecimal();
            var openBefore = before.GetProperty("payableOpen").GetDecimal();

            var settled = await mine.PatchAsJsonAsync(Url("/api/cashflow/payables"), new
            {
                kind = 2,
                code = storeExpenseCode,
                isPaid = true,
            });

            settled.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var after = await ReadDataAsync(await mine.GetAsync(Url($"/api/cashflow{janela}")));

            after.GetProperty("payableOpen").GetDecimal().Should().Be(openBefore - 1_000m);
            after.GetProperty("paidInPeriod").GetDecimal().Should().Be(paidBefore + 1_000m, "a baixa é de hoje");
            after.GetProperty("payables").EnumerateArray()
                .Should().NotContain(l => l.GetProperty("code").GetGuid() == storeExpenseCode);

            // Desfazer devolve a conta para a lista.
            await mine.PatchAsJsonAsync(Url("/api/cashflow/payables"), new
            {
                kind = 2,
                code = storeExpenseCode,
                isPaid = false,
            });

            var back = await ReadDataAsync(await mine.GetAsync(Url("/api/cashflow")));
            back.GetProperty("payables").EnumerateArray()
                .Should().Contain(l => l.GetProperty("code").GetGuid() == storeExpenseCode);
        }

        [Fact]
        public async Task OQueSeRecebe_SaiDoSaldoDaVenda_ESomeQuandoElaQuita()
        {
            var cashflow = await ReadDataAsync(await mine.GetAsync(Url("/api/cashflow")));
            var receivables = cashflow.GetProperty("receivables").EnumerateArray().ToList();

            if (receivables.Count == 0)
            {
                // Pátio sem venda a prazo: o caixa diz zero, e é a resposta certa.
                cashflow.GetProperty("receivableOpen").GetDecimal().Should().Be(0m);
                return;
            }

            var line = receivables[0];
            var vehicleCode = line.GetProperty("vehicleCode").GetGuid();
            var balance = line.GetProperty("amount").GetDecimal();

            line.GetProperty("kind").GetInt32().Should().Be(3);
            balance.Should().BePositive();

            // Metade agora: a venda continua na lista, com o saldo menor.
            await mine.PostAsJsonAsync(Url($"/api/vehicles/{vehicleCode}/sale/receipts"), new
            {
                amount = balance / 2,
                date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                paymentMethod = 2,
                notes = "Metade do banco",
            });

            var half = await ReadDataAsync(await mine.GetAsync(Url("/api/cashflow")));
            var still = half.GetProperty("receivables").EnumerateArray()
                .Single(l => l.GetProperty("vehicleCode").GetGuid() == vehicleCode);

            still.GetProperty("amount").GetDecimal().Should().Be(balance - (balance / 2));

            // O resto: a venda quita e sai da lista sozinha, porque o saldo dela é zero.
            await mine.PostAsJsonAsync(Url($"/api/vehicles/{vehicleCode}/sale/receipts"), new
            {
                amount = balance - (balance / 2),
                date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                paymentMethod = 2,
                notes = "O resto",
            });

            var settled = await ReadDataAsync(await mine.GetAsync(Url("/api/cashflow")));
            settled.GetProperty("receivables").EnumerateArray()
                .Should().NotContain(l => l.GetProperty("vehicleCode").GetGuid() == vehicleCode);
        }

        [Fact]
        public async Task AOutraRevenda_EnxergaNada_NemNosNumerosNemNasLinhas()
        {
            var hers = await api.AsAsync(other.AdminEmail);

            var theirs = await ReadDataAsync(await hers.GetAsync(Url("/api/cashflow")));

            theirs.GetProperty("payables").EnumerateArray()
                .Should().NotContain(l => l.GetProperty("code").GetGuid() == storeExpenseCode);

            var refused = await hers.PatchAsJsonAsync(Url("/api/cashflow/payables"), new
            {
                kind = 2,
                code = storeExpenseCode,
                isPaid = true,
            });

            refused.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task APlanilhaDoCaixa_TrazOsDoisLados()
        {
            var csv = await mine.GetAsync(Url("/api/exports/cashflow?format=csv"));

            csv.StatusCode.Should().Be(HttpStatusCode.OK);
            csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

            var text = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());

            text.Should().Contain("Situação;Descrição");
            text.Should().Contain(marker);
            text.Should().Contain("A pagar");
        }

        private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage answer)
        {
            answer.IsSuccessStatusCode.Should().BeTrue(await answer.Content.ReadAsStringAsync());

            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            return body.GetProperty("data");
        }

        private static Uri Url(string path) => new(path, UriKind.Relative);
    }
}
