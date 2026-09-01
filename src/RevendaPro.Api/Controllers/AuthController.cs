using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Authentication.Commands;
using RevendaPro.Application.Authentication.DTOs;
using RevendaPro.Application.Authentication.Queries;
using RevendaPro.Domain.Interfaces.Security;

namespace RevendaPro.Api.Controllers
{
    /// <summary>Sign in, session and sign out.</summary>
    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
    {
        /// <summary>Signs the user in and issues the tokens.</summary>
        /// <param name="command">E-mail and password.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Tokens and the session with the menu.</returns>
        [AllowAnonymous]
        [HttpPost("login")]
        [ProducesResponseType(typeof(SuccessDetails<AuthenticationResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login(
            [FromBody] AuthenticateUserCommand command,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);

            return Ok(new SuccessDetails<AuthenticationResultDto>(
                StatusCodes.Status200OK, "OK", "Autenticação realizada com sucesso.",
                HttpContext.Request.Path, result));
        }

        /// <summary>Renews the session, rotating the refresh token.</summary>
        /// <param name="command">The refresh token.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>New tokens and the session.</returns>
        [AllowAnonymous]
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(SuccessDetails<AuthenticationResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh(
            [FromBody] RenewSessionCommand command,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);

            return Ok(new SuccessDetails<AuthenticationResultDto>(
                StatusCodes.Status200OK, "OK", "Sessão renovada com sucesso.",
                HttpContext.Request.Path, result));
        }

        /// <summary>
        /// User, roles, screens and the menu already filtered and ordered by the server.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The session of the caller.</returns>
        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType(typeof(SuccessDetails<SessionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Me(CancellationToken cancellationToken)
        {
            var session = await mediator.Send(new GetSessionQuery(currentUser.Id), cancellationToken);

            return Ok(new SuccessDetails<SessionDto>(
                StatusCodes.Status200OK, "OK", "Sessão carregada.",
                HttpContext.Request.Path, session));
        }

        /// <summary>Revokes every refresh token of the caller.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [Authorize]
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            await mediator.Send(new SignOutCommand(currentUser.Id), cancellationToken);

            return NoContent();
        }
    }
}
