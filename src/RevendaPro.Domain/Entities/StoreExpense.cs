using System.Diagnostics;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// O que a loja paga e que jamais pertence a um carro: aluguel, energia, salário, imposto,
    /// contador, internet (M22).
    ///
    /// <b>Por que uma entidade própria, e não um gasto sem carro.</b> O
    /// <see cref="VehicleExpense"/> chega à revenda <b>pelo veículo</b>, de propósito: existe um
    /// lugar onde uma linha pode ser presa à empresa errada, em vez de cinco. Um gasto com
    /// veículo nulo quebraria isso, e toda consulta que hoje chega ao dono pelo carro passaria a
    /// ter dois caminhos. Aqui o <c>IdTenant</c> é próprio, como no fornecedor e no cliente.
    ///
    /// O <see cref="ExpenseType"/> e o <see cref="Supplier"/> são os mesmos do carro: aluguel é
    /// do tipo Aluguel, pago à Imobiliária Central. Dois catálogos de "tipos" seria a pessoa
    /// perguntando qual é qual — o tipo diz para onde serve, em <see cref="ExpenseType.Scope"/>.
    ///
    /// Ver <c>docs/plans/m22-caixa.md</c>.
    /// </summary>
    [DebuggerDisplay("Description={Description}, Amount={Amount}, IsPaid={IsPaid}")]
    public class StoreExpense : TenantEntity
    {
        private StoreExpense() { }

        private StoreExpense(int idTenant) : base(idTenant) { }

        /// <summary>O que é: "Aluguel de outubro", "Energia — setembro".</summary>
        public string Description { get; private set; } = string.Empty;

        /// <summary>O tipo, do mesmo catálogo do gasto do carro.</summary>
        public int IdExpenseType { get; private set; }

        /// <summary>A quem se paga, quando cadastrado. Nulo para imposto e taxa.</summary>
        public int? IdSupplier { get; private set; }

        public decimal Amount { get; private set; }

        /// <summary>A que dia a despesa pertence — o mês do aluguel, e não o dia em que se pagou.</summary>
        public DateOnly Date { get; private set; }

        /// <summary>Quando vence. Sem prazo informado, é a data da despesa.</summary>
        public DateOnly DueDate { get; private set; }

        /// <summary>Quando o dinheiro saiu. Nulo enquanto está previsto.</summary>
        public DateOnly? PaidDate { get; private set; }

        /// <summary>Falso é previsto: o dinheiro ainda está na conta.</summary>
        public bool IsPaid { get; private set; }

        public string? Notes { get; private set; }

        /// <summary>Vencida e ainda sem pagamento, na data de referência.</summary>
        /// <param name="today">O dia de hoje, na visão de quem pergunta.</param>
        /// <returns>Verdadeiro quando o prazo passou e o dinheiro continua na conta.</returns>
        public bool IsOverdueOn(DateOnly today) => !IsPaid && DueDate < today;

        /// <summary>Lança uma despesa da loja.</summary>
        /// <param name="idTenant">Revenda dona da despesa.</param>
        /// <param name="description">O que é.</param>
        /// <param name="idExpenseType">O tipo.</param>
        /// <param name="amount">Quanto.</param>
        /// <param name="date">A que dia pertence.</param>
        /// <param name="dueDate">Quando vence. Sem prazo, é a data da despesa.</param>
        /// <param name="isPaid">Se já foi paga.</param>
        /// <param name="paidDate">Quando o dinheiro saiu. Sem data, é a data da despesa.</param>
        /// <param name="idSupplier">A quem se paga, quando cadastrado.</param>
        /// <param name="notes">Anotação livre.</param>
        /// <param name="createdBy">Quem lançou.</param>
        /// <returns>A despesa.</returns>
        public static StoreExpense Create(
            int idTenant,
            string description,
            int idExpenseType,
            decimal amount,
            DateOnly date,
            DateOnly? dueDate = null,
            bool isPaid = false,
            DateOnly? paidDate = null,
            int? idSupplier = null,
            string? notes = null,
            string createdBy = SystemActor)
        {
            var expense = new StoreExpense(idTenant);

            expense.Fill(description, idExpenseType, amount, date, dueDate, isPaid, paidDate, idSupplier, notes);
            expense.SetCreatedBy(createdBy);

            return expense;
        }

        /// <summary>Edita a despesa.</summary>
        /// <param name="description">O que é.</param>
        /// <param name="idExpenseType">O tipo.</param>
        /// <param name="amount">Quanto.</param>
        /// <param name="date">A que dia pertence.</param>
        /// <param name="dueDate">Quando vence. Sem prazo, é a data da despesa.</param>
        /// <param name="isPaid">Se já foi paga.</param>
        /// <param name="paidDate">Quando o dinheiro saiu.</param>
        /// <param name="idSupplier">A quem se paga.</param>
        /// <param name="notes">Anotação livre.</param>
        /// <param name="updatedBy">Quem mudou.</param>
        public void Update(
            string description,
            int idExpenseType,
            decimal amount,
            DateOnly date,
            DateOnly? dueDate,
            bool isPaid,
            DateOnly? paidDate,
            int? idSupplier,
            string? notes,
            string updatedBy = SystemActor)
        {
            Fill(description, idExpenseType, amount, date, dueDate, isPaid, paidDate ?? PaidDate, idSupplier, notes);

            UpdateAuditInfo(updatedBy);
        }

        /// <summary>Dá baixa: o dinheiro saiu, e o dia em que saiu fica registrado.</summary>
        /// <param name="paidOn">O dia em que o dinheiro saiu.</param>
        /// <param name="updatedBy">Quem deu a baixa.</param>
        public void MarkAsPaid(DateOnly paidOn, string updatedBy = SystemActor)
        {
            IsPaid = true;
            PaidDate = paidOn;

            UpdateAuditInfo(updatedBy);
        }

        /// <summary>Desfaz a baixa: a despesa volta a previsto, e a data de pagamento sai.</summary>
        /// <param name="updatedBy">Quem desfez.</param>
        public void MarkAsPlanned(string updatedBy = SystemActor)
        {
            IsPaid = false;
            PaidDate = null;

            UpdateAuditInfo(updatedBy);
        }

        private void Fill(
            string description,
            int idExpenseType,
            decimal amount,
            DateOnly date,
            DateOnly? dueDate,
            bool isPaid,
            DateOnly? paidDate,
            int? idSupplier,
            string? notes)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new BusinessRuleException("Descreva a despesa.");
            }

            if (idExpenseType <= 0)
            {
                throw new BusinessRuleException("Escolha o tipo da despesa.");
            }

            if (amount <= 0)
            {
                throw new BusinessRuleException("Informe um valor maior que zero.");
            }

            Description = description.Trim();
            IdExpenseType = idExpenseType;
            Amount = amount;
            Date = date;
            DueDate = dueDate ?? date;
            IsPaid = isPaid;

            // O estado e a data jamais discordam, como no gasto do carro.
            PaidDate = isPaid ? paidDate ?? date : null;

            IdSupplier = idSupplier;
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        }
    }
}
