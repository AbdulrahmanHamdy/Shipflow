import { apiClient } from './client';
import {
  CancelShipmentCommand,
  CreateShipmentCommand,
  GetShipmentsQuery,
  PagedResult,
  ShipmentDetailsDto,
  ShipmentSummaryDto,
} from './types';

export const shipmentsApi = {
  create: async (command: CreateShipmentCommand, signal?: AbortSignal): Promise<ShipmentDetailsDto> => {
    const res = await apiClient.post<ShipmentDetailsDto>('/api/shipments', command, { signal });
    return res.data;
  },

  list: async (query?: GetShipmentsQuery, signal?: AbortSignal): Promise<PagedResult<ShipmentSummaryDto>> => {
    const params = new URLSearchParams();
    if (query?.scope) params.append('scope', query.scope);
    if (query?.search) params.append('search', query.search);
    if (query?.createdFromUtc) params.append('createdFromUtc', query.createdFromUtc);
    if (query?.createdToUtc) params.append('createdToUtc', query.createdToUtc);
    if (query?.page) params.append('page', query.page.toString());
    if (query?.pageSize) params.append('pageSize', query.pageSize.toString());
    if (query?.statuses && query.statuses.length > 0) {
      query.statuses.forEach((s) => params.append('statuses', s));
    }

    const res = await apiClient.get<PagedResult<ShipmentSummaryDto>>('/api/shipments', {
      params,
      signal,
    });
    return res.data;
  },

  getById: async (id: string, signal?: AbortSignal): Promise<ShipmentDetailsDto> => {
    const res = await apiClient.get<ShipmentDetailsDto>(`/api/shipments/${id}`, { signal });
    return res.data;
  },

  openForQuotes: async (id: string, signal?: AbortSignal): Promise<ShipmentDetailsDto> => {
    const res = await apiClient.post<ShipmentDetailsDto>(`/api/shipments/${id}/open-for-quotes`, {}, { signal });
    return res.data;
  },

  cancel: async (id: string, command: CancelShipmentCommand, signal?: AbortSignal): Promise<ShipmentDetailsDto> => {
    const res = await apiClient.post<ShipmentDetailsDto>(`/api/shipments/${id}/cancel`, command, { signal });
    return res.data;
  },
};
