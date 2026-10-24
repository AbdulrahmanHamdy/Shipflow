# Testing

## Strategy

| Level | Project | Scope |
|---|---|---|
| Unit | ShipFlow.Domain.Tests | Domain invariants, lifecycle transitions, quote and assignment rules, value objects, architecture rules |
| Unit | ShipFlow.Application.Tests | Use-case logic, validation, composition (`ValidateOnBuild`) |
| Integration | ShipFlow.API.IntegrationTests | HTTP, authorization, persistence |

Planned: relational integration tests against a real SQL Server (container) rather than EF Core InMemory, so transactions,
constraints and concurrent bookings are actually exercised. InMemory is not used to claim SQL Server behavior.

## Domain tests (Phase 1)

Scenarios are built with `Support/Fixture`, which advances a shipment to any status using only the public domain API.

| File / class | Covers |
|---|---|
| `ValueObjectTests` | Money, GeoPoint, Capacity/Cargo, TimeWindow (half-open overlap, UTC) |
| `ShipmentCreationTests` | Stop count/types, pickup window, creation event, ownership |
| `ShipmentLifecycleTests` | Every status pair checked against an independent copy of the documented table; terminal states; reachability |
| `ShipmentTransitionTests` | Progress transitions, failed delivery, retry, cancellation by customer/carrier/platform, rejected transitions leave state and events unchanged |
| `QuoteTests` | Submission rules (state, price, expiry, duplicates), acceptance (owner only, one winner, competitors rejected, expired/withdrawn) |
| `AssignmentTests` | Eligibility (state, cross-company, inactive, capacity at/over the limit), driver/vehicle window conflicts, back-to-back windows, release/unassign/complete |
| `CompanyTests` | Membership uniqueness, last-admin protection, driver/vehicle registration rules |
| `TrackingTests` | Tracking-point and delivery-proof state/time/validation rules |
| `DomainArchitectureTests` | Reads the `.csproj` files: Domain has no references; Application/Infrastructure/API reference only what the layering allows; no `using` of framework namespaces in Domain |

## Verification status (honest)

- Real xunit has **never** run in this project: nuget.org is blocked in the authoring environment.
- In Phase 1 the real test source files (Domain.Tests and Application.Tests) were compiled against a throwaway
  xunit-compatible shim (`Fact`, `Theory`, `InlineData`, the `Assert` methods used) and executed:
  **185 passed, 0 failed**. The shim lives outside the repository.
- That run found one real defect (`RetryDelivery`/`StartTransit` accepting each other's source state), which was fixed and
  now has regression tests.
- `HealthEndpointTests` (needs `Microsoft.AspNetCore.Mvc.Testing`) were not run in Phase 1; `/health` was verified with curl in Phase 0.
- No coverage numbers exist; none are claimed. Concurrency safety is not tested and not claimed.

## Phase 2 additions

- `ShipFlow.Application.Tests/Auth`: validators and handlers with fakes.
- `ShipFlow.API.IntegrationTests`: no-database tests (JWT rejection matrix, policy matrix, anonymous-endpoint allow-list, validation
  shape, rate limiting, CORS, Swagger, `ProblemMapper`) and SQL Server tests (`[SqlServerFact]`).
- SQL Server tests need `SHIPFLOW_TEST_SQLSERVER` (a connection string whose login can create/drop databases; each test class
  gets a throwaway `ShipFlow_Test_<guid>` database). Without it they are **skipped**. EF InMemory is deliberately not used.
- The fixture uses `EnsureCreated` until `InitialCreate` exists; then switch it to `MigrateAsync`.
- Authoring-session result: Domain 184/184 and `ProblemMapperTests` 6/6 under the shim; everything needing NuGet packages
  (FluentValidation, EF, Mvc.Testing, xunit) was **not compiled or run**.

## Running

```bash
dotnet test ShipFlow.sln
```

## Phase 3 tests

- `ShipFlow.Application.Tests/Shipments`: 30 handler tests against in-memory ports (ownership, tenant isolation, quote visibility, company
  attribution from membership, list scoping, cancellation by capacity, assignment release, expired/duplicate acceptance, concurrency surfacing).
  Handlers were run offline with a permissive stand-in for FluentValidation, so these prove handler logic, **not** the validators.
- `ShipFlow.API.IntegrationTests/Api/ShipmentsSqlServerTests` (needs `SHIPFLOW_TEST_SQLSERVER`): full flow, cross-tenant 404s, role matrix,
  paging/filtering, validation, no generic status update, and the **two simultaneous acceptances** test. Not run yet: no claim of concurrency safety until it passes on a real server.

## Phase 4 tests

| Where | What |
|---|---|
| `Domain.Tests/CompanyTests` | `ActivateDriver`: needs an active Driver membership, same company, cross-company rejected (4 new tests; Domain total 188) |
| `Application.Tests/Dispatching/DispatchingTests` | assign (state, tenant 404s, capacity 422, inactive, overlap vs back-to-back, window validation), unassign/re-assign, availability filters, cancel frees resources, **race tests** |
| `Application.Tests/Dispatching/CompanyAndFleetHandlerTests` | company creation + role grant, member add/remove (role revoked, driver deactivated, active booking blocks, last admin), driver/vehicle registration and validation, capacity rules, tenant 404s |
| `API.IntegrationTests/Api/FleetAndDispatchingSqlServerTests` | **SQL Server, HTTP:** tenant isolation, role boundaries (incl. stale token after removal), eligibility, windows (overlap 409, back-to-back 201, availability agrees), unassign, cancel releases, duplicate plate/licence, capacity update persistence, real register/login onboarding with Identity-role consistency, member removal, and 4 race tests |

The Application race tests use `FakeFleetTransaction` (a real per-resource lock with the same contract as `FleetTransaction`) and an assignment store that yields during the
overlap read so requests truly interleave. They prove the HANDLER is correct when the locks hold: 8 parallel requests for one driver -> exactly 1 wins, 7 get
`assignment_conflict`; same for one vehicle; independent resources all succeed; and a **control run with the lock disabled double-books** (so the scenario is a real race).
They do NOT prove anything about SQL Server or `sp_getapplock`. That is the job of the four `Race_*` SQL tests (8 parallel HTTP requests -> exactly one 201, the rest 409, one
active row in the table; partially overlapping windows never overlap in the table; two requests for the same shipment -> one wins).

### Actual results of the Phase 4 authoring session (2026-10-24)

- NuGet blocked (403), no Docker daemon, no SQL Server: **`dotnet restore/build/test` of the solution did NOT run; no migration was generated; no SQL Server test ran.**
- Offline harness (xunit shim + FluentValidation stand-in; not the real packages), real source files: Domain build 0 warnings/0 errors (warnings-as-errors);
  Domain tests **188 passed, 0 failed**; Application source built with warnings-as-errors; Application tests **95 passed, 0 failed** (60 before + 35 new); API controllers/auth/
  middleware/config (everything except `Program.cs` and Swagger) compiled with warnings-as-errors against that Application build.
- Race tests repeated 6 times: 5/5 each time.
- **Never compiled:** all of `Infrastructure` (including the new stores, `FleetTransaction`, `UserDirectory`, `MembershipRoles`, seed), `Program.cs`, `SwaggerSetup`, and every
  `API.IntegrationTests` file (including `FleetAndDispatchingSqlServerTests`). **Never run:** any SQL Server test. The concurrency design is therefore **unproven on SQL Server**.
- Coverage: not measured.

