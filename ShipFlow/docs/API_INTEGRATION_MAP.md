# ShipFlow API Integration Map

**Reference:** Section 11.1 of Product Specification  
**Methodology:** Strict traceability: `Endpoint → Typed API Function → TanStack Query Hook → Screen / Component → UI States`

---

## 1. Traceability Table

| Area | Method | Endpoint | Roles Allowed | API Function | Hook | Screen / Route | Request Fields | Response Type | Notes / Gaps |
|---|---|---|---|---|---|---|---|---|---|
| **Health** | GET | `/health` | Anonymous | `api.health.getLiveness` | `useHealthCheck` | Splash / Boot | None | `{ status, timestampUtc }` | Liveness check |
| **Health** | GET | `/health/ready` | Anonymous | `api.health.getReadiness` | `useReadinessCheck` | App Boot / Settings | None | `{ status, timestampUtc }` | Database reachable |
| **Auth** | POST | `/api/auth/register` | Anonymous | `api.auth.register` | `useRegisterMutation` | `/register` | `{ email, password, fullName }` | `UserDto` | Self-registration grants Customer only |
| **Auth** | POST | `/api/auth/login` | Anonymous | `api.auth.login` | `useLoginMutation` | `/login` | `{ email, password }` | `LoginResult` | Returns access token + UserDto |
| **Auth** | GET | `/api/auth/me` | Authenticated | `api.auth.getMe` | `useCurrentUserQuery` | App Shell / Guards | None | `CurrentUserResponse` | Validates token claims |
| **Shipments** | POST | `/api/shipments` | Customer | `api.shipments.create` | `useCreateShipmentMutation` | `/app/shipments/new` | `{ reference?, cargo, stops }` | `ShipmentDetailsDto` | Pickup requires UTC window |
| **Shipments** | GET | `/api/shipments` | ShipmentViewer | `api.shipments.list` | `useShipmentsListQuery` | `/app/shipments`, `/carrier/shipments` | `scope`, `statuses`, `search`, `page`, `pageSize`, etc. | `PagedResult<ShipmentSummaryDto>` | Paged shipments list |
| **Shipments** | GET | `/api/shipments/{id}` | ShipmentViewer | `api.shipments.getById` | `useShipmentDetailQuery` | `/app/shipments/:id`, `/carrier/shipments/:id` | `id` (path) | `ShipmentDetailsDto` | Complete shipment & quotes |
| **Shipments** | POST | `/api/shipments/{id}/open-for-quotes` | Customer | `api.shipments.openForQuotes` | `useOpenForQuotesMutation` | `/app/shipments/:id` | `id` (path) | `ShipmentDetailsDto` | Pending -> Quoting |
| **Shipments** | POST | `/api/shipments/{id}/cancel` | ShipmentViewer | `api.shipments.cancel` | `useCancelShipmentMutation` | `/app/shipments/:id` | `reason` | `ShipmentDetailsDto` | Releases active assignment |
| **Quotations** | POST | `/api/shipments/{id}/quotes` | Dispatcher, CompanyAdmin | `api.quotations.submit` | `useSubmitQuoteMutation` | `/carrier/shipments/:id` | `{ amount, currency, expiresAtUtc, terms }` | `QuoteDto` | Company derived from membership |
| **Quotations** | GET | `/api/shipments/{id}/quotes` | ShipmentViewer | `api.quotations.list` | `useShipmentQuotesQuery` | `/app/shipments/:id`, `/carrier/shipments/:id` | `id` (path) | `QuoteDto[]` | Owner sees all; carrier sees own |
| **Quotations** | POST | `/api/shipments/{id}/quotes/{quoteId}/accept` | Customer | `api.quotations.accept` | `useAcceptQuoteMutation` | `/app/shipments/:id` | `id`, `quoteId` | `ShipmentDetailsDto` | Sets carrier and agreed price |
| **Quotations** | POST | `/api/shipments/{id}/quotes/{quoteId}/withdraw` | Dispatcher, CompanyAdmin | `api.quotations.withdraw` | `useWithdrawQuoteMutation` | `/carrier/shipments/:id` | `id`, `quoteId` | `QuoteDto` | Withdraws active quote |
| **Companies** | POST | `/api/companies` | Customer | `api.companies.create` | `useCreateCompanyMutation` | `/carrier/onboarding` | `{ name }` | `CompanyDto` | Caller becomes CompanyAdmin |
| **Companies** | GET | `/api/companies/me` | CompanyStaff | `api.companies.getMine` | `useMyCompanyQuery` | `/carrier/settings`, Carrier Nav | None | `CompanyDto` | Caller's current transport company |
| **Companies** | GET | `/api/companies/me/members` | CompanyAdmin | `api.companies.getMembers` | `useCompanyMembersQuery` | `/carrier/team` | `includeFormer` (query) | `MemberDto[]` | Lists dispatchers and drivers |
| **Companies** | POST | `/api/companies/me/members` | CompanyAdmin | `api.companies.addMember` | `useAddCompanyMemberMutation` | `/carrier/team` | `{ email, role }` | `MemberDto` | Adds Dispatcher or Driver |
| **Companies** | DELETE | `/api/companies/me/members/{userId}` | CompanyAdmin | `api.companies.removeMember` | `useRemoveCompanyMemberMutation` | `/carrier/team` | `userId` (path) | `void` (204) | Revokes role & deactivates profile |
| **Fleet** | POST | `/api/fleet/drivers` | CompanyAdmin | `api.fleet.registerDriver` | `useRegisterDriverMutation` | `/carrier/drivers/new` | `{ userId, fullName, licenseNumber }` | `DriverDto` | Driver profile registration |
| **Fleet** | GET | `/api/fleet/drivers` | Dispatcher, CompanyAdmin | `api.fleet.listDrivers` | `useDriversListQuery` | `/carrier/drivers` | `isActive`, `page`, `pageSize` | `PagedResult<DriverDto>` | List company drivers |
| **Fleet** | GET | `/api/fleet/drivers/{id}` | Dispatcher, CompanyAdmin | `api.fleet.getDriverById` | `useDriverDetailQuery` | `/carrier/drivers/:id` | `id` (path) | `DriverDto` | Single driver profile |
| **Fleet** | PUT | `/api/fleet/drivers/{id}` | CompanyAdmin | `api.fleet.updateDriver` | `useUpdateDriverMutation` | `/carrier/drivers/:id/edit` | `{ fullName, licenseNumber }` | `DriverDto` | Edit driver profile |
| **Fleet** | POST | `/api/fleet/drivers/{id}/activate` | CompanyAdmin | `api.fleet.activateDriver` | `useActivateDriverMutation` | `/carrier/drivers/:id` | `id` (path) | `DriverDto` | Reactivate driver |
| **Fleet** | POST | `/api/fleet/drivers/{id}/deactivate` | CompanyAdmin | `api.fleet.deactivateDriver` | `useDeactivateDriverMutation` | `/carrier/drivers/:id` | `id` (path) | `DriverDto` | Blocked if active assignment |
| **Fleet** | POST | `/api/fleet/vehicles` | CompanyAdmin | `api.fleet.registerVehicle` | `useRegisterVehicleMutation` | `/carrier/fleet/new` | `{ plateNumber, type, maxWeightKg, maxVolumeM3 }` | `VehicleDto` | Register fleet vehicle |
| **Fleet** | GET | `/api/fleet/vehicles` | Dispatcher, CompanyAdmin | `api.fleet.listVehicles` | `useVehiclesListQuery` | `/carrier/fleet` | `isActive`, `page`, `pageSize` | `PagedResult<VehicleDto>` | List company vehicles |
| **Fleet** | GET | `/api/fleet/vehicles/{id}` | Dispatcher, CompanyAdmin | `api.fleet.getVehicleById` | `useVehicleDetailQuery` | `/carrier/fleet/:id` | `id` (path) | `VehicleDto` | Single vehicle detail |
| **Fleet** | PUT | `/api/fleet/vehicles/{id}/capacity` | CompanyAdmin | `api.fleet.updateVehicleCapacity` | `useUpdateVehicleCapacityMutation` | `/carrier/fleet/:id/capacity` | `{ maxWeightKg, maxVolumeM3 }` | `VehicleDto` | Cannot shrink during active job |
| **Fleet** | POST | `/api/fleet/vehicles/{id}/activate` | CompanyAdmin | `api.fleet.activateVehicle` | `useActivateVehicleMutation` | `/carrier/fleet/:id` | `id` (path) | `VehicleDto` | Reactivate vehicle |
| **Fleet** | POST | `/api/fleet/vehicles/{id}/deactivate` | CompanyAdmin | `api.fleet.deactivateVehicle` | `useDeactivateVehicleMutation` | `/carrier/fleet/:id` | `id` (path) | `VehicleDto` | Blocked if active assignment |
| **Dispatch** | GET | `/api/shipments/{shipmentId}/available-vehicles` | Dispatcher, CompanyAdmin | `api.dispatch.getAvailableVehicles` | `useAvailableVehiclesQuery` | `/carrier/dispatch/:shipmentId` | `windowStartUtc`, `windowEndUtc` | `VehicleDto[]` | Filters by cargo capacity & schedule |
| **Dispatch** | GET | `/api/shipments/{shipmentId}/available-drivers` | Dispatcher, CompanyAdmin | `api.dispatch.getAvailableDrivers` | `useAvailableDriversQuery` | `/carrier/dispatch/:shipmentId` | `windowStartUtc`, `windowEndUtc` | `DriverDto[]` | Filters by schedule overlap |
| **Dispatch** | POST | `/api/shipments/{shipmentId}/assignment` | Dispatcher, CompanyAdmin | `api.dispatch.assign` | `useAssignShipmentMutation` | `/carrier/dispatch/:shipmentId` | `{ driverId, vehicleId, windowStartUtc, windowEndUtc }` | `AssignmentDto` | Accepted -> Assigned |
| **Dispatch** | GET | `/api/shipments/{shipmentId}/assignment` | Dispatcher, CompanyAdmin | `api.dispatch.getAssignment` | `useShipmentAssignmentQuery` | `/carrier/dispatch/:shipmentId`, `/carrier/shipments/:id` | `shipmentId` (path) | `AssignmentDto` | Returns active assignment |
| **Dispatch** | POST | `/api/shipments/{shipmentId}/unassign` | Dispatcher, CompanyAdmin | `api.dispatch.unassign` | `useUnassignShipmentMutation` | `/carrier/dispatch/:shipmentId` | `{ reason }` | `ShipmentDetailsDto` | Assigned -> Accepted, frees resources |
| **Tracking** | POST | `/api/tracking` | Driver | `api.tracking.submitPoint` | `useSubmitTrackingPointMutation` | `/driver/current-job` | `{ shipmentId, latitude, longitude, recordedAtUtc, speedKph? }` | `TrackingPointDto` | Rate-limited: 60/min |
| **Tracking** | GET | `/api/shipments/{shipmentId}/tracking/latest` | ShipmentViewer | `api.tracking.getLatest` | `useLatestTrackingPointQuery` | `/app/shipments/:id/tracking`, `/carrier/shipments/:id` | `shipmentId` (path) | `TrackingPointDto` | Latest GPS coordinate |
| **Tracking** | GET | `/api/shipments/{shipmentId}/tracking` | ShipmentViewer | `api.tracking.getHistory` | `useTrackingHistoryQuery` | `/app/shipments/:id/tracking`, `/carrier/shipments/:id` | `fromUtc`, `toUtc`, `page`, `pageSize` | `PagedResult<TrackingPointDto>` | Paged GPS trail |
| **Driver** | POST | `/api/shipments/{shipmentId}/pickup` | Driver | `api.driver.pickup` | `useDriverPickupMutation` | `/driver/current-job` | `shipmentId` (path) | `ShipmentSummaryDto` | Assigned -> PickedUp |
| **Driver** | POST | `/api/shipments/{shipmentId}/start-transit` | Driver | `api.driver.startTransit` | `useDriverStartTransitMutation` | `/driver/current-job` | `shipmentId` (path) | `ShipmentSummaryDto` | PickedUp -> InTransit |
| **Driver** | POST | `/api/shipments/{shipmentId}/deliver` | Driver | `api.driver.deliver` | `useDriverDeliverMutation` | `/driver/current-job` | `{ deliveredAtUtc, recipientName, notes?, signatureReference?, photoReference? }` | `DeliveryProofDto` | InTransit -> Delivered, creates proof |
| **Driver** | POST | `/api/shipments/{shipmentId}/fail-delivery` | Driver | `api.driver.failDelivery` | `useDriverFailDeliveryMutation` | `/driver/current-job` | `{ reason }` | `ShipmentSummaryDto` | InTransit -> DeliveryFailed |
| **Driver** | POST | `/api/shipments/{shipmentId}/retry-delivery` | Driver | `api.driver.retryDelivery` | `useDriverRetryDeliveryMutation` | `/driver/current-job` | `shipmentId` (path) | `ShipmentSummaryDto` | DeliveryFailed -> InTransit |
| **Driver** | GET | `/api/shipments/{shipmentId}/delivery-proof` | ShipmentViewer | `api.driver.getDeliveryProof` | `useDeliveryProofQuery` | `/app/shipments/:id/proof`, `/carrier/shipments/:id` | `shipmentId` (path) | `DeliveryProofDto` | Proof of delivery |
| **Driver** | GET | `/api/driver/assignments/current` | Driver | `api.driver.getCurrentAssignment` | `useDriverCurrentAssignmentQuery` | `/driver/current-job`, `/driver/dashboard` | None | `AssignmentDto` | Driver's active job |
| **Driver** | GET | `/api/driver/assignments` | Driver | `api.driver.getAssignmentHistory` | `useDriverAssignmentHistoryQuery` | `/driver/assignments` | `page`, `pageSize` | `PagedResult<AssignmentDto>` | Driver's completed/past jobs |
| **Payments** | POST | `/api/payments/initiate` | Customer | `api.payments.initiate` | `useInitiatePaymentMutation` | `/app/shipments/:id/payment` | `{ shipmentId }` | `PaymentResponseDto` | Generates Paymob checkout iframe/url |
| **Payments** | POST | `/api/payments/callback` | Anonymous | Paymob Server Webhook | Handled by backend | Backend webhook | HMAC Payload | `PaymentCallbackResponse` | Server-to-server webhook |
| **Payments** | GET | `/api/payments/callback` | Anonymous | `api.payments.handleRedirect` | `usePaymentRedirectCallbackQuery` | `/payments/callback` | Query string params | `PaymentCallbackResponse` | Client return URL |

---

## 2. Backend Endpoints with No Dedicated Frontend Screen Yet (and Why)
- `POST /api/payments/callback`: This is a backend-to-backend Paymob webhook endpoint called directly by Paymob's servers, not a browser screen.
- Health checks (`/health`, `/health/ready`): Used during app boot / splash and connection health monitoring rather than standalone pages.

---

## 3. Frontend Views Handled Transparently Without Direct Backend Endpoints
- **Public Anonymous Tracking (`/tracking`):** Handled with transparent UX explaining that tracking requires user authentication for cargo security, routing authenticated users directly to their shipment tracking view.
- **Password Reset (`/forgot-password`, `/reset-password`):** Renders structured UI with an honest banner noting backend email delivery is pending configuration.
- **Aggregated Analytics / Fleet Utilization Cards:** Computed strictly from currently loaded shipment and fleet lists, with explicit disclosure of scope (e.g. "Calculated from current page data").
