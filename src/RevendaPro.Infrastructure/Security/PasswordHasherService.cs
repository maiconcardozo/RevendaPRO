using Foundation.Shared.Helpers;
using RevendaPro.Domain.Interfaces.Security;

namespace RevendaPro.Infrastructure.Security
{
    /// <summary>
    /// Argon2 hashing, from Foundation's <see cref="StringHelper"/>.
    ///
    /// Replaces the ASP.NET Identity PasswordHasher so every Global project derives the
    /// hash the same way. See ADR-0003.
    /// </summary>
    public class PasswordHasherService : IPasswordHasher
    {
        /// <inheritdoc/>
        public string Hash(string password)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            return StringHelper.ComputeArgon2Hash(password, salt: null!, pepper: null!);
        }

        /// <inheritdoc/>
        public bool Verify(string passwordHash, string password)
        {
            if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            try
            {
                return StringHelper.VerifyArgon2Hash(password, passwordHash, pepper: null!);
            }
            catch (FormatException)
            {
                // Corrupt or legacy hash: treated as a wrong password, not as a failure.
                return false;
            }
        }
    }
}
