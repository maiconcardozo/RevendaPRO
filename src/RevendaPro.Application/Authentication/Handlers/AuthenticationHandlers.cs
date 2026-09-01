using Foundation.Domain.Abstractions;
using MediatR;
using RevendaPro.Application.Authentication.Commands;
using RevendaPro.Application.Authentication.DTOs;
using RevendaPro.Application.Authentication.Queries;
using RevendaPro.Application.Authentication.Services;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Authentication.Handlers
{
    /// <summary>Signs a user in and issues the tokens.</summary>
    public class AuthenticateUserHandler(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ISessionBuilder sessionBuilder)
        : IRequestHandler<AuthenticateUserCommand, AuthenticationResultDto>
    {
        /// <summary>
        /// One message for both a missing e-mail and a wrong password, so the endpoint does
        /// not reveal which addresses exist.
        /// </summary>
        private const string InvalidCredentials = "E-mail ou senha inválidos.";

        /// <inheritdoc/>
        public async Task<AuthenticationResultDto> Handle(
            AuthenticateUserCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await unitOfWork.UserRepository
                .GetByEmailAsync(request.Email, cancellationToken)
                .ConfigureAwait(false);

            if (user is null || !passwordHasher.Verify(user.PasswordHash, request.Password))
            {
                throw new UnauthenticatedException(InvalidCredentials);
            }

            if (!user.CanSignIn())
            {
                throw new UnauthenticatedException(
                    "Esta conta está inativa. Fale com o administrador da revenda.");
            }

            var tokens = await IssueTokensAsync(user, tokenService, unitOfWork, cancellationToken)
                .ConfigureAwait(false);

            var session = await sessionBuilder.BuildAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            return new AuthenticationResultDto(tokens, session);
        }

        /// <summary>Issues the pair of tokens and stores the hash of the refresh token.</summary>
        internal static async Task<TokensDto> IssueTokensAsync(
            User user,
            ITokenService tokenService,
            IUnitOfWork unitOfWork,
            CancellationToken cancellationToken)
        {
            var access = tokenService.CreateAccessToken(user);
            var (value, hash, expiresAt) = tokenService.CreateRefreshToken();

            unitOfWork.RefreshTokenRepository.Add(RefreshToken.Create(user.Id, hash, expiresAt));
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new TokensDto(access.Value, access.ExpiresAt, value, expiresAt);
        }
    }

    /// <summary>
    /// Renews the session, rotating the refresh token: the one used is revoked and a new one
    /// is issued. A revoked token renews nothing.
    /// </summary>
    public class RenewSessionHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ISessionBuilder sessionBuilder)
        : IRequestHandler<RenewSessionCommand, AuthenticationResultDto>
    {
        /// <inheritdoc/>
        public async Task<AuthenticationResultDto> Handle(
            RenewSessionCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                throw new UnauthenticatedException("Sessão expirada. Entre novamente.");
            }

            var hash = tokenService.ComputeHash(request.RefreshToken);

            var stored = await unitOfWork.RefreshTokenRepository
                .GetByHashAsync(hash, cancellationToken)
                .ConfigureAwait(false);

            if (stored is null || !stored.IsValid(DateTime.UtcNow))
            {
                throw new UnauthenticatedException("Sessão expirada. Entre novamente.");
            }

            var user = await unitOfWork.UserRepository
                .GetByIdAsync(stored.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (user is null || !user.CanSignIn())
            {
                unitOfWork.RefreshTokenRepository.RevokeAllByUser(stored.UserId, Entity.SystemActor);
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

                throw new UnauthenticatedException("Esta conta está inativa.");
            }

            // Rotation and issuing travel in the same transaction: a failure halfway must not
            // leave the old token revoked without a new one in its place.
            var tokens = await unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    stored.Revoke();
                    unitOfWork.RefreshTokenRepository.Update(stored);

                    var access = tokenService.CreateAccessToken(user);
                    var (value, newHash, expiresAt) = tokenService.CreateRefreshToken();

                    unitOfWork.RefreshTokenRepository.Add(
                        RefreshToken.Create(user.Id, newHash, expiresAt));

                    return new TokensDto(access.Value, access.ExpiresAt, value, expiresAt);
                },
                cancellationToken).ConfigureAwait(false);

            var session = await sessionBuilder.BuildAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            return new AuthenticationResultDto(tokens, session);
        }
    }

    /// <summary>Revokes every refresh token of the user.</summary>
    public class SignOutHandler(IUnitOfWork unitOfWork) : IRequestHandler<SignOutCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(SignOutCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            unitOfWork.RefreshTokenRepository.RevokeAllByUser(request.UserId, Entity.SystemActor);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Returns the session of the authenticated user.</summary>
    public class GetSessionHandler(ISessionBuilder sessionBuilder)
        : IRequestHandler<GetSessionQuery, SessionDto>
    {
        /// <inheritdoc/>
        public Task<SessionDto> Handle(GetSessionQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            return sessionBuilder.BuildAsync(request.UserId, cancellationToken);
        }
    }
}
