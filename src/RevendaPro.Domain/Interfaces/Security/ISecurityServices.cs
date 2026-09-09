using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Security
{
    public interface IPasswordHasher
    {
        string Hash(string password);

        /// <summary>Returns false when the password does not match. Never throws for a wrong password.</summary>
        bool Verify(string passwordHash, string password);
    }

    public sealed record IssuedToken(string Value, DateTime ExpiresAt);

    public interface ITokenService
    {
        /// <summary>
        /// Access token carries only sub, tenant and exp. Screen keys are NOT claims:
        /// they are resolved per request so a permission change applies immediately
        /// and the token does not grow with the catalog. See ADR-0002.
        /// </summary>
        IssuedToken CreateAccessToken(User user);

        (string Value, string Hash, DateTime ExpiresAt) CreateRefreshToken();

        string ComputeHash(string refreshToken);
    }

    /// <summary>
    /// O alcance de uma pessoa: as telas que ela abre, e o pátio que ela enxerga.
    ///
    /// As duas coisas moram aqui porque as duas são lidas do banco a cada requisição, e as duas
    /// deixam de valer no mesmo instante — quando alguém salva o usuário. Uma claim no token
    /// faria a mudança demorar até o token expirar, e uma fronteira de segurança desatualizada
    /// por quinze minutos é uma fronteira que já vazou. Ver ADR-0002.
    /// </summary>
    public interface IPermissionService
    {
        Task<IReadOnlySet<string>> GetScreenKeysAsync(int idUser, CancellationToken ct = default);

        /// <summary>
        /// O pátio a que a pessoa está presa, ou nulo enquanto ela enxerga o pátio inteiro (M24).
        /// </summary>
        /// <param name="idUser">A pessoa.</param>
        /// <param name="ct">Token to cancel the operation.</param>
        /// <returns>O Id do pátio, ou nulo.</returns>
        Task<int?> GetYardRestrictionAsync(int idUser, CancellationToken ct = default);

        void InvalidateRole(int idRole);

        void InvalidateUser(int idUser);
    }

    /// <summary>Data of the authenticated caller for the current request.</summary>
    public interface ICurrentUser
    {
        int Id { get; }

        Guid Code { get; }

        int IdTenant { get; }

        /// <summary>
        /// O pátio a que quem está chamando está preso, ou nulo enquanto enxerga o pátio inteiro
        /// (M24).
        ///
        /// Resolvido uma vez por requisição, antes de qualquer handler rodar, e lido daqui pelo
        /// <b>repositório de veículo</b> — e jamais por cada handler. O M12 encontrou oito
        /// handlers que liam por código sem filtrar a empresa, e um deles já conferia: uma regra
        /// que vive na disciplina de quem escreve o próximo handler já falhou em algum lugar.
        ///
        /// Nulo fora de uma requisição — o semeador, a rotina mensal da tabela —, e é o certo:
        /// uma tarefa agendada tem pessoa nenhuma a quem restringir.
        /// </summary>
        int? IdYard { get; }

        bool IsAuthenticated { get; }
    }

    public sealed record StoredPhoto(Stream Content, string ContentType);

    /// <summary>
    /// Keeps the photo of a user outside the database; only the file name is persisted, and
    /// the tenant and the user decide where the file lives.
    /// </summary>
    public interface IUserPhotoStorage
    {
        /// <summary>Stores the photo and answers the name to keep on the row.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="userCode">Public identifier of the user.</param>
        /// <param name="content">The uploaded bytes.</param>
        /// <param name="ct">Token to cancel the operation.</param>
        /// <returns>The file name.</returns>
        Task<string> SaveAsync(int idTenant, Guid userCode, Stream content, CancellationToken ct = default);

        /// <summary>Reads the photo, or null when it is gone.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="userCode">Public identifier of the user.</param>
        /// <param name="fileName">The name kept on the row.</param>
        /// <param name="ct">Token to cancel the operation.</param>
        /// <returns>The photo, or null.</returns>
        Task<StoredPhoto?> ReadAsync(int idTenant, Guid userCode, string fileName, CancellationToken ct = default);

        /// <summary>Removes the photo. Removing one that is already gone changes nothing.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="userCode">Public identifier of the user.</param>
        /// <param name="fileName">The name kept on the row.</param>
        /// <param name="ct">Token to cancel the operation.</param>
        /// <returns>A task.</returns>
        Task DeleteAsync(int idTenant, Guid userCode, string fileName, CancellationToken ct = default);
    }
}
