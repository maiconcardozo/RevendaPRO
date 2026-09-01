using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Infrastructure.Queries.Screens;

namespace RevendaPro.Infrastructure.Repositories.Screens
{
    /// <summary>
    /// Dapper repository for <see cref="Screen"/>.
    ///
    /// Overrides the conventional reads because Key and Order are reserved words in MySQL
    /// and need backticks, which the generic statement builder does not add.
    /// </summary>
    public class ScreenRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<Screen>(unitOfWork), IScreenRepository
    {
        /// <inheritdoc/>
        public Task<Screen?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return QuerySingleAsync(
                new FindScreenByKeyQuery(key.Trim().ToLowerInvariant()), cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> GetKeysByRoleAsync(
            int roleId,
            CancellationToken cancellationToken = default) =>
            QueryColumnAsync<string>(new ListScreenKeysByRoleQuery(roleId), cancellationToken);

        /// <inheritdoc/>
        public new Task<IReadOnlyList<Screen>> GetAllAsync(CancellationToken cancellationToken = default) =>
            QueryAsync(new ListActiveScreensQuery(), cancellationToken);

        /// <inheritdoc/>
        public new Task<IReadOnlyList<Screen>> GetAllIncludingDeletedAsync(
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListAllScreensQuery(), cancellationToken);

        /// <inheritdoc/>
        public new void Add(Screen entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            Enqueue(new InsertScreenQuery(entity));
        }

        /// <inheritdoc/>
        public new void Update(Screen entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            Enqueue(new UpdateScreenQuery(entity));
        }

        /// <inheritdoc/>
        public new void Remove(Screen entity, string deletedBy)
        {
            ArgumentNullException.ThrowIfNull(entity);

            entity.SoftDelete(deletedBy);
            Enqueue(new UpdateScreenQuery(entity));
        }
    }
}
