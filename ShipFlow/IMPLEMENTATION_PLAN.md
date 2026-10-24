# ShipFlow — Implementation Plan

Legend: ✅ done and verified · 🟡 done with caveats · ⬜ not started

| Phase | Scope | Status |
|---|---|---|
| 0 | Foundation: solution, projects, DI, `/health`, tests scaffold, docs, checkpoints | ✅ Verified |
| 1 | Domain: entities, value objects, shipment lifecycle, invariants, unit tests | ✅ 188 tests pass |
| 2 | Persistence and Identity: SQL Server, EF Core, migrations, Identity, JWT, roles, policies | ✅ Migration applied, SQL Server connected |
| 3 | Shipment and Quotation APIs: pagination, filtering, quote submit/accept, cancellation | ✅ All integration tests pass |
| 4 | Fleet and Dispatching: companies, drivers, vehicles, assignment, concurrency | ✅ Race tests verified on SQL Server |
| 5 | Tracking and Delivery: events, tracking, driver updates, delivery proof, timelines | ✅ All tests pass, SQL Server verified |
| 6 | Real-time and Notifications: SignalR, notification abstractions, background processing | ⬜ Out of scope |
| 7 | Advanced Reliability: idempotency, outbox, retries, optional Redis/RabbitMQ | ⬜ Out of scope |
| 8 | Quality and Observability: tests, logging, health checks, metrics, perf tests | ⬜ Out of scope |
| 9 | Deployment and Handoff: Docker, CI, config templates, migration docs, README | ⬜ Out of scope |

---

## Phase 5 deliverables (2026-10-24)

### Driver shipment lifecycle
- `POST /api/shipments/{id}/pickup` — `Assigned → PickedUp`
- `POST /api/shipments/{id}/start-transit` — `PickedUp → InTransit`
- `POST /api/shipments/{id}/deliver` — `InTransit → Delivered` (creates proof atomically)
- `POST /api/shipments/{id}/fail-delivery` — `InTransit → DeliveryFailed`
- `POST /api/shipments/{id}/retry-delivery` — `DeliveryFailed → InTransit`

All endpoints verify: caller is authenticated driver → active company membership (Driver role) → active driver profile → is the assigned driver for this shipment.

### GPS Tracking
- `POST /api/tracking` — Submit a GPS point. Rate-limited per user (60/min configurable). Idempotent by `(shipmentId, recordedAtUtc, driverUserId)`.
- `GET /api/shipments/{id}/tracking/latest` — Latest position.
- `GET /api/shipments/{id}/tracking` — Paginated history with optional `fromUtc`/`toUtc` filter.

### Delivery Proof
- `GET /api/shipments/{id}/delivery-proof` — Retrieve the proof for a delivered shipment.
- Created atomically during `/deliver`: proof saved, shipment transitioned to `Delivered`, assignment marked `Completed` — all in one `SaveChangesAsync`.

### Driver Assignment reads
- `GET /api/driver/assignments/current` — Active assignment for the calling driver.
- `GET /api/driver/assignments` — Paginated assignment history.

### Payment (Paymob)
- `POST /api/payments/initiate` — Start a payment session (Customer only).
- `POST /api/payments/callback` — Paymob webhook; HMAC-SHA512 verification required.
- `GET /api/payments/callback` — Paymob redirect callback (read-only, no state change).

### Email (`IEmailSender`)
- Abstraction in Application layer. `SmtpEmailSender` in Infrastructure.
- Configured via `Email:*` settings. Credentials are never logged (source-generated `[LoggerMessage]`).

### EF Core migrations
- `InitialCreate` — contains all tables including `TrackingPoints`, `DeliveryProofs`, `Assignments`, Identity tables, company/fleet tables, and all unique indexes.

---

## Architecture Decisions Added in Phase 5

- **D-029** — GPS deduplication: `(shipmentId, recordedAtUtc, recordedByUserId)` composite idempotency key; duplicate points are silently ignored (no error, no duplicate row).
- **D-030** — Paymob integration: 3-step API flow (auth → order → payment key), HMAC-SHA512 callback verification, `UseMock` toggle for development.
- **D-031** — SMTP email: `IEmailSender` port in Application; `SmtpEmailSender` in Infrastructure. High-performance `[LoggerMessage]` source-generated methods.

---

## Backlog (out of scope for Phase 5)

- Refresh tokens and token revocation (D-017 revisit).
- SignalR real-time tracking updates (Phase 6).
- Notification background workers.
- Metrics, distributed tracing, Prometheus exporter.
- Docker Compose, CI/CD pipeline.
