using FluentValidation;
using MediatR;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Vehicles.Validators
{
    /// <summary>
    /// What an expense has to carry.
    ///
    /// Deliberately short. The dealership records a line in a Word file today: a form that
    /// demands five fields for a ten reais bulb is slower than the Word, and the fast path has
    /// to stay description plus amount (RNF-02).
    /// </summary>
    public class SaveVehicleExpenseValidator : AbstractValidator<SaveVehicleExpenseCommand>
    {
        /// <summary>Builds the rules.</summary>
        public SaveVehicleExpenseValidator()
        {
            RuleFor(c => c.Description)
                .NotEmpty().WithMessage("Descreva o gasto.")
                .MaximumLength(160).WithMessage("A descrição pode ter no máximo 160 caracteres.");

            RuleFor(c => c.Amount)
                .GreaterThan(0).WithMessage("Informe um valor maior que zero.");

            RuleFor(c => c.ExpenseTypeCode)
                .NotEmpty().WithMessage("Selecione o tipo de gasto.");

            RuleFor(c => c.Notes)
                .MaximumLength(1000).WithMessage("A observação pode ter no máximo 1000 caracteres.");
        }
    }

    /// <summary>What a kind of expense has to carry (RF-09).</summary>
    public class SaveExpenseTypeValidator : AbstractValidator<SaveExpenseTypeCommand>
    {
        /// <summary>Builds the rules.</summary>
        public SaveExpenseTypeValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Informe o nome do tipo de gasto.")
                .MaximumLength(80).WithMessage("O nome pode ter no máximo 80 caracteres.");

            RuleFor(c => c.Keywords)
                .MaximumLength(500).WithMessage("As palavras-chave podem ter no máximo 500 caracteres.");
        }
    }
}

namespace RevendaPro.Application.Vehicles.Handlers
{
    /// <summary>Shared reads that several expense handlers need.</summary>
    internal static class ExpenseContext
    {
        /// <summary>Finds a vehicle of the current tenant, or refuses.</summary>
        /// <param name="unitOfWork">Unit of work.</param>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The vehicle.</returns>
        public static async Task<Vehicle> VehicleOrRefuseAsync(
            IUnitOfWork unitOfWork,
            int idTenant,
            Guid code,
            CancellationToken cancellationToken) =>
            await unitOfWork.VehicleRepository
                .GetByCodeAsync(idTenant, code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Veículo inexistente.");

        /// <summary>The kinds of expense of the tenant, keyed by internal Id.</summary>
        /// <param name="unitOfWork">Unit of work.</param>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The kinds, by Id.</returns>
        public static async Task<Dictionary<int, ExpenseType>> TypesByIdAsync(
            IUnitOfWork unitOfWork,
            int idTenant,
            CancellationToken cancellationToken)
        {
            var types = await unitOfWork.ExpenseTypeRepository
                .ListByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false);

            return types.ToDictionary(type => type.Id);
        }

        /// <summary>Builds the DTO of one expense.</summary>
        /// <param name="expense">The expense.</param>
        /// <param name="types">The kinds of the tenant, by Id.</param>
        /// <returns>The expense as the screen reads it.</returns>
        public static VehicleExpenseDto ToDto(
            VehicleExpense expense,
            IReadOnlyDictionary<int, ExpenseType> types)
        {
            var type = types.GetValueOrDefault(expense.IdExpenseType);

            return new VehicleExpenseDto(
                expense.Code,
                type?.Code ?? Guid.Empty,
                type?.Name ?? "Outros",
                expense.Description,
                expense.Amount,
                expense.Date,
                expense.Notes,
                expense.IsPaid);
        }
    }

    /// <summary>Lists what was spent on a vehicle (RF-08).</summary>
    public class ListVehicleExpensesHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListVehicleExpensesQuery, IReadOnlyList<VehicleExpenseDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<VehicleExpenseDto>> Handle(
            ListVehicleExpensesQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await ExpenseContext.VehicleOrRefuseAsync(
                unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var expenses = await unitOfWork.VehicleExpenseRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            var types = await ExpenseContext
                .TypesByIdAsync(unitOfWork, currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            return [.. expenses.Select(expense => ExpenseContext.ToDto(expense, types))];
        }
    }

    /// <summary>Records or changes what was spent on a vehicle (RF-08).</summary>
    public class SaveVehicleExpenseHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<SaveVehicleExpenseCommand, VehicleExpenseDto>
    {
        /// <inheritdoc/>
        public async Task<VehicleExpenseDto> Handle(
            SaveVehicleExpenseCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var actor = currentUser.Code.ToString();

            var vehicle = await ExpenseContext
                .VehicleOrRefuseAsync(unitOfWork, idTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var type = await unitOfWork.ExpenseTypeRepository
                .GetByCodeAsync(idTenant, request.ExpenseTypeCode, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Tipo de gasto inexistente.");

            VehicleExpense expense;

            if (request.Code is null)
            {
                expense = VehicleExpense.Create(
                    vehicle.Id, request.Description, type.Id, request.Amount, request.Date,
                    request.Notes, request.IsPaid, actor);

                unitOfWork.VehicleExpenseRepository.Add(expense);
            }
            else
            {
                expense = await unitOfWork.VehicleExpenseRepository
                    .GetByCodeAsync(request.Code.Value, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new NotFoundException("Gasto inexistente.");

                // The expense carries no tenant of its own: the isolation comes through the
                // vehicle, so it is checked here rather than trusted.
                if (expense.IdVehicle != vehicle.Id)
                {
                    throw new NotFoundException("Gasto inexistente.");
                }

                expense.Update(
                    request.Description, type.Id, request.Amount, request.Date,
                    request.Notes, request.IsPaid, actor);

                unitOfWork.VehicleExpenseRepository.Update(expense);
            }

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(VehicleExpense), expense.Code,
                request.Code is null ? AuditAction.Create : AuditAction.Update, null, null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            var types = await ExpenseContext
                .TypesByIdAsync(unitOfWork, idTenant, cancellationToken)
                .ConfigureAwait(false);

            return ExpenseContext.ToDto(expense, types);
        }
    }

    /// <summary>Turns a planned expense into a paid one (RF-11).</summary>
    public class ConfirmExpensePaymentHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ConfirmExpensePaymentCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(
            ConfirmExpensePaymentCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var expense = await ExpenseOfTenantAsync(request.Code, cancellationToken)
                .ConfigureAwait(false);

            expense.ConfirmPayment(currentUser.Code.ToString());

            unitOfWork.VehicleExpenseRepository.Update(expense);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Finds an expense and confirms it belongs to the current tenant.
        ///
        /// The expense carries no tenant of its own: the isolation comes through the vehicle,
        /// so it is checked rather than assumed. Answering "inexistente" for an expense of
        /// another dealership is deliberate — saying that a record exists but is off limits
        /// already tells the asker something.
        /// </summary>
        private async Task<VehicleExpense> ExpenseOfTenantAsync(
            Guid code,
            CancellationToken cancellationToken)
        {
            var expense = await unitOfWork.VehicleExpenseRepository
                .GetByCodeAsync(code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Gasto inexistente.");

            var vehicle = await unitOfWork.VehicleRepository
                .GetByIdAsync(expense.IdVehicle, cancellationToken)
                .ConfigureAwait(false);

            if (vehicle is null || vehicle.IdTenant != currentUser.IdTenant)
            {
                throw new NotFoundException("Gasto inexistente.");
            }

            return expense;
        }
    }

    /// <summary>Soft deletes an expense.</summary>
    public class DeleteVehicleExpenseHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeleteVehicleExpenseCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(
            DeleteVehicleExpenseCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var expense = await unitOfWork.VehicleExpenseRepository
                .GetByCodeAsync(request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Gasto inexistente.");

            var vehicle = await unitOfWork.VehicleRepository
                .GetByIdAsync(expense.IdVehicle, cancellationToken)
                .ConfigureAwait(false);

            if (vehicle is null || vehicle.IdTenant != currentUser.IdTenant)
            {
                throw new NotFoundException("Gasto inexistente.");
            }

            var actor = currentUser.Code.ToString();

            unitOfWork.VehicleExpenseRepository.Remove(expense, actor);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(VehicleExpense), expense.Code,
                AuditAction.Delete, null, null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Lists the kinds of expense of the tenant (RF-09).</summary>
    public class ListExpenseTypesHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListExpenseTypesQuery, IReadOnlyList<ExpenseTypeDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<ExpenseTypeDto>> Handle(
            ListExpenseTypesQuery request,
            CancellationToken cancellationToken)
        {
            var types = await unitOfWork.ExpenseTypeRepository
                .ListByTenantAsync(currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            var result = new List<ExpenseTypeDto>(types.Count);

            foreach (var type in types)
            {
                var uses = await unitOfWork.ExpenseTypeRepository
                    .CountExpensesAsync(type.Id, cancellationToken)
                    .ConfigureAwait(false);

                result.Add(new ExpenseTypeDto(type.Code, type.Name, type.Keywords, type.Position, uses));
            }

            return result;
        }
    }

    /// <summary>Creates or renames a kind of expense (RF-09).</summary>
    public class SaveExpenseTypeHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<SaveExpenseTypeCommand, ExpenseTypeDto>
    {
        /// <inheritdoc/>
        public async Task<ExpenseTypeDto> Handle(
            SaveExpenseTypeCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var actor = currentUser.Code.ToString();

            var existing = await unitOfWork.ExpenseTypeRepository
                .ListByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false);

            var duplicated = existing.Any(type =>
                string.Equals(type.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                && type.Code != request.Code);

            if (duplicated)
            {
                throw new BusinessRuleException($"Já existe um tipo de gasto chamado {request.Name.Trim()}.");
            }

            ExpenseType type;

            if (request.Code is null)
            {
                type = ExpenseType.Create(
                    idTenant, request.Name, request.Keywords, request.Position, actor);

                unitOfWork.ExpenseTypeRepository.Add(type);
            }
            else
            {
                type = existing.FirstOrDefault(t => t.Code == request.Code.Value)
                    ?? throw new NotFoundException("Tipo de gasto inexistente.");

                type.Update(request.Name, request.Keywords, request.Position, actor);

                unitOfWork.ExpenseTypeRepository.Update(type);
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ExpenseTypeDto(type.Code, type.Name, type.Keywords, type.Position, 0);
        }
    }

    /// <summary>Soft deletes a kind of expense that no expense uses.</summary>
    public class DeleteExpenseTypeHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeleteExpenseTypeCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(DeleteExpenseTypeCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var type = await unitOfWork.ExpenseTypeRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Tipo de gasto inexistente.");

            var uses = await unitOfWork.ExpenseTypeRepository
                .CountExpensesAsync(type.Id, cancellationToken)
                .ConfigureAwait(false);

            // Deleting a kind in use would turn every line pointing at it into an orphan: the
            // cost would stay right and the breakdown would become fiction.
            if (uses > 0)
            {
                throw new BusinessRuleException(
                    $"O tipo {type.Name} está em uso por {uses} gasto(s). " +
                    "Troque o tipo desses lançamentos para removê-lo.");
            }

            unitOfWork.ExpenseTypeRepository.Remove(type, currentUser.Code.ToString());

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Suggests a description and the kind that goes with it.
    ///
    /// Two layers, in this order: what this dealership already wrote, which is the strongest
    /// signal and makes the second entry faster than the first; then the keywords of each kind,
    /// for a word appearing for the first time.
    /// </summary>
    public class SuggestExpenseHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<SuggestExpenseQuery, IReadOnlyList<ExpenseSuggestionDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<ExpenseSuggestionDto>> Handle(
            SuggestExpenseQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.Term))
            {
                return [];
            }

            var idTenant = currentUser.IdTenant;

            var types = await ExpenseContext
                .TypesByIdAsync(unitOfWork, idTenant, cancellationToken)
                .ConfigureAwait(false);

            var used = await unitOfWork.VehicleExpenseRepository
                .SuggestDescriptionsAsync(idTenant, request.Term, cancellationToken)
                .ConfigureAwait(false);

            var suggestions = used
                .Where(suggestion => types.ContainsKey(suggestion.IdExpenseType))
                .Select(suggestion => new ExpenseSuggestionDto(
                    suggestion.Description,
                    types[suggestion.IdExpenseType].Code,
                    types[suggestion.IdExpenseType].Name))
                .ToList();

            if (suggestions.Count > 0)
            {
                return suggestions;
            }

            // Nothing written before matches. Fall back to the keywords, so a word appearing
            // for the first time still arrives classified.
            var byKeyword = types.Values
                .FirstOrDefault(type => type.Matches(request.Term));

            return byKeyword is null
                ? []
                : [new ExpenseSuggestionDto(request.Term.Trim(), byKeyword.Code, byKeyword.Name)];
        }
    }
}
