using RevendaPro.Application.Authentication.DTOs;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Application.Authentication.Services
{
    /// <summary>Builds the session and the menu of a user.</summary>
    public interface ISessionBuilder
    {
        /// <summary>Assembles the session for the given user.</summary>
        /// <param name="idUser">Internal identifier of the user.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>User, roles, screens and the menu already filtered.</returns>
        Task<SessionDto> BuildAsync(int idUser, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Assembles the session.
    ///
    /// The menu carries only screens with ShowInMenu = true that the user can reach,
    /// grouped and ordered. See ADR-0002.
    /// </summary>
    public class SessionBuilder(IUnitOfWork unitOfWork, IFileStorage storage) : ISessionBuilder
    {
        /// <inheritdoc/>
        public async Task<SessionDto> BuildAsync(int idUser, CancellationToken cancellationToken = default)
        {
            var user = await unitOfWork.UserRepository.GetByIdAsync(idUser, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Usuário inexistente.");

            var allowedKeys = (await unitOfWork.UserRepository
                    .GetScreenKeysAsync(idUser, cancellationToken)
                    .ConfigureAwait(false))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var screens = await unitOfWork.ScreenRepository
                .GetAllAsync(cancellationToken)
                .ConfigureAwait(false);

            var allowed = screens.Where(s => allowedKeys.Contains(s.Key)).ToList();

            var roleIds = await unitOfWork.UserRepository
                .GetRoleIdsAsync(idUser, cancellationToken)
                .ConfigureAwait(false);

            var roles = await unitOfWork.RoleRepository
                .GetByIdsAsync(roleIds, cancellationToken)
                .ConfigureAwait(false);

            return new SessionDto(
                new SessionUserDto(user.Code, user.Name, user.Email, !string.IsNullOrEmpty(user.Photo)),
                [.. roles.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal)],
                [.. allowed.Select(s => s.Key)],
                BuildMenu(allowed),
                new SessionLimitsDto(storage.MaxSizeInBytes));
        }

        private static IReadOnlyList<MenuGroupDto> BuildMenu(IReadOnlyList<Screen> allowed)
        {
            var inMenu = allowed.Where(s => s.ShowInMenu).ToList();
            var byParent = inMenu.ToLookup(s => s.IdParentScreen);

            return
            [
                .. byParent[null]
                    .GroupBy(s => s.MenuGroup ?? string.Empty)
                    .OrderBy(g => g.Min(s => s.Order))
                    .Select(g => new MenuGroupDto(
                        g.Key,
                        [.. g.OrderBy(s => s.Order).Select(s => BuildItem(s, byParent))]))
            ];
        }

        private static MenuItemDto BuildItem(Screen screen, ILookup<int?, Screen> byParent) =>
            new(screen.Key,
                screen.Name,
                screen.Route,
                screen.Icon,
                [.. byParent[screen.Id].OrderBy(c => c.Order).Select(c => BuildItem(c, byParent))]);
    }
}
