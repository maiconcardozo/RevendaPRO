using System.Net;
using FluentAssertions;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// O que cada perfil <b>jamais</b> pode alcançar, dito em português e à mão.
    ///
    /// A matriz derivada do <c>PermissionMatrixTests</c> prova que a fechadura combina com a
    /// própria etiqueta: se um endpoint declara <c>market</c>, quem tem <c>market</c> passa e
    /// quem não tem leva 403. <b>Ela não prova que a etiqueta é a certa.</b> Trocar
    /// <c>[RequireScreen("market")]</c> por <c>[RequireScreen("vehicles")]</c> deixaria a matriz
    /// verde e abriria a tela de Mercado para o Vendedor e para a Oficina, em silêncio.
    ///
    /// É por isso que esta lista existe, e é por isso que ela é <b>curta e escrita à mão</b>:
    /// ela não repete o mapa de permissões, que envelheceria. Ela declara a intenção de
    /// segurança nos poucos lugares onde errar custa caro — dinheiro, dado pessoal e o próprio
    /// controle de acesso.
    ///
    /// Uma linha nova aqui é uma decisão de negócio, e deve ser lida como tal.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class ForbiddenByDesignTests(ApiFixture api)
    {
        private const string Manager = "renata.albuquerque@revendapro.local";
        private const string Finance = "sergio.bittencourt@revendapro.local";
        private const string Salesperson = "joao.vendedor@revendapro.local";
        private const string Workshop = "wagner.toledo@revendapro.local";

        public static TheoryData<string, string, string, string, string> Refusals()
        {
            var data = new TheoryData<string, string, string, string, string>();

            void Refuse(string profile, string email, string method, string url, string why) =>
                data.Add(profile, email, method, url, why);

            // Dinheiro. Quem repara o carro jamais fecha o negócio dele.
            Refuse("Oficina", Workshop, "POST", $"/api/vehicles/{Guid.NewGuid()}/sale",
                "a oficina repara o carro, e quem fecha venda é quem vende");

            Refuse("Oficina", Workshop, "GET", "/api/sales",
                "o resultado das vendas é leitura de gestão");

            Refuse("Oficina", Workshop, "POST", $"/api/vehicles/{Guid.NewGuid()}/proposals",
                "receber oferta é trabalho de quem vende");

            // A tela de Mercado responde por preço e margem contra a tabela.
            Refuse("Vendedor", Salesperson, "GET", "/api/market",
                "a comparação com a tabela é leitura de gestão, e não de balcão");

            Refuse("Oficina", Workshop, "GET", "/api/market",
                "a comparação com a tabela é leitura de gestão");

            // Controle de acesso: quem pode conceder tela pode conceder tudo.
            foreach (var (profile, email) in new[]
            {
                ("Gestor", Manager), ("Financeiro", Finance),
                ("Vendedor", Salesperson), ("Oficina", Workshop),
            })
            {
                Refuse(profile, email, "GET", "/api/users",
                    "quem administra usuário cria conta, e conta criada entra no sistema");

                Refuse(profile, email, "GET", "/api/roles",
                    "quem administra perfil concede tela, e tela concedida abre tudo o mais");

                Refuse(profile, email, "GET", "/api/deleted-documents",
                    "a tela de excluídos mostra o que toda outra leitura esconde");
            }

            // Dado pessoal do comprador vive dentro da venda.
            Refuse("Oficina", Workshop, "GET", $"/api/vehicles/{Guid.NewGuid()}/sale",
                "a venda carrega CPF e telefone do comprador (RNF-13)");

            // O catálogo de gastos é da revenda, e mexer nele muda o passado de todo carro.
            Refuse("Vendedor", Salesperson, "POST", "/api/expense-types",
                "criar tipo de gasto é manutenção do catálogo da revenda");

            Refuse("Oficina", Workshop, "POST", "/api/expense-types",
                "criar tipo de gasto é manutenção do catálogo da revenda");

            return data;
        }

        [Theory]
        [MemberData(nameof(Refusals))]
        public async Task WhatMustNeverBeReached_Answers403(
            string profile,
            string email,
            string method,
            string url,
            string why)
        {
            var client = await api.ClientOfAsync(email);

            using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(url, UriKind.Relative));

            if (method is "POST" or "PUT" or "PATCH")
            {
                request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            }

            var answer = await client.SendAsync(request);

            // 403, e jamais 404: a resposta certa para "existe, e não é para você" é a recusa.
            answer.StatusCode.Should().Be(
                HttpStatusCode.Forbidden, "{0} jamais alcança {1} {2} — {3}", profile, method, url, why);
        }

        [Fact]
        public async Task TheAdministratorReachesWhatTheOthersCannot()
        {
            var client = await api.ClientOfAsync("admin@revendapro.local");

            foreach (var url in new[] { "/api/users", "/api/roles", "/api/deleted-documents", "/api/market" })
            {
                var answer = await client.GetAsync(new Uri(url, UriKind.Relative));

                // Guarda desta lista: se tudo respondesse 403 por outro motivo — uma sessão
                // quebrada, por exemplo —, as recusas acima passariam sem provar nada.
                answer.StatusCode.Should().Be(HttpStatusCode.OK, url);
            }
        }
    }
}
