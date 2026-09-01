using Foundation.Domain.Abstractions;
using System.Diagnostics;

namespace RevendaPro.Domain.Entities
{
    /// <summary>A person who signs in. Rich domain: state changes only through methods.</summary>
    [DebuggerDisplay("Email={Email}, TenantId={TenantId}")]
    public class User : TenantEntity
    {
        private User() { }

        private User(int tenantId) : base(tenantId) { }

        public string Name { get; private set; } = string.Empty;

        public string Email { get; private set; } = string.Empty;

        /// <summary>Argon2 hash. Plain text never reaches this class.</summary>
        public string PasswordHash { get; private set; } = string.Empty;

        /// <summary>Photo file name. The file lives outside the database; this is the pointer.</summary>
        public string? Photo { get; private set; }

        /// <summary>CPF or CNPJ, digits only. Masking is a UI concern.</summary>
        public string? Document { get; private set; }

        /// <summary>Phone with area code, digits only.</summary>
        public string? Phone { get; private set; }

        public static User Create(
            int tenantId,
            string name,
            string email,
            string passwordHash,
            string createdBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("User name cannot be null or empty.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("User email cannot be null or empty.", nameof(email));
            }

            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new ArgumentException("Password hash cannot be null or empty.", nameof(passwordHash));
            }

            var user = new User(tenantId)
            {
                Name = name.Trim(),
                Email = Normalize(email),
                PasswordHash = passwordHash
            };

            user.SetCreatedBy(createdBy);

            return user;
        }

        public void Update(
            string name,
            string email,
            string? document,
            string? phone,
            string updatedBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("User name cannot be null or empty.", nameof(name));
            }

            Name = name.Trim();
            Email = Normalize(email);
            Document = DigitsOnly(document);
            Phone = DigitsOnly(phone);
            UpdateAuditInfo(updatedBy);
        }

        public void ChangePassword(string passwordHash, string updatedBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new ArgumentException("Password hash cannot be null or empty.", nameof(passwordHash));
            }

            PasswordHash = passwordHash;
            UpdateAuditInfo(updatedBy);
        }

        public void ChangePhoto(string? fileName, string updatedBy = SystemActor)
        {
            Photo = string.IsNullOrWhiteSpace(fileName) ? null : fileName;
            UpdateAuditInfo(updatedBy);
        }

        /// <summary>Only an active, non-deleted user can sign in.</summary>
        public bool CanSignIn() => IsActive;

        private static string Normalize(string email) => email.Trim().ToLowerInvariant();

        /// <summary>Stores digits only; the mask is reapplied when displaying.</summary>
        private static string? DigitsOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());

            return digits.Length == 0 ? null : digits;
        }
    }

    /// <summary>Refresh token. The database stores the hash, never the emitted value.</summary>
    [DebuggerDisplay("UserId={UserId}, ExpiresAt={ExpiresAt}")]
    public class RefreshToken : Entity
    {
        private RefreshToken() { }

        public int UserId { get; private set; }

        public string TokenHash { get; private set; } = string.Empty;

        public DateTime ExpiresAt { get; private set; }

        public DateTime? RevokedAt { get; private set; }

        public static RefreshToken Create(
            int userId,
            string tokenHash,
            DateTime expiresAt,
            string createdBy = SystemActor)
        {
            var token = new RefreshToken
            {
                UserId = userId,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt
            };

            token.SetCreatedBy(createdBy);

            return token;
        }

        public bool IsValid(DateTime now) => RevokedAt is null && ExpiresAt > now;

        public void Revoke() => RevokedAt ??= DateTime.UtcNow;
    }
}
