# Architecture Decision Log

| ID | Date | Decision | Status |
|---|---|---|---|
| D-001 | 2026-10-21 | Target .NET 8 (SDK 8.0.132), per the baseline in the master prompt | Accepted |
| D-002 | 2026-10-21 | Clean Architecture + Modular Monolith with the four-project layout | Accepted |
| D-003 | 2026-10-21 | `global.json` pins SDK 8.0.x with `rollForward: latestFeature` | Accepted |
| D-004 | 2026-10-21 | Application/Infrastructure use `FrameworkReference Microsoft.AspNetCore.App` for DI/config abstractions | Accepted (revisitable) |
| D-005 | 2026-10-21 | Warnings-as-errors for `src/`; relaxed for `tests/` | Accepted |
| D-006 | 2026-10-21 | Health endpoint uses built-in health checks with a minimal JSON writer | Accepted |
| D-007 | 2026-10-21 | HTTPS redirection/HSTS deferred; TLS termination handled in the deployment phase | Accepted |
| D-008 | 2026-10-21 | Swagger deferred until NuGet is reachable | Superseded in Phase 2 (Swashbuckle 6.9.0 added) |
| D-009 | 2026-10-22 | Users referenced by `Guid` in the domain; Identity (Phase 2) will use `Guid` keys | Accepted |
| D-010 | 2026-10-22 | Quotes and assignment history live inside/next to `Shipment`; `Assignment`, `TrackingPoint` are separate roots | Accepted |
| D-011 | 2026-10-22 | Domain failures are `DomainException` with stable string codes, mapped to HTTP in the API | Accepted |
| D-012 | 2026-10-22 | Domain methods take `nowUtc` explicitly (UTC `DateTime`, kind validated) instead of reading a clock | Accepted |
| D-013 | 2026-10-22 | EF Core 8.0.11 (SqlServer, Identity.EntityFrameworkCore) on net8.0; JwtBearer 8.0.11 | Accepted |
| D-014 | 2026-10-22 | Domain stays ORM-free: private parameterless ctors, shadow `RowVersion`, backing-field collections | Accepted |
| D-015 | 2026-10-22 | All keys `ValueGeneratedNever`; all non-aggregate-child FKs `Restrict` | Accepted |
| D-016 | 2026-10-22 | Plain handler classes + FluentValidation; no MediatR (commercial licence, no need yet) | Accepted |
| D-017 | 2026-10-22 | Access token only (15 min); refresh tokens deferred | Accepted (revisit) |
| D-018 | 2026-10-22 | Configuration read lazily via options (no eager reads at registration) | Accepted |
| D-019 | 2026-10-22 | Secure-by-default authorization fallback policy; anonymous endpoints are an explicit, tested list | Accepted |
| D-020 | 2026-10-22 | Persistence tests run on real SQL Server via `SHIPFLOW_TEST_SQLSERVER`; skipped otherwise | Accepted |

## D-001 .NET 8
The master prompt sets .NET 8 as the baseline. The only SDK obtainable in the authoring environment was 8.0.132 from the
Ubuntu archive, which matches.

## D-004 FrameworkReference for libraries
Context: `Microsoft.Extensions.DependencyInjection.Abstractions` and `IConfiguration` are not in the base
`Microsoft.NETCore.App` runtime; they ship in the ASP.NET Core shared framework. NuGet was unreachable, so package
references could not be restored.
Decision: reference the shared framework from Application and Infrastructure.
Consequences: avoids extra packages and version drift against the runtime. It does make those libraries ASP.NET-flavored;
Domain remains free of it (enforced by tests). Infrastructure needs ASP.NET Core anyway (Identity, JWT). Revisit if
Application should be host-agnostic.

## D-007 HTTPS
`UseHttpsRedirection` was removed from the template pipeline because no HTTPS port is configured for local/test runs.
Production TLS/HSTS policy is to be defined with deployment (Phase 9).

## D-008 Swagger
Swashbuckle (or equivalent) requires NuGet. It is required for the MVP definition of done and will be added in the first
phase run with package access.

## D-009 User ids
The domain stores `CustomerId`, actor ids etc. as `Guid`. Phase 2 configures ASP.NET Core Identity with `Guid` keys so no conversion is needed.

## D-010 Aggregate boundaries
"One accepted quote per shipment" is a single-aggregate invariant, so quotes are inside `Shipment` (persistence adds a row-version token).
`Assignment` is its own root because conflicts span shipments; `TrackingPoint` is separate because it is high volume.
See docs/ARCHITECTURE.md for the table.

## D-011 Domain errors
`DomainException.Code` is a stable identifier (`validation`, `invalid_transition`, `not_owner`, `cross_company`, `capacity_exceeded`,
`assignment_conflict`, `quote_rule`, `invalid_state`, `not_found`). Messages are safe to show; the API maps codes to RFC 7807 in Phase 2/3.

## D-012 Time
Time comes in as a parameter (`nowUtc`), which keeps the domain deterministic in tests. The Application layer supplies it from `TimeProvider`.

## D-014 Domain vs ORM
EF cannot bind constructors that take owned value objects, so each entity has a private parameterless constructor used only for
materialization. Everything else (private setters, get-only properties, `_stops/_quotes/_events/_memberships`) is mapped by
backing field. The concurrency token is a shadow property so no ORM concept enters Domain.

## D-015 Keys and delete behavior
Guid keys are generated in the domain. With the default `ValueGeneratedOnAdd`, EF treats an entity with a preset key that is
added to a tracked aggregate as an UPDATE; `ValueGeneratedNever` makes it an INSERT (covered by a SQL Server test).
Only aggregate children cascade (Shipment -> stops/quotes/events, Company -> memberships). `AcceptedQuoteId` and
`CurrentAssignmentId` have no FK (circular dependency); `ShipmentEvent.ActorId` has none (user or company id).

## D-016 No mediator
Controllers call feature handlers directly. Cross-cutting behavior (validation) is explicit in each handler.

## D-017 Tokens
Short-lived access token only. Revocation/refresh is not implemented; a deactivated user keeps a valid token until it expires
(max 15 min). Decide on refresh tokens before the MVP is called done.

## D-018 Lazy configuration
`WebApplicationFactory` overrides apply after `Program` registers services, so eager `configuration[...]` reads would ignore
them. DbContext, JWT, CORS and rate limits read configuration when first used.

## D-019 Authorization
Roles say what kind of user the caller is. Company membership and ownership are never taken from token or request; they are
resolved from the database in use cases (Phases 3-5).

| D-020 | 2026-10-23 | Resource authorization in handlers via `Viewer` (membership read from DB per request); non-visible resources return 404 | Accepted |
| D-021 | 2026-10-23 | Narrow per-use-case persistence ports instead of generic repositories or DbContext in Application | Accepted |
| D-022 | 2026-10-23 | No generic status endpoint; transitions only via named actions (open-for-quotes, cancel, accept) | Accepted |
| D-023 | 2026-10-24 | Identity roles are derived from memberships and written in the same commit; handlers trust the database, tokens are only a coarse gate | Accepted |
| D-024 | 2026-10-24 | A user has at most one active company membership (unique filtered index on `UserId`) | Accepted |
| D-025 | 2026-10-24 | Double-booking protection = one transaction with named exclusive locks (`sp_getapplock`) + re-check; no SERIALIZABLE, no index trick | Accepted (unverified on SQL Server) |
| D-026 | 2026-10-24 | Deactivating/removing a driver or vehicle with an Active assignment is refused (409); capacity may only shrink when unassigned | Accepted |
| D-027 | 2026-10-24 | Unique-index violations answer 409 `conflict` with a safe, index-specific message (`UnitOfWork`); rowversion losses answer 409 `concurrency_conflict` | Accepted |
| D-028 | 2026-10-24 | Company routes are `/me`-relative; there is never a company id in a company, fleet or dispatch request | Accepted |

## D-023 Roles follow memberships
Memberships (`CompanyMembership`) are the source of truth. The Identity role (`CompanyAdmin`, `Dispatcher`, `Driver`) in the token is
derived: `CompanyRoleMapping` maps `Admin/Dispatcher/Driver` to the Identity role. When a company is created, a member added or
removed, `IMembershipRoles` **stages** the `AspNetUserRoles` row on the shared DbContext and `IUnitOfWork.SaveChangesAsync` writes it
in the SAME transaction as the membership, so they never disagree in the database. `UserManager` is not used for this because it
saves immediately (two commits). `Customer` is never removed.
Tokens last 15 minutes and are not refreshed (D-017), so: a user who gains a role must **sign in again** to get it in a token;
a user who loses a role keeps the old claim for up to 15 minutes, but that claim is useless: every Phase 4 handler resolves the
membership from the database (`ViewerResolver`) and answers 403/404 without it. The claim is only the coarse gate in front of the
handler. Removing a member is therefore effective immediately.
Not solved here: a user deactivated in Identity keeps a token for <= 15 min (creating a company re-checks `IsActive`).

## D-024 One company per user
`ICompanyAccess` returns one membership, the Viewer has one company, roles are not per company. So a user may hold only one
active membership. Handlers pre-check (409 with a clear message); `UX_CompanyMemberships_User_Active` (filtered unique index on
`UserId WHERE IsActive = 1`) catches the race. Former memberships are kept as history and the user can join again.

## D-025 Concurrency strategy for driver/vehicle bookings
Problem: `Assignment.Create` checks overlap in memory only; two requests can both read "no conflict" and both insert. An overlap
of `[start,end)` windows cannot be a unique index.
Options weighed:
- **SERIALIZABLE** (or `HOLDLOCK`) range locks: correctness depends on the optimizer choosing the intended index, and two
  competitors take shared range locks first and then deadlock (one is killed with error 1205), so the "loser" is a retry storm.
- **UPDLOCK on the driver/vehicle row**: sound, but needs the row to exist and only covers those tables.
- **`sp_getapplock` per resource (chosen)**: exclusive, transaction-scoped named locks: independent of query plans and isolation
  level, work for rows that do not exist yet (member locks), have a built-in timeout, and the loser simply waits and then sees the
  winner's committed row.
Implementation (`FleetTransaction`, port `IFleetTransaction`): inside `CreateExecutionStrategy().ExecuteAsync` -> clear change tracker
-> begin transaction -> acquire locks in one global order (names `shipflow:<kind>:<guid>`, sorted ordinally: no lock cycles) ->
run the handler's work (which RE-READS shipment, driver, vehicle and overlapping assignments, runs `Assignment.Create`, inserts, saves)
-> commit. Wait limit 10 s; timeout (-1), deadlock victim (-3 / 1205) and lock timeout (1222) answer 409 `concurrency_conflict`.
Also used by: driver activate/deactivate, vehicle capacity/activate/deactivate, member removal, driver registration, so a
deactivation or removal can never interleave with a booking of the same resource.
Defence in depth: rowversion on `Shipment` (two assigns of the same shipment, assign vs cancel), filtered unique index
`UX_Assignments_Shipment_Active`.
**Limits:** (1) only code using this protocol is protected; the database does not reject an overlapping row inserted by other means
(manual SQL, a future writer that forgets the lock). (2) Locks live in one SQL Server database; fine for a single database, not a
substitute for a distributed lock. (3) READ COMMITTED snapshot or default both work because the re-read happens after the locks; a
transaction-wide SNAPSHOT isolation level would break the guarantee and must not be configured for this context. (4) The 10 s wait
holds a connection, so heavy contention on one driver queues requests. (5) **This design has only been exercised against an in-memory
lock fake so far, not against SQL Server** (no server was available); see docs/TESTING.md.
Execution strategy: retry-on-failure is still off. The only user transaction is already wrapped in the strategy, so enabling retries
later is safe for it; every handler `work` callback is written to be re-runnable.

## D-026 Active bookings block removal
Releasing a booking of an in-flight shipment is impossible (only Assigned goes back to Accepted), so instead of orphaning or
silently releasing, removing a member whose driver has an Active assignment, deactivating such a driver/vehicle, or shrinking such a
vehicle is refused with 409; unassign first. "Active" ignores the window (a completed delivery is closed in Phase 5).

## D-027 Unique violations
`UnitOfWork` maps SQL 2601/2627 to `ConflictException` (code `conflict`) with a message chosen by index name
(plate, licence, driver-user, membership, assignment); unknown indexes get a generic message. SQL Server text is never returned.

## D-028 No company ids in requests
`/api/companies/me/...`, `/api/fleet/...`, and `/api/shipments/{id}/assignment` derive the company from the caller's membership. IDs in
routes/bodies (driver, vehicle, user) are always looked up inside that company, so another tenant's id is "not found" (404) and no lock
is ever taken on another tenant's rows (pre-checks run before locking).

