using MediatR;
using RevendaPro.Global.Application.Authentication.DTOs;

namespace RevendaPro.Global.Application.Authentication.Commands
{
    /// <summary>
    /// Signs a user in. This record is also the request contract: the controller binds it
    /// straight from the body, so there is no second type to drift from it.
    /// </summary>
    /// <param name="Email">E-mail of the user.</param>
    /// <param name="Password">Plain password, hashed and compared on the server.</param>
    public sealed record AuthenticateUserCommand(string Email, string Password)
        : IRequest<AuthenticationResultDto>;

    /// <summary>Renews the session from a refresh token, rotating it.</summary>
    /// <param name="RefreshToken">Refresh token handed to the client at sign in.</param>
    public sealed record RenewSessionCommand(string RefreshToken) : IRequest<AuthenticationResultDto>;

    /// <summary>Revokes every refresh token of the user.</summary>
    /// <param name="UserId">Internal identifier of the user.</param>
    public sealed record SignOutCommand(int UserId) : IRequest;
}

namespace RevendaPro.Global.Application.Authentication.Queries
{
    using MediatR;
    using RevendaPro.Global.Application.Authentication.DTOs;

    /// <summary>Returns the session of the authenticated user, with the menu.</summary>
    /// <param name="UserId">Internal identifier of the user.</param>
    public sealed record GetSessionQuery(int UserId) : IRequest<SessionDto>;
}
