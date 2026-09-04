using System.Diagnostics;
using RevendaPro.Domain.Enums;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// A vehicle, from the moment it is evaluated until it is sold.
    ///
    /// No total is stored here. The cost is the purchase plus the expenses, computed on every
    /// read by <see cref="ValueObjects.VehicleCost"/> — see that type for why.
    /// </summary>
    [DebuggerDisplay("Plate={Plate}, Status={Status}")]
    public class Vehicle : TenantEntity
    {
        /// <summary>
        /// Which status can follow which. Going back is allowed where the business goes back:
        /// a car returns to the workshop when something turns up after it was ready, and a
        /// negotiation that collapses puts it back on the market.
        /// </summary>
        private static readonly Dictionary<VehicleStatus, VehicleStatus[]> Allowed = new()
        {
            [VehicleStatus.UnderReview] = [VehicleStatus.Purchased],
            [VehicleStatus.Purchased] = [VehicleStatus.InRepair, VehicleStatus.ReadyForSale],
            [VehicleStatus.InRepair] = [VehicleStatus.ReadyForSale],
            [VehicleStatus.ReadyForSale] =
                [VehicleStatus.Advertised, VehicleStatus.Negotiating, VehicleStatus.InRepair],
            [VehicleStatus.Advertised] =
                [VehicleStatus.Negotiating, VehicleStatus.ReadyForSale, VehicleStatus.InRepair],
            [VehicleStatus.Negotiating] =
                [VehicleStatus.Advertised, VehicleStatus.ReadyForSale],

            // Sold is reached through the sale, and through nothing else: see Sell. A status
            // change that said "sold" without a sale behind it would leave a car with no
            // buyer, no price and no profit — the exact hole the sale record exists to close.
            // Undoing a sale is undoing that record, which puts the car back on the lot.
            [VehicleStatus.Sold] = []
        };

        /// <summary>Where a buyer can take the car from. The rest of the pipeline is not for sale yet.</summary>
        private static readonly VehicleStatus[] Sellable =
            [VehicleStatus.ReadyForSale, VehicleStatus.Advertised, VehicleStatus.Negotiating];

        private Vehicle() { }

        private Vehicle(int idTenant) : base(idTenant) { }

        public string Plate { get; private set; } = string.Empty;

        public string Chassis { get; private set; } = string.Empty;

        public string Brand { get; private set; } = string.Empty;

        public string Model { get; private set; } = string.Empty;

        public string? Version { get; private set; }

        public short ModelYear { get; private set; }

        public short ManufactureYear { get; private set; }

        public string? Color { get; private set; }

        /// <summary>Kilometres. It only ever goes up — see <see cref="UpdateMileage"/>.</summary>
        public int Mileage { get; private set; }

        public FuelType FuelType { get; private set; }

        public TransmissionType Transmission { get; private set; }

        public string? Renavam { get; private set; }

        public VehicleOrigin Origin { get; private set; }

        /// <summary>Whether the vehicle was in a crash. Central to this operation.</summary>
        public bool HasDamage { get; private set; }

        public string? DamageDescription { get; private set; }

        public VehicleStatus Status { get; private set; } = VehicleStatus.UnderReview;

        public decimal PurchasePrice { get; private set; }

        public DateOnly? PurchaseDate { get; private set; }

        /// <summary>Supplier or auction house.</summary>
        public string? SupplierName { get; private set; }

        public PaymentMethod? PurchasePaymentMethod { get; private set; }

        /// <summary>
        /// The most that this vehicle is meant to cost, purchase included.
        ///
        /// This is the number the business consults all day while buying parts. What matters
        /// on screen is how much room is left, and not the percentage in itself.
        /// </summary>
        public decimal? BudgetCeiling { get; private set; }

        public decimal? FipeValue { get; private set; }

        /// <summary>The table changes monthly, so the value means nothing without the date.</summary>
        public DateOnly? FipeReferenceDate { get; private set; }

        /// <summary>
        /// Code of the exact model in the FIPE table. Empty while the value is typed by hand.
        ///
        /// It exists now so that the automatic lookup costs little later: without it, matching
        /// a car would mean comparing "Cruze", "Hatch" and "2014" against a catalogue, which
        /// fails precisely on the models with several versions.
        /// </summary>
        public string? FipeCode { get; private set; }

        /// <summary>
        /// Year and fuel of the exact priced row in the table (<c>2014-5</c>), which belongs
        /// to <see cref="FipeCode"/> and is meaningless without it.
        ///
        /// The year alone would be ambiguous: the same model and year exist as flex and as
        /// petrol, at different prices. Nobody types this — it is written by the lookup, and
        /// it is what turns the next lookup into a direct call.
        /// </summary>
        public string? FipeYearFuel { get; private set; }

        /// <summary>What the seller wants to take home, which is the price they think in.</summary>
        public decimal? DesiredNetPrice { get; private set; }

        public decimal? MinimumNetPrice { get; private set; }

        /// <summary>Advertised price, with a partner's cut already on top.</summary>
        public decimal? AdvertisedPrice { get; private set; }

        /// <summary>What comparable cars are asking nearby.</summary>
        public string? MarketNotes { get; private set; }

        public string? Notes { get; private set; }

        public int? IdCoverPhoto { get; private set; }

        /// <summary>Registers a vehicle. Plate and chassis are validated before anything else.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="plate">Plate, in either Brazilian format.</param>
        /// <param name="chassis">Chassis (VIN).</param>
        /// <param name="brand">Brand.</param>
        /// <param name="model">Model.</param>
        /// <param name="modelYear">Model year.</param>
        /// <param name="manufactureYear">Manufacture year.</param>
        /// <param name="createdBy">Who is registering.</param>
        /// <returns>The vehicle, under review.</returns>
        public static Vehicle Create(
            int idTenant,
            string plate,
            string chassis,
            string brand,
            string model,
            short modelYear,
            short manufactureYear,
            string createdBy = SystemActor)
        {
            var vehicle = new Vehicle(idTenant);

            vehicle.SetIdentification(plate, chassis, brand, model, modelYear, manufactureYear);
            vehicle.SetCreatedBy(createdBy);

            return vehicle;
        }

        /// <summary>Changes what identifies the vehicle.</summary>
        /// <param name="plate">Plate.</param>
        /// <param name="chassis">Chassis.</param>
        /// <param name="brand">Brand.</param>
        /// <param name="model">Model.</param>
        /// <param name="modelYear">Model year.</param>
        /// <param name="manufactureYear">Manufacture year.</param>
        public void SetIdentification(
            string plate,
            string chassis,
            string brand,
            string model,
            short modelYear,
            short manufactureYear)
        {
            if (!VehicleIdentifiers.IsValidPlate(plate))
            {
                throw new BusinessRuleException("Informe uma placa válida.");
            }

            if (!VehicleIdentifiers.IsValidChassis(chassis))
            {
                throw new BusinessRuleException("Informe um chassi com 17 caracteres válidos.");
            }

            if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(model))
            {
                throw new BusinessRuleException("Informe a marca e o modelo.");
            }

            // A car is built in one year and sold as the next year's model, which is why the
            // model year runs ahead. The other way round exists nowhere.
            if (modelYear < manufactureYear)
            {
                throw new BusinessRuleException(
                    "O ano do modelo é igual ou posterior ao ano de fabricação.");
            }

            Plate = VehicleIdentifiers.Normalize(plate);
            Chassis = VehicleIdentifiers.Normalize(chassis);
            Brand = brand.Trim();
            Model = model.Trim();
            ModelYear = modelYear;
            ManufactureYear = manufactureYear;
        }

        /// <summary>Fills in what describes the vehicle.</summary>
        /// <param name="version">Version or trim.</param>
        /// <param name="color">Colour.</param>
        /// <param name="fuelType">Fuel.</param>
        /// <param name="transmission">Transmission.</param>
        /// <param name="renavam">Renavam.</param>
        /// <param name="notes">Free notes.</param>
        public void SetDetails(
            string? version,
            string? color,
            FuelType fuelType,
            TransmissionType transmission,
            string? renavam,
            string? notes)
        {
            Version = Trim(version);
            Color = Trim(color);
            FuelType = fuelType;
            Transmission = transmission;
            Renavam = Trim(renavam);
            Notes = Trim(notes);
        }

        /// <summary>Records where the vehicle came from and what shape it was in (RF-04, RF-05).</summary>
        /// <param name="origin">Origin.</param>
        /// <param name="hasDamage">Whether it was in a crash.</param>
        /// <param name="damageDescription">What happened.</param>
        public void SetOrigin(VehicleOrigin origin, bool hasDamage, string? damageDescription)
        {
            if (hasDamage && string.IsNullOrWhiteSpace(damageDescription))
            {
                // The description is what the buyer is shown alongside the damage photos, and
                // it is what turns "it came from an auction" into "this is what it had".
                throw new BusinessRuleException("Descreva o sinistro do veículo.");
            }

            Origin = origin;
            HasDamage = hasDamage;
            DamageDescription = hasDamage ? Trim(damageDescription) : null;
        }

        /// <summary>Records the purchase (RF-07).</summary>
        /// <param name="price">What was paid.</param>
        /// <param name="date">When.</param>
        /// <param name="supplierName">Supplier or auction house.</param>
        /// <param name="paymentMethod">How it was paid.</param>
        public void SetPurchase(
            decimal price,
            DateOnly? date,
            string? supplierName,
            PaymentMethod? paymentMethod)
        {
            if (price < 0)
            {
                throw new BusinessRuleException("Informe um valor de compra válido.");
            }

            PurchasePrice = price;
            PurchaseDate = date;
            SupplierName = Trim(supplierName);
            PurchasePaymentMethod = paymentMethod;
        }

        /// <summary>Sets the ceiling for what this vehicle may cost in total.</summary>
        /// <param name="ceiling">The ceiling, or null to remove it.</param>
        public void SetBudgetCeiling(decimal? ceiling)
        {
            if (ceiling is <= 0)
            {
                throw new BusinessRuleException("Informe um teto de orçamento maior que zero.");
            }

            BudgetCeiling = ceiling;
        }

        /// <summary>Records the FIPE reference (RF-14).</summary>
        /// <param name="value">Value from the table.</param>
        /// <param name="referenceDate">Which month it came from.</param>
        /// <param name="code">Code of the exact model, when known.</param>
        public void SetFipe(decimal? value, DateOnly? referenceDate, string? code)
        {
            if (value is <= 0)
            {
                throw new BusinessRuleException("Informe um valor de FIPE maior que zero.");
            }

            if (value is not null && referenceDate is null)
            {
                throw new BusinessRuleException("Informe o mês de referência da FIPE.");
            }

            var model = Trim(code);

            if (!string.Equals(model, FipeCode, StringComparison.OrdinalIgnoreCase))
            {
                // The pair belongs to the code. Keeping it across a change of model would
                // leave the next lookup asking the table for a row of the previous car.
                FipeYearFuel = null;
            }

            FipeValue = value;
            FipeReferenceDate = referenceDate;
            FipeCode = model;
        }

        /// <summary>
        /// Points the vehicle at the exact row of the reference table.
        ///
        /// Written by the lookup, and never by hand: the two together are what the table
        /// prices, and what makes every later reading a direct call instead of a search
        /// through brand, model and year.
        /// </summary>
        /// <param name="code">Code of the model in the table.</param>
        /// <param name="yearFuel">Year and fuel of the priced row.</param>
        public void SetFipeModel(string code, string yearFuel)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(yearFuel))
            {
                throw new BusinessRuleException(
                    "Informe o código da FIPE e o ano-combustível do modelo.");
            }

            FipeCode = code.Trim();
            FipeYearFuel = yearFuel.Trim();
        }

        /// <summary>Sets the prices (RF-16).</summary>
        /// <param name="desiredNet">What the seller wants to take home.</param>
        /// <param name="minimumNet">The least they accept.</param>
        /// <param name="advertised">The advertised price.</param>
        /// <param name="marketNotes">What comparable cars are asking.</param>
        public void SetPricing(
            decimal? desiredNet,
            decimal? minimumNet,
            decimal? advertised,
            string? marketNotes)
        {
            if (desiredNet is not null && minimumNet is not null && minimumNet > desiredNet)
            {
                throw new BusinessRuleException(
                    "O preço mínimo aceito é igual ou menor que o preço desejado.");
            }

            DesiredNetPrice = desiredNet;
            MinimumNetPrice = minimumNet;
            AdvertisedPrice = advertised;
            MarketNotes = Trim(marketNotes);
        }

        /// <summary>
        /// Updates the mileage, which only ever goes up.
        ///
        /// A reading that falls is the classic sign of a tampered odometer, and the business
        /// photographs the dashboard precisely because the number is evidence. A genuine
        /// correction is possible, and it is deliberate rather than accidental.
        /// </summary>
        /// <param name="mileage">The new reading.</param>
        /// <param name="correction">True to accept a lower reading, as an explicit correction.</param>
        public void UpdateMileage(int mileage, bool correction = false)
        {
            if (mileage < 0)
            {
                throw new BusinessRuleException("Informe uma quilometragem válida.");
            }

            if (mileage < Mileage && !correction)
            {
                throw new BusinessRuleException(
                    $"A quilometragem informada é menor que a atual ({Mileage} km). " +
                    "Marque como correção para confirmar.");
            }

            Mileage = mileage;
        }

        /// <summary>Whether the vehicle can move to a status.</summary>
        /// <param name="target">The status to move to.</param>
        /// <returns>True when the move is allowed.</returns>
        public bool CanChangeTo(VehicleStatus target) =>
            Allowed.TryGetValue(Status, out var next) && next.Contains(target);

        /// <summary>Moves the vehicle along the pipeline (RF-06).</summary>
        /// <param name="target">The status to move to.</param>
        /// <param name="updatedBy">Who is moving it.</param>
        /// <returns>The status it came from, for the history.</returns>
        public VehicleStatus ChangeStatus(VehicleStatus target, string updatedBy = SystemActor)
        {
            if (!CanChangeTo(target))
            {
                throw new BusinessRuleException(
                    $"Um veículo em \"{Describe(Status)}\" segue para {Next(Status)}.");
            }

            var previous = Status;

            Status = target;
            UpdateAuditInfo(updatedBy);

            return previous;
        }

        /// <summary>Whether a buyer can take the car right now.</summary>
        public bool CanBeSold => Sellable.Contains(Status);

        /// <summary>
        /// Marks the vehicle as sold. The only way to reach <see cref="VehicleStatus.Sold"/>.
        ///
        /// Allowed from ready, advertised or negotiating. The M6 pipeline only arrived at sold
        /// through negotiating, but a buyer walks into the lot and takes a ready car all the
        /// time; demanding a pass through "negotiating" first would be a click that lies.
        /// </summary>
        /// <param name="updatedBy">Who sold it.</param>
        /// <returns>The status it came from, for the history.</returns>
        public VehicleStatus Sell(string updatedBy = SystemActor)
        {
            if (!CanBeSold)
            {
                throw new BusinessRuleException(
                    $"Um veículo em \"{Describe(Status)}\" ainda está fora da venda. " +
                    "Deixe-o pronto para venda antes de vender.");
            }

            var previous = Status;

            Status = VehicleStatus.Sold;
            UpdateAuditInfo(updatedBy);

            return previous;
        }

        /// <summary>Puts a sold vehicle back on the lot, when the sale is undone.</summary>
        /// <param name="updatedBy">Who undid it.</param>
        public void CancelSale(string updatedBy = SystemActor)
        {
            if (Status != VehicleStatus.Sold)
            {
                throw new BusinessRuleException("Este veículo está sem venda registrada.");
            }

            Status = VehicleStatus.ReadyForSale;
            UpdateAuditInfo(updatedBy);
        }

        /// <summary>
        /// Registers the car that came in as part of a sale. Its purchase price is what it
        /// was valued at in the trade, and its supplier is the person who drove it in.
        ///
        /// It starts under review, like every other car: nobody has looked under it yet.
        /// </summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="plate">Plate.</param>
        /// <param name="chassis">Chassis.</param>
        /// <param name="brand">Brand.</param>
        /// <param name="model">Model.</param>
        /// <param name="modelYear">Model year.</param>
        /// <param name="manufactureYear">Manufacture year.</param>
        /// <param name="tradeInValue">What it was valued at in the deal.</param>
        /// <param name="date">The date of the sale it came from.</param>
        /// <param name="fromWhom">The buyer of the other car.</param>
        /// <param name="createdBy">Who registered it.</param>
        /// <returns>The incoming vehicle.</returns>
        public static Vehicle CreateFromTradeIn(
            int idTenant,
            string plate,
            string chassis,
            string brand,
            string model,
            short modelYear,
            short manufactureYear,
            decimal tradeInValue,
            DateOnly date,
            string fromWhom,
            string createdBy = SystemActor)
        {
            if (tradeInValue <= 0)
            {
                throw new BusinessRuleException("Informe o valor do carro que entrou na troca.");
            }

            var vehicle = Create(
                idTenant, plate, chassis, brand, model, modelYear, manufactureYear, createdBy);

            vehicle.SetOrigin(VehicleOrigin.TradeIn, hasDamage: false, damageDescription: null);
            vehicle.SetPurchase(tradeInValue, date, fromWhom, PaymentMethod.TradeIn);

            return vehicle;
        }

        /// <summary>Points the cover at one of the photos.</summary>
        /// <param name="idPhoto">Photo to use, or null to clear it.</param>
        public void SetCoverPhoto(int? idPhoto) => IdCoverPhoto = idPhoto;

        /// <summary>How long the vehicle has been in stock (RF-24).</summary>
        /// <param name="today">Today, passed in so the calculation stays testable.</param>
        /// <returns>Days since the purchase, or null while there is no purchase date.</returns>
        public int? DaysInStock(DateOnly today) =>
            PurchaseDate is null ? null : today.DayNumber - PurchaseDate.Value.DayNumber;

        private static string? Trim(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>Portuguese, because the message reaches the screen.</summary>
        private static string Describe(VehicleStatus status) => status switch
        {
            VehicleStatus.UnderReview => "Em análise",
            VehicleStatus.Purchased => "Comprado",
            VehicleStatus.InRepair => "Em reparo",
            VehicleStatus.ReadyForSale => "Pronto para venda",
            VehicleStatus.Advertised => "Anunciado",
            VehicleStatus.Negotiating => "Negociando",
            VehicleStatus.Sold => "Vendido",
            _ => status.ToString()
        };

        /// <summary>Says where the vehicle can go, instead of only refusing where it cannot.</summary>
        private static string Next(VehicleStatus status)
        {
            var options = Allowed[status].Select(Describe).ToList();

            return options.Count switch
            {
                0 => "nenhum outro estado",
                1 => $"\"{options[0]}\"",
                _ => $"\"{string.Join("\", \"", options[..^1])}\" ou \"{options[^1]}\""
            };
        }
    }
}
