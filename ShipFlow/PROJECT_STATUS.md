# ShipFlow — Project Status

**Last updated:** 2026-10-24
**Current phase:** Phase 5 — Tracking and Delivery (**COMPLETE**)
**Build:** `dotnet build ShipFlow.sln` → 0 errors, 0 warnings
**Tests:** 426 passed, 0 failed, 0 skipped across all three test projects (with SQL Server)

---

## Verification (actual — 2026-10-24 Phase 5 session)

| Check | Result |
|---|---|
| `dotnet restore && dotnet build ShipFlow.sln` | ✅ 0 errors, 0 warnings |
| Domain tests (`ShipFlow.Domain.Tests`) | ✅ 188 passed |
| Application tests (`ShipFlow.Application.Tests`) | ✅ 109 passed |
| API Integration tests (`ShipFlow.API.IntegrationTests`) | ✅ 129 passed (with `SHIPFLOW_TEST_SQLSERVER`) |
| SQL Server (`MSSQLSERVER`) available | ✅ Yes — `localhost`, Integrated Security |
| EF Core migration generated and applied | ✅ `InitialCreate` migration applied via `MigrateAsync()` |
| Concurrency — two simultaneous delivery attempts | ✅ Exactly one wins (409 or 422 for the loser); verified on SQL Server |
| Concurrency — driver/vehicle double-booking (Race_*) | ✅ All race tests pass on SQL Server |
| Payment integration prepared (Paymob) | ✅ Initiation, HMAC-SHA512 verification, callback processing |
| SMTP email integration prepared | ✅ `IEmailSender` + `SmtpEmailSender` with high-performance logging |
| Tracking (GPS) — submit, deduplicate, paginate | ✅ Idempotent submission (D-029), paginated history, rate-limited |
| Delivery proof — atomic create + deliver + complete assignment | ✅ Unique index backstop (UX_DeliveryProofs_Shipment) |
| Driver lifecycle — pickup, start-transit, deliver, fail, retry | ✅ All endpoints exercised by SQL Server integration tests |

---

## How to Run Tests

```powershell
$env:SHIPFLOW_TEST_SQLSERVER="Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
cd "f:\New folder (5)\Shipflow\ShipFlow"
dotnet test ShipFlow.sln
```

Without `SHIPFLOW_TEST_SQLSERVER`, the `[SqlServerFact]` tests are skipped automatically (Domain + Application unit tests still run).

---

## Configuration Keys (User Secrets / Environment Variables)

### Database
```json
"ConnectionStrings:DefaultConnection": "Server=localhost;Database=ShipFlow;Trusted_Connection=True;TrustServerCertificate=True"
```

### JWT
```json
"Jwt:Key": "<at-least-32-character-secret>",
"Jwt:Issuer": "ShipFlow",
"Jwt:Audience": "ShipFlow",
"Jwt:ExpiresMinutes": 15
```

### Paymob
```json
"Paymob:BaseUrl": "https://accept.paymob.com",
"Paymob:ApiKey": "<paymob-api-key>",
"Paymob:PublicKey": "<paymob-public-key>",
"Paymob:HmacSecret": "<paymob-hmac-secret>",
"Paymob:IntegrationId": "<integration-id>",
"Paymob:IframeId": "<iframe-id>",
"Paymob:UseMock": false
```

### SMTP Email
```json
"Email:SmtpHost": "smtp.gmail.com",
"Email:SmtpPort": 587,
"Email:UseSsl": false,
"Email:SenderEmail": "your@email.com",
"Email:SenderName": "ShipFlow",
"Email:Password": "<app-password>"
```

### Rate Limiting
```json
"RateLimiting:AuthPermitLimit": 10,
"RateLimiting:AuthWindowSeconds": 60,
"RateLimiting:TrackingPermitLimit": 60,
"RateLimiting:TrackingWindowSeconds": 60
```

### Swagger
```json
"Swagger:Enabled": true
```

### Development Seed
```json
"DevelopmentSeed:Enabled": true,
"DevelopmentSeed:AdminPassword": "Admin@123456"
```

---

## Phase Summary

### Phase 5 adds (this session)
- **Driver shipment lifecycle** — Pickup, StartTransit, Deliver, FailDelivery, RetryDelivery endpoints with proper state-machine enforcement.
- **Delivery proof** — Atomic: creates `DeliveryProof`, transitions Shipment to `Delivered`, and completes the `Assignment` in one unit of work. Unique index prevents duplicate proof. Only the assigned driver may create proof while the shipment is `InTransit`.
- **GPS tracking** — Submit (per-user rate-limited), get latest position, paginated history with time-range filter. Idempotency by `(shipmentId, recordedAtUtc, driverUserId)` key (D-029).
- **Driver assignment reads** — `GET /api/driver/assignments/current`, `GET /api/driver/assignments` (paginated history).
- **Paymob payment** — 3-step initiation flow, server-side HMAC-SHA512 webhook verification, idempotent callback processing, mock/live toggle (D-030).
- **SMTP email** — `IEmailSender` port, `SmtpEmailSender` implementation with `[LoggerMessage]` source generation (D-031).
- **EF Core migrations** — `InitialCreate` migration generated and applied. `SqlServerDatabaseFixture` uses `MigrateAsync()`.
- **Concurrency** — `Concurrency_TwoSimultaneousDeliveries_OnlyOneWins` verified on real SQL Server. Unique index on `DeliveryProofs` provides database backstop.
- **Auth audit** — Payment callback endpoints correctly listed as `[AllowAnonymous]`; `OnlyTheIntendedEndpoints_AreAnonymous` test updated to reflect the two Paymob callback routes.

### Previous phases
- **Phase 4** — Fleet, dispatching, `sp_getapplock` concurrency (D-025), company membership, Identity role sync.
- **Phase 3** — Shipment and Quotation APIs, `Viewer`-based authorization, 404 for hidden resources.
- **Phase 2** — Persistence, Identity, JWT, policies, `ProblemMapper`, rate limiting, CORS, Swagger.
- **Phase 1** — Domain: entities, value objects, shipment lifecycle, invariants.
- **Phase 0** — Foundation: solution, projects, DI, `/health`, test scaffold.

---

## Known Limitations (by design, not defects)

1. **Tokens are not refreshed** (D-017): 15-minute access tokens only; new roles require a fresh sign-in.
2. **One active company per user** (D-024): users cannot be members of multiple companies simultaneously.
3. **GPS tracking not linked to the `Shipment` aggregate** (D-010): high-volume tracking points are queried separately.
4. **No refresh token endpoint**: planned for a future phase.
5. **No push notifications** (Phase 6): SignalR and background workers are out of scope for the current phase.
6. **HTTPS/HSTS deferred** (D-007): TLS termination is handled at the reverse proxy level.
