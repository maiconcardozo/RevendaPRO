using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Global.Domain.Entities;

namespace RevendaPro.Global.Domain.Interfaces.Repositories
{
    public interface IScreenRepository : IDapperRepository<Screen>
    {
        Task<Screen?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetKeysByRoleAsync(int roleId, CancellationToken cancellationToken = default);
    }
}
