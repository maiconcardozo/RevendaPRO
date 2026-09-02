using System.Diagnostics;
using RevendaPro.Domain.Enums;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// What was spent on a vehicle (RF-08).
    ///
    /// The purchase is deliberately outside this table, in <see cref="Vehicle.PurchasePrice"/>,
    /// even though the business writes it as the first line of the spending sheet. Keeping it
    /// apart is what lets the screen answer "how much did the car cost, and how much did I
    /// spend on it" without a category standing for the vehicle itself.
    /// </summary>
    [DebuggerDisplay("IdVehicle={IdVehicle}, Amount={Amount}, IsPaid={IsPaid}")]
    public class VehicleExpense : VehicleEntity
    {
        private VehicleExpense() { }

        public string Description { get; private set; } = string.Empty;

        public ExpenseCategory Category { get; private set; }

        public decimal Amount { get; private set; }

        public DateOnly Date { get; private set; }

        /// <summary>False means the expense is planned, and stays out of the real cost (RF-11).</summary>
        public bool IsPaid { get; private set; }

        /// <summary>Records an expense.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="description">What it was.</param>
        /// <param name="category">Which category.</param>
        /// <param name="amount">How much.</param>
        /// <param name="date">When.</param>
        /// <param name="isPaid">Whether it was already paid.</param>
        /// <param name="createdBy">Who recorded it.</param>
        /// <returns>The expense.</returns>
        public static VehicleExpense Create(
            int idVehicle,
            string description,
            ExpenseCategory category,
            decimal amount,
            DateOnly date,
            bool isPaid = true,
            string createdBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new BusinessRuleException("Descreva a despesa.");
            }

            if (amount <= 0)
            {
                throw new BusinessRuleException("Informe um valor maior que zero.");
            }

            var expense = new VehicleExpense
            {
                IdVehicle = idVehicle,
                Description = description.Trim(),
                Category = category,
                Amount = amount,
                Date = date,
                IsPaid = isPaid
            };

            expense.SetCreatedBy(createdBy);

            return expense;
        }

        /// <summary>Changes the expense.</summary>
        /// <param name="description">What it was.</param>
        /// <param name="category">Which category.</param>
        /// <param name="amount">How much.</param>
        /// <param name="date">When.</param>
        /// <param name="isPaid">Whether it was already paid.</param>
        /// <param name="updatedBy">Who changed it.</param>
        public void Update(
            string description,
            ExpenseCategory category,
            decimal amount,
            DateOnly date,
            bool isPaid,
            string updatedBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new BusinessRuleException("Descreva a despesa.");
            }

            if (amount <= 0)
            {
                throw new BusinessRuleException("Informe um valor maior que zero.");
            }

            Description = description.Trim();
            Category = category;
            Amount = amount;
            Date = date;
            IsPaid = isPaid;

            UpdateAuditInfo(updatedBy);
        }

        /// <summary>Turns a planned expense into a paid one.</summary>
        /// <param name="updatedBy">Who confirmed it.</param>
        public void ConfirmPayment(string updatedBy = SystemActor)
        {
            if (IsPaid)
            {
                return;
            }

            IsPaid = true;
            UpdateAuditInfo(updatedBy);
        }
    }

    /// <summary>A photo of a vehicle (RF-12).</summary>
    [DebuggerDisplay("IdVehicle={IdVehicle}, Kind={Kind}, Order={Order}")]
    public class VehiclePhoto : VehicleEntity
    {
        private VehiclePhoto() { }

        public VehiclePhotoKind Kind { get; private set; }

        /// <summary>
        /// Prefix shared by the three renditions. The suffix of each size is appended when the
        /// address is built, so one column addresses all of them.
        /// </summary>
        public string StorageKey { get; private set; } = string.Empty;

        public string ContentType { get; private set; } = string.Empty;

        /// <summary>The three renditions together, which is what the gallery costs to keep.</summary>
        public int SizeInBytes { get; private set; }

        public short Width { get; private set; }

        public short Height { get; private set; }

        /// <summary>Position in the gallery. The business curates and reorders by hand.</summary>
        public int Order { get; private set; }

        /// <summary>Records a photo that is already stored.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="kind">What the photo is for.</param>
        /// <param name="storageKey">Prefix of the three renditions.</param>
        /// <param name="contentType">Media type.</param>
        /// <param name="sizeInBytes">Bytes of all renditions.</param>
        /// <param name="width">Width of the largest rendition.</param>
        /// <param name="height">Height of the largest rendition.</param>
        /// <param name="order">Position in the gallery.</param>
        /// <param name="createdBy">Who uploaded it.</param>
        /// <returns>The photo.</returns>
        public static VehiclePhoto Create(
            int idVehicle,
            VehiclePhotoKind kind,
            string storageKey,
            string contentType,
            int sizeInBytes,
            short width,
            short height,
            int order,
            string createdBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
            {
                throw new BusinessRuleException("Falha ao registrar a foto do veículo.");
            }

            var photo = new VehiclePhoto
            {
                IdVehicle = idVehicle,
                Kind = kind,
                StorageKey = storageKey,
                ContentType = contentType,
                SizeInBytes = sizeInBytes,
                Width = width,
                Height = height,
                Order = order
            };

            photo.SetCreatedBy(createdBy);

            return photo;
        }

        /// <summary>Moves the photo in the gallery.</summary>
        /// <param name="order">New position.</param>
        /// <param name="updatedBy">Who moved it.</param>
        public void Reorder(int order, string updatedBy = SystemActor)
        {
            Order = order;
            UpdateAuditInfo(updatedBy);
        }

        /// <summary>Changes what the photo is for.</summary>
        /// <param name="kind">New kind.</param>
        /// <param name="updatedBy">Who changed it.</param>
        public void Reclassify(VehiclePhotoKind kind, string updatedBy = SystemActor)
        {
            Kind = kind;
            UpdateAuditInfo(updatedBy);
        }
    }

    /// <summary>A document attached to a vehicle (RF-13). Always in the private store.</summary>
    [DebuggerDisplay("IdVehicle={IdVehicle}, Kind={Kind}, FileName={FileName}")]
    public class VehicleDocument : VehicleEntity
    {
        private VehicleDocument() { }

        public VehicleDocumentKind Kind { get; private set; }

        public string StorageKey { get; private set; } = string.Empty;

        /// <summary>
        /// The name the file arrived with, kept only to show. It never becomes the key: it
        /// carries accents, spaces and whatever the sender decided.
        /// </summary>
        public string FileName { get; private set; } = string.Empty;

        public string ContentType { get; private set; } = string.Empty;

        public int SizeInBytes { get; private set; }

        /// <summary>Records a document that is already stored.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="kind">Which kind.</param>
        /// <param name="storageKey">Key in the private store.</param>
        /// <param name="fileName">Name to show.</param>
        /// <param name="contentType">Media type.</param>
        /// <param name="sizeInBytes">Size.</param>
        /// <param name="createdBy">Who uploaded it.</param>
        /// <returns>The document.</returns>
        public static VehicleDocument Create(
            int idVehicle,
            VehicleDocumentKind kind,
            string storageKey,
            string fileName,
            string contentType,
            int sizeInBytes,
            string createdBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(storageKey) || string.IsNullOrWhiteSpace(fileName))
            {
                throw new BusinessRuleException("Falha ao registrar o documento do veículo.");
            }

            var document = new VehicleDocument
            {
                IdVehicle = idVehicle,
                Kind = kind,
                StorageKey = storageKey,
                FileName = fileName.Trim(),
                ContentType = contentType,
                SizeInBytes = sizeInBytes
            };

            document.SetCreatedBy(createdBy);

            return document;
        }

        /// <summary>Changes what the document is.</summary>
        /// <param name="kind">New kind.</param>
        /// <param name="updatedBy">Who changed it.</param>
        public void Reclassify(VehicleDocumentKind kind, string updatedBy = SystemActor)
        {
            Kind = kind;
            UpdateAuditInfo(updatedBy);
        }
    }

    /// <summary>
    /// Every move along the pipeline (RF-26).
    ///
    /// Without it the time spent in each stage is lost at each change, and the business wants
    /// to know how long a car sat — and how much money sat with it (RF-24).
    /// </summary>
    [DebuggerDisplay("IdVehicle={IdVehicle}, {FromStatus}->{ToStatus}")]
    public class VehicleStatusHistory : VehicleEntity
    {
        private VehicleStatusHistory() { }

        /// <summary>Null on the first record, when the vehicle had no status yet.</summary>
        public VehicleStatus? FromStatus { get; private set; }

        public VehicleStatus ToStatus { get; private set; }

        public string? Reason { get; private set; }

        /// <summary>Records a move.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="fromStatus">Where it came from.</param>
        /// <param name="toStatus">Where it went.</param>
        /// <param name="reason">Why, when there is a reason.</param>
        /// <param name="createdBy">Who moved it.</param>
        /// <returns>The record.</returns>
        public static VehicleStatusHistory Create(
            int idVehicle,
            VehicleStatus? fromStatus,
            VehicleStatus toStatus,
            string? reason = null,
            string createdBy = SystemActor)
        {
            var history = new VehicleStatusHistory
            {
                IdVehicle = idVehicle,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
            };

            history.SetCreatedBy(createdBy);

            return history;
        }
    }
}
