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

        /// <summary>Which kind of expense this is. Maintained by the dealership (RF-09).</summary>
        public int IdExpenseType { get; private set; }

        public decimal Amount { get; private set; }

        public DateOnly Date { get; private set; }

        /// <summary>
        /// Who was paid, when the dealership registered them (M18). Null for what has no
        /// supplier: a tax, a fine, an auction fee.
        /// </summary>
        public int? IdSupplier { get; private set; }

        /// <summary>
        /// Free text for what has no column of its own: warranty, who recommended the shop,
        /// the invoice number. It used to hold the supplier as well, before the supplier table.
        /// </summary>
        public string? Notes { get; private set; }

        /// <summary>False means the expense is planned, and stays out of the real cost (RF-11).</summary>
        public bool IsPaid { get; private set; }

        /// <summary>
        /// Quando vence (M22). Sem prazo informado, é a data do gasto: quem lança e paga na hora
        /// jamais precisa pensar nisso, e a pergunta "o que vence esta semana" continua tendo
        /// resposta para toda linha.
        /// </summary>
        public DateOnly DueDate { get; private set; }

        /// <summary>
        /// Quando o dinheiro saiu (M22). Nulo enquanto o gasto está previsto.
        ///
        /// <see cref="IsPaid"/> continua sendo o estado que as consultas leem, e esta é a data
        /// que conta a história: um gasto pago em 3 de outubro que vencia em 30 de setembro é um
        /// atraso, e sem as duas o sistema jamais saberia. As duas se movem juntas, sempre por
        /// esta entidade — <see cref="MarkAsPaid"/>, <see cref="MarkAsPlanned"/> e
        /// <see cref="Update"/> são os únicos lugares onde elas mudam.
        /// </summary>
        public DateOnly? PaidDate { get; private set; }

        /// <summary>Vencido e ainda sem pagamento, na data de referência.</summary>
        /// <param name="today">O dia de hoje, na visão de quem pergunta.</param>
        /// <returns>Verdadeiro quando o prazo passou e o dinheiro continua na conta.</returns>
        public bool IsOverdueOn(DateOnly today) => !IsPaid && DueDate < today;

        /// <summary>Records an expense.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="description">What it was.</param>
        /// <param name="idExpenseType">Which kind of expense.</param>
        /// <param name="amount">How much.</param>
        /// <param name="date">When.</param>
        /// <param name="notes">Free text, such as where it was bought.</param>
        /// <param name="isPaid">Whether it was already paid.</param>
        /// <param name="createdBy">Who recorded it.</param>
        /// <param name="idSupplier">Who was paid, when registered.</param>
        /// <param name="dueDate">Quando vence. Sem prazo, é a data do gasto (M22).</param>
        /// <param name="paidDate">Quando o dinheiro saiu. Sem data, é a data do gasto (M22).</param>
        /// <returns>The expense.</returns>
        public static VehicleExpense Create(
            int idVehicle,
            string description,
            int idExpenseType,
            decimal amount,
            DateOnly date,
            string? notes = null,
            bool isPaid = true,
            string createdBy = SystemActor,
            int? idSupplier = null,
            DateOnly? dueDate = null,
            DateOnly? paidDate = null)
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
                IdExpenseType = idExpenseType,
                Amount = amount,
                Date = date,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                IsPaid = isPaid,
                IdSupplier = idSupplier,
                DueDate = dueDate ?? date,
                PaidDate = isPaid ? paidDate ?? date : null
            };

            expense.SetCreatedBy(createdBy);

            return expense;
        }

        /// <summary>Changes the expense.</summary>
        /// <param name="description">What it was.</param>
        /// <param name="idExpenseType">Which kind of expense.</param>
        /// <param name="amount">How much.</param>
        /// <param name="date">When.</param>
        /// <param name="notes">Free text, such as where it was bought.</param>
        /// <param name="isPaid">Whether it was already paid.</param>
        /// <param name="updatedBy">Who changed it.</param>
        /// <param name="idSupplier">Who was paid, when registered.</param>
        /// <param name="dueDate">Quando vence. Sem prazo, é a data do gasto (M22).</param>
        /// <param name="paidDate">Quando o dinheiro saiu. Sem data, a que já estava, ou a data do gasto (M22).</param>
        public void Update(
            string description,
            int idExpenseType,
            decimal amount,
            DateOnly date,
            string? notes,
            bool isPaid,
            string updatedBy = SystemActor,
            int? idSupplier = null,
            DateOnly? dueDate = null,
            DateOnly? paidDate = null)
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
            IdExpenseType = idExpenseType;
            Amount = amount;
            Date = date;
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            IsPaid = isPaid;
            IdSupplier = idSupplier;
            DueDate = dueDate ?? date;

            // Editar jamais deixa as duas discordarem: previsto perde a data de pagamento, e
            // pago sem data informada mantém a que tinha, ou cai na data do gasto.
            PaidDate = isPaid ? paidDate ?? PaidDate ?? date : null;

            UpdateAuditInfo(updatedBy);
        }

        /// <summary>
        /// Dá baixa: o dinheiro saiu, e o dia em que saiu fica registrado (M22).
        ///
        /// Existe para a tela do Caixa e para a aba de gastos do carro pagarem em um clique, sem
        /// reenviar a descrição, o tipo e o valor só para mudar um estado.
        /// </summary>
        /// <param name="paidOn">O dia em que o dinheiro saiu.</param>
        /// <param name="updatedBy">Quem deu a baixa.</param>
        public void MarkAsPaid(DateOnly paidOn, string updatedBy = SystemActor)
        {
            IsPaid = true;
            PaidDate = paidOn;

            UpdateAuditInfo(updatedBy);
        }

        /// <summary>Desfaz a baixa: o gasto volta a previsto, e a data de pagamento sai (M22).</summary>
        /// <param name="updatedBy">Quem desfez.</param>
        public void MarkAsPlanned(string updatedBy = SystemActor)
        {
            IsPaid = false;
            PaidDate = null;

            UpdateAuditInfo(updatedBy);
        }

        /// <summary>
        /// Points the expense at a supplier without touching anything else.
        ///
        /// Exists for the demonstration seeder, which fills in who was paid on expenses that
        /// were recorded before the supplier table existed. The screen goes through
        /// <see cref="Update"/>, where the supplier is one field among the others.
        /// </summary>
        /// <param name="idSupplier">Who was paid, or null.</param>
        /// <param name="updatedBy">Who changed it.</param>
        public void AssignSupplier(int? idSupplier, string updatedBy = SystemActor)
        {
            if (IdSupplier == idSupplier)
            {
                return;
            }

            IdSupplier = idSupplier;
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

        /// <summary>
        /// Position in the gallery. The business curates and reorders by hand.
        ///
        /// Named Position, and not Order, because Order is a reserved word in MySQL. The
        /// project rule is to rename the property rather than write SQL by hand around it.
        /// </summary>
        public int Position { get; private set; }

        /// <summary>Records a photo that is already stored.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="kind">What the photo is for.</param>
        /// <param name="storageKey">Prefix of the three renditions.</param>
        /// <param name="contentType">Media type.</param>
        /// <param name="sizeInBytes">Bytes of all renditions.</param>
        /// <param name="width">Width of the largest rendition.</param>
        /// <param name="height">Height of the largest rendition.</param>
        /// <param name="position">Position in the gallery.</param>
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
            int position,
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
                Position = position
            };

            photo.SetCreatedBy(createdBy);

            return photo;
        }

        /// <summary>Moves the photo in the gallery.</summary>
        /// <param name="position">New position.</param>
        /// <param name="updatedBy">Who moved it.</param>
        public void Reorder(int position, string updatedBy = SystemActor)
        {
            Position = position;
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
    /// <summary>
    /// A passagem do carro por um pátio: de onde ele saiu, e para onde foi.
    ///
    /// Existe pelo mesmo motivo do histórico de situação: sem ela, a passagem some no instante
    /// da mudança, e o sistema deixa de responder <i>"esse carro ficou dois meses na Loja do
    /// Joãozinho e voltou sem vender"</i> — que é a informação que decide se vale deixar carro
    /// lá de novo.
    ///
    /// Ela não carrega empresa: pende do veículo, e é ele que diz de quem é (ver
    /// <see cref="VehicleEntity"/>).
    /// </summary>
    public class VehicleYardHistory : VehicleEntity
    {
        private VehicleYardHistory() { }

        /// <summary>De onde o carro saiu. Nulo quando ele ainda não estava em pátio nenhum.</summary>
        public int? IdFromYard { get; private set; }

        /// <summary>Para onde o carro foi. Nulo quando ele saiu de todos os pátios.</summary>
        public int? IdToYard { get; private set; }

        /// <summary>Por que ele mudou, quando alguém escreveu.</summary>
        public string? Reason { get; private set; }

        /// <summary>Registra a passagem.</summary>
        /// <param name="idVehicle">O carro.</param>
        /// <param name="idFromYard">De onde ele saiu.</param>
        /// <param name="idToYard">Para onde ele foi.</param>
        /// <param name="reason">Por que mudou.</param>
        /// <param name="createdBy">Quem moveu.</param>
        /// <returns>A passagem.</returns>
        public static VehicleYardHistory Create(
            int idVehicle,
            int? idFromYard,
            int? idToYard,
            string? reason = null,
            string createdBy = SystemActor)
        {
            var history = new VehicleYardHistory
            {
                IdVehicle = idVehicle,
                IdFromYard = idFromYard,
                IdToYard = idToYard,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
            };

            history.SetCreatedBy(createdBy);

            return history;
        }
    }

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
