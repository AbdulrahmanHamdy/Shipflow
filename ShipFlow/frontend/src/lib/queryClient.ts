import { QueryClient } from '@tanstack/react-query';
import { AppError } from '@/api/types';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30 * 1000, // 30 seconds
      gcTime: 5 * 60 * 1000, // 5 minutes
      refetchOnWindowFocus: false,
      retry: (failureCount, error) => {
        const appErr = error as unknown as AppError;
        // Never retry 4xx errors
        if (appErr?.status && appErr.status >= 400 && appErr.status < 500) {
          return false;
        }
        return failureCount < 2;
      },
    },
    mutations: {
      retry: false,
    },
  },
});

export const queryKeys = {
  health: {
    live: ['health', 'live'] as const,
    ready: ['health', 'ready'] as const,
  },
  auth: {
    me: ['auth', 'me'] as const,
  },
  shipments: {
    all: ['shipments'] as const,
    list: (filters?: Record<string, any>) => ['shipments', 'list', filters] as const,
    detail: (id: string) => ['shipments', 'detail', id] as const,
    quotes: (id: string) => ['shipments', 'quotes', id] as const,
  },
  companies: {
    mine: ['companies', 'me'] as const,
    members: (includeFormer?: boolean) => ['companies', 'members', includeFormer] as const,
  },
  fleet: {
    drivers: (filters?: Record<string, any>) => ['fleet', 'drivers', filters] as const,
    driverDetail: (id: string) => ['fleet', 'drivers', id] as const,
    vehicles: (filters?: Record<string, any>) => ['fleet', 'vehicles', filters] as const,
    vehicleDetail: (id: string) => ['fleet', 'vehicles', id] as const,
  },
  dispatch: {
    availableVehicles: (shipmentId: string, window: any) => ['dispatch', 'vehicles', shipmentId, window] as const,
    availableDrivers: (shipmentId: string, window: any) => ['dispatch', 'drivers', shipmentId, window] as const,
    assignment: (shipmentId: string) => ['dispatch', 'assignment', shipmentId] as const,
  },
  tracking: {
    latest: (shipmentId: string) => ['tracking', 'latest', shipmentId] as const,
    history: (shipmentId: string, filters?: any) => ['tracking', 'history', shipmentId, filters] as const,
    proof: (shipmentId: string) => ['tracking', 'proof', shipmentId] as const,
  },
  driver: {
    currentAssignment: ['driver', 'assignments', 'current'] as const,
    history: (page?: number, pageSize?: number) => ['driver', 'assignments', 'history', page, pageSize] as const,
  },
};
