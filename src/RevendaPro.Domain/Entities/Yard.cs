using System.Diagnostics;
using RevendaPro.Domain.Enums;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// Um lugar onde o carro fica: o pátio da própria revenda, ou a loja de outra pessoa onde
    /// ela deixou o carro para vender.
    ///
    /// <b>Um cadastro só, com um tipo dentro.</b> Pátio próprio e loja de terceiro não viram
    /// duas tabelas: o stakeholder descreveu os dois como a mesma coisa — <i>"tudo seria pátio,
    /// são os mesmos carros, só discriminado por pátio"</i> —, e é isso que mantém a soma
    /// possível. Dois cadastros exigiriam somar duas coisas diferentes em todo relatório, e
    /// alguém acabaria somando só uma.
    ///
    /// O que o tipo muda é o comportamento: pátio próprio jamais paga comissão, e loja de
    /// terceiro quase sempre paga. Ver ADR-0002 para a tela que o administra.
    /// </summary>
    [DebuggerDisplay("Name={Name}, Kind={Kind}, IdTenant={IdTenant}")]
    public class Yard : TenantEntity
    {
        private Yard() { }

        private Yard(int idTenant) : base(idTenant) { }

        /// <summary>Como a revenda chama o lugar: "Pátio Centro", "Loja do Joãozinho".</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>Se o lugar é da própria revenda ou de outra pessoa.</summary>
        public YardKind Kind { get; private set; }

        /// <summary>Quem responde pelo lugar, quando ele é de outra pessoa.</summary>
        public string? ContactName { get; private set; }

        /// <summary>Telefone do responsável, só dígitos.</summary>
        public string? ContactPhone { get; private set; }

        /// <summary>
        /// O percentual combinado de repasse, quando ele foi combinado em percentual.
        ///
        /// Ele é <b>sugestão</b>, e não regra: a tela de venda chega preenchida com ele, e quem
        /// fecha o negócio pode mudar. O combinado de hoje pode não ser o do próximo carro.
        /// </summary>
        public decimal? CutPercent { get; private set; }

        /// <summary>O valor combinado de repasse, quando ele foi combinado em reais.</summary>
        public decimal? CutAmount { get; private set; }

        /// <summary>Anotação livre sobre o lugar.</summary>
        public string? Notes { get; private set; }

        /// <summary>Ordem na lista, para o pátio mais usado ficar em cima.</summary>
        public int Position { get; private set; }

        /// <summary>Se o lugar é da própria revenda.</summary>
        public bool IsOwn => Kind == YardKind.Own;

        /// <summary>Cadastra um lugar.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="name">Como a revenda chama o lugar.</param>
        /// <param name="kind">Próprio ou de outra pessoa.</param>
        /// <param name="position">Ordem na lista.</param>
        /// <param name="createdBy">Quem cadastrou.</param>
        /// <returns>O pátio.</returns>
        public static Yard Create(
            int idTenant,
            string name,
            YardKind kind,
            int position = 0,
            string createdBy = SystemActor)
        {
            var yard = new Yard(idTenant) { Position = position };

            yard.Rename(name);
            yard.SetKind(kind);
            yard.SetCreatedBy(createdBy);

            return yard;
        }

        /// <summary>Muda o nome do lugar.</summary>
        /// <param name="name">O nome.</param>
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessRuleException("Informe o nome do pátio.");
            }

            Name = name.Trim();
        }

        /// <summary>
        /// Diz se o lugar é próprio ou de outra pessoa.
        ///
        /// Virar próprio limpa o repasse combinado: pátio da casa jamais cobra da casa, e um
        /// percentual esquecido ali apareceria preenchido numa venda que não deve ter repasse.
        /// </summary>
        /// <param name="kind">O tipo.</param>
        public void SetKind(YardKind kind)
        {
            Kind = kind;

            if (kind == YardKind.Own)
            {
                CutPercent = null;
                CutAmount = null;
            }
        }

        /// <summary>Guarda quem responde pelo lugar.</summary>
        /// <param name="contactName">Nome do responsável.</param>
        /// <param name="contactPhone">Telefone, só dígitos.</param>
        /// <param name="notes">Anotação livre.</param>
        public void SetContact(string? contactName, string? contactPhone, string? notes)
        {
            ContactName = Trim(contactName);
            ContactPhone = Digits(contactPhone);
            Notes = Trim(notes);
        }

        /// <summary>
        /// Guarda o repasse combinado com o lugar.
        ///
        /// Percentual <b>ou</b> valor, e jamais os dois: combinar das duas formas ao mesmo tempo
        /// deixa a venda sem saber qual usar — que é a mesma regra que a proposta e a venda já
        /// seguem desde o M8.
        /// </summary>
        /// <param name="cutPercent">Percentual combinado.</param>
        /// <param name="cutAmount">Valor combinado.</param>
        public void SetCut(decimal? cutPercent, decimal? cutAmount)
        {
            if (Kind == YardKind.Own && (cutPercent is > 0 || cutAmount is > 0))
            {
                throw new BusinessRuleException("O pátio da própria revenda fica sem repasse.");
            }

            if (cutPercent is > 0 && cutAmount is > 0)
            {
                throw new BusinessRuleException(
                    "Combine o repasse em percentual ou em valor, e jamais nos dois.");
            }

            if (cutPercent is < 0 || cutAmount is < 0)
            {
                throw new BusinessRuleException("Informe um repasse maior que zero.");
            }

            if (cutPercent is > 100)
            {
                throw new BusinessRuleException("O repasse em percentual fica até 100%.");
            }

            CutPercent = cutPercent is > 0 ? cutPercent : null;
            CutAmount = cutAmount is > 0 ? cutAmount : null;
        }

        /// <summary>Muda a ordem na lista.</summary>
        /// <param name="position">A posição.</param>
        public void MoveTo(int position) => Position = position;

        private static string? Trim(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? Digits(string? value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

            return digits.Length == 0 ? null : digits;
        }
    }
}
