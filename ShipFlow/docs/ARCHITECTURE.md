# Architecture

ShipFlow is a Modular Monolith organized with Clean Architecture. This document describes what exists as of Phase 1
and records the intended direction; sections marked *Planned* are not implemented.

## Layers and dependency rule

```
API ──► Application ──► Domain
 │          ▲
 └► Infrastructure ──► (Application, Domain)
```

| Project | Responsibility | May reference |
|---|---|---|
| ShipFlow.Domain | Entities, value objects, domain rules, lifecycle | nothing |
| ShipFlow.Application | Use cases organized by feature, abstractions (interfaces), validation, behaviors | Domain |
| ShipFlow.Infrastructure | EF Core, Identity, JWT, external integrations (implements Application interfaces) | Application, Domain |
| ShipFlow.API | Composition root, controllers, middleware, auth wiring | Application, Infrastructure |

Business rules live in Domain/Application, never in controllers. Controllers translate HTTP to use-case calls.

## Application organization (Planned, per phase)

Feature folders (`Features/Shipments/CreateShipment`, `Features/Quotations/AcceptQuote`, ...) plus `Common/`
(Interfaces, Models, Exceptions, Behaviors). No generic repository per entity; persistence abstractions are introduced
only where a use case needs them.

## Implemented in Phase 0

- Composition root (`ShipFlow.API/Program.cs`): `AddApplication()`, `AddInfrastructure(configuration)`, controllers,
  ProblemDetails, global exception handler, health checks.
- `GET /health` — JSON `{ status, timestampUtc }`. No dependency checks yet (database check arrives with Phase 2).
- `GlobalExceptionHandler` — logs unhandled exceptions and returns a generic RFC 7807 500 with a `traceId`.
- `TimeProvider.System` registered for injectable UTC time.

## Shipment lifecycle (designed in Phase 1; implemented in `ShipFlow.Domain`)

Generic status-update endpoints are not permitted. `Shipment.Status` has a private setter; every transition goes
through a named domain method, is validated against the transition table below, and appends a `ShipmentEvent`
(actor, UTC timestamp, previous status, new status, metadata).

### States

| State | Meaning | Terminal |
|---|---|---|
| Pending | Created by the customer, not yet open for quotes | no |
| Quoting | Open: transport companies may submit quotes | no |
| Accepted | Customer accepted one quote; a carrier company is fixed | no |
| Assigned | Dispatcher assigned a driver + vehicle (an active `Assignment` exists) | no |
| PickedUp | Driver picked the cargo up | no |
| InTransit | Cargo is moving to the destination | no |
| DeliveryFailed | A delivery attempt failed (recipient absent, refused, ...) | no |
| Delivered | Delivered successfully | **yes** |
| Cancelled | Cancelled before completion | **yes** |

### Allowed transitions (exhaustive; anything else is rejected)

```
Pending        → Quoting | Cancelled
Quoting        → Accepted | Cancelled
Accepted       → Assigned | Cancelled
Assigned       → Accepted (unassign, for re-assignment) | PickedUp | Cancelled
PickedUp       → InTransit | DeliveryFailed
InTransit      → Delivered | DeliveryFailed
DeliveryFailed → InTransit (retry delivery) | Cancelled (abandoned / returned)
Delivered      → (none)
Cancelled      → (none)
```

Main path: `Pending → Quoting → Accepted → Assigned → PickedUp → InTransit → Delivered`.

### Cancellation rules (who may cancel, from where)

| Initiator | Identity check | Allowed from |
|---|---|---|
| Customer | actor must be the shipment's owner (`CustomerId`) | Pending, Quoting, Accepted, Assigned |
| Carrier (transport company) | company must be the accepted carrier (`CarrierCompanyId`) | Accepted, Assigned, DeliveryFailed |
| Platform (admin) | authorization decided by the Application layer | any non-terminal state the table allows |

Once cargo is picked up the customer can no longer cancel; problems are expressed as `DeliveryFailed`. A reason is
mandatory. Cancelling clears `CurrentAssignmentId` (the id is recorded in the event metadata so the caller can release the
`Assignment` in the same transaction) and rejects any still-open quotes.

### Failed delivery

`PickedUp | InTransit → DeliveryFailed` (reason required). From there the carrier may retry (`→ InTransit`) or the shipment
may be cancelled (carrier or platform). Delivery failure never silently returns to an earlier state.

## Domain model (Phase 1)

### Aggregates and ownership

| Aggregate root | Owns (inside the aggregate) | References by id only |
|---|---|---|
| Shipment | ShipmentStop[], Quote[], ShipmentEvent[] | CustomerId, CarrierCompanyId, CurrentAssignmentId |
| TransportCompany | CompanyMembership[] | user ids |
| Driver | — | CompanyId, UserId |
| Vehicle | — | CompanyId |
| Assignment | — | ShipmentId, CompanyId, DriverId, VehicleId |
| TrackingPoint | — | ShipmentId (high volume: not loaded with the shipment) |
| DeliveryProof | — | ShipmentId |

Quotes live inside the Shipment aggregate so that "only one quote may be accepted per shipment" is a single-aggregate
invariant. Persistence (Phase 2/3) must add optimistic concurrency (row version) on Shipment so two concurrent acceptances
cannot both commit. `ApplicationUser` is an Identity concern (Phase 2); Domain refers to users by `Guid`.

### Ownership and tenant rules enforced in the domain

- Only the owning customer can open a shipment for quotes, accept a quote, or cancel as a customer.
- A quote is attributed to a company id supplied by the Application layer from the authenticated membership, never from client input.
- Drivers and vehicles are created through `TransportCompany.RegisterDriver/RegisterVehicle`; a driver must be an active
  member with the Driver role of an active company.
- `Assignment.Create` rejects cross-company combinations (shipment carrier, driver and vehicle must be the same company).
- A company always keeps at least one active CompanyAdmin.

### Rules the domain does NOT decide (Application/Infrastructure responsibility)

- Authenticating the caller and mapping the caller to a user/company id.
- Verifying that the dispatcher belongs to the carrier company.
- Loading the existing assignments to pass into conflict detection, and the database-level enforcement of no-overlap
  (Phase 4: transaction + concurrency control; SQL Server limitations to be documented then).

### Assignment conflicts

`Assignment.FindConflicts` is pure logic over the active assignments it is given. Time windows are half-open
`[Start, End)`: back-to-back windows do not conflict. The in-memory check is a first line of defense only; it does not
prevent two concurrent requests from both passing it.

### Known domain limitations (deliberate, to be resolved in the named phase)

- **Delivery proof is not enforced.** `Shipment.Deliver` does not require a `DeliveryProof`; `DeliveryProof.Create` is
  allowed while InTransit or Delivered. Phase 5 decides whether Delivered must require proof (and does it in the use case).
- **Cancelling does not release the `Assignment`.** `Shipment.Cancel` clears `CurrentAssignmentId` and records the id in
  the event metadata; the Application layer must call `Assignment.Release` in the same transaction (Phase 3/4).
- **Removing a company member does not deactivate their `Driver`.** `Assignment.Create` checks `Driver.IsActive`, not
  membership. The Application layer must deactivate the driver when the membership ends (Phase 4).
- **`Quote` company and `Cancel(Carrier)` ids are trusted inputs to the domain.** The Application layer derives them from
  the authenticated membership; the domain cannot verify who the caller is.
- `InTransit` is reachable from two states, so `StartTransit` (from PickedUp only) and `RetryDelivery` (from DeliveryFailed
  only) each guard their source state; otherwise the audit trail could record the wrong action.

### Conventions

- Money: `decimal`, at most 4 decimal places, ISO 4217 currency code. Times: UTC `DateTime` (kind must be `Utc`).
- Domain failures throw `DomainException` (with a stable `Code`); the API maps these in Phase 2/3.
- No `IsAvailable` flag: availability is derived from assignment windows.

## Cross-cutting (Planned)

Security (Identity, JWT, role- and resource-based authorization, tenant isolation), concurrency strategy for assignments
(Phase 4, with documented SQL Server limitations), idempotency and outbox (Phase 7), observability (Phase 8).

## Persistence mapping (Phase 2)

| Concern | Choice |
|---|---|
| Value objects | Owned types in the owner's table with explicit columns (`CargoWeightKg`, `PriceAmount`/`PriceCurrency`, `WindowStartUtc`...) |
| Money / quantities | `decimal(18,4)` money, `decimal(12,3)` weight/volume, `decimal(6,2)` speed |
| Enums | strings (<= 20 chars) |
| Times | `datetime2`, converter re-tags as UTC on read |
| `ShipmentEvent.Metadata` | JSON in `nvarchar(max)` with value comparer |
| Concurrency | shadow `rowversion` on `Shipment` (second concurrent writer gets `DbUpdateConcurrencyException`) |
| DB backstops | filtered unique indexes: accepted quote per shipment, submitted quote per (shipment, company), active membership, active assignment per shipment; unique plate / licence per company |
| Not enforceable by index | overlapping driver/vehicle windows: prevented by `FleetTransaction` (named locks + re-check), see "Fleet, dispatching and concurrency (Phase 4)" |
| Phase 4 index | `UX_CompanyMemberships_User_Active`: one active membership per user |

## Identity and authorization (Phase 2)

Identity (`Guid` keys) with `AddIdentityCore`; JWT HS256 with `sub`, `email`, `name`, `role`, `jti`; inbound claim mapping off.
Public registration can only create Customers (no role field exists). Fallback policy requires authentication everywhere;
anonymous endpoints: register, login, `/health`, `/health/ready`. Caveats: no refresh/revocation (D-017); rate limiting keys on
remote IP (configure forwarded headers behind a proxy in Phase 9); duplicate-email registration reveals existence (409).

## Shipment and quotation use cases (Phase 3)

- Persistence ports in Application (`IShipmentStore`, `IAssignmentStore`, `ICompanyAccess`, `IUnitOfWork`), implemented in
  `Infrastructure/Persistence/Stores`. No generic repository, no DbContext in Application.
- **Authorization = role policy at the controller + resource check in the handler.** `Viewer` is built per request from the token's
  user id and the company membership read from the database (a removed member loses access immediately). It decides view rights:
  owner, platform admin, carrier company, or any active company while the shipment is Quoting. Everyone else gets 404.
- Company ids, prices after acceptance and statuses are never taken from the client.
- **Quote acceptance is protected three ways:** (1) the aggregate allows one acceptance while Quoting; (2) rowversion on `Shipment`
  makes the second concurrent writer fail (`DbUpdateConcurrencyException` to `ConcurrencyConflictException` to 409); (3) filtered unique
  index `UX_Quotes_Shipment_Accepted`. Submitting a quote also touches `UpdatedAtUtc`, so a quote racing an accept conflicts too.
- Cancelling releases the active `Assignment` in the same commit (closes the Phase 1 backlog item).
- Not here yet: assigning drivers/vehicles (Phase 4), driver progress updates (Phase 5). Company onboarding/membership endpoints
  do not exist yet, so companies and memberships can only be created by seed/tests until Phase 4.

## Fleet, dispatching and concurrency (Phase 4)

**Flow of `POST /api/shipments/{id}/assignment`**

1. Policy `RequireDispatcher` (CompanyAdmin or Dispatcher token role) - coarse gate.
2. Validation (FluentValidation): ids, UTC window, end after start, at most 14 days.
3. `ViewerResolver`: membership from the DATABASE; must be Admin/Dispatcher, else 403.
4. Pre-checks outside the transaction (untracked reads): the shipment's carrier is the caller's company, the driver and vehicle belong
   to it. Otherwise 404. Nothing is locked yet, so another tenant's ids can never take a lock.
5. `IFleetTransaction.RunAsync(locks: driver, vehicle)`: transaction + `sp_getapplock` (exclusive, ordered, 10 s).
6. Inside: re-read shipment (tracked), driver, vehicle, and the active overlapping assignments of that driver OR vehicle;
   `Assignment.Create` (Accepted state, same company, active driver/vehicle, `Capacity.CanCarry`, future end, overlap) -> insert -> `SaveChanges`
   (rowversion on the shipment) -> commit, which releases the locks.
7. Errors: `assignment_conflict` 409, `capacity_exceeded` 422, `invalid_state` 409, lost lock/deadlock/rowversion 409 `concurrency_conflict`.

**Why the second request gets a correct 409:** it blocks on the lock until the first commits, then its step 6 read sees the committed row, and the
domain check fails. Independent drivers/vehicles do not block each other. Lock order is global (sorted names), so there are no lock cycles.

**Membership, roles, drivers**

- Create company: company + Admin membership + `CompanyAdmin` role row in one commit. Add/remove member: membership + role row (+ driver deactivation) in one commit.
- Removing a member: serialized (member + driver locks) with registering/activating/assigning that driver; refused while the driver holds an active
  assignment; deactivates the driver in the same commit (closes the Phase 1 backlog item).
- Availability queries (`available-vehicles`, `available-drivers`) are advisory reads (no locks); the authoritative check is the one in step 6.

**Authorization summary:** company admin: members and fleet writes; admin+dispatcher: fleet reads, availability, assign, unassign, get assignment;
driver/customer: none of these. Resources of other companies: 404.

