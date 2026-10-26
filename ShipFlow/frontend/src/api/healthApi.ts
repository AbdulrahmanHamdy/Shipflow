import { apiClient } from './client';

export interface HealthStatus {
  status: string;
  timestampUtc: string;
}

export const healthApi = {
  getLiveness: async (signal?: AbortSignal): Promise<HealthStatus> => {
    const res = await apiClient.get<HealthStatus>('/health', { signal });
    return res.data;
  },

  getReadiness: async (signal?: AbortSignal): Promise<HealthStatus> => {
    const res = await apiClient.get<HealthStatus>('/health/ready', { signal });
    return res.data;
  },
};
