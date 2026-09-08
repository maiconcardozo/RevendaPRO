using FluentValidation;
using MediatR;
using RevendaPro.Application.Cashflow.Commands;
using RevendaPro.Application.Cashflow.DTOs;
using RevendaPro.Application.Cashflow.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Application.Cashflow.Validators
{
    /// <summary>Formato da despesa da loja. A regra de negócio fica na entidade.</summary>
    public class SaveStoreExpenseValidator : AbstractValidator<SaveStoreExpenseCommand>
    {
        /// <summary>Builds the rules.</summary>
        public SaveStoreExpenseValidator()
        {
            RuleFor(c => c.Description)
                .NotEmpty().WithMessage("Descreva a despesa.")
                .MaximumLength(160).WithMessage("A descrição pode ter no máximo 160 caracteres.");

            RuleFor(c => c.ExpenseTypeCode)
                .NotEmpty().WithMessage("Escolha o tipo da despesa.");

            RuleFor(c => c.Amount)
                .GreaterThan(0).WithMessage("Informe um valor maior que zero.");

            RuleFor(c => c.Notes)
                .MaximumLength(1000).WithMessage("A anotação pode ter no máximo 1000 caracteres.");
        }
    }
}

namespace RevendaPro.Application.Cashflow.Handlers
{
    /// <summary>O que os handlers da despesa da loja repetem.</summary>
    internal static class StoreExpenseContext
    {
        /// <summary>
        /// O tipo escolhido, recusado quando é de outra revenda ou quando serve só para carro.
        ///
        /// A tela já oferece só os tipos da loja; a recusa aqui é para quem chama a API direto,
        /// e é o que impede uma despesa de aluguel classificada como Funilaria.
        /// </summary>
        /// <param name="unitOfWork">Acesso aos dados.</param>
        /// <param name="idTenant">Revenda dona do catálogo.</param>
        /// <param name="code">Identificador público do tipo.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O tipo.</returns>
        public static async Task<ExpenseType> TypeOrRefuseAsync(
            IUnitOfWork unitOfWork,
            int idTenant,
            Guid code,
            CancellationToken cancellationToken)
        {
            var type = await unitOfWork.ExpenseTypeRepository
                .GetByCodeAsync(idTenant, code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new BusinessRuleException("Escolha um tipo desta revenda.");

            if (!type.ServesStore)
            {
                throw new BusinessRuleException(
                    $"O tipo {type.Name} serve para gasto de carro. Escolha um tipo da loja, "
                    + "ou marque esse tipo como dos dois em Tipos de gasto.");
            }

            return type;
        }

        /// <summary>O fornecedor escolhido, recusado quando é de outra revenda.</summary>
        /// <param name="unitOfWork">Acesso aos dados.</param>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="code">Identificador público do fornecedor, ou nulo.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O fornecedor, ou nulo.</returns>
        public static async Task<Supplier?> SupplierOrRefuseAsync(
            IUnitOfWork unitOfWork,
            int idTenant,
            Guid? code,
            CancellationToken cancellationToken)
        {
            if (code is not { } supplierCode)
            {
                return null;
            }

            return await unitOfWork.SupplierRepository
                .GetByCodeAsync(idTenant, supplierCode, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new BusinessRuleException("Escolha um fornecedor desta revenda.");
        }

        /// <summary>A despesa da revenda de quem está logado, ou a recusa.</summary>
        /// <param name="unitOfWork">Acesso aos dados.</param>
        /// <param name="idTenant">Revenda dona da despesa.</param>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A despesa.</returns>
        public static async Task<StoreExpense> ExpenseOrRefuseAsync(
            IUnitOfWork unitOfWork,
            int idTenant,
            Guid code,
            CancellationToken cancellationToken) =>
            await unitOfWork.StoreExpenseRepository
                .GetByCodeAsync(idTenant, code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Despesa inexistente.");

        /// <summary>A despesa como a tela lê.</summary>
        /// <param name="expense">A despesa.</param>
        /// <param name="type">O tipo dela.</param>
        /// <param name="supplier">O fornecedor dela, quando há.</param>
        /// <returns>O DTO.</returns>
        public static StoreExpenseDto ToDto(StoreExpense expense, ExpenseType? type, Supplier? supplier) =>
            new(expense.Code,
                expense.Description,
                type?.Code ?? Guid.Empty,
                type?.Name ?? "Outros",
                supplier?.Code,
                supplier?.Name,
                expense.Amount,
                expense.Date,
                expense.DueDate,
                expense.PaidDate,
                expense.IsPaid,
                expense.IsOverdueOn(BrazilTime.Today),
                expense.Notes);
    }

    /// <summary>As despesas da loja num período.</summary>
    public class ListStoreExpensesHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListStoreExpensesQuery, IReadOnlyList<StoreExpenseDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<StoreExpenseDto>> Handle(
            ListStoreExpensesQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;

            var expenses = await unitOfWork.StoreExpenseRepository
                .ListByTenantAsync(idTenant, request.From, request.To, cancellationToken)
                .ConfigureAwait(false);

            if (expenses.Count == 0)
            {
                return [];
            }

            // Os dois catálogos de uma vez, e não um por linha: uma lista de cinquenta despesas
            // custaria cem consultas para escrever dois nomes.
            var types = (await unitOfWork.ExpenseTypeRepository
                .ListByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false))
                .ToDictionary(type => type.Id);

            var suppliers = (await unitOfWork.SupplierRepository
                .ListByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false))
                .ToDictionary(supplier => supplier.Id);

            return [.. expenses.Select(expense => StoreExpenseContext.ToDto(
                expense,
                types.GetValueOrDefault(expense.IdExpenseType),
                expense.IdSupplier is { } id ? suppliers.GetValueOrDefault(id) : null))];
        }
    }

    /// <summary>Lança ou edita uma despesa da loja.</summary>
    public class SaveStoreExpenseHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<SaveStoreExpenseCommand, StoreExpenseDto>
    {
        /// <inheritdoc/>
        public async Task<StoreExpenseDto> Handle(
            SaveStoreExpenseCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var actor = currentUser.Code.ToString();

            var type = await StoreExpenseContext
                .TypeOrRefuseAsync(unitOfWork, idTenant, request.ExpenseTypeCode, cancellationToken)
                .ConfigureAwait(false);

            var supplier = await StoreExpenseContext
                .SupplierOrRefuseAsync(unitOfWork, idTenant, request.SupplierCode, cancellationToken)
                .ConfigureAwait(false);

            var idSupplier = supplier?.Id;

            StoreExpense expense;
            var isNew = request.Code is null;

            if (isNew)
            {
                expense = StoreExpense.Create(
                    idTenant, request.Description, type.Id, request.Amount, request.Date,
                    request.DueDate, request.IsPaid, request.PaidDate, idSupplier, request.Notes, actor);

                unitOfWork.StoreExpenseRepository.Add(expense);
            }
            else
            {
                expense = await StoreExpenseContext
                    .ExpenseOrRefuseAsync(unitOfWork, idTenant, request.Code!.Value, cancellationToken)
                    .ConfigureAwait(false);

                expense.Update(
                    request.Description, type.Id, request.Amount, request.Date, request.DueDate,
                    request.IsPaid, request.PaidDate, idSupplier, request.Notes, actor);

                unitOfWork.StoreExpenseRepository.Update(expense);
            }

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(StoreExpense), expense.Code,
                isNew ? AuditAction.Create : AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            return StoreExpenseContext.ToDto(expense, type, supplier);
        }
    }

    /// <summary>Dá baixa numa despesa da loja, ou desfaz a baixa.</summary>
    public class PayStoreExpenseHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<PayStoreExpenseCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(PayStoreExpenseCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var expense = await StoreExpenseContext
                .ExpenseOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false);

            var actor = currentUser.Code.ToString();

            if (request.IsPaid)
            {
                expense.MarkAsPaid(request.PaidDate ?? BrazilTime.Today, actor);
            }
            else
            {
                expense.MarkAsPlanned(actor);
            }

            unitOfWork.StoreExpenseRepository.Update(expense);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Exclui uma despesa da loja, logicamente.</summary>
    public class DeleteStoreExpenseHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeleteStoreExpenseCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(DeleteStoreExpenseCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;

            var expense = await StoreExpenseContext
                .ExpenseOrRefuseAsync(unitOfWork, idTenant, request.Code, cancellationToken)
                .ConfigureAwait(false);

            unitOfWork.StoreExpenseRepository.Remove(expense, currentUser.Code.ToString());

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(StoreExpense), expense.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
