# ShipFlow — Implementation Plan

Legend: ✅ done and verified · 🟡 done with caveats · ⬜ not started

| Phase | Scope | Status |
|---|---|---|
| 0 | Foundation: solution, projects, DI, `/health`, tests scaffold, docs, checkpoints | 🟡 src build + `/health` verified; xunit projects never restored (NuGet blocked) |
| 1 | Domain: entities, value objects, shipment lifecycle, invariants, unit tests | 🟡 185 tests pass under an xunit shim; not yet run with real xunit |
| 2 | Persistence and Identity: SQL Server, EF Core, migrations, Identity, JWT, roles, policies | 🟡 code written; only framework-only parts compiled; **no migration generated; packages never restored** |
| 3 | Shipment and Quotation APIs: pagination, filtering, quote submit/accept, cancellation | 🟡 handlers verified offline (30 tests); EF stores/controller/SQL tests never compiled |
| 4 | Fleet and Dispatching: companies, drivers, vehicles, assignment, concurrency | 🟡 Domain 188 + Application 95 tests pass offline (shim); Infrastructure/API host/SQL tests never compiled or run; concurrency unproven on SQL Server |
| 5 | Tracking and Delivery: events, tracking, driver updates, delivery proof, timelines | ⬜ |
| 6 | Real-time and Notifications: SignalR, notification abstractions, background processing | ⬜ |
| 7 | Advanced Reliability: idempotency, outbox, retries, optional Redis/RabbitMQ | ⬜ |
| 8 | Quality and Observability: tests, logging, health checks, metrics, perf tests | ⬜ |
| 9 | Deployment and Handoff: Docker, CI, config templates, migration docs, README | ⬜ |

## Carry-over into Phase 5 (do first, on a machine with NuGet and Docker)

- [ ] `dotnet restore && dotnet build ShipFlow.sln`; fix compile errors in: all of `Infrastructure/Persistence/*` (incl. Phase 4 `FleetStores`, `FleetTransaction`, `Identity/UserDirectory.cs`), `Program.cs`, `SwaggerSetup`, all `API.IntegrationTests`.
- [ ] Generate `InitialCreate` (must contain `UX_CompanyMemberships_User_Active`); switch `SqlServerDatabaseFixture` to `MigrateAsync`.
- [ ] `dotnet test` with `SHIPFLOW_TEST_SQLSERVER` set. Priorities: `FleetAndDispatchingSqlServerTests.Race_*` (proves or disproves D-025), the double-accept race, owned-type replacement in `UpdateCapacity`, list projection translation.
- [ ] Verify the `sp_getapplock` call in `FleetTransaction.AcquireAsync` (return-value parameter, `GetDbTransaction`, execution-strategy overload).

## Phase 4 delivered

Companies, members, drivers, vehicles, availability, assign/unassign, locking transaction, member removal deactivates driver. See CHANGELOG.md.

## Backlog from Phase 1 review

- ~~Release the `Assignment` on cancel~~ done in Phase 3.
- ~~Deactivate `Driver` when the company membership ends~~ done in Phase 4.
- Phase 5: complete assignments (`Assignment.Complete`) on delivery; an Active assignment blocks driver/vehicle deactivation until then (D-026).
- Phase 5: decide whether `Deliver` must require a `DeliveryProof`.
