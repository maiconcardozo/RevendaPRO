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
            int userId,
            CancellationToken cancellationToken = default)
        {
            var key = UserKey(userId);

            if (cache.TryGetValue(key, out IReadOnlySet<string>? cached) && cached is not null)
            {
                return cached;
            }

            var keys = await unitOfWork.UserRepository
                .GetScreenKeysAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            var set = (IReadOnlySet<string>)keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            cache.Set(key, set, Lifetime);

            return set;
        }

        /// <inheritdoc/>
        public void InvalidateUser(int userId) => cache.Remove(UserKey(userId));

        /// <summary>
        /// Drops every entry, because the users holding the role are not known here without
        /// another query. The catalog is small and the lifetime is short, so paying a
        /// rebuild is cheaper than serving a revoked permission.
        /// </summary>
        public void InvalidateRole(int roleId)
        {
            if (cache is MemoryCache concrete)
            {
                concrete.Clear();
            }
        }

        private static string UserKey(int userId) => $"permissions:user:{userId}";
    }
}
