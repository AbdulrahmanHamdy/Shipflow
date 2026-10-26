# ShipFlow Frontend

> **Production-quality React 18 SPA** for the ShipFlow Smart Logistics and Shipment Management Platform, integrated with the .NET 8 ASP.NET Core Web API backend.

---

## Quick Start

```bash
cd frontend/
npm install

# Start BOTH backend (.NET 8 Web API on port 5177) and frontend (Vite on port 3000)
# Automatically opens http://localhost:3000 in your browser when backend is ready:
npm run dev

# Or run components independently:
npm run dev:backend     # Runs only the ASP.NET Core API on http://localhost:5177
npm run dev:frontend    # Waits for backend health check, then runs Vite
npm run dev:standalone  # Runs only the Vite dev server directly

# Verification commands:
npm run build           # Production build → dist/
npm run test            # Vitest test suite (36 tests)
npm run lint            # ESLint + TypeScript check
npm run typecheck       # tsc --noEmit strict check
```

> **Prerequisites:** .NET 8 SDK, Node.js ≥ 18, npm ≥ 9, and SQL Server / LocalDB.
> Running `npm run dev` automatically handles database migrations and seeds development accounts (`DevPass1234!`).

---

## Architecture Overview

```
frontend/src/
├── api/                  # Typed API layer (Axios client, typed functions per domain)
│   ├── client.ts         # Axios instance, token storage, 401 listener
│   ├── errorMapper.ts    # RFC 7807 Problem Details → AppError normalizer
│   ├── types/            # Hand-authored TS interfaces from backend DTOs
│   └── *.Api.ts          # Per-domain API functions
├── features/
│   ├── auth/             # AuthContext, ProtectedRoute, role guards
│   ├── shipments/        # TanStack Query hooks for shipment domain
│   ├── carrier/          # Fleet, dispatch, company hooks
│   ├── tracking/         # GPS telemetry hooks
│   └── payments/         # Payment initiation + callback hooks
├── components/
│   ├── brand/            # SVG logo, symbol, wordmark (light + dark variants)
│   ├── ui/               # Design system: Button, Input, Table, Modal, Toast, …
│   ├── map/              # Leaflet + OpenStreetMap shipment map
│   └── layout/           # AppShell, Sidebar, TopBar, MobileNav
├── pages/
│   ├── public/           # Landing, Login, Register, Tracking, 404, 403
│   ├── customer/         # Dashboard, Shipments list/detail, Create, Tracking, Proof
│   ├── driver/           # Cockpit (current job), Assignments history, Dashboard
│   ├── carrier/          # Dashboard, Freight exchange, Dispatch, Fleet, Drivers, Team
│   └── shared/           # Payment callback, Dev components showcase
├── hooks/                # usePageTitle
├── lib/                  # queryClient, formatters, boot, utils
└── test/
    ├── mocks/            # MSW handlers (real backend DTO shapes)
    └── test-utils.tsx    # renderWithProviders wrapper
```

---

## Technology Stack

| Concern | Library | Version |
|---|---|---|
| UI Framework | React | 18.3.1 |
| Build | Vite | 6.x |
| Language | TypeScript | 5.7 (`strict: true`) |
| Routing | React Router v6 | 6.28 (data router, lazy splits) |
| Styles | Tailwind CSS | 3.4 |
| Icons | Lucide React | 0.468 |
| Headless UI | Radix UI (Dialog, DropdownMenu, Select, Tabs, Tooltip) | 1.x–2.x |
| Server state | TanStack Query | 5.62 |
| Forms | React Hook Form + Zod | 7.54 / 3.24 |
| HTTP | Axios | 1.7 |
| Map | Leaflet + react-leaflet | 1.9 / 4.2 (OpenStreetMap tiles) |
| Testing | Vitest + RTL + MSW | 2.1 / 16.1 / 2.7 |
| Linting | ESLint + @typescript-eslint | 8.57 |
| Formatting | Prettier | 3.4 |

---

## Environment Variables

Create `frontend/.env.local` for local overrides (never commit secrets):

```env
# Backend base URL — dev proxy handles /api and /health automatically.
# Only set this for non-proxied deployments (e.g. Docker, staging).
VITE_BACKEND_URL=http://localhost:5000

# Paymob iframe origin for postMessage CSP (optional, tightens CSP in production)
VITE_PAYMOB_ORIGIN=https://accept.paymob.com
```

The Vite dev server proxies `/api/*` and `/health/*` to `VITE_BACKEND_URL` automatically, so no CORS configuration is needed in development.

---

## Role System

The backend defines five platform roles. The frontend enforces navigation guards (not security — the backend enforces authorization):

| Role | Access |
|---|---|
| `Customer` | Create shipments, view own shipments, accept quotes, initiate payment, track |
| `CompanyAdmin` | All Dispatcher capabilities + company/team/fleet management, driver registration |
| `Dispatcher` | View freight exchange, submit quotes, dispatch (assign driver+vehicle) |
| `Driver` | Active job cockpit, GPS telemetry, pickup/transit/deliver actions |
| `PlatformAdmin` | Reserved (no dedicated frontend screens in v1) |

### Role Guard Behavior
- `ProtectedRoute` wraps every authenticated route with role checking
- Visiting a route with an incorrect role shows the branded **403 Forbidden** page
- Backend anti-probing: 404 responses for resources belonging to other companies are shown as clean "not found" states, not access-denied errors
- JWT does not refresh automatically (15-min expiry). When a 401 is received, the 401 interceptor fires the unauthorized listener → clears token and redirects to `/login`

---

## Token Storage

- **Primary:** In-memory (`let currentAccessToken`), cleared on page close
- **Fallback:** `localStorage` key `shipflow_auth_token` for session restoration on reload
- No `httpOnly` cookies (backend does not issue them). This is disclosed in `docs/INTEGRATION_GAPS.md`
- On logout: token cleared from memory + localStorage + full TanStack Query cache invalidation

---

## Map Provider

Leaflet + OpenStreetMap tiles (no API key required). Tile URL:
```
https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png
```
Attribution is preserved as required by the ODbL license. The `ShipmentMap` component renders:
- Origin marker (green brand marker)
- Destination marker (emerald accent marker)
- GPS tracking point (blue pulsing marker) with live polling
- Route polyline between stops

No proprietary map API keys or paid tile services are used.

---

## Payment Flow

1. Customer clicks **"Pay Now"** on an Accepted shipment
2. Frontend calls `POST /api/payments/initiate` with `{ shipmentId }`
3. Backend returns `{ iframeUrl, transactionId }` (Paymob hosted checkout)
4. Frontend renders the iframe at `iframeUrl`
5. Paymob redirects browser to `/payments/callback?…` with query params
6. `PaymentCallbackPage` reads redirect params, calls `GET /api/payments/callback` to verify server-side
7. On success: re-fetches shipment detail. On failure: shows clear error with retry link

> **Note:** Paymob server-to-server webhook (`POST /api/payments/callback`) is handled by the backend only. The frontend never handles raw webhook HMAC payloads.

---

## Splash Screen

Defined in `src/lib/boot.ts` and inlined in `index.html`:
- **Minimum display:** ~400ms (so animation does not flash on fast loads)
- **Hard timeout:** 3.8 seconds — if API is unreachable, splash dismisses regardless
- `prefers-reduced-motion`: replaces animation with simple opacity fade
- Shows only on first boot, not on route navigation or hot reload

---

## Design Tokens

All tokens are defined as CSS variables in `src/index.css` and extended into Tailwind via `tailwind.config.js`.

Key semantic classes:
- `bg-forest-night`, `bg-deep-forest` — dark sidebar/splash surfaces
- `text-brand`, `bg-brand`, `hover:bg-brand-hover` — primary interactive color
- `text-brand-accent` — emerald highlight (not for interactive elements)
- `bg-neutral-appBg`, `bg-neutral-surface` — app background / card surface
- `text-neutral-primary`, `text-neutral-secondary`, `text-neutral-muted` — text hierarchy

Status badge palette (WCAG AA, icon + color, never color-only):

| Status | Color | Icon |
|---|---|---|
| Pending | gray | clock |
| Quoting | blue | activity |
| Accepted | indigo | check |
| Assigned | purple | truck |
| PickedUp | amber | package |
| InTransit | cyan | arrow-right |
| Delivered | mint/green | check-circle |
| DeliveryFailed | red | alert-triangle |
| Cancelled | neutral | x-circle |

---

## Testing

```bash
npm run test           # Run all 36 tests once
npm run test:watch     # Watch mode
```

**Test files and coverage:**

| File | What is tested |
|---|---|
| `src/api/errors.test.ts` | `mapApiError` — 400 field errors, 409, 429 Retry-After, network failure, generic Error |
| `src/features/auth/AuthContext.test.tsx` | Session restore, login, invalid credentials, logout, token store |
| `src/components/brand/ShipFlowLogo.test.tsx` | Light/dark variant rendering, standalone symbol |
| `src/components/ui/StatusBadge.test.tsx` | All 9 shipment statuses, icon presence, label accuracy |
| `src/pages/public/LoginPage.test.tsx` | Form rendering, validation errors, 401 server error |
| `src/pages/public/RegisterPage.test.tsx` | Customer role disclosure, password validation, 409 conflict |
| `src/pages/customer/ShipmentsListPage.test.tsx` | Data rendering, empty state, error state |
| `src/pages/driver/DriverCurrentJobPage.test.tsx` | Active job with pickup button, idle/no-assignment state |

MSW handlers in `src/test/mocks/handlers.ts` mirror real backend DTO shapes derived from the TypeScript types in `src/api/types/`.

---

## Integration Gaps

Features not yet fully supported by the backend are documented in `docs/INTEGRATION_GAPS.md`:

| Gap | Frontend Handling |
|---|---|
| No refresh token endpoint | 15-min JWT; 401 → prompt re-login. No silent renewal. |
| No server logout | Client-side token clear only |
| No password reset | Honest "not supported" banner on `/forgot-password` |
| No public anonymous tracking | `/tracking` explains auth requirement; quick sign-in link |
| No payment status query endpoint | Poll shipment detail after payment callback; manual "Refresh" button |
| No file upload for delivery proof | Reference string input only; no fake upload pretended |
| No analytics/aggregation endpoints | Dashboard metrics scoped to loaded data, labeled honestly |

---

## Production Deployment

### Static hosting (Vercel, Netlify, S3+CloudFront, Nginx)

```bash
npm run build
# Serve dist/ as a static site
# All routes must fall back to index.html (SPA routing)
```

**Required security headers:**
```
Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https://*.tile.openstreetmap.org; connect-src 'self' https://YOUR_API_DOMAIN; frame-src https://accept.paymob.com;
X-Frame-Options: SAMEORIGIN
X-Content-Type-Options: nosniff
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: geolocation=(self)
```

### Docker example (Nginx)

```dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
ARG VITE_BACKEND_URL=http://api:5000
ENV VITE_BACKEND_URL=$VITE_BACKEND_URL
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
```

`nginx.conf` must include `try_files $uri $uri/ /index.html;` for SPA routing.

---

## Backend Audit Summary

Full audit at `docs/BACKEND_AUDIT.md`. Quick summary:

- **8 controllers**, 48 endpoints across Auth, Shipments, Quotes, Dispatch, Fleet, Tracking, Payments, Companies, Health
- **Auth scheme:** Stateless JWT, 15-minute expiry, `Bearer` scheme, roles in token claims
- **Error format:** RFC 7807 Problem Details (`application/problem+json`)
- **Pagination:** `{ items, totalCount, page, pageSize, totalPages }` envelope
- **Rate limits:** GPS tracking 60 req/min; auth 20 req/min; general 200 req/min
- **CORS:** `AllowAnyOrigin` in development — tighten for production

---

## Scripts Reference

| Script | Purpose |
|---|---|
| `npm run dev` | Vite dev server on port 3000 with API proxy |
| `npm run build` | TypeScript compile + Vite production bundle |
| `npm run test` | Vitest single run (36 tests) |
| `npm run test:watch` | Vitest watch mode |
| `npm run lint` | ESLint with `@typescript-eslint` |
| `npm run typecheck` | `tsc --noEmit` (strict) |
| `npm run format` | Prettier over `src/**` |
| `npm run generate-icons` | Regenerate PWA/favicon rasters from SVG via `sharp` |

---

*ShipFlow Frontend — built against ShipFlow API v1 (October 2026)*
