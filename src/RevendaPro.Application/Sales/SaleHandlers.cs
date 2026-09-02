using FluentValidation;
using MediatR;
using RevendaPro.Application.Sales.Commands;
using RevendaPro.Application.Sales.DTOs;
using RevendaPro.Application.Sales.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Application.Sales.Validators
{
    /// <summary>What the screen has to answer before the domain is asked.</summary>
    public class RegisterProposalValidator : AbstractValidator<RegisterProposalCommand>
    {
        /// <summary>Builds the rules.</summary>
        public RegisterProposalValidator()
        {
            RuleFor(c => c.ProspectName)
                .NotEmpty().WithMessage("Informe quem fez a proposta.")
                .MaximumLength(120).WithMessage("O nome pode ter no máximo 120 caracteres.");

            RuleFor(c => c.Amount)
                .GreaterThan(0).WithMessage("Informe um valor maior que zero.");

            RuleFor(c => c.Notes)
                .MaximumLength(500).WithMessage("As observações cabem em 500 caracteres.");
        }
    }

    /// <summary>What the screen has to answer before the domain is asked.</summary>
    public class RegisterSaleValidator : AbstractValidator<RegisterSaleCommand>
    {
        /// <summary>Builds the rules.</summary>
        public RegisterSaleValidator()
        {
            RuleFor(c => c.Amount)
                .GreaterThan(0).WithMessage("Informe o valor da venda.");

            RuleFor(c => c.BuyerName)
                .NotEmpty().WithMessage("Informe quem comprou.")
                .MaximumLength(120).WithMessage("O nome pode ter no máximo 120 caracteres.");

            RuleFor(c => c.PartnerStoreName)
                .NotEmpty().WithMessage("Informe a loja parceira.")
                .When(c => c.Channel == SaleChannel.PartnerStore);

            RuleFor(c => c.Commission)
                .GreaterThanOrEqualTo(0).WithMessage("A comissão é um valor positivo, ou zero.");

            RuleFor(c => c.TradeIn)
                .NotNull().WithMessage("Descreva o carro que entrou na troca.")
                .When(c => c.PaymentMethod is PaymentMethod.TradeIn or PaymentMethod.TradeInWithCash);

            RuleFor(c => c.TradeIn!.Plate)
                .Must(VehicleIdentifiers.IsValidPlate)
                .WithMessage("Placa do carro da troca inválida. Use ABC1234 ou ABC1D23.")
                .When(c => c.TradeIn is not null);

            RuleFor(c => c.TradeIn!.Chassis)
                .Must(VehicleIdentifiers.IsValidChassis)
                .WithMessage("Chassi do carro da troca inválido. São 17 caracteres, sem I, O e Q.")
                .When(c => c.TradeIn is not null);
        }
    }
}

namespace RevendaPro.Application.Sales.Handlers
{
    /// <summary>What every sale handler needs: the vehicle of the tenant, and its cost.</summary>
    internal static class SaleContext
    {
        /// <summary>The vehicle, refused when it belongs to nobody or to another tenant.</summary>
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

        /// <summary>The cost of the vehicle, summed now.</summary>
        /// <param name="unitOfWork">Unit of work.</param>
        /// <param name="vehicle">The vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The cost.</returns>
        public static async Task<VehicleCost> CostOfAsync(
            IUnitOfWork unitOfWork,
            Vehicle vehicle,
            CancellationToken cancellationToken)
        {
            var expenses = await unitOfWork.VehicleExpenseRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            return VehicleCost.Of(vehicle, expenses);
        }

        /// <summary>Turns the arithmetic into what the screen reads.</summary>
        /// <param name="result">The result.</param>
        /// <returns>The DTO.</returns>
        public static DealResultDto ToDto(DealResult result) =>
            new(result.Amount,
                result.PartnerCut,
                result.Commission,
                result.Cost,
                result.Received,
                result.GrossProfit,
                result.NetProfit,
                result.Margin);

        /// <summary>Builds the DTO of one proposal against the cost of its vehicle.</summary>
        /// <param name="proposal">The proposal.</param>
        /// <param name="cost">The cost of the vehicle.</param>
        /// <returns>The DTO.</returns>
        public static ProposalDto ToDto(Proposal proposal, VehicleCost cost) =>
            new(proposal.Code,
                proposal.ProspectName,
                proposal.ProspectPhone,
                proposal.Amount,
                proposal.Date,
                proposal.PaymentMethod,
                proposal.Channel,
                proposal.PartnerCutPercent,
                proposal.PartnerCutAmount,
                proposal.Status,
                proposal.Notes,
                ToDto(proposal.ResultAgainst(cost)));

        /// <summary>Builds the DTO of the sale against the cost of its vehicle.</summary>
        /// <param name="sale">The sale.</param>
        /// <param name="vehicle">The vehicle sold.</param>
        /// <param name="cost">Its cost.</param>
        /// <param name="proposalCode">Public code of the proposal it closed.</param>
        /// <param name="tradeInVehicleCode">Public code of the car that came in.</param>
        /// <returns>The DTO.</returns>
        public static SaleDto ToDto(
            Sale sale,
            Vehicle vehicle,
            VehicleCost cost,
            Guid? proposalCode,
            Guid? tradeInVehicleCode) =>
            new(sale.Code,
                proposalCode,
                sale.Date,
                sale.Amount,
                sale.CashAmount,
                sale.PaymentMethod,
                sale.Channel,
                sale.PartnerStoreName,
                sale.PartnerCutPercent,
                sale.PartnerCutAmount,
                sale.Commission,
                sale.CommissionNotes,
                sale.BuyerName,
                sale.BuyerDocument,
                sale.BuyerPhone,
                sale.TradeInValue,
                tradeInVehicleCode,
                sale.Notes,
                vehicle.DaysInStock(sale.Date),
                ToDto(sale.ResultAgainst(cost)));
    }

    /// <summary>Lists the proposals of a vehicle, each with its projected profit (RF-19).</summary>
    public class ListProposalsHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListProposalsQuery, IReadOnlyList<ProposalDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<ProposalDto>> Handle(
            ListProposalsQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await SaleContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var cost = await SaleContext.CostOfAsync(unitOfWork, vehicle, cancellationToken)
                .ConfigureAwait(false);

            var proposals = await unitOfWork.ProposalRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            return [.. proposals.Select(proposal => SaleContext.ToDto(proposal, cost))];
        }
    }

    /// <summary>Answers what a deal would leave, before anything is saved (RF-19).</summary>
    public class PreviewDealHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<PreviewDealQuery, DealResultDto>
    {
        /// <inheritdoc/>
        public async Task<DealResultDto> Handle(PreviewDealQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await SaleContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var cost = await SaleContext.CostOfAsync(unitOfWork, vehicle, cancellationToken)
                .ConfigureAwait(false);

            var partnerCut = request.Channel == SaleChannel.PartnerStore
                ? DealResult.PartnerCutOf(request.Amount, request.PartnerCutPercent, request.PartnerCutAmount)
                : 0;

            return SaleContext.ToDto(
                new DealResult(request.Amount, partnerCut, request.Commission, cost.Total));
        }
    }

    /// <summary>Records what somebody offered (RF-18).</summary>
    public class RegisterProposalHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<RegisterProposalCommand, ProposalDto>
    {
        /// <inheritdoc/>
        public async Task<ProposalDto> Handle(RegisterProposalCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await SaleContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            if (vehicle.Status == VehicleStatus.Sold)
            {
                throw new BusinessRuleException("Este veículo já foi vendido.");
            }

            var proposal = Proposal.Create(
                vehicle.Id, request.ProspectName, request.ProspectPhone, request.Amount,
                request.Date, request.PaymentMethod, request.Channel,
                request.PartnerCutPercent, request.PartnerCutAmount, request.Notes,
                currentUser.Code.ToString());

            unitOfWork.ProposalRepository.Add(proposal);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(Proposal), proposal.Code,
                AuditAction.Create, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            var cost = await SaleContext.CostOfAsync(unitOfWork, vehicle, cancellationToken)
                .ConfigureAwait(false);

            return SaleContext.ToDto(proposal, cost);
        }
    }

    /// <summary>Declines a proposal. It stays on record: a declined offer is information.</summary>
    public class DeclineProposalHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeclineProposalCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(DeclineProposalCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var (proposal, _) = await ProposalOfVehicleAsync(
                unitOfWork, currentUser.IdTenant, request.VehicleCode, request.ProposalCode, cancellationToken)
                .ConfigureAwait(false);

            proposal.Decline(currentUser.Code.ToString());

            unitOfWork.ProposalRepository.Update(proposal);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>The proposal, refused when it belongs to another vehicle or tenant.</summary>
        /// <param name="unitOfWork">Unit of work.</param>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="vehicleCode">Public identifier of the vehicle.</param>
        /// <param name="proposalCode">Public identifier of the proposal.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The proposal and its vehicle.</returns>
        internal static async Task<(Proposal Proposal, Vehicle Vehicle)> ProposalOfVehicleAsync(
            IUnitOfWork unitOfWork,
            int idTenant,
            Guid vehicleCode,
            Guid proposalCode,
            CancellationToken cancellationToken)
        {
            var vehicle = await SaleContext
                .VehicleOrRefuseAsync(unitOfWork, idTenant, vehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var proposal = await unitOfWork.ProposalRepository
                .GetByCodeAsync(proposalCode, cancellationToken)
                .ConfigureAwait(false);

            if (proposal is null || proposal.IdVehicle != vehicle.Id)
            {
                throw new NotFoundException("Proposta inexistente.");
            }

            return (proposal, vehicle);
        }
    }

    /// <summary>Soft deletes a proposal recorded by mistake. An accepted one is refused.</summary>
    public class DeleteProposalHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeleteProposalCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(DeleteProposalCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var (proposal, _) = await DeclineProposalHandler.ProposalOfVehicleAsync(
                unitOfWork, currentUser.IdTenant, request.VehicleCode, request.ProposalCode, cancellationToken)
                .ConfigureAwait(false);

            if (proposal.Status == ProposalStatus.Accepted)
            {
                throw new BusinessRuleException(
                    "Esta proposta fechou uma venda. Cancele a venda antes de excluí-la.");
            }

            unitOfWork.ProposalRepository.Remove(proposal, currentUser.Code.ToString());

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(Proposal), proposal.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reads the sale of a vehicle, or null while it is on the lot.</summary>
    public class GetSaleHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<GetSaleQuery, SaleDto?>
    {
        /// <inheritdoc/>
        public async Task<SaleDto?> Handle(GetSaleQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await SaleContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var sale = await unitOfWork.SaleRepository
                .GetByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            if (sale is null)
            {
                return null;
            }

            var cost = await SaleContext.CostOfAsync(unitOfWork, vehicle, cancellationToken)
                .ConfigureAwait(false);

            return SaleContext.ToDto(
                sale, vehicle, cost,
                await CodeOfProposalAsync(sale, cancellationToken).ConfigureAwait(false),
                await CodeOfTradeInAsync(sale, cancellationToken).ConfigureAwait(false));
        }

        private async Task<Guid?> CodeOfProposalAsync(Sale sale, CancellationToken cancellationToken)
        {
            if (sale.IdProposal is null)
            {
                return null;
            }

            var proposal = await unitOfWork.ProposalRepository
                .GetByIdAsync(sale.IdProposal.Value, cancellationToken)
                .ConfigureAwait(false);

            return proposal?.Code;
        }

        private async Task<Guid?> CodeOfTradeInAsync(Sale sale, CancellationToken cancellationToken)
        {
            if (sale.IdTradeInVehicle is null)
            {
                return null;
            }

            var incoming = await unitOfWork.VehicleRepository
                .GetByIdAsync(sale.IdTradeInVehicle.Value, cancellationToken)
                .ConfigureAwait(false);

            return incoming?.Code;
        }
    }

    /// <summary>
    /// Registers the sale (RF-20). The only door to "sold".
    ///
    /// Order matters here, and each step exists for a reason:
    /// <list type="number">
    /// <item>the car of the trade is registered first and committed, because the sale needs
    /// its Id and the database is what assigns it;</item>
    /// <item>the sale is recorded, the vehicle moves to sold and writes its history;</item>
    /// <item>the accepted proposal is marked, and the other open ones are declined — the car
    /// has one buyer.</item>
    /// </list>
    /// </summary>
    public class RegisterSaleHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<RegisterSaleCommand, SaleDto>
    {
        /// <inheritdoc/>
        public async Task<SaleDto> Handle(RegisterSaleCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var actor = currentUser.Code.ToString();

            var vehicle = await SaleContext
                .VehicleOrRefuseAsync(unitOfWork, idTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var existing = await unitOfWork.SaleRepository
                .GetByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                throw new BusinessRuleException(
                    "Este veículo já tem uma venda registrada. Cancele-a para registrar outra.");
            }

            var proposal = await AcceptedProposalAsync(vehicle, request.ProposalCode, cancellationToken)
                .ConfigureAwait(false);

            // The domain refuses a car that is not for sale yet, before anything is written.
            var previous = vehicle.Sell(actor);

            var incoming = await RegisterTradeInAsync(request, vehicle, idTenant, actor, cancellationToken)
                .ConfigureAwait(false);

            var sale = Sale.Create(
                vehicle.Id, proposal?.Id, request.Date, request.Amount, request.PaymentMethod,
                request.Channel, request.PartnerStoreName, request.PartnerCutPercent,
                request.PartnerCutAmount, request.Commission, request.CommissionNotes,
                request.BuyerName, request.BuyerDocument, request.BuyerPhone,
                request.TradeInValue, request.Notes, actor);

            if (incoming is not null)
            {
                sale.AttachTradeInVehicle(incoming.Id);
            }

            unitOfWork.SaleRepository.Add(sale);
            unitOfWork.VehicleRepository.Update(vehicle);

            unitOfWork.VehicleStatusHistoryRepository.Add(VehicleStatusHistory.Create(
                vehicle.Id, previous, VehicleStatus.Sold, "Venda registrada", actor));

            if (proposal is not null)
            {
                proposal.Accept(actor);
                unitOfWork.ProposalRepository.Update(proposal);
            }

            // One buyer per car: whatever else was on the table is declined, and stays on
            // record as what the market offered.
            var others = await unitOfWork.ProposalRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            foreach (var other in others.Where(p => p.Status == ProposalStatus.Open && p.Id != proposal?.Id))
            {
                other.Decline(actor);
                unitOfWork.ProposalRepository.Update(other);
            }

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(Sale), sale.Code,
                AuditAction.Create, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            var cost = await SaleContext.CostOfAsync(unitOfWork, vehicle, cancellationToken)
                .ConfigureAwait(false);

            return SaleContext.ToDto(sale, vehicle, cost, proposal?.Code, incoming?.Code);
        }

        private async Task<Proposal?> AcceptedProposalAsync(
            Vehicle vehicle,
            Guid? proposalCode,
            CancellationToken cancellationToken)
        {
            if (proposalCode is null)
            {
                return null;
            }

            var proposal = await unitOfWork.ProposalRepository
                .GetByCodeAsync(proposalCode.Value, cancellationToken)
                .ConfigureAwait(false);

            if (proposal is null || proposal.IdVehicle != vehicle.Id)
            {
                throw new NotFoundException("Proposta inexistente.");
            }

            return proposal;
        }

        /// <summary>
        /// Puts the car that came in into stock, valued at what the deal said, and returns it
        /// with the Id the database assigned. Null when the buyer paid with money only.
        /// </summary>
        private async Task<Vehicle?> RegisterTradeInAsync(
            RegisterSaleCommand request,
            Vehicle sold,
            int idTenant,
            string actor,
            CancellationToken cancellationToken)
        {
            var hasTrade = request.PaymentMethod is PaymentMethod.TradeIn or PaymentMethod.TradeInWithCash;

            if (!hasTrade)
            {
                return null;
            }

            var input = request.TradeIn
                ?? throw new BusinessRuleException("Descreva o carro que entrou na troca.");

            var value = request.TradeInValue
                ?? throw new BusinessRuleException("Informe o valor do carro que entrou na troca.");

            var plate = VehicleIdentifiers.Normalize(input.Plate);
            var chassis = VehicleIdentifiers.Normalize(input.Chassis);

            var taken = await unitOfWork.VehicleRepository
                .IdentifierExistsAsync(idTenant, plate, chassis, null, cancellationToken)
                .ConfigureAwait(false);

            if (taken)
            {
                throw new BusinessRuleException(
                    $"A placa {plate} ou o chassi {chassis} do carro da troca já pertencem a outro veículo.");
            }

            var incoming = Vehicle.CreateFromTradeIn(
                idTenant, plate, chassis, input.Brand, input.Model, input.ModelYear,
                input.ManufactureYear, value, request.Date, request.BuyerName, actor);

            incoming.UpdateMileage(input.Mileage);

            unitOfWork.VehicleRepository.Add(incoming);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Read back so the Id assigned by the database is known before the sale points at it.
            incoming = await unitOfWork.VehicleRepository
                .GetByCodeAsync(idTenant, incoming.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new BusinessRuleException("Falha ao registrar o carro da troca.");

            unitOfWork.VehicleStatusHistoryRepository.Add(VehicleStatusHistory.Create(
                incoming.Id, null, incoming.Status,
                $"Entrou na troca pelo {sold.Plate} ({sold.Brand} {sold.Model})", actor));

            return incoming;
        }
    }

    /// <summary>Undoes a sale. See <see cref="CancelSaleCommand"/> for what stays.</summary>
    public class CancelSaleHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<CancelSaleCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(CancelSaleCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var actor = currentUser.Code.ToString();

            var vehicle = await SaleContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var sale = await unitOfWork.SaleRepository
                .GetByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Este veículo está sem venda registrada.");

            vehicle.CancelSale(actor);

            unitOfWork.SaleRepository.Remove(sale, actor);
            unitOfWork.VehicleRepository.Update(vehicle);

            unitOfWork.VehicleStatusHistoryRepository.Add(VehicleStatusHistory.Create(
                vehicle.Id, VehicleStatus.Sold, VehicleStatus.ReadyForSale,
                string.IsNullOrWhiteSpace(request.Reason) ? "Venda cancelada" : $"Venda cancelada: {request.Reason.Trim()}",
                actor));

            if (sale.IdProposal is not null)
            {
                var proposal = await unitOfWork.ProposalRepository
                    .GetByIdAsync(sale.IdProposal.Value, cancellationToken)
                    .ConfigureAwait(false);

                if (proposal is not null)
                {
                    proposal.Reopen(actor);
                    unitOfWork.ProposalRepository.Update(proposal);
                }
            }

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(Sale), sale.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
