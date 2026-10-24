using Microsoft.OpenApi.Models;

namespace ShipFlow.API.Configuration;

public static class SwaggerSetup
{
    public static IServiceCollection AddShipFlowSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ShipFlow API",
                Version = "v1",
                Description = "Smart logistics and shipment management backend."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Paste the accessToken returned by POST /api/auth/login (without the 'Bearer ' prefix).",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
