using FluentAssertions;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O prazo e a baixa do gasto (M22).
    ///
    /// Duas datas entram no gasto: <b>quando vence</b> e <b>quando o dinheiro saiu</b>. O que se
    /// prova aqui é que elas jamais discordam do estado — um gasto previsto com data de
    /// pagamento, ou um pago sem ela, seria uma linha que mente no relatório do caixa — e que o
    /// atraso é a conta simples que a tela pinta de vermelho.
    /// </summary>
    public class ExpenseDueDateTests
    {
        private static readonly DateOnly Lancamento = new(2026, 9, 1);

        [Fact]
        public void QuemPagaNaHora_JamaisPensaEmPrazo_EAsDuasDatasCaemNoDiaDoGasto()
        {
            var expense = VehicleExpense.Create(1, "Jogo de pneus", 1, 3_200m, Lancamento);

            expense.IsPaid.Should().BeTrue();
            expense.DueDate.Should().Be(Lancamento);
            expense.PaidDate.Should().Be(Lancamento);
            expense.IsOverdueOn(Lancamento.AddYears(1)).Should().BeFalse("pago jamais atrasa");
        }

        [Fact]
        public void OGastoPrevisto_NasceSemDataDePagamento_EComOPrazoInformado()
        {
            var vence = new DateOnly(2026, 9, 30);

            var expense = VehicleExpense.Create(
                1, "Retífica do cabeçote", 1, 3_900m, Lancamento, isPaid: false, dueDate: vence);

            expense.PaidDate.Should().BeNull();
            expense.DueDate.Should().Be(vence);

            expense.IsOverdueOn(vence).Should().BeFalse("no dia do vencimento ainda dá tempo");
            expense.IsOverdueOn(vence.AddDays(1)).Should().BeTrue();
        }

        [Fact]
        public void ABaixa_GuardaODiaEmQueODinheiroSaiu_EDesfazerDevolveOGastoParaPrevisto()
        {
            var expense = VehicleExpense.Create(
                1, "Funilaria", 1, 2_600m, Lancamento, isPaid: false, dueDate: new DateOnly(2026, 9, 30));

            expense.MarkAsPaid(new DateOnly(2026, 10, 3));

            expense.IsPaid.Should().BeTrue();
            expense.PaidDate.Should().Be(new DateOnly(2026, 10, 3));
            expense.DueDate.Should().Be(new DateOnly(2026, 9, 30), "pagar com atraso jamais reescreve o prazo");

            expense.MarkAsPlanned();

            expense.IsPaid.Should().BeFalse();
            expense.PaidDate.Should().BeNull("desfazer a baixa tira a data junto");
        }

        [Fact]
        public void EditarJamaisDeixaOEstadoEADataDiscordarem()
        {
            var expense = VehicleExpense.Create(1, "Revisão", 1, 800m, Lancamento);

            // De pago para previsto: a data de pagamento sai.
            expense.Update("Revisão", 1, 800m, Lancamento, null, isPaid: false);
            expense.PaidDate.Should().BeNull();

            // De previsto para pago, sem dizer quando: cai na data do gasto.
            expense.Update("Revisão", 1, 800m, Lancamento, null, isPaid: true);
            expense.PaidDate.Should().Be(Lancamento);

            // Editando um pago sem tocar na data: a que estava fica.
            expense.MarkAsPaid(new DateOnly(2026, 9, 15));
            expense.Update("Revisão completa", 1, 900m, Lancamento, null, isPaid: true);
            expense.PaidDate.Should().Be(new DateOnly(2026, 9, 15));
        }

        [Fact]
        public void OPrazoEmBranco_CaiNaDataDoGasto_MesmoAoEditar()
        {
            var expense = VehicleExpense.Create(
                1, "Documentação", 1, 1_200m, Lancamento, isPaid: false, dueDate: new DateOnly(2026, 9, 20));

            var outraData = new DateOnly(2026, 8, 10);

            expense.Update("Documentação", 1, 1_200m, outraData, null, isPaid: false);

            expense.DueDate.Should().Be(outraData, "sem prazo informado, o vencimento segue a data do gasto");
        }
    }
}
