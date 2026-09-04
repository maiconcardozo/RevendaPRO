using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// Uma revenda jamais alcança o dado da outra (RNF-04).
    ///
    /// É a promessa mais cara do sistema: a isolação por empresa está escrita em toda consulta,
    /// e até aqui ninguém tinha provado que ela vale <b>pela API</b>. A matriz do V2 responde
    /// outra pergunta — <i>"este perfil pode chamar este endpoint?"</i>. Esta aqui responde
    /// <i>"este dado é meu?"</i>.
    ///
    /// <b>A resposta certa é 404, e jamais 403.</b> Para quem está na empresa A, o registro da B
    /// simplesmente não existe: um 403 confirmaria que ele existe, e isso já é informação.
    ///
    /// O administrador da outra revenda tem <b>todas as telas</b>, de propósito. Isolação que
    /// dependesse de permissão curta não seria isolação.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class TenantIsolationTests(ApiFixture api) : IAsyncLifetime
    {
        private SecondDealership other = default!;
        private HttpClient mine = default!;

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            other = await api.OtherDealershipAsync();
            mine = await api.ClientOfAsync("admin@revendapro.local");
        }

        /// <inheritdoc/>
        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task TheOwnerReachesItsOwnCar()
        {
            // Guarda deste arquivo inteiro: sem isto, um 404 lá embaixo poderia significar que
            // o carro jamais existiu — e a isolação estaria "provada" sobre o nada.
            var hers = await api.AsAsync(other.AdminEmail);

            var answer = await hers.GetAsync(Url($"/api/vehicles/{other.VehicleCode}"));

            answer.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("data").GetProperty("plate").GetString().Should().Be(other.Plate);
        }

        public static TheoryData<string, string, string> Reads()
        {
            var data = new TheoryData<string, string, string>();

            void Read(string what, string url) => data.Add(what, "GET", url);

            Read("o veículo", "/api/vehicles/{v}");
            Read("os gastos", "/api/vehicles/{v}/expenses");
            Read("as fotos", "/api/vehicles/{v}/photos");
            Read("os documentos", "/api/vehicles/{v}/documents");
            Read("a etiqueta de um documento", "/api/vehicles/{v}/documents");
            Read("as propostas", "/api/vehicles/{v}/proposals");
            Read("a venda", "/api/vehicles/{v}/sale");
            Read("a linha do tempo", "/api/vehicles/{v}/timeline");
            Read("a simulação do negócio", "/api/vehicles/{v}/deal-preview?amount=1000");
            Read("a foto da pessoa", "/api/users/{u}/photo");

            return data;
        }

        public static TheoryData<string, string, string> Writes()
        {
            var data = new TheoryData<string, string, string>();

            void Write(string what, string method, string url) => data.Add(what, method, url);

            Write("excluir o veículo", "DELETE", "/api/vehicles/{v}");
            Write("mover na esteira", "PATCH", "/api/vehicles/{v}/status");
            Write("consultar a FIPE dele", "POST", "/api/vehicles/{v}/fipe");
            Write("excluir o gasto dele", "DELETE", "/api/vehicles/{v}/expenses/{e}");
            Write("excluir a foto dele", "DELETE", "/api/vehicles/{v}/photos/{p}");
            Write("excluir o documento dele", "DELETE", "/api/vehicles/{v}/documents/{d}");
            Write("excluir a proposta dele", "DELETE", "/api/vehicles/{v}/proposals/{pr}");
            Write("desfazer a venda dele", "DELETE", "/api/vehicles/{v}/sale");
            Write("bloquear a pessoa", "PATCH", "/api/users/{u}/status");
            Write("excluir a pessoa", "DELETE", "/api/users/{u}");
            Write("devolver a pessoa excluída", "POST", "/api/users/{u}/restore");
            Write("apagar a foto da pessoa", "DELETE", "/api/users/{u}/photo");
            Write("excluir o perfil", "DELETE", "/api/roles/{r}");

            return data;
        }

        /// <summary>
        /// As escritas que exigem um corpo válido antes de chegar ao handler.
        ///
        /// A validação roda como comportamento do pipeline, <b>antes</b> do handler: com corpo
        /// vazio elas respondem 400, e um 400 não prova isolação nenhuma — ele prova apenas que
        /// o corpo estava vazio. Aqui o que se afirma é o que de fato importa: a resposta jamais
        /// é sucesso, e portanto nada da outra revenda mudou.
        /// </summary>
        public static TheoryData<string, string, string> WritesThatNeedABody()
        {
            var data = new TheoryData<string, string, string>();

            void Write(string what, string method, string url) => data.Add(what, method, url);

            Write("editar o veículo", "PUT", "/api/vehicles/{v}");
            Write("lançar gasto nele", "POST", "/api/vehicles/{v}/expenses");
            Write("editar o gasto dele", "PUT", "/api/vehicles/{v}/expenses/{e}");
            Write("receber proposta nele", "POST", "/api/vehicles/{v}/proposals");
            Write("vender o carro dele", "POST", "/api/vehicles/{v}/sale");
            Write("editar a pessoa", "PUT", "/api/users/{u}");
            Write("editar o perfil", "PUT", "/api/roles/{r}");

            return data;
        }

        [Theory]
        [MemberData(nameof(WritesThatNeedABody))]
        public async Task WhatBelongsToTheOtherDealership_IsNeverChangedByMe(
            string what,
            string method,
            string template)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), Url(Fill(template)));
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var answer = await mine.SendAsync(request);

            answer.IsSuccessStatusCode.Should().BeFalse(
                "{0} é da outra revenda ({1} {2})", what, method, template);
        }

        [Theory]
        [MemberData(nameof(Reads))]
        [MemberData(nameof(Writes))]
        public async Task WhatBelongsToTheOtherDealership_DoesNotExistForMe(
            string what,
            string method,
            string template)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), Url(Fill(template)));

            if (method is "POST" or "PUT" or "PATCH")
            {
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            }

            var answer = await mine.SendAsync(request);

            answer.StatusCode.Should().Be(
                HttpStatusCode.NotFound,
                "{0} é da outra revenda ({1} {2})", what, method, template);
        }

        [Fact]
        public async Task TheYardListing_NeverBringsTheOtherDealershipsCar()
        {
            var answer = await mine.GetAsync(Url("/api/vehicles"));
            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            var plates = body.GetProperty("data").EnumerateArray()
                .Select(vehicle => vehicle.GetProperty("plate").GetString())
                .ToList();

            plates.Should().NotContain(other.Plate);
        }

        [Fact]
        public async Task SearchingForTheOtherDealershipsPlate_FindsNothing()
        {
            // A busca é o caminho mais curto para vazar: quem sabe a placa, digita a placa.
            var answer = await mine.GetAsync(Url($"/api/vehicles?search={other.Plate}"));
            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            body.GetProperty("data").GetArrayLength().Should().Be(0);
        }

        [Fact]
        public async Task TheMarketScreen_NeverBringsTheOtherDealershipsCar()
        {
            var answer = await mine.GetAsync(Url("/api/market"));
            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            var data = body.GetProperty("data");

            foreach (var section in new[] { "yard", "sold" })
            {
                data.GetProperty(section).EnumerateArray()
                    .Select(line => line.GetProperty("plate").GetString())
                    .Should().NotContain(other.Plate, section);
            }
        }

        [Fact]
        public async Task ThePeopleListing_NeverBringsTheOtherDealershipsStaff()
        {
            var answer = await mine.GetAsync(Url("/api/users"));
            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            body.GetProperty("data").EnumerateArray()
                .Select(user => user.GetProperty("email").GetString())
                .Should().NotContain(other.AdminEmail);
        }

        [Fact]
        public async Task TheDeletedDocumentsScreen_NeverBringsTheOtherDealershipsFile()
        {
            // Ela existe para mostrar o que toda outra leitura esconde, então é o lugar mais
            // provável de a isolação escapar — e o documento nem carrega empresa: ele pende do
            // veículo, e depende do join.
            var answer = await mine.GetAsync(Url("/api/deleted-documents"));
            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            body.GetProperty("data").EnumerateArray()
                .Select(document => document.GetProperty("fileName").GetString())
                .Should().NotContain("nota-da-esquina.pdf");
        }

        [Fact]
        public async Task TheOtherDealershipsDocument_CannotBeRestoredByMe()
        {
            var answer = await mine.PostAsync(
                Url($"/api/deleted-documents/{other.DocumentCode}/restore"), content: null);

            answer.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task TheDashboard_CountsOnlyMyOwnYard()
        {
            var answer = await mine.GetAsync(Url("/api/dashboard"));
            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            // A revenda piloto nasce sem carro nenhum, e a outra tem um. Contar 1 aqui seria o
            // dinheiro da vizinha aparecendo no painel desta.
            body.GetProperty("data").GetProperty("inStock").GetInt32().Should().Be(0);
        }

        private string Fill(string template) => template
            .Replace("{v}", other.VehicleCode.ToString(), StringComparison.Ordinal)
            .Replace("{e}", other.ExpenseCode.ToString(), StringComparison.Ordinal)
            .Replace("{p}", other.PhotoCode.ToString(), StringComparison.Ordinal)
            .Replace("{d}", other.DocumentCode.ToString(), StringComparison.Ordinal)
            .Replace("{pr}", other.ProposalCode.ToString(), StringComparison.Ordinal)
            .Replace("{u}", other.AdminCode.ToString(), StringComparison.Ordinal)
            .Replace("{r}", other.RoleCode.ToString(), StringComparison.Ordinal);

        private static Uri Url(string path) => new(path, UriKind.Relative);
    }
}
