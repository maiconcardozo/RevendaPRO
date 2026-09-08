using FluentValidation;
using MediatR;
using RevendaPro.Application.Company.Commands;
using RevendaPro.Application.Company.DTOs;
using RevendaPro.Application.Company.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Company.Validators
{
    /// <summary>Formato dos dados da revenda. A regra de negócio fica na entidade.</summary>
    public class SaveCompanyValidator : AbstractValidator<SaveCompanyCommand>
    {
        /// <summary>Builds the rules.</summary>
        public SaveCompanyValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Informe o nome da revenda.")
                .MaximumLength(160).WithMessage("O nome pode ter no máximo 160 caracteres.");

            RuleFor(c => c.Email)
                .EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email))
                .WithMessage("Informe um e-mail válido.")
                .MaximumLength(160).WithMessage("O e-mail pode ter no máximo 160 caracteres.");

            RuleFor(c => c.Address)
                .MaximumLength(240).WithMessage("O endereço pode ter no máximo 240 caracteres.");
        }
    }
}

namespace RevendaPro.Application.Company.Handlers
{
    /// <summary>Os dados da revenda de quem está logado.</summary>
    public class GetCompanyHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<GetCompanyQuery, CompanyDto>
    {
        /// <inheritdoc/>
        public async Task<CompanyDto> Handle(GetCompanyQuery request, CancellationToken cancellationToken)
        {
            var tenant = await CompanyContext.TenantOrRefuseAsync(unitOfWork, currentUser, cancellationToken)
                .ConfigureAwait(false);

            return CompanyContext.ToDto(tenant);
        }
    }

    /// <summary>Edita os dados da revenda.</summary>
    public class SaveCompanyHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<SaveCompanyCommand, CompanyDto>
    {
        /// <inheritdoc/>
        public async Task<CompanyDto> Handle(SaveCompanyCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var tenant = await CompanyContext.TenantOrRefuseAsync(unitOfWork, currentUser, cancellationToken)
                .ConfigureAwait(false);

            var actor = currentUser.Code.ToString();

            tenant.Rename(request.Name, actor);
            tenant.SetDetails(request.Document, request.Phone, request.Email, request.Address, actor);

            unitOfWork.TenantRepository.Update(tenant);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                tenant.Id, currentUser.Id, nameof(Tenant), tenant.Code,
                AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            return CompanyContext.ToDto(tenant);
        }
    }

    /// <summary>O que os handlers repetem: achar a revenda do token, e mapeá-la.</summary>
    public static class CompanyContext
    {
        /// <summary>A revenda de quem está logado, ou a recusa.</summary>
        /// <param name="unitOfWork">Acesso aos dados.</param>
        /// <param name="currentUser">Quem está logado.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A revenda.</returns>
        public static async Task<Tenant> TenantOrRefuseAsync(
            IUnitOfWork unitOfWork,
            ICurrentUser currentUser,
            CancellationToken cancellationToken) =>
            await unitOfWork.TenantRepository
                .FindAsync(currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Revenda inexistente.");

        /// <summary>A revenda como a tela e os documentos leem.</summary>
        /// <param name="tenant">A revenda.</param>
        /// <returns>O DTO.</returns>
        public static CompanyDto ToDto(Tenant tenant) =>
            new(tenant.Name, tenant.Document, tenant.Phone, tenant.Email, tenant.Address);
    }
}
