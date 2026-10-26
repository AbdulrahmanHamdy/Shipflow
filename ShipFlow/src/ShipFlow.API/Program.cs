using ShipFlow.API.Auth;
using ShipFlow.API.Configuration;
using ShipFlow.API.Health;
using ShipFlow.API.Middleware;
using ShipFlow.Application;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Infrastructure;
using ShipFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Composition root: the API is the only project that wires all layers together.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddShipFlowAuthorization();
builder.Services.AddShipFlowCors();
builder.Services.AddShipFlowRateLimiting();
builder.Services.AddShipFlowSwagger();

builder.Services
    .AddControllers(options =>
    {
        // FluentValidation owns request validation, so one consistent error shape comes back for every
        // missing or invalid field instead of MVC's implicit [Required] messages.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
        // Enums travel as readable strings ("Quoting"), in responses and query strings alike.
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

// Swagger documents the API; it is on in Development and can be switched on elsewhere with Swagger:Enabled.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors(CorsSetup.PolicyName);
app.UseRateLimiter();

// An unknown route should answer 404 Not Found rather than letting the FallbackPolicy challenge with 401 Unauthorized.
app.Use(async (context, next) =>
{
    if (context.GetEndpoint() == null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next(context);
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "ShipFlow Web API",
    status = "Online",
    version = "1.0.0",
    documentation = "/swagger",
    health = "/health",
    frontend = "https://s-hipflow.vercel.app"
})).AllowAnonymous();

// Liveness: the process is up. No dependency checks, so an orchestrator does not restart the app for a database blip.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();

// Readiness: dependencies (database) are reachable.
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();

// Optional startup work (migrate in Development, seed roles, seed demo users in Development). See DatabaseOptions.
var databaseOptions = app.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
if (databaseOptions.MigrateOnStartup || databaseOptions.SeedReferenceData || databaseOptions.SeedDevelopmentData)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
}

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap the app in integration tests.
public partial class Program { }
