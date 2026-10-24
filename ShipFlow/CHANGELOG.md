# Changelog

All notable changes to this project are documented here. Format loosely follows Keep a Changelog.

## [Unreleased]

### Phase 5 — Tracking and Delivery (2026-10-24)

#### Added
- **Driver shipment lifecycle**: `pickup`, `start-transit`, `deliver`, `fail-delivery`, `retry-delivery` endpoints. Each verifies authenticated caller is the shipment's assigned driver via `DriverAccess` (membership + driver profile + active assignment, all checked against the DB on every request).
- **Delivery proof**: `DeliverShipmentHandler` atomically creates `DeliveryProof`, transitions `Shipment → Delivered`, and completes the `Assignment` in a single `SaveChangesAsync`. Unique index `UX_DeliveryProofs_Shipment` prevents duplicate proof at the database level.
- **GPS tracking**: `POST /api/tracking` (per-user fixed-window rate-limited, 60/min), `GET /api/shipments/{id}/tracking/latest`, `GET /api/shipments/{id}/tracking` (paginated, time-range filtered). Idempotent submission: duplicate `(shipmentId, recordedAtUtc, userId)` is silently dropped (D-029).
- **Driver assignment reads**: `GET /api/driver/assignments/current`, `GET /api/driver/assignments` (paginated history).
- **Paymob payment integration**: `InitiatePaymentHandler` (3-step API flow: auth token → order → payment key), `ProcessPaymentCallbackHandler` (HMAC-SHA512 verification, idempotent), `GET /api/payments/callback` redirect. Mock mode via `Paymob:UseMock` (D-030).
- **SMTP email**: `IEmailSender` + `EmailMessage` in Application; `SmtpEmailSender` in Infrastructure with `[LoggerMessage]` source generation; no credentials or message content in logs (D-031).
- **EF Core migration**: `InitialCreate` migration generated and applied. `SqlServerDatabaseFixture` uses `context.Database.MigrateAsync()`.
- **Authorization updates**: `[AllowAnonymous]` on Paymob callback endpoints; `OnlyTheIntendedEndpoints_AreAnonymous` test updated to include both callback routes.

#### Fixed
- `DriverAccess.RequireAssignedDriverAsync` — was checking assignment before verifying the shipment belongs to the caller's company; reordered to check company membership → driver profile → shipment carrier match → active assignment.
- `Deliver_DuplicateProof_ThrowsConflict` test — proof existence check is now performed before the driver/assignment authorization check, so the test correctly expects `ConflictException`.
- `PickUp_UnauthorizedDriver_ThrowsForbidden` — test was calling `company.RegisterDriver` without first adding the user as a member; fixed by adding `company.AddMember(...)` before `RegisterDriver`.
- `Concurrency_TwoSimultaneousDeliveries_OnlyOneWins` — used `Task.Run` with a `ManualResetEventSlim` gate for true concurrent start, ensuring both requests race in parallel.
- `DriverAccess` status-aware overload added so `PickUp`, `StartTransit`, `FailDelivery`, and `RetryDelivery` handlers validate required status before calling domain method, returning `ConflictException` instead of `DomainException` for clearer HTTP 409 responses.

#### Changed
- `DriverAccess` now has a two-overload design: single-argument (assigns only) and a `(requiredStatus, ...)` overload that checks the status transition pre-condition.
- `DeliverShipmentHandler` checks for existing proof *before* driver authorization to ensure idempotency-friendly conflict detection.

### Phase 4 — Fleet and Dispatching (2026-10-24)

#### Added
- Company onboarding and membership: `POST /api/companies`, `GET /api/companies/me`, members list/add/remove. Identity roles follow memberships in the same commit (D-023); one active company per user (D-024).
- Drivers and vehicles: register, list (paged), get, update, activate/deactivate, vehicle capacity update; per-company isolation (404), unique plate/licence -> 409 with safe messages (D-027).
- Dispatching: available vehicles/drivers for a shipment and window, assign (`Assignment.Create`), get assignment, unassign (Assigned -> Accepted).
- Concurrency: `FleetTransaction` (`sp_getapplock` exclusive transaction locks in a global order, execution-strategy pattern, 409 on timeout/deadlock) around every booking-affecting write (D-025).
- Removing a member deactivates their driver in the same commit; refused while the driver has an active assignment (D-026). `TransportCompany.ActivateDriver` domain rule.
- Development seed: demo company, driver profile and vehicle.
- Tests: +4 domain, +35 application (incl. race tests with a control run), new SQL Server HTTP test class (not run).

#### Changed
- `UnitOfWork`: unique violations now answer `conflict` (409) with an index-specific message instead of `concurrency_conflict`.
- `IAssignmentStore` extended; new ports `ICompanyStore`, `IFleetStore`, `IUserDirectory`, `IMembershipRoles`, `IFleetTransaction`.

#### Not done / caveats
- Still no NuGet, no SQL Server, no migration: Infrastructure, `Program.cs` and all integration tests are uncompiled; the concurrency protection is unproven on SQL Server. See PROJECT_STATUS.md.
- Tokens are not refreshed: new roles need a new sign-in (D-023).

### Phase 3 — Shipment and Quotation APIs (2026-10-23)

#### Added
- `POST/GET /api/shipments`, `GET {id}`, `open-for-quotes`, `cancel`; quotes: submit, list, accept, withdraw (see docs/API_CONTRACTS.md).
- Pagination (page/pageSize 1-100), filtering (statuses, created range, search), scope (`Mine`, `OpenForQuotes`).
- `Viewer` authorization (ownership, carrier company, company membership from the DB), 404 for non-visible resources.
- Application ports + EF stores, `UnitOfWork` mapping concurrency/unique races to 409; `NotFound`/`Forbidden`/`ConcurrencyConflict` mapped in `ProblemMapper`.
- Cancel releases the active assignment in the same commit. New policy `RequireShipmentViewer`. Enums serialized as strings.
- 30 handler tests; SQL Server HTTP tests (skipped without a server) including the simultaneous-accept race.

#### Fixed (found by compiling Phase 2/3 code offline)
- `LoginCommandValidator` was missing a `using` (would not compile).

#### Not done / caveats
- Still no migration and no NuGet-dependent code compiled; see PROJECT_STATUS.md. Companies/memberships have no API yet (Phase 4).

### Phase 2 — Persistence and Identity (2026-10-22)

#### Added
- EF Core 8 / SQL Server `AppDbContext` (Identity + all domain aggregates) with explicit configurations: owned value
  objects, enums as strings, `decimal(18,4)` money, UTC `DateTime` converters, JSON `ShipmentEvent.Metadata`,
  `RowVersion` concurrency token on `Shipment`, unique/filtered indexes as database backstops (one accepted quote per
  shipment, one live quote per company per shipment, one active membership, one active assignment per shipment,
  vehicle plate and driver licence per company), restrict-by-default foreign keys.
- ASP.NET Core Identity (`Guid` keys), `POST /api/auth/register` (Customer only, no role input), `POST /api/auth/login`,
  `GET /api/auth/me`, JWT bearer (HS256, strict validation, 15 min), lockout (5 attempts / 15 min), role seeding.
- Policies `RequireCustomer|CompanyAdmin|Dispatcher|Driver|CompanyStaff|PlatformAdmin` and a secure-by-default fallback
  policy (every endpoint needs a token unless `[AllowAnonymous]`).
- FluentValidation request validation; `ProblemMapper` + `GlobalExceptionHandler` map validation, auth, conflict and domain
  errors to RFC 7807 (domain codes -> 400/403/404/409/422); unknown errors stay a leak-free 500.
- Rate limiting for auth endpoints, configurable CORS allow-list, Swagger/OpenAPI with bearer scheme,
  `/health` (liveness) and `/health/ready` (database).
- `DatabaseInitializer` (migrate in Development, idempotent role seeding, Development-only demo users with a password
  from configuration), design-time DbContext factory.
- Tests: Application (auth validators/handlers, composition), API integration (JWT boundary, policy matrix, anonymous
  endpoint allow-list, validation shape, rate limit, CORS, Swagger, mapper) and SQL Server tests (skipped without
  `SHIPFLOW_TEST_SQLSERVER`): mapping round-trips, constraints, concurrency, auth flow, lockout, seeding.

#### Changed
- Domain: private parameterless constructors on entities (ORM materialization only). No ORM types in Domain.
- `/health` is now pure liveness; readiness is `/health/ready`.

#### Not done / caveats
- **No EF migration is committed** (cannot run `dotnet ef` without NuGet); command in `docs/SETUP.md`.
- **Nothing that needs NuGet packages was compiled or run** in the authoring session. See PROJECT_STATUS.md.

### Phase 1 — Domain (2026-10-22)

#### Added
- Shipment lifecycle, cancellation and failed-delivery rules documented in `docs/ARCHITECTURE.md`.
- Domain model in `ShipFlow.Domain` (no external references): value objects `Money`, `GeoPoint`, `Capacity`, `Cargo`,
  `TimeWindow`; aggregates `Shipment` (stops, quotes, events), `TransportCompany` (memberships); `Driver`, `Vehicle`,
  `Assignment`, `TrackingPoint`, `DeliveryProof`; `ShipmentLifecycle` transition table; `DomainException` with stable codes.
- 185 domain/composition tests, including `.csproj`-based architecture checks that replace the weak reflection checks.

#### Fixed
- `Shipment.RetryDelivery` and `StartTransit` could be called from each other's source state; each now requires its own.
- Architecture tests also locate the repository from the working directory when run outside the repo tree.

#### Notes
- Verified with a throwaway xunit-compatible shim, not real xunit (NuGet still blocked). See PROJECT_STATUS.
- Domain limitations deferred to later phases are listed in `docs/ARCHITECTURE.md`.

### Phase 0 — Foundation (2026-10-21)

#### Added
- Solution `ShipFlow.sln` with projects: `ShipFlow.Domain`, `ShipFlow.Application`, `ShipFlow.Infrastructure`, `ShipFlow.API`,
  and test projects `ShipFlow.Domain.Tests`, `ShipFlow.Application.Tests`, `ShipFlow.API.IntegrationTests`.
- Clean Architecture project references and DI extension methods `AddApplication` / `AddInfrastructure`.
- `GET /health` endpoint (JSON, no internal details).
- `GlobalExceptionHandler` returning RFC 7807 problem responses.
- Initial tests: layering/architecture, DI composition, health endpoint (xunit; not yet executed — see PROJECT_STATUS).
- `Directory.Build.props`, `global.json`, `.gitignore`, `.editorconfig`, `.env.example`, placeholder `docker-compose.yml`.
- Documentation: README, ARCHITECTURE, DECISIONS, API_CONTRACTS, TESTING, SETUP, PROJECT_STATUS, IMPLEMENTATION_PLAN.

#### Notes
- Installed .NET SDK 8.0.132 via apt because none was present.
- nuget.org was unreachable from the authoring environment; Swagger and test-package restore are pending.
