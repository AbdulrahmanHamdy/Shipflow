import { apiClient } from './client';
import {
  AssignmentDto,
  DeliverShipmentCommand,
  DeliveryProofDto,
  FailDeliveryCommand,
  PagedResult,
  ShipmentSummaryDto,
  SubmitTrackingPointCommand,
  TrackingHistoryQuery,
  TrackingPointDto,
} from './types';

export const trackingApi = {
  // GPS points
  submitPoint: async (command: SubmitTrackingPointCommand, signal?: AbortSignal): Promise<TrackingPointDto> => {
    const res = await apiClient.post<TrackingPointDto>('/api/tracking', command, { signal });
    return res.data;
  },

  getLatest: async (shipmentId: string, signal?: AbortSignal): Promise<TrackingPointDto | null> => {
    try {
      const res = await apiClient.get<TrackingPointDto>(`/api/shipments/${shipmentId}/tracking/latest`, { signal });
      return res.data;
    } catch (err: any) {
      if (err.status === 404) return null;
      throw err;
    }
  },

  getHistory: async (
    shipmentId: string,
    query?: TrackingHistoryQuery,
    signal?: AbortSignal
  ): Promise<PagedResult<TrackingPointDto>> => {
    const params = new URLSearchParams();
    if (query?.fromUtc) params.append('fromUtc', query.fromUtc);
    if (query?.toUtc) params.append('toUtc', query.toUtc);
    if (query?.page) params.append('page', query.page.toString());
    if (query?.pageSize) params.append('pageSize', query.pageSize.toString());

    const res = await apiClient.get<PagedResult<TrackingPointDto>>(`/api/shipments/${shipmentId}/tracking`, {
      params,
      signal,
    });
    return res.data;
  },

  // Driver transitions
  pickup: async (shipmentId: string, signal?: AbortSignal): Promise<ShipmentSummaryDto> => {
    const res = await apiClient.post<ShipmentSummaryDto>(`/api/shipments/${shipmentId}/pickup`, {}, { signal });
    return res.data;
  },

  startTransit: async (shipmentId: string, signal?: AbortSignal): Promise<ShipmentSummaryDto> => {
    const res = await apiClient.post<ShipmentSummaryDto>(`/api/shipments/${shipmentId}/start-transit`, {}, { signal });
    return res.data;
  },

  deliver: async (
    shipmentId: string,
    command: DeliverShipmentCommand,
    signal?: AbortSignal
  ): Promise<DeliveryProofDto> => {
    const res = await apiClient.post<DeliveryProofDto>(`/api/shipments/${shipmentId}/deliver`, command, { signal });
    return res.data;
  },

  failDelivery: async (
    shipmentId: string,
    command: FailDeliveryCommand,
    signal?: AbortSignal
  ): Promise<ShipmentSummaryDto> => {
    const res = await apiClient.post<ShipmentSummaryDto>(`/api/shipments/${shipmentId}/fail-delivery`, command, {
      signal,
    });
    return res.data;
  },

  retryDelivery: async (shipmentId: string, signal?: AbortSignal): Promise<ShipmentSummaryDto> => {
    const res = await apiClient.post<ShipmentSummaryDto>(`/api/shipments/${shipmentId}/retry-delivery`, {}, { signal });
    return res.data;
  },

  getDeliveryProof: async (shipmentId: string, signal?: AbortSignal): Promise<DeliveryProofDto | null> => {
    try {
      const res = await apiClient.get<DeliveryProofDto>(`/api/shipments/${shipmentId}/delivery-proof`, { signal });
      return res.data;
    } catch (err: any) {
      if (err.status === 404) return null;
      throw err;
    }
  },

  // Driver assignments
  getCurrentAssignment: async (signal?: AbortSignal): Promise<AssignmentDto | null> => {
    try {
      const res = await apiClient.get<AssignmentDto>('/api/driver/assignments/current', { signal });
      return res.data;
    } catch (err: any) {
      if (err.status === 404) return null;
      throw err;
    }
  },

  getAssignmentHistory: async (
    page = 1,
    pageSize = 20,
    signal?: AbortSignal
  ): Promise<PagedResult<AssignmentDto>> => {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });
    const res = await apiClient.get<PagedResult<AssignmentDto>>('/api/driver/assignments', { params, signal });
    return res.data;
  },
};
