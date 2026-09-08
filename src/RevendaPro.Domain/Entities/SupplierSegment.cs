using System.Diagnostics;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// O ramo de um fornecedor: oficina mecânica, funilaria e pintura, autopeças, despachante.
    ///
    /// Cadastro da revenda, e não enum, pela mesma razão do tipo de gasto: o ramo que falta só
    /// aparece no uso — a estofaria, o chaveiro, a loja de som —, e uma lista fixa mandaria
    /// todos para "Outros", que é onde a leitura por ramo deixa de valer. A revenda nasce com
    /// uma lista farta, e mexe nela quando o trabalho pedir.
    ///
    /// O ramo diz <b>o que o fornecedor faz</b>. O tipo de gasto diz <b>o que foi comprado</b>.
    /// A mesma oficina cobra Mecânica num carro e Peças no outro, e é por isso que o gasto
    /// aponta para o tipo, e o fornecedor aponta para o ramo.
    /// </summary>
    [DebuggerDisplay("Name={Name}, IdTenant={IdTenant}")]
    public class SupplierSegment : TenantEntity
    {
        private SupplierSegment() { }

        private SupplierSegment(int idTenant) : base(idTenant) { }

        /// <summary>Como a revenda chama o ramo. Lido na tela, e por isso em português.</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>Ordem na lista que a pessoa escolhe.</summary>
        public int Position { get; private set; }

        /// <summary>Cadastra um ramo.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="name">Como a revenda chama o ramo.</param>
        /// <param name="position">Ordem na lista.</param>
        /// <param name="createdBy">Quem cadastrou.</param>
        /// <returns>O ramo.</returns>
        public static SupplierSegment Create(
            int idTenant,
            string name,
            int position = 0,
            string createdBy = SystemActor)
        {
            var segment = new SupplierSegment(idTenant) { Position = position };

            segment.Rename(name);
            segment.SetCreatedBy(createdBy);

            return segment;
        }

        /// <summary>Muda o nome do ramo.</summary>
        /// <param name="name">O nome.</param>
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessRuleException("Informe o nome do ramo.");
            }

            Name = name.Trim();
        }

        /// <summary>Muda a ordem na lista.</summary>
        /// <param name="position">A posição.</param>
        public void MoveTo(int position) => Position = position;
    }
}
