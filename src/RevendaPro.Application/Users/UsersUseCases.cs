using MediatR;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Users.DTOs
{
    /// <summary>A user of the tenant.</summary>
    /// <param name="Code">Public identifier. The internal Id is never exposed.</param>
    /// <param name="Name">Full name.</param>
    /// <param name="Email">E-mail, unique inside the tenant.</param>
    /// <param name="IsBlocked">Whether the person is barred from signing in.</param>
    /// <param name="IsActive">Whether the row is still present. False means it was deleted, and only a listing that asks for deleted rows brings it back.</param>
    /// <param name="Roles">Codes of the roles held.</param>
    /// <param name="RoleNames">Role names, displayed to the user.</param>
    /// <param name="HasPhoto">Whether there is a photo to load.</param>
    /// <param name="Document">CPF or CNPJ, digits only. The mask lives in the UI.</param>
    /// <param name="Phone">Phone with area code, digits only.</param>
    public sealed record UserDto(
        Guid Code,
        string Name,
        string Email,
        bool IsBlocked,
        bool IsActive,
        IReadOnlyList<Guid> Roles,
        IReadOnlyList<string> RoleNames,
        bool HasPhoto,
        string? Document,
        string? Phone);
}

namespace RevendaPro.Application.Users.Queries
{
    using MediatR;
    using RevendaPro.Application.Users.DTOs;
    using RevendaPro.Domain.Interfaces.Security;

    /// <summary>Lists the users of the tenant, optionally filtered.</summary>
    /// <param name="Search">Matches name, e-mail or role name.</param>
    /// <param name="IncludeDeleted">Brings deleted rows along, so the screen can offer them back.</param>
    public sealed record ListUsersQuery(string? Search, bool IncludeDeleted = false)
        : IRequest<IReadOnlyList<UserDto>>;

    /// <summary>Reads the photo of a user.</summary>
    /// <param name="Code">Public identifier of the user.</param>
    public sealed record GetUserPhotoQuery(Guid Code) : IRequest<StoredPhoto?>;
}

namespace RevendaPro.Application.Users.Commands
{
    using MediatR;
    using RevendaPro.Application.Users.DTOs;

    /// <summary>Creates or updates a user.</summary>
    /// <param name="Code">Null creates; filled updates.</param>
    /// <param name="Name">Full name.</param>
    /// <param name="Email">E-mail, unique inside the tenant.</param>
    /// <param name="Password">Null on update means keep the current one.</param>
    /// <param name="IsBlocked">Whether the person is barred from signing in. The row stays in the list either way; deleting is a separate operation.</param>
    /// <param name="Roles">Codes of the roles to assign.</param>
    /// <param name="Document">CPF or CNPJ. Required.</param>
    /// <param name="Phone">Phone with area code. Optional.</param>
    public sealed record SaveUserCommand(
        Guid? Code,
        string Name,
        string Email,
        string? Password,
        bool IsBlocked,
        IReadOnlyList<Guid> Roles,
        string? Document,
        string? Phone = null) : IRequest<UserDto>;

    /// <summary>Activates or deactivates a user.</summary>
    /// <param name="Code">Public identifier of the user.</param>
    /// <param name="IsBlocked">Target state.</param>
    public sealed record ChangeUserStatusCommand(Guid Code, bool IsBlocked) : IRequest;

    /// <summary>Soft deletes a user.</summary>
    /// <param name="Code">Public identifier of the user.</param>
    public sealed record DeleteUserCommand(Guid Code) : IRequest;

    /// <summary>Brings a deleted user back into the listing.</summary>
    /// <param name="Code">Public identifier of the user.</param>
    public sealed record RestoreUserCommand(Guid Code) : IRequest;

    /// <summary>Stores the photo of a user.</summary>
    /// <param name="Code">Public identifier of the user.</param>
    /// <param name="Content">Image bytes.</param>
    /// <param name="FileName">Original file name, used for the extension.</param>
    public sealed record UploadUserPhotoCommand(Guid Code, Stream Content, string FileName)
        : IRequest<string>;

    /// <summary>Removes the photo of a user.</summary>
    /// <param name="Code">Public identifier of the user.</param>
    public sealed record RemoveUserPhotoCommand(Guid Code) : IRequest;
}

namespace RevendaPro.Application.Users.Validators
{
    using FluentValidation;
    using RevendaPro.Application.Users.Commands;
    using RevendaPro.Shared.Helpers;

    /// <summary>Validates the user form. Messages are in Portuguese: the user reads them.</summary>
    public class SaveUserValidator : AbstractValidator<SaveUserCommand>
    {
        public SaveUserValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Informe o nome.")
                .MaximumLength(160).WithMessage("O nome pode ter no máximo 160 caracteres.");

            RuleFor(c => c.Email)
                .NotEmpty().WithMessage("Informe o e-mail.")
                .EmailAddress().WithMessage("E-mail inválido.")
                .MaximumLength(180).WithMessage("O e-mail pode ter no máximo 180 caracteres.");

            RuleFor(c => c.Roles)
                .NotEmpty().WithMessage("Selecione um perfil de acesso.");

            RuleFor(c => c.Document)
                .NotEmpty().WithMessage("Informe o CPF ou CNPJ.")
                .Must(BrazilianDocuments.IsValidCpfOrCnpj)
                .WithMessage("CPF ou CNPJ inválido.");

            RuleFor(c => c.Phone)
                .Must(BrazilianDocuments.IsValidPhone)
                .WithMessage("Telefone inválido. Informe DDD e número.");

            // The password is required only on creation; empty on update means keep it.
            RuleFor(c => c.Password)
                .NotEmpty().WithMessage("Informe a senha.")
                .MinimumLength(8).WithMessage("A senha precisa ter pelo menos 8 caracteres.")
                .When(c => c.Code is null);

            RuleFor(c => c.Password)
                .MinimumLength(8).WithMessage("A senha precisa ter pelo menos 8 caracteres.")
                .When(c => c.Code is not null && !string.IsNullOrEmpty(c.Password));
        }
    }
}

namespace RevendaPro.Application.Users.Handlers
{
    using MediatR;
    using RevendaPro.Application.Users.Commands;
    using RevendaPro.Application.Users.DTOs;
    using RevendaPro.Application.Users.Queries;

    /// <summary>Shared mapping and lookups for the user handlers.</summary>
    internal static class UserMapper
    {
        public static async Task<UserDto> ToDtoAsync(
            User user,
            IUnitOfWork unitOfWork,
            CancellationToken cancellationToken)
        {
            var roleIds = await unitOfWork.UserRepository
                .GetRoleIdsAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            var roles = await unitOfWork.RoleRepository
                .GetByIdsAsync(roleIds, cancellationToken)
                .ConfigureAwait(false);

            return new UserDto(
                user.Code,
                user.Name,
                user.Email,
                user.IsBlocked,
                user.IsActive,
                [.. roles.Select(r => r.Code)],
                [.. roles.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal)],
                !string.IsNullOrEmpty(user.Photo),
                user.Document,
                user.Phone);
        }
    }

    /// <summary>Lists the users of the tenant.</summary>
    public class ListUsersHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<UserDto>> Handle(
            ListUsersQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var users = await unitOfWork.UserRepository
                .ListByTenantAsync(
                    currentUser.IdTenant, request.Search, request.IncludeDeleted, cancellationToken)
                .ConfigureAwait(false);

            var result = new List<UserDto>(users.Count);

            foreach (var user in users)
            {
                result.Add(await UserMapper.ToDtoAsync(user, unitOfWork, cancellationToken)
                    .ConfigureAwait(false));
            }

            return result;
        }
    }

    /// <summary>Creates or updates a user.</summary>
    public class SaveUserHandler(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ICurrentUser currentUser,
        IPermissionService permissionService)
        : IRequestHandler<SaveUserCommand, UserDto>
    {
        /// <inheritdoc/>
        public async Task<UserDto> Handle(SaveUserCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var email = request.Email.Trim().ToLowerInvariant();
            var actor = currentUser.Code.ToString();
            var isNew = request.Code is null;

            User user;

            if (isNew)
            {
                if (await unitOfWork.UserRepository
                        .EmailExistsAsync(idTenant, email, ignoreId: null, cancellationToken)
                        .ConfigureAwait(false))
                {
                    throw new BusinessRuleException($"O e-mail {email} já está em uso.");
                }

                user = User.Create(
                    idTenant, request.Name.Trim(), email, passwordHasher.Hash(request.Password!), actor);

                user.Update(request.Name.Trim(), email, request.Document, request.Phone, actor);

                unitOfWork.UserRepository.Add(user);
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

                // Read back so the Id assigned by the database is known before linking roles.
                user = await unitOfWork.UserRepository
                    .GetByEmailAsync(email, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new BusinessRuleException("Falha ao criar o usuário.");
            }
            else
            {
                user = await unitOfWork.UserRepository
                    .GetByCodeAsync(idTenant, request.Code!.Value, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new NotFoundException("Usuário inexistente.");

                if (await unitOfWork.UserRepository
                        .EmailExistsAsync(idTenant, email, user.Id, cancellationToken)
                        .ConfigureAwait(false))
                {
                    throw new BusinessRuleException($"O e-mail {email} já está em uso.");
                }

                user.Update(request.Name.Trim(), email, request.Document, request.Phone, actor);

                if (!string.IsNullOrWhiteSpace(request.Password))
                {
                    user.ChangePassword(passwordHasher.Hash(request.Password), actor);
                }

                if (request.IsBlocked && user.Id == currentUser.Id)
                {
                    throw new BusinessRuleException("A inativação da sua conta fica a cargo de outro administrador.");
                }

                if (request.IsBlocked)
                {
                    user.Block(actor);
                }
                else
                {
                    user.Unblock(actor);
                }

                unitOfWork.UserRepository.Update(user);
            }

            var roleIds = await ResolveRoleIdsAsync(idTenant, request.Roles, cancellationToken)
                .ConfigureAwait(false);

            unitOfWork.UserRepository.ReplaceRoles(user.Id, roleIds, actor);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(User), user.Code,
                isNew ? AuditAction.Create : AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            permissionService.InvalidateUser(user.Id);

            var saved = await unitOfWork.UserRepository
                .GetByCodeAsync(user.Code, cancellationToken)
                .ConfigureAwait(false) ?? user;

            return await UserMapper.ToDtoAsync(saved, unitOfWork, cancellationToken).ConfigureAwait(false);
        }

        private async Task<List<int>> ResolveRoleIdsAsync(
            int idTenant,
            IReadOnlyList<Guid> codes,
            CancellationToken cancellationToken)
        {
            var roles = await unitOfWork.RoleRepository
                .ListByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false);

            var idByCode = roles.ToDictionary(r => r.Code, r => r.Id);

            if (codes.Any(c => !idByCode.ContainsKey(c)))
            {
                throw new BusinessRuleException("Selecione um perfil desta empresa.");
            }

            return [.. codes.Select(code => idByCode[code])];
        }
    }

    /// <summary>Activates or deactivates a user.</summary>
    public class ChangeUserStatusHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ChangeUserStatusCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(ChangeUserStatusCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await unitOfWork.UserRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Usuário inexistente.");

            if (request.IsBlocked && user.Id == currentUser.Id)
            {
                throw new BusinessRuleException("A inativação da sua conta fica a cargo de outro administrador.");
            }

            var actor = currentUser.Code.ToString();

            // Blocking, and never SoftDelete: an inactive person stays in the list so that
            // somebody can bring them back.
            if (request.IsBlocked)
            {
                user.Block(actor);
            }
            else
            {
                user.Unblock(actor);
            }

            unitOfWork.UserRepository.Update(user);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(User), user.Code,
                request.IsBlocked ? AuditAction.Deactivate : AuditAction.Activate, null, null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Soft deletes a user and revokes their sessions.</summary>
    public class DeleteUserHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeleteUserCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await unitOfWork.UserRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Usuário inexistente.");

            if (user.Id == currentUser.Id)
            {
                throw new BusinessRuleException("Outro administrador precisa excluir a sua conta.");
            }

            var actor = currentUser.Code.ToString();

            unitOfWork.UserRepository.Remove(user, actor);
            unitOfWork.RefreshTokenRepository.RevokeAllByUser(user.Id, actor);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(User), user.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// Brings a deleted user back. The one handler that reads past the soft delete, because
    /// restoring a row is the single operation that has to see what the others hide.
    ///
    /// The person comes back blocked, on purpose: whoever restores decides afterwards whether
    /// they may sign in again, instead of a deletion silently turning into an open account.
    /// </summary>
    public class RestoreUserHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<RestoreUserCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(RestoreUserCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await unitOfWork.UserRepository
                .GetByCodeIncludingDeletedAsync(request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Usuário inexistente.");

            if (user.IdTenant != currentUser.IdTenant)
            {
                throw new NotFoundException("Usuário inexistente.");
            }

            var actor = currentUser.Code.ToString();

            user.Activate(actor);
            user.Block(actor);

            unitOfWork.UserRepository.Update(user);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(User), user.Code,
                AuditAction.Activate, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    /// <summary>Stores the photo of a user.</summary>
    public class UploadUserPhotoHandler(
        IUnitOfWork unitOfWork,
        IUserPhotoStorage photoStorage,
        ICurrentUser currentUser)
        : IRequestHandler<UploadUserPhotoCommand, string>
    {
        /// <inheritdoc/>
        public async Task<string> Handle(
            UploadUserPhotoCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await unitOfWork.UserRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Usuário inexistente.");

            var previous = user.Photo;

            var fileName = await photoStorage
                .SaveAsync(user.IdTenant, user.Code, request.Content, cancellationToken)
                .ConfigureAwait(false);

            user.ChangePhoto(fileName, currentUser.Code.ToString());
            unitOfWork.UserRepository.Update(user);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // The old file is deleted only after the new one is stored and the database has
            // confirmed the change.
            if (!string.IsNullOrEmpty(previous))
            {
                await photoStorage.DeleteAsync(user.IdTenant, user.Code, previous, cancellationToken)
                    .ConfigureAwait(false);
            }

            return fileName;
        }
    }

    /// <summary>Removes the photo of a user.</summary>
    public class RemoveUserPhotoHandler(
        IUnitOfWork unitOfWork,
        IUserPhotoStorage photoStorage,
        ICurrentUser currentUser)
        : IRequestHandler<RemoveUserPhotoCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(RemoveUserPhotoCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await unitOfWork.UserRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Usuário inexistente.");

            var fileName = user.Photo;

            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            user.ChangePhoto(null, currentUser.Code.ToString());
            unitOfWork.UserRepository.Update(user);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            await photoStorage.DeleteAsync(user.IdTenant, user.Code, fileName, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the photo of a user.
    ///
    /// Not guarded by the users screen on purpose: anyone signed in has to be able to see
    /// their own avatar in the sidebar, even without access to user administration.
    ///
    /// Sem tela para guardar, a empresa é a única fronteira que sobra — e por isso ela é
    /// conferida aqui. Ler por código sem ela devolveria a foto de gente de outra revenda
    /// (RNF-04, e dado pessoal pela RNF-13).
    /// </summary>
    public class GetUserPhotoHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IUserPhotoStorage photoStorage)
        : IRequestHandler<GetUserPhotoQuery, StoredPhoto?>
    {
        /// <inheritdoc/>
        public async Task<StoredPhoto?> Handle(
            GetUserPhotoQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await unitOfWork.UserRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false);

            return string.IsNullOrEmpty(user?.Photo)
                ? null
                : await photoStorage.ReadAsync(user.IdTenant, user.Code, user.Photo, cancellationToken)
                    .ConfigureAwait(false);
        }
    }
}
