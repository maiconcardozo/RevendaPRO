namespace RevendaPro.Domain.Enums
{
    /// <summary>
    /// Where the vehicle is in the operation, in the sequence the business described (RF-06).
    ///
    /// The order of the values follows the usual path, which lets a listing sort by it and
    /// read as a pipeline. Going back is allowed where the business goes back — a car returns
    /// to the workshop when something turns up after it was ready.
    /// </summary>
    public enum VehicleStatus
    {
        /// <summary>Being evaluated, and still outside the stock.</summary>
        UnderReview = 1,

        /// <summary>Bought, and the cost starts here.</summary>
        Purchased = 2,

        /// <summary>In the workshop.</summary>
        InRepair = 3,

        /// <summary>Ready, and still without an advertisement.</summary>
        ReadyForSale = 4,

        /// <summary>Advertised.</summary>
        Advertised = 5,

        /// <summary>Somebody is negotiating.</summary>
        Negotiating = 6,

        /// <summary>Sold.</summary>
        Sold = 7
    }

    /// <summary>Where the vehicle came from (RF-04).</summary>
    public enum VehicleOrigin
    {
        /// <summary>The dominant one in this operation.</summary>
        Auction = 1,

        Individual = 2,

        Store = 3,

        /// <summary>Taken in a trade, which is what a sale can produce.</summary>
        TradeIn = 4,

        Other = 5
    }

    public enum FuelType
    {
        Flex = 1,
        Gasoline = 2,
        Ethanol = 3,
        Diesel = 4,
        Hybrid = 5,
        Electric = 6,
        Gas = 7
    }

    public enum TransmissionType
    {
        Manual = 1,
        Automatic = 2,
        AutomatedManual = 3,
        Cvt = 4
    }

    /// <summary>
    /// How money moved. The business said the accepted price changes with it, and that a deal
    /// can be closed with a car — which is what turns a sale into a new vehicle in stock.
    /// </summary>
    public enum PaymentMethod
    {
        Cash = 1,
        BankTransfer = 2,
        Financing = 3,
        Card = 4,

        /// <summary>A vehicle, and nothing else.</summary>
        TradeIn = 5,

        /// <summary>A vehicle plus money.</summary>
        TradeInWithCash = 6,

        Other = 7
    }

    /// <summary>
    /// What a photo is for (RF-12).
    ///
    /// <see cref="Damage"/> earns its own value because it has a job: it is sent to the buyer
    /// to explain the history of a car that came from an auction.
    /// </summary>
    public enum VehiclePhotoKind
    {
        Damage = 1,
        Repair = 2,
        Finished = 3,
        Other = 4
    }

    /// <summary>Kinds of document (RF-13), named after what the real archive holds.</summary>
    public enum VehicleDocumentKind
    {
        SaleInvoice = 1,
        PaymentReceipt = 2,

        /// <summary>Gate pass and the rest of what an auction sends.</summary>
        AuctionDocument = 3,

        Term = 4,
        Inspection = 5,
        CustomsBrokerDocument = 6,
        ProofOfAddress = 7,

        /// <summary>Personal identification. The reason documents stay in the private bucket.</summary>
        PersonalDocument = 8,

        Other = 9
    }

    /// <summary>
    /// Who the car is sold through (RF-22).
    ///
    /// A partner store adds its own cut on top of what the seller wants to receive, which is
    /// how the business described it: "eu quero 58 para mim, a loja põe dela em cima".
    /// </summary>
    public enum SaleChannel
    {
        Direct = 1,
        PartnerStore = 2
    }

    /// <summary>
    /// Where the reference value of a vehicle came from.
    ///
    /// Exists because the two are read differently by whoever prices the car: a value the
    /// table answered is worth trusting as the market, and a value somebody typed carries the
    /// judgement of a person who knows a rare, imported or off-table car. Without this, the
    /// automatic routine would silently overwrite the second kind. See ADR-0005.
    /// </summary>
    public enum FipeSource
    {
        /// <summary>Typed by a person.</summary>
        Manual = 1,

        /// <summary>Read from the reference table.</summary>
        Automatic = 2
    }

    /// <summary>Where a proposal stands (RF-18).</summary>
    public enum ProposalStatus
    {
        Open = 1,
        Accepted = 2,
        Declined = 3
    }

    /// <summary>
    /// What kind of thing happened to a vehicle, in the single history the file shows (RF-26).
    ///
    /// The order of the values is the order a car usually lives them, so a reader scanning
    /// the enum sees the operation itself. It carries no meaning for sorting: the timeline is
    /// ordered by when things happened, never by kind.
    /// </summary>
    public enum TimelineEventKind
    {
        /// <summary>The purchase, which is where the cost of a car starts.</summary>
        Purchase = 1,

        /// <summary>One move along the pipeline.</summary>
        StatusChange = 2,

        /// <summary>One expense, paid or still planned.</summary>
        Expense = 3,

        /// <summary>Photos sent by one person on one day, counted together.</summary>
        Photos = 4,

        /// <summary>Documents attached by one person on one day, counted together.</summary>
        Documents = 5,

        /// <summary>One offer somebody made.</summary>
        Proposal = 6,

        /// <summary>The sale, which is where the story of a car ends.</summary>
        Sale = 7
    }
}
