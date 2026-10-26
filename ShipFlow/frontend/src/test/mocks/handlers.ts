import { http, HttpResponse } from 'msw';
import { ShipmentDetailsDto, ShipmentSummaryDto, CurrentUserResponse, DriverDto, VehicleDto, AssignmentDto } from '@/api/types';

export const mockUser: CurrentUserResponse = {
  id: 'usr_c1a2b3',
  email: 'customer@shipflow.local',
  roles: ['Customer'],
};

export const mockCarrierUser: CurrentUserResponse = {
  id: 'usr_car123',
  email: 'carrier@shipflow.local',
  roles: ['CompanyAdmin'],
};

export const mockDriverUser: CurrentUserResponse = {
  id: 'usr_drv123',
  email: 'driver@shipflow.local',
  roles: ['Driver'],
};

export const mockShipment: ShipmentDetailsDto = {
  id: 'sh_test_001',
  reference: 'REF-2026-0001',
  status: 'Pending',
  createdAtUtc: '2026-10-25T08:00:00Z',
  updatedAtUtc: '2026-10-25T08:00:00Z',
  cargo: {
    description: 'Palletized electronics and sensors',
    weightKg: 1250,
    volumeM3: 4.8,
  },
  stops: [
    {
      id: 'st_001',
      sequence: 1,
      type: 'Pickup',
      address: '100 Industrial Pkwy, Chicago, IL',
      latitude: 41.8781,
      longitude: -87.6298,
      windowStartUtc: '2026-11-01T08:00:00Z',
      windowEndUtc: '2026-11-01T12:00:00Z',
    },
    {
      id: 'st_002',
      sequence: 2,
      type: 'Delivery',
      address: '500 Logistics Ave, Indianapolis, IN',
      latitude: 39.7684,
      longitude: -86.1581,
      windowStartUtc: '2026-11-01T16:00:00Z',
      windowEndUtc: '2026-11-01T20:00:00Z',
    },
  ],
  agreedAmount: null,
  agreedCurrency: null,
  carrierCompanyId: null,
  quotes: [],
};

export const mockDriver: DriverDto = {
  id: 'drv_001',
  userId: 'usr_drv123',
  companyId: 'comp_001',
  fullName: 'John Driver',
  licenseNumber: 'CDL-IL-987654',
  isActive: true,
  createdAtUtc: '2026-01-01T00:00:00Z',
};

export const mockVehicle: VehicleDto = {
  id: 'veh_001',
  companyId: 'comp_001',
  plateNumber: 'IL-TRK-408',
  type: 'SemiTruck',
  maxWeightKg: 18000,
  maxVolumeM3: 65,
  isActive: true,
  createdAtUtc: '2026-01-01T00:00:00Z',
};

export const mockAssignment: AssignmentDto = {
  id: 'asg_001',
  shipmentId: 'sh_test_001',
  companyId: 'comp_001',
  driverId: 'drv_001',
  vehicleId: 'veh_001',
  windowStartUtc: '2026-11-01T08:00:00Z',
  windowEndUtc: '2026-11-01T12:00:00Z',
  status: 'Active',
  shipmentStatus: 'Assigned',
  assignedByUserId: 'usr_disp1',
  createdAtUtc: '2026-10-25T09:00:00Z',
  endedAtUtc: null,
  releaseReason: null,
};

export const handlers = [
  // Health
  http.get('*/health', () => {
    return HttpResponse.json({ status: 'Healthy', timestampUtc: new Date().toISOString() });
  }),
  http.get('*/health/ready', () => {
    return HttpResponse.json({ status: 'Healthy', timestampUtc: new Date().toISOString() });
  }),

  // Auth
  http.get('*/api/auth/me', () => {
    return HttpResponse.json(mockUser);
  }),
  http.post('*/api/auth/login', async ({ request }) => {
    const body = (await request.json()) as any;
    if (body.password === 'WrongPassword!1') {
      return HttpResponse.json(
        { title: 'Unauthorized', status: 401, detail: 'Invalid email or password.' },
        { status: 401 }
      );
    }
    return HttpResponse.json({
      accessToken: 'mock-valid-jwt-token-xyz',
      tokenType: 'Bearer',
      expiresAtUtc: '2026-10-25T08:15:00Z',
      user: {
        id: mockUser.id!,
        email: mockUser.email!,
        fullName: 'Acme Logistics Manager',
        roles: mockUser.roles,
      },
    });
  }),
  http.post('*/api/auth/register', async ({ request }) => {
    const body = (await request.json()) as any;
    if (body.email === 'duplicate@shipflow.local') {
      return HttpResponse.json(
        {
          title: 'Conflict',
          status: 409,
          detail: 'A user with this email already exists.',
        },
        { status: 409 }
      );
    }
    return HttpResponse.json({
      id: 'usr_new_999',
      email: body.email,
      fullName: body.fullName,
      roles: ['Customer'],
    });
  }),

  // Shipments
  http.get('*/api/shipments', ({ request }) => {
    const url = new URL(request.url);
    const page = Number(url.searchParams.get('page') || '1');
    const pageSize = Number(url.searchParams.get('pageSize') || '20');
    return HttpResponse.json({
      items: [
        {
          id: mockShipment.id,
          reference: mockShipment.reference,
          status: mockShipment.status,
          createdAtUtc: mockShipment.createdAtUtc,
          updatedAtUtc: mockShipment.updatedAtUtc,
          cargoDescription: mockShipment.cargo.description,
          cargoWeightKg: mockShipment.cargo.weightKg,
          cargoVolumeM3: mockShipment.cargo.volumeM3,
          pickupAddress: '100 Industrial Pkwy, Chicago, IL',
          deliveryAddress: '500 Logistics Ave, Indianapolis, IN',
          agreedAmount: null,
          agreedCurrency: null,
          carrierCompanyId: null,
        } as ShipmentSummaryDto,
      ],
      totalCount: 1,
      page,
      pageSize,
      totalPages: 1,
    });
  }),
  http.get('*/api/shipments/:id', ({ params }) => {
    if (params.id === 'not-found') {
      return HttpResponse.json(
        { title: 'Not Found', status: 404, detail: 'Shipment not found.' },
        { status: 404 }
      );
    }
    return HttpResponse.json(mockShipment);
  }),
  http.post('*/api/shipments', async ({ request }) => {
    const body = (await request.json()) as any;
    return HttpResponse.json({
      ...mockShipment,
      id: 'sh_created_999',
      reference: body.reference || 'REF-AUTO-999',
    });
  }),
  http.post('*/api/shipments/:id/open-for-quotes', () => {
    return HttpResponse.json({ ...mockShipment, status: 'Quoting' });
  }),
  http.post('*/api/shipments/:id/cancel', () => {
    return HttpResponse.json({ ...mockShipment, status: 'Cancelled' });
  }),

  // Driver Endpoints
  http.get('*/api/driver/assignments/current', () => {
    return HttpResponse.json(mockAssignment);
  }),
  http.get('*/api/driver/assignments', () => {
    return HttpResponse.json({
      items: [mockAssignment],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  }),
  http.post('*/api/shipments/:id/pickup', () => {
    return HttpResponse.json({ ...mockShipment, status: 'PickedUp' });
  }),
  http.post('*/api/shipments/:id/start-transit', () => {
    return HttpResponse.json({ ...mockShipment, status: 'InTransit' });
  }),
  http.post('*/api/shipments/:id/deliver', async ({ request }) => {
    const body = (await request.json()) as any;
    return HttpResponse.json({
      shipmentId: mockShipment.id,
      deliveredAtUtc: body.deliveredAtUtc || new Date().toISOString(),
      recipientName: body.recipientName,
      notes: body.notes || null,
      signatureReference: body.signatureReference || null,
      photoReference: body.photoReference || null,
    });
  }),

  // Tracking
  http.get('*/api/shipments/:id/tracking/latest', () => {
    return HttpResponse.json({
      id: 'trk_001',
      shipmentId: mockShipment.id,
      latitude: 40.5,
      longitude: -86.9,
      recordedAtUtc: new Date().toISOString(),
      speedKph: 88,
    });
  }),
  http.post('*/api/tracking', async ({ request }) => {
    const body = (await request.json()) as any;
    return HttpResponse.json({
      id: 'trk_new_1',
      shipmentId: body.shipmentId,
      latitude: body.latitude,
      longitude: body.longitude,
      speedKph: body.speedKph || null,
      recordedAtUtc: body.recordedAtUtc,
    });
  }),

  // Fleet & Companies
  http.get('*/api/companies/me', () => {
    return HttpResponse.json({
      id: 'comp_001',
      name: 'Atlas Freight Lines LLC',
      createdAtUtc: '2026-01-01T00:00:00Z',
    });
  }),
  http.get('*/api/fleet/drivers', () => {
    return HttpResponse.json({
      items: [mockDriver],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  }),
  http.get('*/api/fleet/vehicles', () => {
    return HttpResponse.json({
      items: [mockVehicle],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  }),
];
