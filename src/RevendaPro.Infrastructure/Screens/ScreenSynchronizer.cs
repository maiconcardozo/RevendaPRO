using Foundation.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces;

namespace RevendaPro.Infrastructure.Screens
{
    /// <summary>
    /// Reconciles the database with the <see cref="ScreenCatalog"/> on every API start.
    ///
    /// <list type="bullet">
    /// <item>new screen in the catalog: INSERT, then granted to the Administrator role;</item>
    /// <item>label, icon, order or group changed: UPDATE;</item>
    /// <item>screen dropped from the catalog: deactivated, NEVER deleted;</item>
    /// <item>screen back in the catalog: reactivated, with its old grants intact.</item>
    /// </list>
    ///
    /// Never touches RoleScreen beyond that first Administrator grant: adjustments made in
    /// the permission matrix are not undone by a deploy. See ADR-0002.
    /// </summary>
    public class ScreenSynchronizer(IUnitOfWork unitOfWork, ILogger<ScreenSynchronizer> logger)
    {
        /// <summary>Runs the reconciliation.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>What changed.</returns>
        public async Task<SynchronizationResult> RunAsync(CancellationToken cancellationToken = default)
        {
            var stored = await unitOfWork.ScreenRepository
                .GetAllIncludingDeletedAsync(cancellationToken)
                .ConfigureAwait(false);

            var byKey = stored.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
            var declared = ScreenCatalog.Screens;

            var inserted = 0;
            var updated = 0;

            foreach (var item in declared)
            {
                if (byKey.TryGetValue(item.Key, out var existing))
                {
                    if (existing.Sync(
                            item.Name, item.Route, item.Icon, item.MenuGroup,
                            item.Order, item.ShowInMenu, existing.IdParentScreen))
                    {
                        unitOfWork.ScreenRepository.Update(existing);
                        updated++;
                    }

                    continue;
                }

                unitOfWork.ScreenRepository.Add(Screen.Create(
                    item.Key, item.Name, item.Route, item.Icon,
                    item.MenuGroup, item.Order, item.ShowInMenu));

                inserted++;
            }

            var declaredKeys = declared.Select(d => d.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var deactivated = 0;

            foreach (var orphan in stored.Where(s => !declaredKeys.Contains(s.Key) && s.IsActive))
            {
                unitOfWork.ScreenRepository.Remove(orphan, Entity.SystemActor);
                deactivated++;
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Granting comes after the commit because the new screens only have an Id once
            // the database has assigned it.
            var granted = inserted > 0
                ? await GrantNewScreensToAdministratorsAsync(declaredKeys, cancellationToken)
                    .ConfigureAwait(false)
                : 0;

            var result = new SynchronizationResult(inserted, updated, deactivated, granted);

            if (result.Changed)
            {
                logger.LogInformation(
                    "Screens synchronized: {Inserted} inserted, {Updated} updated, " +
                    "{Deactivated} deactivated, {Granted} granted to Administrator.",
                    result.Inserted, result.Updated, result.Deactivated, result.GrantedToAdministrator);
            }

            return result;
        }

        /// <summary>
        /// Grants every screen to the Administrator role of each tenant.
        ///
        /// Without this a brand new screen would belong to nobody, and no one could reach
        /// the roles page to grant it. No other role receives anything automatically.
        /// </summary>
        private async Task<int> GrantNewScreensToAdministratorsAsync(
            HashSet<string> declaredKeys,
            CancellationToken cancellationToken)
        {
            var screens = await unitOfWork.ScreenRepository
                .GetAllAsync(cancellationToken)
                .ConfigureAwait(false);

            var ids = screens
                .Where(s => declaredKeys.Contains(s.Key))
                .Select(s => s.Id)
                .ToList();

            var roles = await unitOfWork.RoleRepository
                .GetAllAsync(cancellationToken)
                .ConfigureAwait(false);

            var administrators = roles
                .Where(r => string.Equals(
                    r.Name, ScreenCatalog.AdministratorRole, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var granted = 0;

            foreach (var role in administrators)
            {
                var current = await unitOfWork.RoleRepository
                    .GetScreenIdsAsync(role.Id, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var idScreen in ids.Except(current))
                {
                    unitOfWork.RoleRepository.GrantScreen(role.Id, idScreen, Entity.SystemActor);
                    granted++;
                }
            }

            if (granted > 0)
            {
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return granted;
        }
    }

    /// <summary>What the synchronization changed.</summary>
    /// <param name="Inserted">Screens created.</param>
    /// <param name="Updated">Screens whose label, icon, order or group changed.</param>
    /// <param name="Deactivated">Screens dropped from the catalog.</param>
    /// <param name="GrantedToAdministrator">Grants created for the Administrator role.</param>
    public sealed record SynchronizationResult(
        int Inserted,
        int Updated,
        int Deactivated,
        int GrantedToAdministrator)
    {
        /// <summary>Whether anything changed at all.</summary>
        public bool Changed =>
            Inserted > 0 || Updated > 0 || Deactivated > 0 || GrantedToAdministrator > 0;
    }
}
