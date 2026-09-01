using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Constants;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Infrastructure.Security
{
    /// <summary>
    /// Issues the access and refresh tokens.
    ///
    /// The access token carries only sub, code, tenant and exp. Screen keys are NOT claims:
    /// they are resolved per request, so a permission change applies immediately and the
    /// token does not grow with the catalog. See ADR-0002.
    /// </summary>
    public class JwtTokenService(IOptions<JwtSettings> settings) : ITokenService
    {
        private readonly JwtSettings _settings = settings.Value;

        /// <inheritdoc/>
        public IssuedToken CreateAccessToken(User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims:
                [
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
                    new Claim(TokenClaims.UserCode, user.Code.ToString()),
                    new Claim(TokenClaims.TenantId, user.TenantId.ToString(CultureInfo.InvariantCulture))
                ],
                expires: expiresAt,
                signingCredentials: credentials);

            return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }

        /// <inheritdoc/>
        public (string Value, string Hash, DateTime ExpiresAt) CreateRefreshToken()
        {
            var value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

            return (value, ComputeHash(value), DateTime.UtcNow.AddDays(_settings.RefreshTokenDays));
        }

        /// <inheritdoc/>
        public string ComputeHash(string refreshToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        }
    }
}
