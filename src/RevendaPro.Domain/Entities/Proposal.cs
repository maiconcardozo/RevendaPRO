using System.Diagnostics;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// What somebody offered for the car (RF-18).
    ///
    /// A proposal exists to answer one question at the moment it arrives: how much is left if
    /// I say yes. The answer is never stored here — see <see cref="DealResult"/> — because the
    /// cost of the car keeps moving until the car leaves.
    /// </summary>
    [DebuggerDisplay("IdVehicle={IdVehicle}, Amount={Amount}, Status={Status}")]
    public class Proposal : VehicleEntity
    {
        private Proposal() { }

        public string ProspectName { get; private set; } = string.Empty;

        /// <summary>Digits only. Optional: a walk-in often leaves no number.</summary>
        public string? ProspectPhone { get; private set; }

        public decimal Amount { get; private set; }

        public DateOnly Date { get; private set; }

        /// <summary>
        /// How they would pay. The business accepts less for cash, so the method is part of
        /// what is being offered and never an afterthought.
        /// </summary>
        public PaymentMethod PaymentMethod { get; private set; }

        public SaleChannel Channel { get; private set; }

        /// <summary>The store's cut as a percentage, when that is how it was agreed.</summary>
        public decimal? PartnerCutPercent { get; private set; }

        /// <summary>The store's cut as an amount, when that is how it was agreed.</summary>
        public decimal? PartnerCutAmount { get; private set; }

        public ProposalStatus Status { get; private set; } = ProposalStatus.Open;

        public string? Notes { get; private set; }

        /// <summary>Records what was offered.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="prospectName">Who offered.</param>
        /// <param name="prospectPhone">Their phone, digits only, optional.</param>
        /// <param name="amount">What they offered.</param>
        /// <param name="date">When.</param>
        /// <param name="paymentMethod">How they would pay.</param>
        /// <param name="channel">Direct, or through a partner store.</param>
        /// <param name="partnerCutPercent">The store's percentage, when agreed that way.</param>
        /// <param name="partnerCutAmount">The store's amount, when agreed that way.</param>
        /// <param name="notes">Anything else.</param>
        /// <param name="createdBy">Who recorded it.</param>
        /// <returns>An open proposal.</returns>
        public static Proposal Create(
            int idVehicle,
            string prospectName,
            string? prospectPhone,
            decimal amount,
            DateOnly date,
            PaymentMethod paymentMethod,
            SaleChannel channel,
            decimal? partnerCutPercent,
            decimal? partnerCutAmount,
            string? notes,
            string createdBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(prospectName))
            {
                throw new BusinessRuleException("Informe quem fez a proposta.");
            }

            if (amount <= 0)
            {
                throw new BusinessRuleException("Informe um valor maior que zero.");
            }

            var proposal = new Proposal
            {
                IdVehicle = idVehicle,
                ProspectName = prospectName.Trim(),
                ProspectPhone = Digits(prospectPhone),
                Amount = amount,
                Date = date,
                PaymentMethod = paymentMethod,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            };

            proposal.SetChannel(channel, partnerCutPercent, partnerCutAmount);
            proposal.SetCreatedBy(createdBy);

            return proposal;
        }

        /// <summary>What the store keeps, in money, whichever way it was agreed.</summary>
        public decimal PartnerCut =>
            Channel == SaleChannel.PartnerStore
                ? DealResult.PartnerCutOf(Amount, PartnerCutPercent, PartnerCutAmount)
                : 0;

        /// <summary>How much is left if this one is accepted (RF-19).</summary>
        /// <param name="cost">What the vehicle cost so far.</param>
        /// <returns>The result. Nothing of it is stored.</returns>
        public DealResult ResultAgainst(VehicleCost cost)
        {
            ArgumentNullException.ThrowIfNull(cost);

            return new DealResult(Amount, PartnerCut, Commission: 0, cost.Total);
        }

        /// <summary>Accepts the proposal. Only an open one can be accepted.</summary>
        /// <param name="updatedBy">Who accepted it.</param>
        public void Accept(string updatedBy = SystemActor)
        {
            if (Status != ProposalStatus.Open)
            {
                throw new BusinessRuleException("Só uma proposta em aberto pode ser aceita.");
            }

            Status = ProposalStatus.Accepted;
            UpdateAuditInfo(updatedBy);
        }

        /// <summary>Declines the proposal. Declining twice changes nothing.</summary>
        /// <param name="updatedBy">Who declined it.</param>
        public void Decline(string updatedBy = SystemActor)
        {
            if (Status == ProposalStatus.Accepted)
            {
                throw new BusinessRuleException(
                    "Esta proposta já foi aceita. Cancele a venda para reabri-la.");
            }

            if (Status == ProposalStatus.Declined)
            {
                return;
            }

            Status = ProposalStatus.Declined;
            UpdateAuditInfo(updatedBy);
        }

        /// <summary>Puts an accepted proposal back on the table, when the sale is undone.</summary>
        /// <param name="updatedBy">Who reopened it.</param>
        public void Reopen(string updatedBy = SystemActor)
        {
            Status = ProposalStatus.Open;
            UpdateAuditInfo(updatedBy);
        }

        private void SetChannel(SaleChannel channel, decimal? percent, decimal? amount)
        {
            Channel = channel;

            if (channel == SaleChannel.Direct)
            {
                PartnerCutPercent = null;
                PartnerCutAmount = null;
                return;
            }

            // Validates the pair, and refuses both at once.
            DealResult.PartnerCutOf(Amount, percent, amount);

            PartnerCutPercent = percent;
            PartnerCutAmount = amount;
        }

        private static string? Digits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var digits = new string([.. value.Where(char.IsDigit)]);

            return digits.Length == 0 ? null : digits;
        }
    }
}
