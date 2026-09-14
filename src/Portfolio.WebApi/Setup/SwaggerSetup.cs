using Microsoft.OpenApi.Models;

namespace Portfolio.WebApi.Setup;

public static class SwaggerSetup
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddOpenApiDocs(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Portfolio API",
                Version = "v1",
                Description =
                    "API de carteira de investimentos: cadastro de investidor, autenticacao por " +
                    "JWT e gestao dos aportes do investidor autenticado."
            });

            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Cole aqui apenas o token devolvido por POST /auth, sem o prefixo 'Bearer'."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = BearerScheme }
                }] = []
            });

            // Traz as descricoes dos comentarios /// dos controllers e dos contratos.
            foreach (var xml in Directory.GetFiles(AppContext.BaseDirectory, "Portfolio.*.xml"))
                options.IncludeXmlComments(xml, includeControllerXmlComments: true);
        });

        return services;
    }
}
