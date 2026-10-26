# ShipFlow Integration Gaps & Backend Observations

**Document Version:** 1.0.0  
**Phase:** Phase A Audit Baseline  
**Principles Applied:** Principle 1 (Backend is source of truth), Principle 2 (No fake data presented as real).

---

## 1. Missing Backend Endpoints

| Area | Missing Capability | Impact on Frontend | Required Frontend Handling / UX Solution |
|---|---|---|---|
| **Auth** | Refresh Token (`/api/auth/refresh`) | JWT tokens expire after 15 minutes without silent renewal. | Access token stored in secure client memory store with session persistence fallback. When an API call returns 401, a clear re-login modal or prompt is displayed without losing the user's active form draft. |
| **Auth** | Server Logout (`/api/auth/logout`) | Token cannot be revoked server-side. | Client-side logout clears memory token, clears React Query cache, and redirects to `/login`. |
| **Auth** | Password Reset (`/api/auth/forgot-password`, `/api/auth/reset-password`) | Users cannot reset forgotten passwords self-service. | Render `/forgot-password` and `/reset-password` screens with a prominent, honest notification: *"Self-service password reset is not currently supported by the server. Please contact your company administrator."* |
| **Auth** | Email Verification (`/api/auth/verify-email`) | Self-registered users cannot verify emails. | Display informative banner if needed, without claiming an email was sent. |
| **Tracking** | Public Anonymous Tracking Lookup | Public users cannot query a shipment by tracking reference without logging in. | `/tracking` screen explains clearly that for privacy and security reasons, shipment location data is restricted to authorized stakeholders (shipment owners, carriers, assigned drivers). Provide quick sign-in link. |
| **Payments** | Payment Status Query (`GET /api/payments/{id}` or `GET /api/shipments/{id}/payment`) | Frontend cannot poll a dedicated payment transaction endpoint. | When returning from Paymob checkout via `/payments/callback`, verify the transaction outcome using the redirect callback data and refetch `GET /api/shipments/{id}` to verify whether the shipment moved forward. Provide a manual "Refresh Status" button. |
| **Attachments**| Media/Photo/Signature Upload | Driver delivery proof accepts string references (`signatureReference`, `photoReference`), but has no file upload endpoint. | Allow optional input of reference tags, order IDs, or notes. Do not pretend an image was uploaded to cloud storage. |
| **Reports** | Analytics / Aggregation Endpoints (`/api/reports/*`) | No aggregate KPIs (e.g. monthly revenue, total distance, fleet utilization). | Dashboards calculate metrics only from loaded datasets with transparent scope labels (e.g., *"Based on recent shipments"*). Do not invent fictitious enterprise totals. |

---

## 2. Authorization & Role Nuances

1. **Role Propagation Delay (D-023):**
   - When a Customer creates a company, the user gains the `CompanyAdmin` role in the database.
   - However, existing JWT access tokens do not retroactively gain the new role claim.
   - **Frontend UX:** When a company is successfully created or a member is assigned, notify the user: *"Company created successfully! Please re-authenticate to activate your administrator permissions."* Prompt them to log in again.

2. **404 vs 403 for Anti-Probing:**
   - The backend intentionally returns `404 Not Found` (rather than `403 Forbidden`) when accessing shipments, vehicles, or drivers belonging to other companies.
   - **Frontend UX:** Present a clean "Resource not found" empty state rather than confusing the user with access denied.

3. **Status Transitions:**
   - There is NO endpoint to directly set a status (e.g. `status = "InTransit"`). Transitions occur ONLY via specific action commands:
     - `POST /open-for-quotes`: `Pending` -> `Quoting`
     - `POST /quotes/{quoteId}/accept`: `Quoting` -> `Accepted`
     - `POST /assignment`: `Accepted` -> `Assigned`
     - `POST /pickup`: `Assigned` -> `PickedUp`
     - `POST /start-transit`: `PickedUp` -> `InTransit`
     - `POST /deliver`: `InTransit` -> `Delivered`
     - `POST /fail-delivery`: `InTransit` -> `DeliveryFailed`
     - `POST /retry-delivery`: `DeliveryFailed` -> `InTransit`
     - `POST /cancel`: Cancels according to caller role.
     - `POST /unassign`: Releases assignment.
   - **Frontend UX:** Action buttons will be strictly enabled/disabled according to the exact current shipment status.
