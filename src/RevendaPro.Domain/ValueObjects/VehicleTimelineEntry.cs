using RevendaPro.Domain.Enums;

namespace RevendaPro.Domain.ValueObjects
{
    /// <summary>
    /// One thing that happened to a vehicle, in the single history the file shows (RF-26).
    ///
    /// <b>Nothing here is stored.</b> The timeline is a reading over the tables that already
    /// hold the operation — the purchase on the vehicle, the expenses, the attachments, the
    /// proposals, the moves along the pipeline and the sale. There is no timeline table, and
    /// there is no event written twice: a second copy of what happened would start drifting
    /// from the first the moment somebody corrected an expense.
    ///
    /// The audit log is deliberately <b>not</b> a source. It exists for forensics and keeps
    /// values as JSON; a file needs meaning — "funilaria, R$ 350" — and never
    /// <c>{"Amount":350.00}</c>.
    ///
    /// Fields are nullable because the kinds differ: an expense has an amount and no status,
    /// a move along the pipeline has statuses and no amount. Each kind states below what it
    /// fills, and the screen reads only what its kind carries.
    /// </summary>
    /// <param name="Moment">When it happened. The only thing the whole timeline is sorted by.</param>
    /// <param name="Kind">What kind of thing happened.</param>
    /// <param name="Code">
    /// Public identifier of the record, so the screen can link to it. Null when the entry
    /// counts several records at once — photos and documents of the same day.
    /// </param>
    /// <param name="Title">
    /// What the data itself says: the description of the expense, who made the proposal, who
    /// bought, the name of the single attached file. Null when several records were counted.
    /// </param>
    /// <param name="Detail">The note somebody wrote, or the reason for a move.</param>
    /// <param name="Amount">Money, when the event has money: purchase, expense, offer, sale.</param>
    /// <param name="Quantity">How many records this entry stands for. One, except for attachments.</param>
    /// <param name="FromStatus">Where the vehicle came from. Only on a move.</param>
    /// <param name="ToStatus">Where the vehicle went. Only on a move.</param>
    /// <param name="ProposalStatus">Whether the offer was accepted, refused or is still open.</param>
    /// <param name="IsPaid">Whether the expense was already paid, or is still planned.</param>
    /// <param name="ActorCode">
    /// Who did it, as the tables store it: the public code of the user. Turning it into a
    /// name is the job of whoever reads this, because a name lives on the user and the tables
    /// of the operation keep only the code.
    /// </param>
    public sealed record VehicleTimelineEntry(
        DateTime Moment,
        TimelineEventKind Kind,
        Guid? Code,
        string? Title,
        string? Detail,
        decimal? Amount,
        int Quantity,
        VehicleStatus? FromStatus,
        VehicleStatus? ToStatus,
        ProposalStatus? ProposalStatus,
        bool? IsPaid,
        string? ActorCode);
}
