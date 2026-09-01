using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Global.Api.Authorization;
using RevendaPro.Global.Api.Contracts;
using RevendaPro.Global.Application.Users.Commands;
using RevendaPro.Global.Application.Users.DTOs;
using RevendaPro.Global.Application.Users.Queries;
using RevendaPro.Global.Shared.Exceptions;

namespace RevendaPro.Global.Api.Controllers
{
    /// <summary>User administration. Guarded by the users screen.</summary>
    [ApiController]
    [Route("api/users")]
    [Authorize]
    [RequireScreen("users")]
    public sealed class UsersController(IMediator mediator) : ControllerBase
    {
        /// <summary>Lists the users of the tenant.</summary>
        /// <param name="search">Matches name, e-mail or role name.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The users.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<UserDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(
            [FromQuery] string? search,
            CancellationToken cancellationToken)
        {
            var users = await mediator.Send(new ListUsersQuery(search), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<UserDto>>(
                StatusCodes.Status200OK, "OK", "Usuários carregados.",
                HttpContext.Request.Path, users));
        }

        /// <summary>Creates a user.</summary>
        /// <param name="command">The user data.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The created user.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(SuccessDetails<UserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create(
            [FromBody] SaveUserCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var user = await mediator.Send(command with { Code = null }, cancellationToken);

            return Ok(new SuccessDetails<UserDto>(
                StatusCodes.Status200OK, "OK", "Usuário criado com sucesso.",
                HttpContext.Request.Path, user));
        }

        /// <summary>Updates a user.</summary>
        /// <param name="code">Public identifier of the user.</param>
        /// <param name="command">The user data.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The updated user.</returns>
        [HttpPut("{code:guid}")]
        [ProducesResponseType(typeof(SuccessDetails<UserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(
            Guid code,
            [FromBody] SaveUserCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var user = await mediator.Send(command with { Code = code }, cancellationToken);

            return Ok(new SuccessDetails<UserDto>(
                StatusCodes.Status200OK, "OK", "Usuário atualizado com sucesso.",
                HttpContext.Request.Path, user));
        }

        /// <summary>Activates or deactivates a user.</summary>
        /// <param name="code">Public identifier of the user.</param>
        /// <param name="command">Target state.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPatch("{code:guid}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> ChangeStatus(
            Guid code,
            [FromBody] ChangeUserStatusCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            await mediator.Send(command with { Code = code }, cancellationToken);

            return NoContent();
        }

        /// <summary>Soft deletes a user.</summary>
        /// <param name="code">Public identifier of the user.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{code:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Delete(Guid code, CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteUserCommand(code), cancellationToken);

            return NoContent();
        }

        /// <summary>Stores the photo of a user.</summary>
        /// <param name="code">Public identifier of the user.</param>
        /// <param name="file">The image.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPost("{code:guid}/photo")]
        [RequestSizeLimit(4_194_304)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> UploadPhoto(
            Guid code,
            IFormFile file,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                throw new BusinessRuleException("Selecione uma imagem.");
            }

            await using var content = file.OpenReadStream();

            await mediator.Send(
                new UploadUserPhotoCommand(code, content, file.FileName), cancellationToken);

            return NoContent();
        }

        /// <summary>Removes the photo of a user.</summary>
        /// <param name="code">Public identifier of the user.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{code:guid}/photo")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RemovePhoto(Guid code, CancellationToken cancellationToken)
        {
            await mediator.Send(new RemoveUserPhotoCommand(code), cancellationToken);

            return NoContent();
        }
    }

    /// <summary>
    /// Serves user photos.
    ///
    /// Deliberately without RequireScreen: anyone signed in has to see their own avatar in
    /// the sidebar, even without access to user administration.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public sealed class UserPhotosController(IMediator mediator) : ControllerBase
    {
        /// <summary>Returns the photo of a user.</summary>
        /// <param name="code">Public identifier of the user.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The image, or 404 when there is none.</returns>
        [HttpGet("{code:guid}/photo")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid code, CancellationToken cancellationToken)
        {
            var photo = await mediator.Send(new GetUserPhotoQuery(code), cancellationToken);

            if (photo is null)
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "private, max-age=60";

            return File(photo.Content, photo.ContentType);
        }
    }
}
