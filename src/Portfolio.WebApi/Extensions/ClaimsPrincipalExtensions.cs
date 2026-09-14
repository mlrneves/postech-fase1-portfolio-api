using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Portfolio.WebApi.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Le o id do investidor da claim "sub" do token. Um token sem "sub" ou com um
    /// valor que nao e Guid resulta em 401, nunca em 500 ou em acesso a dados alheios.
    /// </summary>
    public static Guid InvestorId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(subject, out var id)
            ? id
            : throw new UnauthorizedAccessException("O token nao traz um identificador de investidor valido.");
    }
}
