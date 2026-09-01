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
    /// <param name="IsActive">Whether the account can sign in.</param>
    /// <param name="Roles">Codes of the roles held.</param>
    /// <param name="RoleNames">Role names, displayed to the user.</param>
    /// <param name="HasPhoto">Whether there is a photo to load.</param>
    /// <param name="Document">CPF or CNPJ, digits only. The mask lives in the UI.</param>
    /// <param name="Phone">Phone with area code, digits only.</param>
    public sealed record UserDto(
        Guid Code,
        string Name,
        string Email,
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
    public sealed record ListUsersQuery(string? Search) : IRequest<IReadOnlyList<UserDto>>;

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
    /// <param name="IsActive">Whether the account can sign in.</param>
    /// <param name="Roles">Codes of the roles to assign.</param>
    /// <param name="Document">CPF or CNPJ. Optional.</param>
    /// <param name="Phone">Phone with area code. Optional.</param>
    public sealed record SaveUserCommand(
        Guid? Code,
        string Name,
        string Email,
        string? Password,
        bool IsActive,
        IReadOnlyList<Guid> Roles,
        string? Document = null,
        string? Phone = null) : IRequest<UserDto>;

    /// <summary>Activates or deactivates a user.</summary>
    /// <param name="Code">Public identifier of the user.</param>
    /// <param name="IsActive">Target state.</param>
    public sealed record ChangeUserStatusCommand(Guid Code, bool IsActive) : IRequest;

    /// <summary>Soft deletes a user.</summary>
    /// <param name="Code">Public identifier of the user.</param>
    public sealed record DeleteUserCommand(Guid Code) : IRequest;

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
                .ListByTenantAsync(currentUser.TenantId, request.Search, cancellationToken)
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

            var tenantId = currentUser.TenantId;
            var email = request.Email.Trim().ToLowerInvariant();
            var actor = currentUser.Code.ToString();
            var isNew = request.Code is null;

            User user;

            if (isNew)
            {
                if (await unitOfWork.UserRepository
                        .EmailExistsAsync(tenantId, email, ignoreId: null, cancellationToken)
                        .ConfigureAwait(false))
                {
                    throw new BusinessRuleException($"O e-mail {email} já está em uso.");
                }

                user = User.Create(
                    tenantId, request.Name.Trim(), email, passwordHasher.Hash(request.Password!), actor);

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
                    .GetByCodeAsync(request.Code!.Value, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new NotFoundException("Usuário inexistente.");

                if (await unitOfWork.UserRepository
                        .EmailExistsAsync(tenantId, email, user.Id, cancellationToken)
                        .ConfigureAwait(false))
                {
                    throw new BusinessRuleException($"O e-mail {email} já está em uso.");
                }

                user.Update(request.Name.Trim(), email, request.Document, request.Phone, actor);

                if (!string.IsNullOrWhiteSpace(request.Password))
                {
                    user.ChangePassword(passwordHasher.Hash(request.Password), actor);
                }

                if (!request.IsActive && user.Id == currentUser.Id)
                {
                    throw new BusinessRuleException("A inativação da sua conta fica a cargo de outro administrador.");
                }

                if (request.IsActive)
                {
                    user.Restore(actor);
                }
                else
                {
                    user.Delete(actor);
                }

                unitOfWork.UserRepository.Update(user);
            }

            var roleIds = await ResolveRoleIdsAsync(tenantId, request.Roles, cancellationToken)
                .ConfigureAwait(false);

            unitOfWork.UserRepository.ReplaceRoles(user.Id, roleIds, actor);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                tenantId, currentUser.Id, nameof(User), user.Code,
                isNew ? AuditAction.Create : AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            permissionService.InvalidateUser(user.Id);

            var saved = await unitOfWork.UserRepository
                .GetByCodeAsync(user.Code, cancellationToken)
                .ConfigureAwait(false) ?? user;

            return await UserMapper.ToDtoAsync(saved, unitOfWork, cancellationToken).ConfigureAwait(false);
        }

        private async Task<List<int>> ResolveRoleIdsAsync(
            int tenantId,
            IReadOnlyList<Guid> codes,
            CancellationToken cancellationToken)
        {
            var roles = await unitOfWork.RoleRepository
                .ListByTenantAsync(tenantId, cancellationToken)
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
                .GetByCodeAsync(request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Usuário inexistente.");

            if (!request.IsActive && user.Id == currentUser.Id)
            {
                throw new BusinessRuleException("A inativação da sua conta fica a cargo de outro administrador.");
            }

            var actor = currentUser.Code.ToString();

            if (request.IsActive)
            {
                user.Restore(actor);
            }
            else
            {
                user.Delete(actor);
            }

            unitOfWork.UserRepository.Update(user);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.TenantId, currentUser.Id, nameof(User), user.Code,
                request.IsActive ? AuditAction.Activate : AuditAction.Deactivate, null, null));

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
                .GetByCodeAsync(request.Code, cancellationToken)
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
                currentUser.TenantId, currentUser.Id, nameof(User), user.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Stores the photo of a user.</summary>
    public class UploadUserPhotoHandler(
        IUnitOfWork unitOfWork,
        IPhotoStorageService photoStorage,
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
                .GetByCodeAsync(request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Usuário inexistente.");

            var previous = user.Photo;

            var fileName = await photoStorage
                .SaveAsync(request.Content, request.FileName, cancellationToken)
                .ConfigureAwait(false);

            user.ChangePhoto(fileName, currentUser.Code.ToString());
            unitOfWork.UserRepository.Update(user);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // The old file is deleted only after the new one is stored and the database has
            // confirmed the change.
            if (!string.IsNullOrEmpty(previous))
            {
                await photoStorage.DeleteAsync(previous, cancellationToken).ConfigureAwait(false);
            }

            return fileName;
        }
    }

    /// <summary>Removes the photo of a user.</summary>
    public class RemoveUserPhotoHandler(
        IUnitOfWork unitOfWork,
        IPhotoStorageService photoStorage,
        ICurrentUser currentUser)
        : IRequestHandler<RemoveUserPhotoCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(RemoveUserPhotoCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await unitOfWork.UserRepository
                .GetByCodeAsync(request.Code, cancellationToken)
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
            await photoStorage.DeleteAsync(fileName, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the photo of a user.
    ///
    /// Not guarded by the users screen on purpose: anyone signed in has to be able to see
    /// their own avatar in the sidebar, even without access to user administration.
    /// </summary>
    public class GetUserPhotoHandler(IUnitOfWork unitOfWork, IPhotoStorageService photoStorage)
        : IRequestHandler<GetUserPhotoQuery, StoredPhoto?>
    {
        /// <inheritdoc/>
        public async Task<StoredPhoto?> Handle(
            GetUserPhotoQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await unitOfWork.UserRepository
                .GetByCodeAsync(request.Code, cancellationToken)
                .ConfigureAwait(false);

            return string.IsNullOrEmpty(user?.Photo)
                ? null
                : await photoStorage.ReadAsync(user.Photo, cancellationToken).ConfigureAwait(false);
        }
    }
}
