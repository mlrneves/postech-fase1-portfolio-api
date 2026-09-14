using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Portfolio.Infra.Identity;

namespace Portfolio.WebApi.Setup;

public static class AuthenticationSetup
{
    /// <summary>
    /// Liga o esquema Bearer usando as mesmas <see cref="JwtOptions"/> que a fabrica
    /// de tokens usa para assinar, para que emissao e validacao nunca saiam de sincronia.
    /// </summary>
    public static IServiceCollection AddBearerAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var settings = jwt.Value;

                // Sem isso o handler renomeia "sub" para o ClaimType longo do WS-Federation
                // e a leitura da identidade no controller para de funcionar.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)),

                    // Tolerancia minima de relogio. O padrao da biblioteca e 5 minutos,
                    // o que faria um token expirado continuar valendo por tempo demais.
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
