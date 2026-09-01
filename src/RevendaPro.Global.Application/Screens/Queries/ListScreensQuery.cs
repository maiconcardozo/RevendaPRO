using MediatR;
using RevendaPro.Global.Application.Screens.DTOs;

namespace RevendaPro.Global.Application.Screens.Queries
{
    /// <summary>Grouped catalog, used to draw the permission matrix.</summary>
    public sealed record ListScreensQuery : IRequest<IReadOnlyList<ScreenGroupDto>>;
}
