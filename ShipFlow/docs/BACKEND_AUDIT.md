# ShipFlow Backend Audit Report (Phase A)

**Date:** October 2026  
**Auditor:** Senior Frontend Architect & Product Designer  
**Scope:** ShipFlow .NET 8 ASP.NET Core Web API (`Shipflow/ShipFlow/src/ShipFlow.API`)  
**Backend Status:** Verified clean build, 377 automated tests passing across Domain, Application, and API integration test suites.

---

## 1. Executive Summary

ShipFlow's backend is a well-structured Clean Architecture implementation in .NET 8 using ASP.NET Core Web API, Entity Framework Core with SQL Server, ASP.NET Core Identity, FluentValidation, and MediatR-style CQRS command/query handlers.

The API relies on strict authorization gates, explicit state machine transitions, and standard RFC 7807 Problem Details for all errors. Serialization utilizes camelCase JSON property naming with `JsonStringEnumConverter` for string enum serialization.

---

## 2. Authentication, Roles, and Identity

### Scheme & Token Specs
- **Type:** Stateless Bearer JWT tokens via `Authorization: Bearer <token>`.
- **Claims:** `sub` (UserId Guid), `email`, `name` (FullName), `role` (repeated claim for each assigned role), `jti` (TokenId Guid).
- **Lifetime:** 15 minutes (`AccessTokenMinutes: 15`).
- **Token Refresh:** **Missing.** There is no refresh token mechanism, table, or endpoint. When the access token expires, the user must re-authenticate.
- **Revocation / Logout:** **Missing.** The backend does not maintain server-side token blacklists or sessions. Client logout is performed by discarding the token in memory and clearing local caches.

### Platform Roles (`ShipFlow.Application.Common.Security.Roles`)
1. `Customer`: Default role. Self-registration (`POST /api/auth/register`) only creates Customers. Customers can create shipments, request quotes, accept quotes, cancel shipments (prior to transit), and initiate payments.
2. `CompanyAdmin`: Role gained when a Customer creates a company (`POST /api/companies`), or when assigned as an Admin member. Manages company profile, members (Dispatchers & Drivers), fleet vehicles, and drivers.
3. `Dispatcher`: Member added by a CompanyAdmin. Manages quotes (submit, withdraw) and dispatch assignments (assign driver/vehicle, unassign).
4. `Driver`: Member added by a CompanyAdmin with an active driver profile. Executes assigned deliveries (pickup, start-transit, deliver, fail-delivery, retry-delivery) and submits GPS tracking coordinates.
5. `PlatformAdmin`: System administrator with cross-company and system-wide visibility.

*Note on Role Transitions (D-023):* When a user creates a company or is assigned a new role by an admin, the role is updated in the database but will only appear in JWT tokens issued **after** the change. The user must re-login (`POST /api/auth/login`) to receive a token with the new role claims.

---

## 3. Communication Conventions & Response Formats

- **Content Type:** `application/json; charset=utf-8`
- **Timestamps:** ISO 8601 UTC with `Z` suffix (e.g., `2026-10-24T12:00:00Z`).
- **Enums:** Serialized as PascalCase strings (`"Pending"`, `"Quoting"`, `"Accepted"`, `"Assigned"`, `"PickedUp"`, `"InTransit"`, `"DeliveryFailed"`, `"Delivered"`, `"Cancelled"`).
- **Pagination Envelope:** Standardized across all paged endpoints:
  ```json
  {
    "items": [ ... ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 42,
    "totalPages": 3
  }
  ```
- **Error Format (RFC 7807 Problem Details):**
  - Content-Type: `application/problem+json`
  - Shape:
    ```json
    {
      "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
      "title": "Validation failed",
      "status": 400,
      "detail": "One or more validation errors occurred.",
      "instance": "/api/shipments",
      "code": "validation",
      "traceId": "0HN1234567890",
      "errors": {
        "Cargo.WeightKg": ["'Cargo Weight Kg' must be greater than '0'."]
      }
    }
    ```
  - Standard error codes: `validation` (400), `invalid_state` (400), `not_found` (404), `forbidden` (403), `conflict` (409), `concurrency_conflict` (409), `assignment_conflict` (409), `capacity_exceeded` (422), `internal` (500).
- **Rate Limiting:**
  - Auth policy: 10 requests per 60 seconds (`RateLimiting:AuthPermitLimit`, `AuthWindowSeconds`). Returns 429 with `Retry-After` header.
  - Tracking policy: 60 requests per 60 seconds (`RateLimiting:TrackingPermitLimit`, `TrackingWindowSeconds`). Returns 429 with `Retry-After` header.
- **CORS:**
  - Configured via `Cors:AllowedOrigins` in `appsettings.json`. By default empty (`[]`). For local frontend development, Vite dev proxy (`/api` -> backend) will be used to completely avoid CORS overhead during development.

---

## 4. Endpoint-to-Feature Matrix

| Area | Feature | Endpoint | HTTP Verb | Backend Status | Auth & Role Required |
|---|---|---|---|---|---|
| **Health** | Process Liveness | `/health` | GET | **Supported** | Anonymous |
| **Health** | Database Readiness | `/health/ready` | GET | **Supported** | Anonymous |
| **Auth** | Customer Registration | `/api/auth/register` | POST | **Supported** | Anonymous (Rate-limited) |
| **Auth** | User Login | `/api/auth/login` | POST | **Supported** | Anonymous (Rate-limited) |
| **Auth** | Current User Profile | `/api/auth/me` | GET | **Supported** | Bearer (Any authenticated user) |
| **Auth** | Token Refresh | `/api/auth/refresh` | POST | **Missing** | Not implemented on backend |
| **Auth** | Server Logout | `/api/auth/logout` | POST | **Missing** | Not implemented (stateless JWT) |
| **Auth** | Forgot Password | `/api/auth/forgot-password`| POST | **Missing** | Not implemented on backend |
| **Auth** | Reset Password | `/api/auth/reset-password` | POST | **Missing** | Not implemented on backend |
| **Auth** | Verify Email | `/api/auth/verify-email` | POST | **Missing** | Not implemented on backend |
| **Shipments** | Create Shipment | `/api/shipments` | POST | **Supported** | Customer |
| **Shipments** | List Shipments | `/api/shipments` | GET | **Supported** | ShipmentViewer (Customer/CompanyStaff/Admin) |
| **Shipments** | Get Shipment Detail | `/api/shipments/{id}` | GET | **Supported** | ShipmentViewer |
| **Shipments** | Open for Quotes | `/api/shipments/{id}/open-for-quotes` | POST | **Supported** | Customer (Owner) |
| **Shipments** | Cancel Shipment | `/api/shipments/{id}/cancel` | POST | **Supported** | ShipmentViewer (Owner/Carrier/Admin) |
| **Shipments** | Public Anonymous Tracking | `/api/public/tracking/{ref}` | GET | **Missing** | Tracking requires authentication |
| **Quotations** | Submit Carrier Quote | `/api/shipments/{id}/quotes` | POST | **Supported** | Dispatcher / CompanyAdmin |
| **Quotations** | List Quotes | `/api/shipments/{id}/quotes` | GET | **Supported** | ShipmentViewer |
| **Quotations** | Accept Quote | `/api/shipments/{id}/quotes/{quoteId}/accept` | POST | **Supported** | Customer (Owner) |
| **Quotations** | Withdraw Quote | `/api/shipments/{id}/quotes/{quoteId}/withdraw` | POST | **Supported** | Dispatcher / CompanyAdmin |
| **Companies** | Create Company | `/api/companies` | POST | **Supported** | Customer (Caller becomes CompanyAdmin) |
| **Companies** | Get My Company | `/api/companies/me` | GET | **Supported** | CompanyStaff (Admin, Dispatcher, Driver) |
| **Companies** | List Members | `/api/companies/me/members` | GET | **Supported** | CompanyAdmin |
| **Companies** | Add Member | `/api/companies/me/members` | POST | **Supported** | CompanyAdmin |
| **Companies** | Remove Member | `/api/companies/me/members/{userId}` | DELETE | **Supported** | CompanyAdmin |
| **Fleet** | Register Driver | `/api/fleet/drivers` | POST | **Supported** | CompanyAdmin |
| **Fleet** | List Drivers | `/api/fleet/drivers` | GET | **Supported** | Dispatcher / CompanyAdmin |
| **Fleet** | Get Driver Detail | `/api/fleet/drivers/{id}` | GET | **Supported** | Dispatcher / CompanyAdmin |
| **Fleet** | Update Driver | `/api/fleet/drivers/{id}` | PUT | **Supported** | CompanyAdmin |
| **Fleet** | Activate Driver | `/api/fleet/drivers/{id}/activate` | POST | **Supported** | CompanyAdmin |
| **Fleet** | Deactivate Driver | `/api/fleet/drivers/{id}/deactivate` | POST | **Supported** | CompanyAdmin |
| **Fleet** | Register Vehicle | `/api/fleet/vehicles` | POST | **Supported** | CompanyAdmin |
| **Fleet** | List Vehicles | `/api/fleet/vehicles` | GET | **Supported** | Dispatcher / CompanyAdmin |
| **Fleet** | Get Vehicle Detail | `/api/fleet/vehicles/{id}` | GET | **Supported** | Dispatcher / CompanyAdmin |
| **Fleet** | Update Vehicle Capacity | `/api/fleet/vehicles/{id}/capacity` | PUT | **Supported** | CompanyAdmin |
| **Fleet** | Activate Vehicle | `/api/fleet/vehicles/{id}/activate` | POST | **Supported** | CompanyAdmin |
| **Fleet** | Deactivate Vehicle | `/api/fleet/vehicles/{id}/deactivate` | POST | **Supported** | CompanyAdmin |
| **Dispatch** | Available Vehicles for Window | `/api/shipments/{shipmentId}/available-vehicles` | GET | **Supported** | Dispatcher / CompanyAdmin |
| **Dispatch** | Available Drivers for Window | `/api/shipments/{shipmentId}/available-drivers` | GET | **Supported** | Dispatcher / CompanyAdmin |
| **Dispatch** | Assign Driver & Vehicle | `/api/shipments/{shipmentId}/assignment` | POST | **Supported** | Dispatcher / CompanyAdmin |
| **Dispatch** | Get Active Assignment | `/api/shipments/{shipmentId}/assignment` | GET | **Supported** | Dispatcher / CompanyAdmin |
| **Dispatch** | Unassign Shipment | `/api/shipments/{shipmentId}/unassign` | POST | **Supported** | Dispatcher / CompanyAdmin |
| **Tracking** | Submit GPS Tracking Point | `/api/tracking` | POST | **Supported** | Driver (Rate-limited: 60/min) |
| **Tracking** | Latest GPS Tracking Point | `/api/shipments/{shipmentId}/tracking/latest` | GET | **Supported** | ShipmentViewer |
| **Tracking** | Tracking History (GPS Trail) | `/api/shipments/{shipmentId}/tracking` | GET | **Supported** | ShipmentViewer |
| **Driver** | Confirm Pickup | `/api/shipments/{shipmentId}/pickup` | POST | **Supported** | Driver (Assigned driver only) |
| **Driver** | Start Transit | `/api/shipments/{shipmentId}/start-transit` | POST | **Supported** | Driver (Assigned driver only) |
| **Driver** | Record Delivery (Proof) | `/api/shipments/{shipmentId}/deliver` | POST | **Supported** | Driver (Assigned driver only) |
| **Driver** | Report Delivery Failure | `/api/shipments/{shipmentId}/fail-delivery` | POST | **Supported** | Driver (Assigned driver only) |
| **Driver** | Retry Delivery | `/api/shipments/{shipmentId}/retry-delivery` | POST | **Supported** | Driver (Assigned driver only) |
| **Driver** | Get Delivery Proof | `/api/shipments/{shipmentId}/delivery-proof` | GET | **Supported** | ShipmentViewer |
| **Driver** | Driver Current Assignment | `/api/driver/assignments/current` | GET | **Supported** | Driver |
| **Driver** | Driver Assignment History | `/api/driver/assignments` | GET | **Supported** | Driver |
| **Payments** | Initiate Paymob Session | `/api/payments/initiate` | POST | **Supported** | Customer (Owner) |
| **Payments** | Webhook Notification | `/api/payments/callback` | POST | **Supported** | Paymob Webhook (Anonymous, HMAC) |
| **Payments** | Client Redirect Landing | `/api/payments/callback` | GET | **Supported** | Paymob Client Redirect (Anonymous) |
| **Payments** | Direct Payment Status Check | `/api/payments/{id}/status` | GET | **Missing** | Verified via callback / shipment state |
| **Analytics** | Aggregate BI / Reports | `/api/reports/*` | GET | **Missing** | Derived from client shipment lists |

---

## 5. Architectural Conclusions & Strategy for Frontend

1. **Token Strategy:** Since backend JWTs have 15-minute validity without a refresh endpoint or cookie support, tokens will be maintained in a secured React memory store with local fallback persistence for session restoration on page reload. The UI will gracefully detect 401s and provide a clear re-login modal/redirect preserving unsaved context.
2. **Public Tracking:** Because anonymous shipment tracking is not supported by the backend (all tracking routes demand authenticated `RequireShipmentViewer`), the landing page's "Track a Shipment" CTA will direct users to authenticate or to their customer dashboard tracking screen with a transparent note explaining that shipment visibility is restricted to authorized parties for cargo security.
3. **Password Reset / Email Verification:** The landing and auth interfaces will clearly note that automated email self-service is pending backend integration, rendering honest guidance rather than simulating fake reset emails.
4. **Driver Workflow Experience:** The driver endpoints require precise sequencing: `Assigned` -> `PickedUp` -> `InTransit` -> (`DeliveryFailed` <-> `InTransit`) -> `Delivered`. The driver portal will strictly reflect current state, enable the appropriate actions, capture recipient name and notes for delivery proof, and submit GPS coordinates at standard intervals.
5. **Paymob Flow:** Payment initiation produces an `iframeUrl` / payment key. The frontend will present the official Paymob checkout modal/iframe, handle the redirect return to `/payments/callback`, and verify the shipment's updated state through the server.
