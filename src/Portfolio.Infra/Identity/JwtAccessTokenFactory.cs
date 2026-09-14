using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Portfolio.Core.Investors;
using Portfolio.UseCases.Abstractions;

namespace Portfolio.Infra.Identity;

/// <summary>
/// Emite o JWT assinado em HMAC-SHA256. O id do investidor vai na claim padrao
/// "sub" e e a unica fonte de identidade aceita pelos endpoints protegidos.
/// </summary>
public class JwtAccessTokenFactory(IOptions<JwtOptions> options) : IAccessTokenFactory
{
    private static readonly JwtSecurityTokenHandler Handler = new();
    private readonly JwtOptions _options = options.Value;

    public AccessToken IssueFor(Investor investor)
    {
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.LifetimeMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, investor.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, investor.Email),
                new Claim(JwtRegisteredClaimNames.Name, investor.FullName),

                // Identificador unico do token: permitiria revogacao por lista negra.
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ])
        };

        return new AccessToken(Handler.CreateEncodedJwt(descriptor), expiresAt);
    }
}
