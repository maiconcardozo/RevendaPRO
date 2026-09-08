using System.Diagnostics;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// De quem a revenda compra serviço e peça: a oficina, a funilaria, a loja de autopeças, o
    /// despachante.
    ///
    /// <b>Fornecedor diz de quem; tipo de gasto diz o quê.</b> São perguntas diferentes, e a
    /// mesma oficina responde a várias da segunda — cobra Mecânica num carro e Peças no outro.
    /// Juntar as duas coisas obrigaria a escolher entre saber o que se gastou e saber com quem.
    /// É por isso que o fornecedor é cadastro próprio, e o gasto aponta para os dois.
    ///
    /// O ramo é outro cadastro da revenda (<see cref="SupplierSegment"/>), e o fornecedor
    /// aponta para um. Ver <c>docs/plans/m18-fornecedores.md</c>.
    /// </summary>
    [DebuggerDisplay("Name={Name}, IdSupplierSegment={IdSupplierSegment}, IdTenant={IdTenant}")]
    public class Supplier : TenantEntity
    {
        private Supplier() { }

        private Supplier(int idTenant) : base(idTenant) { }

        /// <summary>Como a revenda chama o fornecedor: "Auto Mecânica Silva", "Funilaria do Zé".</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>O ramo: oficina, funilaria, autopeças, despachante.</summary>
        public int IdSupplierSegment { get; private set; }

        /// <summary>Com quem falar lá.</summary>
        public string? ContactName { get; private set; }

        /// <summary>Telefone, só dígitos.</summary>
        public string? ContactPhone { get; private set; }

        /// <summary>CNPJ ou CPF, só dígitos. Opcional: a oficina de bairro raramente tem um à mão.</summary>
        public string? Document { get; private set; }

        /// <summary>Anotação livre sobre o fornecedor.</summary>
        public string? Notes { get; private set; }

        /// <summary>Cadastra um fornecedor.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="name">Como a revenda chama o fornecedor.</param>
        /// <param name="idSupplierSegment">O ramo.</param>
        /// <param name="createdBy">Quem cadastrou.</param>
        /// <returns>O fornecedor.</returns>
        public static Supplier Create(
            int idTenant,
            string name,
            int idSupplierSegment,
            string createdBy = SystemActor)
        {
            var supplier = new Supplier(idTenant);

            supplier.Rename(name);
            supplier.SetSegment(idSupplierSegment);
            supplier.SetCreatedBy(createdBy);

            return supplier;
        }

        /// <summary>Muda o nome do fornecedor.</summary>
        /// <param name="name">O nome.</param>
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessRuleException("Informe o nome do fornecedor.");
            }

            Name = name.Trim();
        }

        /// <summary>Muda o ramo.</summary>
        /// <param name="idSupplierSegment">O ramo.</param>
        public void SetSegment(int idSupplierSegment)
        {
            if (idSupplierSegment <= 0)
            {
                throw new BusinessRuleException("Escolha o ramo do fornecedor.");
            }

            IdSupplierSegment = idSupplierSegment;
        }

        /// <summary>
        /// Guarda como chegar ao fornecedor.
        ///
        /// O documento é conferido só pelo tamanho: onze dígitos é CPF, catorze é CNPJ, e o resto
        /// é erro de digitação. O dígito verificador fica com a tela, que já sabe fazê-lo.
        /// </summary>
        /// <param name="contactName">Com quem falar.</param>
        /// <param name="contactPhone">Telefone, em qualquer formato.</param>
        /// <param name="document">CNPJ ou CPF, em qualquer formato.</param>
        /// <param name="notes">Anotação livre.</param>
        public void SetContact(string? contactName, string? contactPhone, string? document, string? notes)
        {
            var digits = Digits(document);

            if (digits is not null && digits.Length != 11 && digits.Length != 14)
            {
                throw new BusinessRuleException("Informe um CPF com 11 dígitos ou um CNPJ com 14.");
            }

            ContactName = Trim(contactName);
            ContactPhone = Digits(contactPhone);
            Document = digits;
            Notes = Trim(notes);
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
