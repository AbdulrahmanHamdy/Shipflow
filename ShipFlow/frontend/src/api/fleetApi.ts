import { apiClient } from './client';
import {
  DriverDto,
  ListFleetQuery,
  PagedResult,
  RegisterDriverCommand,
  RegisterVehicleCommand,
  UpdateDriverCommand,
  UpdateVehicleCapacityCommand,
  VehicleDto,
} from './types';

export const fleetApi = {
  // Drivers
  registerDriver: async (command: RegisterDriverCommand, signal?: AbortSignal): Promise<DriverDto> => {
    const res = await apiClient.post<DriverDto>('/api/fleet/drivers', command, { signal });
    return res.data;
  },

  listDrivers: async (query?: ListFleetQuery, signal?: AbortSignal): Promise<PagedResult<DriverDto>> => {
    const params = new URLSearchParams();
    if (query?.isActive != null) params.append('isActive', query.isActive.toString());
    if (query?.page) params.append('page', query.page.toString());
    if (query?.pageSize) params.append('pageSize', query.pageSize.toString());

    const res = await apiClient.get<PagedResult<DriverDto>>('/api/fleet/drivers', { params, signal });
    return res.data;
  },

  getDriverById: async (id: string, signal?: AbortSignal): Promise<DriverDto> => {
    const res = await apiClient.get<DriverDto>(`/api/fleet/drivers/${id}`, { signal });
    return res.data;
  },

  updateDriver: async (id: string, command: UpdateDriverCommand, signal?: AbortSignal): Promise<DriverDto> => {
    const res = await apiClient.put<DriverDto>(`/api/fleet/drivers/${id}`, command, { signal });
    return res.data;
  },

  activateDriver: async (id: string, signal?: AbortSignal): Promise<DriverDto> => {
    const res = await apiClient.post<DriverDto>(`/api/fleet/drivers/${id}/activate`, {}, { signal });
    return res.data;
  },

  deactivateDriver: async (id: string, signal?: AbortSignal): Promise<DriverDto> => {
    const res = await apiClient.post<DriverDto>(`/api/fleet/drivers/${id}/deactivate`, {}, { signal });
    return res.data;
  },

  // Vehicles
  registerVehicle: async (command: RegisterVehicleCommand, signal?: AbortSignal): Promise<VehicleDto> => {
    const res = await apiClient.post<VehicleDto>('/api/fleet/vehicles', command, { signal });
    return res.data;
  },

  listVehicles: async (query?: ListFleetQuery, signal?: AbortSignal): Promise<PagedResult<VehicleDto>> => {
    const params = new URLSearchParams();
    if (query?.isActive != null) params.append('isActive', query.isActive.toString());
    if (query?.page) params.append('page', query.page.toString());
    if (query?.pageSize) params.append('pageSize', query.pageSize.toString());

    const res = await apiClient.get<PagedResult<VehicleDto>>('/api/fleet/vehicles', { params, signal });
    return res.data;
  },

  getVehicleById: async (id: string, signal?: AbortSignal): Promise<VehicleDto> => {
    const res = await apiClient.get<VehicleDto>(`/api/fleet/vehicles/${id}`, { signal });
    return res.data;
  },

  updateVehicleCapacity: async (
    id: string,
    command: UpdateVehicleCapacityCommand,
    signal?: AbortSignal
  ): Promise<VehicleDto> => {
    const res = await apiClient.put<VehicleDto>(`/api/fleet/vehicles/${id}/capacity`, command, { signal });
    return res.data;
  },

  activateVehicle: async (id: string, signal?: AbortSignal): Promise<VehicleDto> => {
    const res = await apiClient.post<VehicleDto>(`/api/fleet/vehicles/${id}/activate`, {}, { signal });
    return res.data;
  },

  deactivateVehicle: async (id: string, signal?: AbortSignal): Promise<VehicleDto> => {
    const res = await apiClient.post<VehicleDto>(`/api/fleet/vehicles/${id}/deactivate`, {}, { signal });
    return res.data;
  },
};
