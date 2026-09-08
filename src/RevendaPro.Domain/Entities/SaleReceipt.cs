using System.Diagnostics;
using RevendaPro.Domain.Enums;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// Uma entrada de dinheiro de uma venda (M22).
    ///
    /// A venda registrada era dinheiro no bolso para o sistema, mesmo quando o banco paga em
    /// quinze dias. Aqui fica <b>o que entrou</b>, com data: à vista, uma linha no dia; no
    /// financiamento, a linha nasce quando o banco deposita.
    ///
    /// <b>O saldo a receber é subtração, e jamais coluna</b>: o esperado em dinheiro da venda
    /// menos a soma das entradas, feita a cada leitura — a mesma regra do custo, desde o M6. Uma
    /// coluna <c>Recebido</c> seria mais um número para discordar da soma.
    ///
    /// O isolamento chega pela venda, que chega pelo veículo, que carrega a revenda: toda
    /// consulta daqui passa por esses dois <c>JOIN</c>, e é onde o filtro do <c>IdTenant</c> mora.
    /// </summary>
    [DebuggerDisplay("IdSale={IdSale}, Amount={Amount}, Date={Date}")]
    public class SaleReceipt : Foundation.Domain.Abstractions.Entity
    {
        private SaleReceipt() { }

        /// <summary>A venda a que esta entrada pertence.</summary>
        public int IdSale { get; private set; }

        /// <summary>Quanto entrou.</summary>
        public decimal Amount { get; private set; }

        /// <summary>Quando entrou.</summary>
        public DateOnly Date { get; private set; }

        /// <summary>Como entrou. Pode diferir do combinado: fechou financiado e pagou à vista.</summary>
        public PaymentMethod PaymentMethod { get; private set; }

        /// <summary>Anotação livre: o banco, o número do contrato, a parcela.</summary>
        public string? Notes { get; private set; }

        /// <summary>Registra uma entrada de dinheiro.</summary>
        /// <param name="idSale">A venda.</param>
        /// <param name="amount">Quanto entrou.</param>
        /// <param name="date">Quando entrou.</param>
        /// <param name="paymentMethod">Como entrou.</param>
        /// <param name="notes">Anotação livre.</param>
        /// <param name="createdBy">Quem registrou.</param>
        /// <returns>A entrada.</returns>
        public static SaleReceipt Create(
            int idSale,
            decimal amount,
            DateOnly date,
            PaymentMethod paymentMethod,
            string? notes = null,
            string createdBy = SystemActor)
        {
            if (amount <= 0)
            {
                throw new BusinessRuleException("Informe um valor maior que zero.");
            }

            var receipt = new SaleReceipt
            {
                IdSale = idSale,
                Amount = amount,
                Date = date,
                PaymentMethod = paymentMethod,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            };

            receipt.SetCreatedBy(createdBy);

            return receipt;
        }
    }
}
