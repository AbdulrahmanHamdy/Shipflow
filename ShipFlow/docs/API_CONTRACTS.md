# API Contracts

Only endpoints that exist are listed. Swagger UI is at /swagger in Development (or `Swagger:Enabled=true`).

## Conventions

- JSON over HTTP. Timestamps are UTC ISO 8601.
- Errors use RFC 7807 `application/problem+json` and include a `traceId`. Exception messages and stack traces are never returned.

## Endpoints

### GET /health

Anonymous liveness check.

**200 OK**
```json
{ "status": "Healthy", "timestampUtc": "2026-10-21T11:06:28.5086218Z" }
```

Verified by running the API locally. Dependency (database) checks are added in Phase 2.

### GET /health/ready
Anonymous. 200 when SQL Server is reachable, otherwise 503. Same JSON shape as `/health`.

### POST /api/auth/register  (anonymous, rate limited)
Body `{ "email", "password", "fullName" }` (password: 10-128 chars, upper, lower, digit). Extra fields such as `role` are ignored.
**201** `{ id, email, fullName, roles: ["Customer"] }` - **400** validation problem with `errors` per field - **409** email taken - **429**.

### POST /api/auth/login  (anonymous, rate limited)
Body `{ "email", "password" }`. **200** `{ accessToken, tokenType: "Bearer", expiresAtUtc, user }` - **400** - **401** identical for
wrong password, unknown email, locked or deactivated account - **429** (with `Retry-After`).

### GET /api/auth/me  (bearer)
**200** `{ id, email, roles }` from the token claims - **401**.

### Error mapping
Validation 400 (+`errors`), authentication 401, conflict 409, domain codes: `validation` 400, `not_found` 404, `not_owner`/`cross_company` 403,
`capacity_exceeded` 422, `invalid_state`/`invalid_transition`/`quote_rule`/`assignment_conflict` 409. All carry `code` and `traceId`.

### Unhandled errors

**500**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "An unexpected error occurred.",
  "status": 500,
  "traceId": "0HN..."
}
```

Implemented via `GlobalExceptionHandler`; not yet exercised by a test.

## Shipments and quotations (Phase 3)

All endpoints need a bearer token. Enums are strings. Other users' shipments answer **404** (not 403) so ids cannot be probed.
Errors: 400 validation (`errors` per field), 401, 403 (wrong role or not allowed in your capacity), 404, 409 (lifecycle/quote rule or
concurrency), 422 (capacity, later phases).

| Method & path | Who | Notes |
|---|---|---|
| `POST /api/shipments` | Customer | Body `{ reference?, cargo{description,weightKg,volumeM3}, stops[2..20]{address,latitude,longitude,windowStartUtc?,windowEndUtc?} }`. Pickup (first stop) needs a UTC window. Owner = caller. 201, status Pending. |
| `GET /api/shipments` | Customer, CompanyAdmin, Dispatcher, PlatformAdmin | Query: `scope=Mine\|OpenForQuotes`, `statuses` (repeatable), `createdFromUtc`, `createdToUtc`, `search`, `page`(>=1), `pageSize`(1-100, default 20). Customers see their own; companies the shipments they carry (`scope=OpenForQuotes`: shipments open for quotes); admins all. Returns `{ items, page, pageSize, totalCount, totalPages }`. |
| `GET /api/shipments/{id}` | owner, carrier company, platform admin; any company while Quoting | Quotes: owner/admin see all, a company only its own. |
| `POST /api/shipments/{id}/open-for-quotes` | owner | Pending to Quoting. |
| `POST /api/shipments/{id}/cancel` | owner, carrier company (Admin/Dispatcher), platform admin | Body `{ reason }`. Capacity derived from caller; lifecycle rules in ARCHITECTURE.md. Releases an active assignment in the same commit. |
| `POST /api/shipments/{id}/quotes` | CompanyAdmin, Dispatcher with an active company membership | Body `{ amount, currency, expiresAtUtc, terms }`. Company comes from the membership. Shipment must be Quoting; one live quote per company. 201. |
| `GET /api/shipments/{id}/quotes` | as `GET {id}` | Filtered as above. |
| `POST /api/shipments/{id}/quotes/{quoteId}/accept` | owner | Fixes carrier and price from the quote, rejects the others. Two simultaneous accepts: exactly one 200, the other 409. |
| `POST /api/shipments/{id}/quotes/{quoteId}/withdraw` | the quoting company | |

There is deliberately **no** endpoint that sets a status directly.

## Companies, fleet and dispatching (Phase 4)

Company, fleet and dispatch routes never contain a company id: the company is the caller's own active membership, read from the database.
Another company's driver, vehicle or shipment answers **404**. Role gates (403): the token role is checked first, then the handler checks the
database membership (so a removed member with a still-valid token gets 403). Roles are granted with memberships, but only appear in tokens
issued **after** the change: sign in again (`POST /api/auth/login`).
Extra errors: 409 `conflict` (duplicate plate/licence, user already in a company, driver/vehicle has an active assignment), 409 `concurrency_conflict`
(lost a race or lock wait), 409 `assignment_conflict`, 422 `capacity_exceeded`.

### Companies and members

| Method & path | Who | Notes |
|---|---|---|
| `POST /api/companies` | Customer | Body `{ name }`. Caller becomes Admin and gets the `CompanyAdmin` role. 201 `{ id, name, isActive, createdAtUtc, myRole }`. 409 if already in a company. |
| `GET /api/companies/me` | CompanyAdmin, Dispatcher, Driver | The caller's company and role. |
| `GET /api/companies/me/members?includeFormer=false` | CompanyAdmin | `[{ userId, email, fullName, role, isActive, joinedAtUtc, leftAtUtc }]` |
| `POST /api/companies/me/members` | CompanyAdmin | Body `{ email, role: "Dispatcher" \| "Driver" }`. Account must exist and be active (404 otherwise; reveals existence to company admins only), not already in a company (409). Grants the Identity role in the same commit. 201. `Admin` is rejected (400). |
| `DELETE /api/companies/me/members/{userId}` | CompanyAdmin | One commit: membership closed, Identity role revoked, driver profile deactivated. 409 if the driver has an active assignment or this is the last admin. 204. |

### Drivers (`/api/fleet/drivers`) and vehicles (`/api/fleet/vehicles`)

| Method & path | Who | Notes |
|---|---|---|
| `POST /api/fleet/drivers` | CompanyAdmin | Body `{ userId, fullName, licenseNumber }`. `userId` must be an active **Driver** member of the caller's company (else 404). Licence upper-cased, unique per company (409), one profile per user (409). 201. |
| `GET /api/fleet/drivers?isActive&page&pageSize` | CompanyAdmin, Dispatcher | Paged `{ items, page, pageSize, totalCount, totalPages }`; pageSize 1-100. |
| `GET /api/fleet/drivers/{id}` | CompanyAdmin, Dispatcher | 404 for other companies. |
| `PUT /api/fleet/drivers/{id}` | CompanyAdmin | Body `{ fullName, licenseNumber }`. |
| `POST /api/fleet/drivers/{id}/activate` | CompanyAdmin | 409 unless the user is still an active Driver member. |
| `POST /api/fleet/drivers/{id}/deactivate` | CompanyAdmin | 409 while the driver has an active assignment. |
| `POST /api/fleet/vehicles` | CompanyAdmin | Body `{ plateNumber, type: Van\|Pickup\|Truck\|Trailer, maxWeightKg, maxVolumeM3 }` (>0, <=1,000,000 kg / 100,000 m3, <=3 decimals). Plate normalised (trimmed, no spaces, upper-case), unique per company (409). |
| `GET /api/fleet/vehicles?isActive&page&pageSize`, `GET /api/fleet/vehicles/{id}` | CompanyAdmin, Dispatcher | |
| `PUT /api/fleet/vehicles/{id}/capacity` | CompanyAdmin | Body `{ maxWeightKg, maxVolumeM3 }`. May not shrink while the vehicle has an active assignment (409). Plate and type are immutable. |
| `POST /api/fleet/vehicles/{id}/activate` / `deactivate` | CompanyAdmin | Deactivate: 409 while assigned. |

### Dispatching (`/api/shipments/{shipmentId}/...`; CompanyAdmin or Dispatcher of the shipment's carrier, otherwise 404/403)

| Method & path | Notes |
|---|---|
| `GET .../available-vehicles?windowStartUtc&windowEndUtc` | Carrier's active vehicles that `CanCarry` the cargo and have no active assignment overlapping the window. Shipment must be Accepted (409 otherwise). Window: UTC (`Z`), end after start, <= 14 days (400). |
| `GET .../available-drivers?windowStartUtc&windowEndUtc` | Carrier's active drivers free in the window. |
| `POST .../assignment` | Body `{ driverId, vehicleId, windowStartUtc, windowEndUtc }`. Accepted -> Assigned. 201 `{ id, shipmentId, companyId, driverId, vehicleId, windowStartUtc, windowEndUtc, status, shipmentStatus, assignedByUserId, createdAtUtc, endedAtUtc, releaseReason }`. 409 `assignment_conflict` (windows are half-open: back-to-back is allowed), 422 `capacity_exceeded`, 409 inactive driver/vehicle or wrong shipment state, 404 other company's ids. Safe under concurrent requests (docs/ARCHITECTURE.md). |
| `GET .../assignment` | The active assignment, 404 if none. |
| `POST .../unassign` | Body `{ reason }`. Assigned -> Accepted, assignment Released. 409 after pickup. Returns the shipment. |

Cancelling a shipment (Phase 3 endpoint) releases its active assignment in the same commit, which frees the driver and vehicle.


---

## Phase 5 Endpoints

### Driver Shipment Lifecycle (`/api/shipments/{id}/...`; Driver role only)

All driver action endpoints require:
- Valid JWT with `Driver` role.
- Caller has an active membership (Driver) in the shipment's carrier company.
- Caller has an active driver profile in that company.
- Caller is the currently assigned driver for this shipment.

| Method & path | Status transition | Notes |
|---|---|---|
| `POST /api/shipments/{id}/pickup` | `Assigned → PickedUp` | 404 if shipment not found or not assigned to caller. 409 if wrong status. |
| `POST /api/shipments/{id}/start-transit` | `PickedUp → InTransit` | 409 if wrong status. |
| `POST /api/shipments/{id}/deliver` | `InTransit → Delivered` | Body: `{ deliveredAtUtc, recipientName, notes?, signatureReference?, photoReference? }`. Atomically creates proof + delivers + completes assignment. 409 if proof already exists. 409 if wrong status. |
| `POST /api/shipments/{id}/fail-delivery` | `InTransit → DeliveryFailed` | Body: `{ reason }` (1-500 chars). Assignment remains active. |
| `POST /api/shipments/{id}/retry-delivery` | `DeliveryFailed → InTransit` | 409 if wrong status. |

### GPS Tracking

| Method & path | Who | Notes |
|---|---|---|
| `POST /api/tracking` | Driver | Body: `{ shipmentId, latitude, longitude, recordedAtUtc, speed?, heading?, accuracy?, altitude? }`. Per-user fixed-window rate limit (60/min). Idempotent by `(shipmentId, recordedAtUtc, userId)` — duplicates return 200 silently. 429 if rate limit exceeded. |
| `GET /api/shipments/{id}/tracking/latest` | Shipment viewer (owner, carrier, admin) | Latest GPS point or 404. |
| `GET /api/shipments/{id}/tracking?fromUtc&toUtc&page&pageSize` | Shipment viewer | Paginated tracking history. Optional time-range filter (UTC). |

### Delivery Proof

| Method & path | Who | Notes |
|---|---|---|
| `GET /api/shipments/{id}/delivery-proof` | Shipment viewer | 404 if not yet delivered or no proof. Returns `{ id, shipmentId, capturedByUserId, recipientName, deliveredAtUtc, receivedAtUtc, notes, signatureReference, photoReference }`. |

### Driver Assignment Reads

| Method & path | Who | Notes |
|---|---|---|
| `GET /api/driver/assignments/current` | Driver | Current active assignment for the calling driver. 404 if none. |
| `GET /api/driver/assignments?page&pageSize` | Driver | Paginated assignment history for the calling driver. |

### Payments (Paymob)

| Method & path | Who | Notes |
|---|---|---|
| `POST /api/payments/initiate` | Customer | Body: `{ shipmentId }`. Returns `{ paymentUrl, orderId, iframeId }`. Shipment must have an accepted quote. |
| `POST /api/payments/callback` | Paymob (anonymous) | Webhook. HMAC-SHA512 verification required; returns 403 if HMAC is invalid. Idempotent: same order ID processed only once. |
| `GET /api/payments/callback` | Paymob redirect (anonymous) | Client redirect after payment; logs and acknowledges, no state change. |

---

## Error Code Reference

| HTTP Status | `code` field | Cause |
|---|---|---|
| 400 | `validation` | FluentValidation failure; `errors` field lists per-field messages |
| 400 | `invalid_state` | Domain state-machine violation (wrong status for the requested action) |
| 401 | — | Missing or invalid JWT |
| 403 | `forbidden` | Valid JWT but caller does not have the required role/permission |
| 404 | `not_found` | Resource not found or not visible to the caller |
| 409 | `conflict` | Unique constraint violation (duplicate plate, duplicate proof, etc.) |
| 409 | `concurrency_conflict` | Row-version or app-lock race; retry the request |
| 409 | `assignment_conflict` | Overlapping assignment window |
| 422 | `capacity_exceeded` | Vehicle capacity insufficient for the cargo |
| 429 | — | Rate limit exceeded; `Retry-After` header present |
| 500 | `internal` | Unexpected server error; no internal details leaked |
