using FluentValidation;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Roles.DTOs
{
    /// <summary>A role and the screens it grants.</summary>
    /// <param name="Code">Public identifier. The internal Id is never exposed.</param>
    /// <param name="Name">Role name, displayed to the user.</param>
    /// <param name="Description">What the role is for.</param>
    /// <param name="IsSystem">System roles cannot be deleted.</param>
    /// <param name="ScreenCount">How many screens it grants.</param>
    /// <param name="Screens">Codes of the granted screens.</param>
    public sealed record RoleDto(
        Guid Code,
        string Name,
        string? Description,
        bool IsSystem,
        int ScreenCount,
        IReadOnlyList<Guid> Screens);
}

namespace RevendaPro.Application.Roles.Queries
{
    using MediatR;
    using RevendaPro.Application.Roles.DTOs;

    /// <summary>Lists the roles of the tenant.</summary>
    public sealed record ListRolesQuery : IRequest<IReadOnlyList<RoleDto>>;
}

namespace RevendaPro.Application.Roles.Commands
{
    using MediatR;
    using RevendaPro.Application.Roles.DTOs;

    /// <summary>Creates or updates a role and the screens it grants.</summary>
    /// <param name="Code">Null creates; filled updates.</param>
    /// <param name="Name">Role name.</param>
    /// <param name="Description">What the role is for.</param>
    /// <param name="Screens">Codes of the screens to grant.</param>
    public sealed record SaveRoleCommand(
        Guid? Code,
        string Name,
        string? Description,
        IReadOnlyList<Guid> Screens) : IRequest<RoleDto>;

    /// <summary>Soft deletes a role that is not a system role and is not in use.</summary>
    /// <param name="Code">Public identifier of the role.</param>
    public sealed record DeleteRoleCommand(Guid Code) : IRequest;
}

namespace RevendaPro.Application.Roles.Validators
{
    using FluentValidation;
    using RevendaPro.Application.Roles.Commands;

    /// <summary>Validates the role form.</summary>
    public class SaveRoleValidator : AbstractValidator<SaveRoleCommand>
    {
        public SaveRoleValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Informe o nome do perfil.")
                .MaximumLength(80).WithMessage("O nome pode ter no máximo 80 caracteres.");

            RuleFor(c => c.Description)
                .MaximumLength(240).WithMessage("A descrição pode ter no máximo 240 caracteres.");
        }
    }
}

namespace RevendaPro.Application.Roles.Handlers
{
    using MediatR;
    using RevendaPro.Application.Roles.Commands;
    using RevendaPro.Application.Roles.DTOs;
    using RevendaPro.Application.Roles.Queries;

    /// <summary>Lists the roles of the tenant with the screens each one grants.</summary>
    public class ListRolesHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<RoleDto>> Handle(
            ListRolesQuery request,
            CancellationToken cancellationToken)
        {
            var roles = await unitOfWork.RoleRepository
                .ListByTenantAsync(currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            var screens = await unitOfWork.ScreenRepository
                .GetAllAsync(cancellationToken)
                .ConfigureAwait(false);

            var codeById = screens.ToDictionary(s => s.Id, s => s.Code);
            var result = new List<RoleDto>(roles.Count);

            foreach (var role in roles)
            {
                var ids = await unitOfWork.RoleRepository
                    .GetScreenIdsAsync(role.Id, cancellationToken)
                    .ConfigureAwait(false);

                var codes = ids.Where(codeById.ContainsKey).Select(id => codeById[id]).ToList();

                result.Add(new RoleDto(
                    role.Code, role.Name, role.Description, role.IsSystem, codes.Count, codes));
            }

            return result;
        }
    }

    /// <summary>Creates or updates a role and the screens it grants.</summary>
    public class SaveRoleHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IPermissionService permissionService)
        : IRequestHandler<SaveRoleCommand, RoleDto>
    {
        /// <inheritdoc/>
        public async Task<RoleDto> Handle(SaveRoleCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var name = request.Name.Trim();
            var isNew = request.Code is null;

            Role role;

            if (isNew)
            {
                if (await unitOfWork.RoleRepository
                        .NameExistsAsync(idTenant, name, ignoreId: null, cancellationToken)
                        .ConfigureAwait(false))
                {
                    throw new BusinessRuleException($"Já existe um perfil chamado \"{name}\".");
                }

                role = Role.Create(idTenant, name, request.Description, isSystem: false);
                unitOfWork.RoleRepository.Add(role);

                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

                // Read back so the Id assigned by the database is known before granting.
                role = await unitOfWork.RoleRepository
                    .GetByNameAsync(idTenant, name, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new BusinessRuleException("Falha ao criar o perfil.");
            }
            else
            {
                role = await unitOfWork.RoleRepository
                    .GetByCodeAsync(request.Code!.Value, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new NotFoundException("Perfil inexistente.");

                if (await unitOfWork.RoleRepository
                        .NameExistsAsync(idTenant, name, role.Id, cancellationToken)
                        .ConfigureAwait(false))
                {
                    throw new BusinessRuleException($"Já existe um perfil chamado \"{name}\".");
                }

                role.Update(name, request.Description, currentUser.Code.ToString());
                unitOfWork.RoleRepository.Update(role);
            }

            var screenIds = await ResolveScreenIdsAsync(request.Screens, cancellationToken)
                .ConfigureAwait(false);

            unitOfWork.RoleRepository.ReplaceScreens(
                role.Id, screenIds, currentUser.Code.ToString());

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(Role), role.Code,
                isNew ? AuditAction.Create : AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Without this, whoever is already signed in would keep the old screens until
            // the cache expires.
            permissionService.InvalidateRole(role.Id);

            return new RoleDto(
                role.Code, role.Name, role.Description, role.IsSystem,
                request.Screens.Count, request.Screens);
        }

        private async Task<List<int>> ResolveScreenIdsAsync(
            IReadOnlyList<Guid> codes,
            CancellationToken cancellationToken)
        {
            var screens = await unitOfWork.ScreenRepository
                .GetAllAsync(cancellationToken)
                .ConfigureAwait(false);

            var idByCode = screens.ToDictionary(s => s.Code, s => s.Id);

            return [.. codes.Where(idByCode.ContainsKey).Select(code => idByCode[code])];
        }
    }

    /// <summary>Soft deletes a role that is not a system role and is not in use.</summary>
    public class DeleteRoleHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IPermissionService permissionService)
        : IRequestHandler<DeleteRoleCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var role = await unitOfWork.RoleRepository
                .GetByCodeAsync(request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Perfil inexistente.");

            if (!role.CanBeDeleted)
            {
                throw new BusinessRuleException(
                    $"O perfil \"{role.Name}\" é de sistema e permanece no cadastro. " +
                    "Crie um perfil próprio para ter um que possa ser excluído.");
            }

            var inUse = await unitOfWork.UserRepository
                .CountByRoleAsync(role.Id, cancellationToken)
                .ConfigureAwait(false);

            if (inUse > 0)
            {
                throw new BusinessRuleException(
                    $"O perfil \"{role.Name}\" esta em uso por {inUse} usuário(s). " +
                    "Troque o perfil dessas pessoas antes de excluir.");
            }

            unitOfWork.RoleRepository.Remove(role, currentUser.Code.ToString());

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(Role), role.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            permissionService.InvalidateRole(role.Id);
        }
    }
}
