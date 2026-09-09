using Microsoft.Extensions.Caching.Memory;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;

namespace RevendaPro.Infrastructure.Security
{
    /// <summary>
    /// Resolves the screen keys of a user on every request.
    ///
    /// The cache is keyed by USER, and invalidated by user or by role. Caching by role
    /// alone would not help: the union still has to be assembled per user, and a single
    /// query already returns it. Adjusting the permission matrix invalidates the entry so
    /// the change applies on the next request, without anyone signing in again.
    /// See ADR-0002.
    /// </summary>
    public class PermissionService(IUnitOfWork unitOfWork, IMemoryCache cache) : IPermissionService
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        /// <inheritdoc/>
        public async Task<IReadOnlySet<string>> GetScreenKeysAsync(
            int idUser,
            CancellationToken cancellationToken = default)
        {
            var key = UserKey(idUser);

            if (cache.TryGetValue(key, out IReadOnlySet<string>? cached) && cached is not null)
            {
                return cached;
            }

            var keys = await unitOfWork.UserRepository
                .GetScreenKeysAsync(idUser, cancellationToken)
                .ConfigureAwait(false);

            var set = (IReadOnlySet<string>)keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            cache.Set(key, set, Lifetime);

            return set;
        }

        /// <inheritdoc/>
        public async Task<int?> GetYardRestrictionAsync(
            int idUser,
            CancellationToken cancellationToken = default)
        {
            var key = YardKey(idUser);

            // O nulo é a resposta mais comum — quase todo mundo enxerga o pátio inteiro —, e um
            // nulo guardado é indistinguível de "ainda não perguntei". Por isso o que se guarda
            // é a resposta embrulhada.
            if (cache.TryGetValue(key, out YardRestriction? cached) && cached is not null)
            {
                return cached.IdYard;
            }

            var user = await unitOfWork.UserRepository
                .GetByIdAsync(idUser, cancellationToken)
                .ConfigureAwait(false);

            var restriction = new YardRestriction(user?.IdYard);

            cache.Set(key, restriction, Lifetime);

            return restriction.IdYard;
        }

        /// <inheritdoc/>
        public void InvalidateUser(int idUser)
        {
            cache.Remove(UserKey(idUser));
            cache.Remove(YardKey(idUser));
        }

        /// <summary>
        /// Drops every entry, because the users holding the role are not known here without
        /// another query. The catalog is small and the lifetime is short, so paying a
        /// rebuild is cheaper than serving a revoked permission.
        /// </summary>
        public void InvalidateRole(int idRole)
        {
            if (cache is MemoryCache concrete)
            {
                concrete.Clear();
            }
        }

        private static string UserKey(int idUser) => $"permissions:user:{idUser}";

        private static string YardKey(int idUser) => $"yard:user:{idUser}";

        /// <summary>A resposta embrulhada, para o nulo poder ser guardado.</summary>
        private sealed record YardRestriction(int? IdYard);
    }
}
