using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Global.Api.Authorization;
using RevendaPro.Global.Api.Contracts;
using RevendaPro.Global.Application.Roles.Commands;
using RevendaPro.Global.Application.Roles.DTOs;
using RevendaPro.Global.Application.Roles.Queries;
using RevendaPro.Global.Application.Screens.DTOs;
using RevendaPro.Global.Application.Screens.Queries;

namespace RevendaPro.Global.Api.Controllers
{
    /// <summary>Role administration. Guarded by the roles screen.</summary>
    [ApiController]
    [Route("api/roles")]
    [Authorize]
    [RequireScreen("roles")]
    public sealed class RolesController(IMediator mediator) : ControllerBase
    {
        /// <summary>Lists the roles of the tenant.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The roles.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var roles = await mediator.Send(new ListRolesQuery(), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<RoleDto>>(
                StatusCodes.Status200OK, "OK", "Perfis carregados.",
                HttpContext.Request.Path, roles));
        }

        /// <summary>Creates a role.</summary>
        /// <param name="command">The role data and the screens it grants.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The created role.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(SuccessDetails<RoleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create(
            [FromBody] SaveRoleCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var role = await mediator.Send(command with { Code = null }, cancellationToken);

            return Ok(new SuccessDetails<RoleDto>(
                StatusCodes.Status200OK, "OK", "Perfil criado com sucesso.",
                HttpContext.Request.Path, role));
        }

        /// <summary>Updates a role and the screens it grants.</summary>
        /// <param name="code">Public identifier of the role.</param>
        /// <param name="command">The role data.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The updated role.</returns>
        [HttpPut("{code:guid}")]
        [ProducesResponseType(typeof(SuccessDetails<RoleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(
            Guid code,
            [FromBody] SaveRoleCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var role = await mediator.Send(command with { Code = code }, cancellationToken);

            return Ok(new SuccessDetails<RoleDto>(
                StatusCodes.Status200OK, "OK", "Perfil atualizado com sucesso.",
                HttpContext.Request.Path, role));
        }

        /// <summary>Soft deletes a role.</summary>
        /// <param name="code">Public identifier of the role.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{code:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Delete(Guid code, CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteRoleCommand(code), cancellationToken);

            return NoContent();
        }
    }

    /// <summary>Screen catalog, used to draw the permission matrix.</summary>
    [ApiController]
    [Route("api/screens")]
    [Authorize]
    [RequireScreen("roles")]
    public sealed class ScreensController(IMediator mediator) : ControllerBase
    {
        /// <summary>Lists the active screens, grouped.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The grouped catalog.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<ScreenGroupDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var screens = await mediator.Send(new ListScreensQuery(), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<ScreenGroupDto>>(
                StatusCodes.Status200OK, "OK", "Telas carregadas.",
                HttpContext.Request.Path, screens));
        }
    }
}
