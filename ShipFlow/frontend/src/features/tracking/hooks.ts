import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { trackingApi } from '@/api';
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
} from '@/api/types';
import { queryKeys } from '@/lib/queryClient';

export function useLatestTrackingPointQuery(shipmentId: string | undefined, enabled = true) {
  return useQuery<TrackingPointDto | null>({
    queryKey: queryKeys.tracking.latest(shipmentId || ''),
    queryFn: ({ signal }) => {
      if (!shipmentId) throw new Error('Shipment ID is required');
      return trackingApi.getLatest(shipmentId, signal);
    },
    enabled: !!shipmentId && enabled,
    refetchInterval: (query) => {
      // Poll every 20 seconds while tab is active, unless document is hidden
      if (typeof document !== 'undefined' && document.hidden) return false;
      return 20000;
    },
  });
}

export function useTrackingHistoryQuery(shipmentId: string | undefined, query?: TrackingHistoryQuery) {
  return useQuery<PagedResult<TrackingPointDto>>({
    queryKey: queryKeys.tracking.history(shipmentId || '', query),
    queryFn: ({ signal }) => {
      if (!shipmentId) throw new Error('Shipment ID is required');
      return trackingApi.getHistory(shipmentId, query, signal);
    },
    enabled: !!shipmentId,
  });
}

export function useDeliveryProofQuery(shipmentId: string | undefined) {
  return useQuery<DeliveryProofDto | null>({
    queryKey: queryKeys.tracking.proof(shipmentId || ''),
    queryFn: ({ signal }) => {
      if (!shipmentId) throw new Error('Shipment ID is required');
      return trackingApi.getDeliveryProof(shipmentId, signal);
    },
    enabled: !!shipmentId,
  });
}

export function useDriverCurrentAssignmentQuery() {
  return useQuery<AssignmentDto | null>({
    queryKey: queryKeys.driver.currentAssignment,
    queryFn: ({ signal }) => trackingApi.getCurrentAssignment(signal),
  });
}

export function useDriverAssignmentHistoryQuery(page = 1, pageSize = 20) {
  return useQuery<PagedResult<AssignmentDto>>({
    queryKey: queryKeys.driver.history(page, pageSize),
    queryFn: ({ signal }) => trackingApi.getAssignmentHistory(page, pageSize, signal),
  });
}

export function useDriverPickupMutation() {
  const queryClient = useQueryClient();
  return useMutation<ShipmentSummaryDto, unknown, string>({
    mutationFn: (shipmentId) => trackingApi.pickup(shipmentId),
    onSuccess: (_, shipmentId) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.currentAssignment });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}

export function useDriverStartTransitMutation() {
  const queryClient = useQueryClient();
  return useMutation<ShipmentSummaryDto, unknown, string>({
    mutationFn: (shipmentId) => trackingApi.startTransit(shipmentId),
    onSuccess: (_, shipmentId) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.currentAssignment });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}

export function useDriverDeliverMutation() {
  const queryClient = useQueryClient();
  return useMutation<DeliveryProofDto, unknown, { shipmentId: string; command: DeliverShipmentCommand }>({
    mutationFn: ({ shipmentId, command }) => trackingApi.deliver(shipmentId, command),
    onSuccess: (proof, { shipmentId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.currentAssignment });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.tracking.proof(shipmentId) });
      queryClient.setQueryData(queryKeys.tracking.proof(shipmentId), proof);
    },
  });
}

export function useDriverFailDeliveryMutation() {
  const queryClient = useQueryClient();
  return useMutation<ShipmentSummaryDto, unknown, { shipmentId: string; command: FailDeliveryCommand }>({
    mutationFn: ({ shipmentId, command }) => trackingApi.failDelivery(shipmentId, command),
    onSuccess: (_, { shipmentId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.currentAssignment });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}

export function useDriverRetryDeliveryMutation() {
  const queryClient = useQueryClient();
  return useMutation<ShipmentSummaryDto, unknown, string>({
    mutationFn: (shipmentId) => trackingApi.retryDelivery(shipmentId),
    onSuccess: (_, shipmentId) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.currentAssignment });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}

export function useSubmitTrackingPointMutation() {
  const queryClient = useQueryClient();
  return useMutation<TrackingPointDto, unknown, SubmitTrackingPointCommand>({
    mutationFn: (command) => trackingApi.submitPoint(command),
    onSuccess: (point) => {
      queryClient.setQueryData(queryKeys.tracking.latest(point.shipmentId), point);
      queryClient.invalidateQueries({ queryKey: queryKeys.tracking.history(point.shipmentId) });
    },
  });
}
