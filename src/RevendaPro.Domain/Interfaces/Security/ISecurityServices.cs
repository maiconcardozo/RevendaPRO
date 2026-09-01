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
    /// Keeps photos OUTSIDE the database; only the file name is persisted.
    /// Swapping to S3 or Azure Blob means implementing this again, nothing else changes.
    /// </summary>
    public interface IPhotoStorageService
    {
        Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default);

        Task<StoredPhoto?> ReadAsync(string fileName, CancellationToken ct = default);

        Task DeleteAsync(string fileName, CancellationToken ct = default);
    }
}
