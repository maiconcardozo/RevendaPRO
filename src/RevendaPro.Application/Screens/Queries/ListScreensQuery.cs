using MediatR;
using RevendaPro.Application.Screens.DTOs;

namespace RevendaPro.Application.Screens.Queries
{
    /// <summary>Grouped catalog, used to draw the permission matrix.</summary>
    public sealed record ListScreensQuery : IRequest<IReadOnlyList<ScreenGroupDto>>;
}
