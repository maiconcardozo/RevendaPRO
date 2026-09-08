using System.Diagnostics;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// Quem a revenda conhece: a pessoa que ofereceu por um carro, a que comprou, a que volta.
    ///
    /// <b>Cliente antes de comprador.</b> Quem ofereceu e foi recusado, e liga de novo em seis
    /// meses, existe aqui desde a primeira proposta. A venda e a proposta apontam para o cliente
    /// e guardam a cópia do que estava no papel no dia (nome, telefone, documento), porque um
    /// contrato diz quem assinou mesmo que a pessoa troque de telefone depois.
    ///
    /// Nome e telefone bastam para nascer: um cadastro na frente da proposta é uma proposta a
    /// menos registrada. Documento, e-mail e endereço vêm depois, na ficha ou na hora da venda.
    /// Ver <c>docs/plans/m21-clientes.md</c>.
    /// </summary>
    [DebuggerDisplay("Name={Name}, Phone={Phone}, IdTenant={IdTenant}")]
    public class Customer : TenantEntity
    {
        private Customer() { }

        private Customer(int idTenant) : base(idTenant) { }

        /// <summary>Como a pessoa se apresenta: "Marcos Silva", "Dona Núbia".</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>CPF ou CNPJ, só dígitos e válido. Opcional até a venda pedir.</summary>
        public string? Document { get; private set; }

        /// <summary>Telefone, só dígitos. É por onde o WhatsApp chega.</summary>
        public string? Phone { get; private set; }

        public string? Email { get; private set; }

        /// <summary>Endereço em uma linha, como vai no papel.</summary>
        public string? Address { get; private set; }

        /// <summary>Anotação livre: "prefere carro branco", "irmão do Zé da oficina".</summary>
        public string? Notes { get; private set; }

        /// <summary>Cadastra um cliente. Nome e telefone bastam.</summary>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="name">Como a pessoa se apresenta.</param>
        /// <param name="phone">Telefone, em qualquer formato. Opcional.</param>
        /// <param name="createdBy">Quem cadastrou.</param>
        /// <returns>O cliente.</returns>
        public static Customer Create(
            int idTenant,
            string name,
            string? phone,
            string createdBy = SystemActor)
        {
            var customer = new Customer(idTenant);

            customer.Rename(name);
            customer.Phone = Digits(phone);
            customer.SetCreatedBy(createdBy);

            return customer;
        }

        /// <summary>Muda o nome.</summary>
        /// <param name="name">O nome.</param>
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessRuleException("Informe o nome do cliente.");
            }

            Name = name.Trim();
        }

        /// <summary>
        /// Guarda como chegar ao cliente e como ele assina.
        ///
        /// O documento é conferido de verdade — dígito verificador incluído —, porque é o que
        /// vai na linha de assinatura da proposta e no contrato da venda. O telefone é só dígitos.
        /// </summary>
        /// <param name="document">CPF ou CNPJ, em qualquer formato.</param>
        /// <param name="phone">Telefone, em qualquer formato.</param>
        /// <param name="email">E-mail.</param>
        /// <param name="address">Endereço, em uma linha.</param>
        /// <param name="notes">Anotação livre.</param>
        /// <param name="updatedBy">Quem mudou.</param>
        public void SetContact(
            string? document,
            string? phone,
            string? email,
            string? address,
            string? notes,
            string updatedBy = SystemActor)
        {
            var digits = Digits(document);

            if (digits is not null && !BrazilianDocuments.IsValidCpfOrCnpj(digits))
            {
                throw new BusinessRuleException("Informe um CPF ou CNPJ válido.");
            }

            Document = digits;
            Phone = Digits(phone);
            Email = Trim(email)?.ToLowerInvariant();
            Address = Trim(address);
            Notes = Trim(notes);

            UpdateAuditInfo(updatedBy);
        }

        /// <summary>
        /// Completa o que ainda está em branco com o que veio de uma proposta ou de uma venda
        /// antiga. Existe para o aproveitamento na primeira subida: o cliente nasce da venda que
        /// tinha o CPF, e a proposta que só tinha o telefone acrescenta o telefone.
        /// </summary>
        /// <param name="document">CPF ou CNPJ, só dígitos, quando havia.</param>
        /// <param name="phone">Telefone, só dígitos, quando havia.</param>
        public void FillBlanks(string? document, string? phone)
        {
            var digits = Digits(document);

            if (Document is null && digits is not null && BrazilianDocuments.IsValidCpfOrCnpj(digits))
            {
                Document = digits;
            }

            Phone ??= Digits(phone);
        }

        private static string? Trim(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? Digits(string? value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

            return digits.Length == 0 ? null : digits;
        }
    }
}
