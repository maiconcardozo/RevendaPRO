using Foundation.Domain.Abstractions;
using RevendaPro.Shared.Exceptions;
using System.Diagnostics;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// A dealership using the system. Every business row belongs to one.
    ///
    /// Since the M19 it also carries what a printed document needs to say who it came from:
    /// the document number, a phone, an e-mail and the address. A proposal handed to a client
    /// without a sender is a piece of paper nobody can answer.
    /// </summary>
    [DebuggerDisplay("Name={Name}, Id={Id}")]
    public class Tenant : Entity
    {
        private Tenant() { }

        public string Name { get; private set; } = string.Empty;

        /// <summary>CNPJ or CPF, digits only. Optional: a dealership starts without it.</summary>
        public string? Document { get; private set; }

        /// <summary>Phone, digits only. What the client calls after reading the proposal.</summary>
        public string? Phone { get; private set; }

        public string? Email { get; private set; }

        /// <summary>Street, number, city: one line, as it goes on the paper.</summary>
        public string? Address { get; private set; }

        public static Tenant Create(string name, string createdBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Tenant name cannot be null or empty.", nameof(name));
            }

            var tenant = new Tenant { Name = name.Trim() };
            tenant.SetCreatedBy(createdBy);

            return tenant;
        }

        public void Rename(string name, string updatedBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Tenant name cannot be null or empty.", nameof(name));
            }

            Name = name.Trim();
            UpdateAuditInfo(updatedBy);
        }

        /// <summary>
        /// Sets what the documents print about the dealership.
        ///
        /// The document number is checked by length only — eleven digits is a CPF, fourteen a
        /// CNPJ — the same rule the supplier follows; the check digit belongs to the screen.
        /// </summary>
        /// <param name="document">CNPJ or CPF, in any format.</param>
        /// <param name="phone">Phone, in any format.</param>
        /// <param name="email">E-mail.</param>
        /// <param name="address">Address, one line.</param>
        /// <param name="updatedBy">Who changed it.</param>
        public void SetDetails(
            string? document,
            string? phone,
            string? email,
            string? address,
            string updatedBy = SystemActor)
        {
            var digits = Digits(document);

            if (digits is not null && digits.Length != 11 && digits.Length != 14)
            {
                throw new BusinessRuleException("Informe um CPF com 11 dígitos ou um CNPJ com 14.");
            }

            Document = digits;
            Phone = Digits(phone);
            Email = Trim(email)?.ToLowerInvariant();
            Address = Trim(address);

            UpdateAuditInfo(updatedBy);
        }

        private static string? Trim(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? Digits(string? value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

            return digits.Length == 0 ? null : digits;
        }
    }
}
