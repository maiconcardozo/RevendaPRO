using RevendaPro.Domain.Interfaces.Security;

namespace RevendaPro.Domain.Interfaces.Storage
{
    /// <summary>
    /// Guarda o logotipo da revenda fora do banco; só o nome do arquivo fica na linha (M25).
    ///
    /// Uma porta própria, e não a da galeria de fotos, porque o logotipo tem três exigências que
    /// a foto não tem: ele é <b>PNG sem perda</b> — o WebP a 80 da galeria é feito para foto, e
    /// borra a borda de uma letra —, ele guarda a <b>transparência</b>, para não chegar ao papel
    /// com um retângulo branco em volta, e ele tem <b>um tamanho só</b>: ocupa dois centímetros
    /// no papel e um quadrado pequeno na tela.
    ///
    /// A chave começa pelo tenant: o arquivo de uma revenda jamais é endereçável de outra
    /// (RNF-04).
    /// </summary>
    public interface ICompanyLogoStorage
    {
        /// <summary>Processa e guarda o logotipo, e responde o nome a manter na linha.</summary>
        /// <param name="idTenant">A revenda.</param>
        /// <param name="content">Os bytes enviados, já recortados pelo navegador.</param>
        /// <param name="ct">Token to cancel the operation.</param>
        /// <returns>O nome do arquivo.</returns>
        Task<string> SaveAsync(int idTenant, Stream content, CancellationToken ct = default);

        /// <summary>Lê o logotipo, ou nulo quando ele se foi.</summary>
        /// <param name="idTenant">A revenda.</param>
        /// <param name="fileName">O nome guardado na linha.</param>
        /// <param name="ct">Token to cancel the operation.</param>
        /// <returns>O arquivo, ou nulo.</returns>
        Task<StoredPhoto?> ReadAsync(int idTenant, string fileName, CancellationToken ct = default);

        /// <summary>Remove o logotipo. Remover o que já se foi muda nada.</summary>
        /// <param name="idTenant">A revenda.</param>
        /// <param name="fileName">O nome guardado na linha.</param>
        /// <param name="ct">Token to cancel the operation.</param>
        /// <returns>A task.</returns>
        Task DeleteAsync(int idTenant, string fileName, CancellationToken ct = default);
    }
}
