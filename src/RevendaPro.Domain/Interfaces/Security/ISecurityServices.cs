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

    public interface IPermissionService
    {
        Task<IReadOnlySet<string>> GetScreenKeysAsync(int idUser, CancellationToken ct = default);

        void InvalidateRole(int idRole);

        void InvalidateUser(int idUser);
    }

    /// <summary>Data of the authenticated caller for the current request.</summary>
    public interface ICurrentUser
    {
        int Id { get; }

        Guid Code { get; }

        int IdTenant { get; }

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
