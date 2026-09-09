using MediatR;
using RevendaPro.Application.Cashflow.Commands;
using RevendaPro.Application.Cashflow.DTOs;
using RevendaPro.Application.Cashflow.Queries;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Application.Cashflow.Handlers
{
    /// <summary>
    /// O caixa de um período: os totais, o que falta pagar, o que falta receber (M22).
    ///
    /// Três idas ao banco: as sete somas numa consulta, a lista do que se deve, e a lista do que
    /// se tem a receber. Lista nenhuma é carregada para somar aqui.
    /// </summary>
    public class GetCashflowHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<GetCashflowQuery, CashflowDto>
    {
        /// <inheritdoc/>
        public async Task<CashflowDto> Handle(GetCashflowQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var today = BrazilTime.Today;

            var summary = await unitOfWork.CashflowRepository
                .ReadSummaryAsync(idTenant, request.From, request.To, today, cancellationToken)
                .ConfigureAwait(false);

            // O que se deve e o que se tem a receber vêm sem período: "quanto eu devo" é a
            // pergunta de hoje, e uma janela de datas a transformaria em outra pergunta.
            var payables = await unitOfWork.CashflowRepository
                .ListPayablesAsync(idTenant, settled: false, from: null, to: null, cancellationToken)
                .ConfigureAwait(false);

            var receivables = await unitOfWork.CashflowRepository
                .ListReceivablesAsync(idTenant, cancellationToken)
                .ConfigureAwait(false);

            return new CashflowDto(
                request.From,
                request.To,
                summary.PayableOpen,
                summary.PayableOverdue,
                summary.PayableDueSoon,
                summary.ReceivableOpen,
                summary.ReceivableOverdue,
                summary.PaidInPeriod,
                summary.ReceivedInPeriod,
                [.. payables.Select(line => ToDto(line, today))],
                [.. receivables.Select(line => ToDto(line, today))]);
        }

        /// <summary>Uma linha do extrato como a tela lê, com o atraso já decidido.</summary>
        /// <param name="line">A linha.</param>
        /// <param name="today">O dia de hoje.</param>
        /// <returns>O DTO.</returns>
        public static CashflowLineDto ToDto(CashflowLine line, DateOnly today)
        {
            ArgumentNullException.ThrowIfNull(line);

            return new CashflowLineDto(
                line.Code,
                line.Kind,
                line.Description,
                line.Party,
                line.Category,
                line.Amount,
                line.DueDate,
                line.SettledDate,
                line.IsSettled,
                !line.IsSettled && line.DueDate is { } due && due < today,
                line.VehicleCode,
                line.Plate);
        }
    }

    /// <summary>
    /// Dá baixa numa conta a pagar, venha ela do carro ou da loja (M22).
    ///
    /// A entidade de cada lado continua sendo quem muda o próprio estado: aqui só se decide em
    /// qual porta bater. É o que permite a tela do caixa ter uma lista só.
    /// </summary>
    public class SettlePayableHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<SettlePayableCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(SettlePayableCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var actor = currentUser.Code.ToString();
            var paidOn = request.PaidDate ?? BrazilTime.Today;

            switch (request.Kind)
            {
                case CashflowKind.VehicleExpense:
                    await SettleVehicleExpenseAsync(request, idTenant, actor, paidOn, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case CashflowKind.StoreExpense:
                    await SettleStoreExpenseAsync(request, idTenant, actor, paidOn, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                default:
                    // O que se recebe entra por outra porta: uma venda recebe em parcelas, e a
                    // entrada tem valor, data e forma próprios. Ver AddSaleReceiptCommand.
                    throw new BusinessRuleException(
                        "O que se recebe de uma venda entra pela ficha do carro, como entrada de dinheiro.");
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task SettleVehicleExpenseAsync(
            SettlePayableCommand request,
            int idTenant,
            string actor,
            DateOnly paidOn,
            CancellationToken cancellationToken)
        {
            var expense = await unitOfWork.VehicleExpenseRepository
                .GetByCodeAsync(request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Gasto inexistente.");

            // O gasto chega à revenda pelo carro, e por isso o dono é conferido, e jamais suposto.
            var vehicle = await unitOfWork.VehicleRepository
                .GetByIdAsync(expense.IdVehicle, cancellationToken)
                .ConfigureAwait(false);

            if (vehicle is null || vehicle.IdTenant != idTenant)
            {
                throw new NotFoundException("Gasto inexistente.");
            }

            if (request.IsPaid)
            {
                expense.MarkAsPaid(paidOn, actor);
            }
            else
            {
                expense.MarkAsPlanned(actor);
            }

            unitOfWork.VehicleExpenseRepository.Update(expense);
        }

        private async Task SettleStoreExpenseAsync(
            SettlePayableCommand request,
            int idTenant,
            string actor,
            DateOnly paidOn,
            CancellationToken cancellationToken)
        {
            var expense = await unitOfWork.StoreExpenseRepository
                .GetByCodeAsync(idTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Despesa inexistente.");

            if (request.IsPaid)
            {
                expense.MarkAsPaid(paidOn, actor);
            }
            else
            {
                expense.MarkAsPlanned(actor);
            }

            unitOfWork.StoreExpenseRepository.Update(expense);
        }
    }
}
