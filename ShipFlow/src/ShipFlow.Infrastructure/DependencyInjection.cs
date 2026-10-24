using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;
using ShipFlow.Infrastructure.Identity;
using ShipFlow.Infrastructure.Persistence;
using ShipFlow.Infrastructure.Persistence.Stores;

namespace ShipFlow.Infrastructure;

public static class DependencyInjection
{
    public const string DefaultConnectionName = "DefaultConnection";

    /// <summary>
    /// Registers persistence (EF Core + SQL Server), ASP.NET Core Identity and JWT bearer authentication.
    /// Configuration is read lazily (when services are first resolved), so values supplied by the host
    /// after registration, such as test overrides, are honoured.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Single injectable source of UTC time so business code never calls DateTime.UtcNow directly.
        services.TryAddSingleton(TimeProvider.System);

        services.AddPersistence();
        services.AddShipFlowIdentity();
        services.AddJwtAuthentication();

        return services;
    }

    private static void AddPersistence(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(DefaultConnectionName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"ConnectionStrings:{DefaultConnectionName} is not configured. See docs/SETUP.md.");
            }

            // Retry-on-failure stays off (Phase 7 decides the policy). The one user-initiated transaction, FleetTransaction,
            // already runs inside CreateExecutionStrategy().ExecuteAsync, so turning retries on later is safe.
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IShipmentStore, ShipmentStore>();
        services.AddScoped<IAssignmentStore, AssignmentStore>();
        services.AddScoped<ICompanyAccess, CompanyAccess>();
        services.AddScoped<ICompanyStore, CompanyStore>();
        services.AddScoped<IFleetStore, FleetStore>();
        services.AddScoped<IFleetTransaction, FleetTransaction>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName);
        services.AddScoped<DatabaseInitializer>();

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
    }

    private static void AddShipFlowIdentity(this IServiceCollection services)
    {
        // AddIdentityCore (not AddIdentity): no cookie authentication, which is wrong for an API.
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = PasswordPolicy.MinLength;
                options.Password.RequireDigit = PasswordPolicy.RequireDigit;
                options.Password.RequireLowercase = PasswordPolicy.RequireLowercase;
                options.Password.RequireUppercase = PasswordPolicy.RequireUppercase;
                options.Password.RequireNonAlphanumeric = PasswordPolicy.RequireNonAlphanumeric;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IMembershipRoles, MembershipRoles>();
    }

    private static void AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(
                JwtOptions.IsValid,
                "Jwt configuration is invalid: Issuer, Audience and a SigningKey of at least 32 bytes are required.")
            .ValidateOnStart();

        services.AddSingleton<ITokenService, JwtTokenService>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configured through options so the signing key is read when first needed, not at registration time.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep the token's own claim names ("sub", "role") instead of remapping them to legacy URIs.
                bearer.MapInboundClaims = false;
                bearer.SaveToken = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                    NameClaimType = ClaimNames.Subject,
                    RoleClaimType = ClaimNames.Role
                };
            });
    }
}
