# ShipFlow — Project Status

**Last updated:** 2026-10-24
**Current phase:** Phase 4 — Fleet and Dispatching, built on Phases 2-3 (**Domain and Application verified offline only; Infrastructure, API host and integration tests NEVER compiled or run — see "Verification"**)
**Next phase:** Phase 5 — Tracking and Delivery. **Do "First actions" below on a machine with NuGet, Docker/SQL Server before it.**

## First actions (required, in order)

1. `dotnet restore && dotnet build ShipFlow.sln` — fix compile errors in the files listed under "Never compiled".
2. `dotnet ef migrations add InitialCreate -p src/ShipFlow.Infrastructure -s src/ShipFlow.API -o Persistence/Migrations` (docs/SETUP.md). Then switch `SqlServerDatabaseFixture` to `MigrateAsync()`.
3. `dotnet test ShipFlow.sln` (real xunit has never run in any session).
4. Start SQL Server, set `SHIPFLOW_TEST_SQLSERVER`, run again. SQL tests are *skipped* without it. Most important: `FleetAndDispatchingSqlServerTests.Race_*`.

## Step 0 of the Phase 4 session (what actually happened)

Same blocker as before: `api.nuget.org` answers 403 through the egress proxy (also nuget.org, MCR, packages.microsoft.com), and there is no Docker daemon, so steps 3-6 of the
brief (restore, build, `InitialCreate`, `dotnet test`, SQL Server runs, fixing EF defects) **could not be done**. Only the offline harness was possible
(xunit shim + FluentValidation stand-in, outside the repo). Baseline before any change: Domain 184/184, Application 60/60.

## What Phase 4 adds

Company onboarding/membership with Identity roles kept consistent (D-023, D-024), drivers and vehicles, availability, assign/unassign, member removal deactivating the driver,
and the locking transaction against double booking (D-025). Endpoints in docs/API_CONTRACTS.md; design in docs/ARCHITECTURE.md ("Fleet, dispatching and concurrency") and docs/DECISIONS.md D-023..D-028.

## Verification (actual)

| Check | Result |
|---|---|
| NuGet restore, `dotnet build ShipFlow.sln`, `dotnet test ShipFlow.sln` | **NOT POSSIBLE this session (NuGet 403)** |
| EF migration `InitialCreate` | **NOT GENERATED** |
| SQL Server available | **No** (no server, no Docker daemon) |
| Domain build (real csproj), warnings-as-errors | 0 warnings, 0 errors |
| Domain tests under the xunit shim | **188 passed, 0 failed** (184 + 4 new) |
| Application source (Phase 2-4) built against a working FluentValidation stand-in, warnings-as-errors | 0 warnings, 0 errors (validators run against the stand-in, **not** real FluentValidation) |
| Application tests under the shim | **95 passed, 0 failed** (60 + 35 new); race tests repeated 6x, stable |
| API controllers/auth/middleware/config (all but `Program.cs`, `SwaggerSetup`) compiled against that Application build, warnings-as-errors | 0 warnings, 0 errors |
| Infrastructure (all, incl. Phase 4 stores, `FleetTransaction`, `UserDirectory`, `MembershipRoles`, seed) | **NEVER COMPILED** |
| `Program.cs`, `SwaggerSetup`, all `API.IntegrationTests` incl. `FleetAndDispatchingSqlServerTests` | **NEVER COMPILED, NEVER RUN** |
| Real xunit | **NOT RUN** |
| Concurrency safety on SQL Server | **UNPROVEN.** Handler logic is shown correct under an in-memory lock with the same contract (8 parallel -> exactly 1 wins; control run without the lock double-books). `sp_getapplock` behaviour, the SQL race tests and rowversion behaviour have not run. **No claim of concurrency safety is made.** |
| Coverage | Not measured |

**Never compiled:** everything in `src/ShipFlow.Infrastructure`, `Program.cs`, `SwaggerSetup`, and every file in `tests/ShipFlow.API.IntegrationTests` except `ProblemMapperTests`.
Highest-risk spots (EF behaviour): owned-record constructor binding, **replacing the owned `Vehicle.Capacity` instance on update**, `IReadOnlyList` navigations with backing fields, the `NormalizedEmail` index override,
the `ShipmentEvent.Metadata` converter, the list projection (Stops subqueries), `Include` of memberships on an untracked query, the `sp_getapplock` command (return-value parameter, `GetDbTransaction`, `ExecuteAsync(Func<Task>)`),
staging `IdentityUserRole<Guid>` rows on the shared context, and the two-simultaneous-accepts / double-booking race tests.

## Known issues / limitations

1. No committed migration. The new index `UX_CompanyMemberships_User_Active` must be in it.
2. Tokens: 15 min, no refresh or revocation (D-017). A user who gains a role (company creator, new member) must sign in again; a removed member's old claim is harmless because handlers read the DB (D-023).
3. Duplicate-email registration reveals existence (409); adding a member by email reveals to company admins whether an email is registered (404 vs success).
4. Rate limiting keys on remote IP (Phase 9).
5. EF retry-on-failure is still off; `FleetTransaction` already uses the execution-strategy pattern.
6. Overlap protection exists only for writers that use `FleetTransaction`; the DB has no declarative backstop for overlapping windows (D-025 limits).
7. One active company per user (D-024); a company admin cannot be added through the API (the creator is the admin) and cannot be demoted; there is no company deactivation endpoint.
8. A driver/vehicle with an Active assignment cannot be deactivated or removed until it is unassigned (or, for in-flight shipments, until Phase 5 completes it) (D-026).
9. A user holding both Customer and a company role can quote on their own shipment (no self-dealing rule yet).
10. Phase 1 domain limitations still open for Phase 5: proof is not required to deliver; `Assignment.Complete` is never called yet.
11. No git repository initialised.

## Earlier phases (summary)

### Phase 3

Shipment create/get/list (paging, filters, scopes), open-for-quotes, cancel (releases assignment), quote submit/list/withdraw/accept,
`Viewer`-based authorization with 404 for hidden resources, EF stores, `UnitOfWork` (409 on races). See docs/API_CONTRACTS.md.

## What Phase 2 contains

- **Domain:** private parameterless constructors on entities (ORM materialization only). No ORM types in Domain.
- **Application:** auth features (Register, Login) with FluentValidation, `IIdentityService`, `ITokenService`, `ICurrentUser`,
  roles/policies/password-policy constants, `AppException` family. Plain handler classes, no mediator.
- **Infrastructure:** `AppDbContext` (Identity + all aggregates) and entity configurations, UTC converters, JSON metadata,
  rowversion on Shipment, filtered unique indexes as DB backstops, `IdentityService` (lockout, inactive users, constant-ish timing),
  `JwtTokenService`, JWT bearer wiring, `DatabaseInitializer` (dev migrate/EnsureCreated, role seed, dev-only demo users), DB health check.
- **API:** `POST /api/auth/register|login`, `GET /api/auth/me`; role policies + fallback "authenticated" policy; rate limiting
  on auth; CORS allow-list; Swagger with bearer; `ProblemMapper` + `GlobalExceptionHandler`; `/health` (liveness), `/health/ready` (DB).

(Older verification tables were superseded by the one above.)
