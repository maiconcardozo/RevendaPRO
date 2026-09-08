using FluentAssertions;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// Toda consulta que materializa uma proposta, uma venda ou um cliente lê todas as colunas
    /// da entidade (M21).
    ///
    /// É o mesmo guarda de <c>ExpenseColumnsTests</c>, para as três tabelas que este marco tocou:
    /// <c>IdCustomer</c> entrou em Proposal e em Sale, e uma lista de colunas esquecida faria o
    /// cliente ser gravado e jamais lido — o defeito do M18, de novo, em outra tabela.
    ///
    /// A consulta é reconhecida pelo que devolve, e não pelo nome: um SELECT que lê da tabela e
    /// traz a coluna de auditoria <c>DeletedBy</c> está materializando a entidade inteira.
    /// </summary>
    public partial class SoftDeleteTests
    {
        public static TheoryData<string, string, string> EntityQueries()
        {
            var data = new TheoryData<string, string, string>();

            foreach (var (name, sql) in QueriesOfKind("SELECT"))
            {
                if (!sql.Contains("DeletedBy", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var table in new[] { "Proposal", "Sale", "Customer" })
                {
                    if (ReadsFrom(sql, table))
                    {
                        data.Add(name, sql, table);
                    }
                }
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(EntityQueries))]
        public void EveryQueryThatMaterializesAProposalASaleOrACustomer_ReadsEveryColumnOfIt(
            string name, string sql, string table)
        {
            var entity = table switch
            {
                "Proposal" => typeof(Proposal),
                "Sale" => typeof(Sale),
                _ => typeof(Customer),
            };

            var columns = entity.GetProperties()
                .Where(property => property.CanWrite && property.GetIndexParameters().Length == 0)
                .Where(property => property.DeclaringType!.Assembly == entity.Assembly)
                .Select(property => property.Name)
                .ToList();

            if (table != "Customer")
            {
                columns.Should().Contain("IdCustomer");
            }

            var select = sql[..sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase)];

            foreach (var column in columns)
            {
                select.Should().MatchRegex(
                    $@"(^|[\s,.]){column}([\s,]|$)",
                    "{0} materializes {1} and has to read {2}; a column written and never read is a silent loss. SQL:\n{3}",
                    name, table, column, sql);
            }
        }

        [Fact]
        public void TheGuard_SeesTheQueriesItExistsFor()
        {
            var names = EntityQueries().Select(row => (string)row[0]).ToList();

            names.Should().Contain("ListProposalsWithoutCustomerQuery");
            names.Should().Contain("ListSalesWithoutCustomerQuery");
            names.Should().Contain("ListCustomersByTenantQuery");
        }

        private static bool ReadsFrom(string sql, string table)
        {
            var from = sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
            var rest = sql[(from + 4)..].TrimStart();

            return rest.StartsWith(table + " ", StringComparison.Ordinal)
                || rest.StartsWith(table + "\n", StringComparison.Ordinal)
                || rest.StartsWith(table + "\r", StringComparison.Ordinal)
                || rest.TrimEnd().Equals(table, StringComparison.Ordinal);
        }
    }
}
