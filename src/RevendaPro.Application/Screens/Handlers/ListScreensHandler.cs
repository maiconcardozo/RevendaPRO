using MediatR;
using RevendaPro.Application.Screens.DTOs;
using RevendaPro.Application.Screens.Queries;
using RevendaPro.Domain.Interfaces;

namespace RevendaPro.Application.Screens.Handlers
{
    /// <summary>Returns the active screens grouped for the permission matrix.</summary>
    public class ListScreensHandler(IUnitOfWork unitOfWork)
        : IRequestHandler<ListScreensQuery, IReadOnlyList<ScreenGroupDto>>
    {
        /// <summary>Group shown for screens that are not part of the menu.</summary>
        private const string OtherScreensGroup = "Outras telas";

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ScreenGroupDto>> Handle(
            ListScreensQuery request,
            CancellationToken cancellationToken)
        {
            var screens = await unitOfWork.ScreenRepository
                .GetAllAsync(cancellationToken)
                .ConfigureAwait(false);

            return
            [
                .. screens
                    .Select(s => new ScreenDto(
                        s.Code,
                        s.Key,
                        s.Name,
                        s.Icon,
                        string.IsNullOrWhiteSpace(s.MenuGroup) ? OtherScreensGroup : s.MenuGroup,
                        s.Order,
                        s.ShowInMenu))
                    .GroupBy(s => s.Group)
                    .OrderBy(g => g.Min(s => s.Order))
                    .Select(g => new ScreenGroupDto(g.Key, [.. g.OrderBy(s => s.Order)]))
            ];
        }
    }
}
