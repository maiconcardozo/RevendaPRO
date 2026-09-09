using FluentAssertions;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// Toda consulta que materializa um gasto lê todas as colunas dele.
    ///
    /// O defeito que este teste fecha aconteceu no M18: a coluna <c>IdSupplier</c> entrou no
    /// gasto, a migration a criou, a gravação a escrevia — e as quatro consultas que listam
    /// gastos continuaram com a lista de colunas antiga. O fornecedor era salvo e jamais lido:
    /// a tela mostrava vazio, e o semeador preenchia o mesmo gasto a cada subida. Nenhum teste
    /// de unidade pegou, porque o repositório é Dapper e a lista de colunas é texto.
    ///
    /// Aqui a lista é conferida contra a entidade: cada propriedade pública do gasto tem de
    /// aparecer no SELECT de toda consulta cujo nome diz que ela devolve gastos.
    /// </summary>
    public partial class SoftDeleteTests
    {
        /// <summary>
        /// Consultas que devolvem gasto sem materializar a entidade. Cada uma diz por quê, para
        /// entrar aqui ser uma decisão que alguém escreveu — e jamais um nome escolhido para
        /// escapar do teste.
        /// </summary>
        private static readonly Dictionary<string, string> ProjectsInsteadOfMaterializing = new()
        {
            ["ListDeletedVehicleExpensesQuery"] =
                "a lixeira do M23 projeta em DeletedVehicleExpense: ela mostra o gasto e o "
                + "carro dele, e jamais devolve a entidade para alguém alterar"
        };

        public static TheoryData<string, string> ExpenseQueries()
        {
            var data = new TheoryData<string, string>();

            foreach (var (name, sql) in QueriesOfKind("SELECT"))
            {
                if (ProjectsInsteadOfMaterializing.ContainsKey(name))
                {
                    continue;
                }

                if (name.Contains("VehicleExpense", StringComparison.Ordinal)
                    || name == "ListExpensesOfVehiclesQuery"
                    || name == "ListExpensesForSuggestionQuery")
                {
                    data.Add(name, sql);
                }
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(ExpenseQueries))]
        public void EveryQueryThatMaterializesAnExpense_ReadsEveryColumnOfIt(string name, string sql)
        {
            // As colunas deste projeto: o que o gasto e o VehicleEntity declaram. As do Foundation
            // (Id, Code, auditoria) vêm da mesma lista há dez marcos; o que se esquece é a nova.
            var columns = typeof(VehicleExpense).GetProperties()
                .Where(property => property.CanWrite && property.GetIndexParameters().Length == 0)
                .Where(property => property.DeclaringType!.Assembly == typeof(VehicleExpense).Assembly)
                .Select(property => property.Name)
                .ToList();

            columns.Should().Contain("IdSupplier");

            var select = sql[..sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase)];

            foreach (var column in columns)
            {
                select.Should().MatchRegex(
                    $@"(^|[\s,.]){column}([\s,]|$)",
                    "{0} materializes VehicleExpense and has to read {1}; a column written and never read is a silent loss. SQL:\n{2}",
                    name, column, sql);
            }
        }
    }
}
