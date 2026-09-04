using MediatR;
using RevendaPro.Application.Fipe;
using RevendaPro.Application.Market.DTOs;
using RevendaPro.Application.Market.Queries;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Application.Market.Handlers
{
    /// <summary>
    /// The dealership against the reference table (RF-14).
    ///
    /// It answers what the stakeholder asked for in one sentence: <i>"vendi acima ou abaixo da
    /// tabela"</i>, and <i>"quanto o pátio perdeu de referência este mês"</i>. Every amount
    /// meets the quote of <b>its own month</b>, which is the whole reason the quote table
    /// exists. See ADR-0005.
    ///
    /// <b>Which table is now costs nothing.</b> The month used for what is being asked comes
    /// from the calendar, and never from the source: a screen that reached the network to draw
    /// itself would fail when the mirror failed, and this screen reads history that is already
    /// in the database.
    /// </summary>
    public class GetMarketOverviewHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<GetMarketOverviewQuery, MarketOverviewDto>
    {
        /// <inheritdoc/>
        public async Task<MarketOverviewDto> Handle(
            GetMarketOverviewQuery request,
            CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var month = new DateOnly(today.Year, today.Month, 1);

            var positions = await unitOfWork.VehicleRepository
                .ListMarketPositionsAsync(currentUser.IdTenant, month, today, cancellationToken)
                .ConfigureAwait(false);

            var proposals = await unitOfWork.VehicleRepository
                .ListMarketProposalsAsync(currentUser.IdTenant, month, cancellationToken)
                .ConfigureAwait(false);

            var yard = positions.Where(position => position.OnTheLot).ToList();
            var sold = positions.Where(position => !position.OnTheLot).ToList();

            return new MarketOverviewDto(
                month,

                // A compra é medida no pátio e nos vendidos: a vantagem do leilão vale por
                // todo carro que a revenda comprou, e não só pelos que ainda estão parados.
                Average(positions.Select(position => position.Purchase)),
                Average(sold.Select(position => position.Sale)),
                Average(yard.Select(position => position.Asking)),

                Sum(yard.Select(position => position.LostThisMonth)),
                Sum(yard.Select(position => position.LostSincePurchase)),

                [.. yard.Select(position => Line(position, position.Asking))],
                [.. sold.Select(position => Line(position, position.Sale))],
                [.. proposals.Select(Line)],

                // Um carro sem código de modelo, ou cujo mês jamais foi buscado, fica de fora
                // de toda média acima. Dizer quantos são é o que impede a tela de apresentar
                // meia revenda como se fosse a revenda inteira.
                positions.Count(position => position.CurrentReference is null));
        }

        /// <summary>
        /// Adds up the amounts and the tables of the cars that have a comparison, and divides
        /// once at the end.
        ///
        /// Averaging the percentages instead would give a cheap car the same weight as an
        /// expensive one, and the question is about money.
        /// </summary>
        /// <param name="comparisons">Every comparison of that kind, including the ones with no table.</param>
        /// <returns>The average.</returns>
        private static MarketAverageDto Average(IEnumerable<MarketComparison?> comparisons)
        {
            var withTable = comparisons
                .Where(comparison => comparison is not null && comparison.HasReference)
                .Select(comparison => comparison!)
                .ToList();

            var amount = withTable.Sum(comparison => comparison.Amount);
            var reference = withTable.Sum(comparison => comparison.Reference!.Value);

            return new MarketAverageDto(
                withTable.Count,
                amount,
                reference,
                amount - reference,
                reference > 0
                    ? Math.Round((amount - reference) / reference * 100, 2, MidpointRounding.AwayFromZero)
                    : null);
        }

        private static decimal Sum(IEnumerable<decimal?> values) =>
            values.Where(value => value is not null).Sum(value => value!.Value);

        private static MarketLineDto Line(MarketPosition position, MarketComparison? about)
        {
            var purchase = position.Purchase;

            return new MarketLineDto(
                position.Code,
                position.Plate,
                position.Brand,
                position.Model,
                position.Version,
                position.ModelYear,
                position.Status,
                position.DaysInStock,
                about?.Amount ?? 0m,
                about?.Reference,
                about?.Difference,
                about?.Percent,
                purchase?.Difference,
                purchase?.Percent,
                position.LostSincePurchase);
        }

        private static MarketProposalDto Line(MarketProposal proposal) =>
            new(proposal.VehicleCode,
                proposal.Plate,
                proposal.Brand,
                proposal.Model,
                proposal.ProspectName,
                proposal.Amount,
                proposal.Date,
                proposal.Offer.Reference,
                proposal.Offer.Difference,
                proposal.Offer.Percent);
    }
}
