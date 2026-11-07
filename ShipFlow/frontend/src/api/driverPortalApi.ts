import { apiClient } from './client';
import {
  AssignmentDto,
  AvailableShipmentDto,
  DriverProfileDto,
  UpdateDriverAvailabilityCommand,
  AssignDriverVehicleCommand,
} from './types';

export const driverPortalApi = {
  getProfile: async (signal?: AbortSignal): Promise<DriverProfileDto> => {
    const res = await apiClient.get<DriverProfileDto>('/api/driver-portal/profile', { signal });
    return res.data;
  },

  updateAvailability: async (
    command: UpdateDriverAvailabilityCommand,
    signal?: AbortSignal
  ): Promise<DriverProfileDto> => {
    const res = await apiClient.put<DriverProfileDto>('/api/driver-portal/availability', command, { signal });
    return res.data;
  },

  getAvailableShipments: async (signal?: AbortSignal): Promise<AvailableShipmentDto[]> => {
    const res = await apiClient.get<AvailableShipmentDto[]>('/api/driver-portal/available-shipments', { signal });
    return res.data;
  },

  acceptShipment: async (shipmentId: string, signal?: AbortSignal): Promise<AssignmentDto> => {
    const res = await apiClient.post<AssignmentDto>(
      `/api/driver-portal/available-shipments/${shipmentId}/accept`,
      {},
      { signal }
    );
    return res.data;
  },

  assignDriverVehicle: async (
    driverId: string,
    command: AssignDriverVehicleCommand,
    signal?: AbortSignal
  ): Promise<void> => {
    await apiClient.put(`/api/drivers/${driverId}/vehicle`, command, { signal });
  },
};
