using Foundation.Domain.Abstractions;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// Something that belongs to a vehicle: an expense, a photo, a document, a status change.
    ///
    /// It carries no <c>IdTenant</c> on purpose. The isolation comes through the vehicle, so
    /// there is one place where a row can be attached to the wrong company, instead of five.
    /// Every query on these tables joins the vehicle and filters the tenant there.
    /// </summary>
    public abstract class VehicleEntity : Entity
    {
        /// <summary>The vehicle this belongs to.</summary>
        public int IdVehicle { get; protected set; }
    }
}
