import { apiClient } from './client';
import {
  AssignShipmentCommand,
  AssignmentDto,
  AvailabilityQuery,
  DriverDto,
  ShipmentDetailsDto,
  UnassignShipmentCommand,
  VehicleDto,
} from './types';

export const dispatchApi = {
  getAvailableVehicles: async (
    shipmentId: string,
    query: AvailabilityQuery,
    signal?: AbortSignal
  ): Promise<VehicleDto[]> => {
    const params = new URLSearchParams();
    params.append('windowStartUtc', query.windowStartUtc);
    params.append('windowEndUtc', query.windowEndUtc);

    const res = await apiClient.get<VehicleDto[]>(`/api/shipments/${shipmentId}/available-vehicles`, {
      params,
      signal,
    });
    return res.data;
  },

  getAvailableDrivers: async (
    shipmentId: string,
    query: AvailabilityQuery,
    signal?: AbortSignal
  ): Promise<DriverDto[]> => {
    const params = new URLSearchParams();
    params.append('windowStartUtc', query.windowStartUtc);
    params.append('windowEndUtc', query.windowEndUtc);

    const res = await apiClient.get<DriverDto[]>(`/api/shipments/${shipmentId}/available-drivers`, {
      params,
      signal,
    });
    return res.data;
  },

  assign: async (
    shipmentId: string,
    command: AssignShipmentCommand,
    signal?: AbortSignal
  ): Promise<AssignmentDto> => {
    const res = await apiClient.post<AssignmentDto>(`/api/shipments/${shipmentId}/assignment`, command, { signal });
    return res.data;
  },

  getAssignment: async (shipmentId: string, signal?: AbortSignal): Promise<AssignmentDto> => {
    const res = await apiClient.get<AssignmentDto>(`/api/shipments/${shipmentId}/assignment`, { signal });
    return res.data;
  },

  unassign: async (
    shipmentId: string,
    command: UnassignShipmentCommand,
    signal?: AbortSignal
  ): Promise<ShipmentDetailsDto> => {
    const res = await apiClient.post<ShipmentDetailsDto>(`/api/shipments/${shipmentId}/unassign`, command, { signal });
    return res.data;
  },
};
