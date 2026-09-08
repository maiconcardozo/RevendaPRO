using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RevendaPro.Domain.Interfaces;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// A despesa da loja e o escopo do tipo de gasto (M22), contra o MariaDB de verdade.
    ///
    /// O que se prova: o catálogo semeado sai com escopo válido em toda linha — um zero ali
    /// seria um tipo invisível nas duas telas —; a lista da loja jamais oferece um tipo de
    /// carro, e a API recusa quem tentar pela porta dos fundos; a baixa muda o estado e a data
    /// juntas; e a outra revenda enxerga nada.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class StoreExpenseTests(ApiFixture api) : IAsyncLifetime
    {
        private HttpClient mine = default!;
        private SecondDealership other = default!;
        private readonly List<Guid> expenses = [];

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            mine = await api.ClientOfAsync("admin@revendapro.local");
            other = await api.OtherDealershipAsync();
        }

        /// <inheritdoc/>
        public async Task DisposeAsync()
        {
            foreach (var code in expenses)
            {
                await mine.DeleteAsync(Url($"/api/store-expenses/{code}"));
            }
        }

        [Fact]
        public async Task OCatalogoSemeado_SaiComEscopoValido_ETemOsTiposDaLoja()
        {
            using var scope = api.Scope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var tenant = (await unitOfWork.TenantRepository.GetFirstAsync())!;

            var types = await unitOfWork.ExpenseTypeRepository.ListByTenantAsync(tenant.Id);

            types.Should().NotBeEmpty();
            types.Should().OnlyContain(
                type => type.ServesVehicles || type.ServesStore,
                "um tipo que serve em lugar nenhum jamais aparece numa lista, e é pior do que um erro");

            types.Should().Contain(type => type.Name == "Aluguel" && type.ServesStore && !type.ServesVehicles);
            types.Should().Contain(type => type.Name == "Mecânica" && type.ServesVehicles && !type.ServesStore);
        }

        [Fact]
        public async Task AListaDeTiposObedeceOEscopo_EAApiRecusaOTipoDeCarroNaLoja()
        {
            var store = await ReadDataAsync(await mine.GetAsync(Url("/api/expense-types?scope=2")));
            var vehicle = await ReadDataAsync(await mine.GetAsync(Url("/api/expense-types?scope=1")));

            var storeNames = store.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
            var vehicleNames = vehicle.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();

            storeNames.Should().Contain("Aluguel").And.NotContain("Mecânica");
            vehicleNames.Should().Contain("Mecânica").And.NotContain("Aluguel");

            var mecanica = vehicle.EnumerateArray().First(t => t.GetProperty("name").GetString() == "Mecânica");

            var refused = await mine.PostAsJsonAsync(Url("/api/store-expenses"), new
            {
                description = "Aluguel classificado errado",
                expenseTypeCode = mecanica.GetProperty("code").GetGuid(),
                amount = 2_400,
                date = "2026-09-01",
                isPaid = false,
            });

            refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await DetailOf(refused)).Should().Contain("serve para gasto de carro");
        }

        [Fact]
        public async Task ADespesaDaLoja_NasceAPagarComPrazo_EABaixaMudaOEstadoEADataJuntas()
        {
            // As datas saem de hoje, e jamais do calendário de quem escreveu o teste: um mês
            // fixo passa a ser passado sozinho, e o teste começa a falhar por conta do relógio.
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            var lancamento = hoje.AddDays(-30);
            var vencimento = hoje.AddDays(-20);
            var pagamento = hoje.AddDays(-18);

            var types = await ReadDataAsync(await mine.GetAsync(Url("/api/expense-types?scope=2")));
            var aluguel = types.EnumerateArray().First(t => t.GetProperty("name").GetString() == "Aluguel");

            var created = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/store-expenses"), new
            {
                description = "Aluguel do mês passado",
                expenseTypeCode = aluguel.GetProperty("code").GetGuid(),
                amount = 2_400,
                date = lancamento.ToString("yyyy-MM-dd"),
                dueDate = vencimento.ToString("yyyy-MM-dd"),
                isPaid = false,
                notes = "Boleto 9 de 12",
            }));

            var code = created.GetProperty("code").GetGuid();
            expenses.Add(code);

            created.GetProperty("isPaid").GetBoolean().Should().BeFalse();
            created.GetProperty("dueDate").GetString().Should().StartWith(vencimento.ToString("yyyy-MM-dd"));
            created.GetProperty("paidDate").ValueKind.Should().Be(JsonValueKind.Null);
            created.GetProperty("isOverdue").GetBoolean().Should().BeTrue("o prazo já passou");
            created.GetProperty("expenseTypeName").GetString().Should().Be("Aluguel");

            var paid = await mine.PatchAsJsonAsync(
                Url($"/api/store-expenses/{code}/payment"),
                new { isPaid = true, paidDate = pagamento.ToString("yyyy-MM-dd") });

            paid.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var janela = $"?from={hoje.AddDays(-40):yyyy-MM-dd}&to={hoje:yyyy-MM-dd}";
            var listed = await ReadDataAsync(await mine.GetAsync(Url($"/api/store-expenses{janela}")));

            var line = listed.EnumerateArray().Single(e => e.GetProperty("code").GetGuid() == code);

            line.GetProperty("isPaid").GetBoolean().Should().BeTrue();
            line.GetProperty("paidDate").GetString().Should().StartWith(pagamento.ToString("yyyy-MM-dd"));
            line.GetProperty("isOverdue").GetBoolean().Should().BeFalse("paga jamais atrasa");
            line.GetProperty("dueDate").GetString().Should().StartWith(
                vencimento.ToString("yyyy-MM-dd"), "pagar com atraso jamais reescreve o prazo");

            // Desfazer devolve a despesa para prevista, e a data de pagamento sai junto.
            await mine.PatchAsJsonAsync(Url($"/api/store-expenses/{code}/payment"), new { isPaid = false });

            var back = await ReadDataAsync(await mine.GetAsync(Url("/api/store-expenses")));
            var reopened = back.EnumerateArray().Single(e => e.GetProperty("code").GetGuid() == code);

            reopened.GetProperty("isPaid").GetBoolean().Should().BeFalse();
            reopened.GetProperty("paidDate").ValueKind.Should().Be(JsonValueKind.Null);
            reopened.GetProperty("isOverdue").GetBoolean().Should().BeTrue();

            // O período é lido sobre a data da despesa, e não sobre o vencimento.
            var outra = $"?from={hoje.AddDays(10):yyyy-MM-dd}&to={hoje.AddDays(40):yyyy-MM-dd}";
            var outraJanela = await ReadDataAsync(await mine.GetAsync(Url($"/api/store-expenses{outra}")));

            outraJanela.EnumerateArray().Should().NotContain(e => e.GetProperty("code").GetGuid() == code);
        }

        [Fact]
        public async Task AOutraRevenda_EnxergaNada_DaDespesaDaLoja()
        {
            var types = await ReadDataAsync(await mine.GetAsync(Url("/api/expense-types?scope=2")));

            var created = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/store-expenses"), new
            {
                description = "Energia de setembro",
                expenseTypeCode = types[0].GetProperty("code").GetGuid(),
                amount = 780,
                date = "2026-09-05",
                isPaid = false,
            }));

            var code = created.GetProperty("code").GetGuid();
            expenses.Add(code);

            var hers = await api.AsAsync(other.AdminEmail);

            var theirs = await ReadDataAsync(await hers.GetAsync(Url("/api/store-expenses")));
            theirs.EnumerateArray().Should().NotContain(e => e.GetProperty("code").GetGuid() == code);

            var edit = await hers.PatchAsJsonAsync(Url($"/api/store-expenses/{code}/payment"), new { isPaid = true });
            edit.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage answer)
        {
            answer.IsSuccessStatusCode.Should().BeTrue(await answer.Content.ReadAsStringAsync());

            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            return body.GetProperty("data");
        }

        /// <summary>O motivo da recusa, já sem o escape de acentos do JSON.</summary>
        private static async Task<string> DetailOf(HttpResponseMessage answer)
        {
            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            return body.GetProperty("detail").GetString() ?? string.Empty;
        }

        private static Uri Url(string path) => new(path, UriKind.Relative);
    }
}
