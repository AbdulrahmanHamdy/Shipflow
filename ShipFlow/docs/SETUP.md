# Setup

## Prerequisites

- .NET SDK 8.0 (`dotnet --list-sdks` should show 8.0.x). Install from https://dotnet.microsoft.com/download/dotnet/8.0
  or, on Ubuntu 24.04, `sudo apt-get install dotnet-sdk-8.0`.
- Network access to nuget.org to restore test packages.

## Build and run

```bash
dotnet restore ShipFlow.sln
dotnet build ShipFlow.sln
dotnet run --project src/ShipFlow.API          # listens on http://localhost:5177 (Development)
curl http://localhost:5177/health
```

## Tests

```bash
dotnet test ShipFlow.sln
```

If you cannot reach nuget.org, only the `src/` projects can be built (they need no external packages in Phase 0):

```bash
dotnet build src/ShipFlow.API
```

## Configuration and secrets

Nothing secret is committed. For local development use user-secrets (API project already has a `UserSecretsId`):

```bash
cd src/ShipFlow.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=ShipFlow;User Id=sa;Password=<your password>;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
dotnet user-secrets set "Seed:DevPassword" "<demo password: 10+ chars, upper, lower, digit>"
```

Other settings (`appsettings.json`): `Jwt:Issuer/Audience/AccessTokenMinutes`, `Database:*`, `Cors:AllowedOrigins`,
`RateLimiting:AuthPermitLimit/AuthWindowSeconds`, `Swagger:Enabled`. The app refuses to start without a valid Jwt section.
In containers/CI use environment variables (`Jwt__SigningKey`, `ConnectionStrings__DefaultConnection`).

## Local SQL Server

```bash
docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='<strong password>' -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

## Migrations (first thing to do: none exists yet)

```bash
dotnet tool install --global dotnet-ef --version 8.0.11
dotnet ef migrations add InitialCreate -p src/ShipFlow.Infrastructure -s src/ShipFlow.API -o Persistence/Migrations
# review the generated SQL, then:
export SHIPFLOW_DESIGNTIME_CONNECTION="<connection string>"
dotnet ef database update -p src/ShipFlow.Infrastructure -s src/ShipFlow.API
```

In Development the API applies migrations at startup (`Database:MigrateOnStartup`); until `InitialCreate` exists it falls back to
`EnsureCreated` and logs a warning. Outside Development it throws if no migration exists and never migrates unless asked.
Production: apply migrations as a deployment step (`dotnet ef migrations script --idempotent`), not at startup.

Development demo users (Development only, needs `Seed:DevPassword`): admin@, customer@, company.admin@, dispatcher@, driver@ `shipflow.test`.
Phase 4 also seeds (Development only, once, when no company exists) "Demo Logistics": company.admin@ is its admin, dispatcher@ and driver@ are members,
with one driver profile (`DEMO-0001`) and one vehicle (`DEMO 001`, 10,000 kg / 60 m3). Membership and Identity roles are consistent (D-023).
Identity roles must exist before members can be added: keep `Database:SeedReferenceData=true` (the default) or seed them yourself.
When you generate `InitialCreate`, it must include the Phase 4 index `UX_CompanyMemberships_User_Active`.

## Swagger

Development: http://localhost:5177/swagger. Log in via `POST /api/auth/login`, paste `accessToken` into Authorize.
