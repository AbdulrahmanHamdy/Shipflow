import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { shipmentsApi, quotesApi } from '@/api';
import {
  CancelShipmentCommand,
  CreateShipmentCommand,
  GetShipmentsQuery,
  PagedResult,
  QuoteDto,
  ShipmentDetailsDto,
  ShipmentSummaryDto,
  SubmitQuoteCommand,
} from '@/api/types';
import { queryKeys } from '@/lib/queryClient';

export function useShipmentsListQuery(query?: GetShipmentsQuery) {
  return useQuery<PagedResult<ShipmentSummaryDto>>({
    queryKey: queryKeys.shipments.list(query),
    queryFn: ({ signal }) => shipmentsApi.list(query, signal),
  });
}

export function useShipmentDetailQuery(id: string | undefined) {
  return useQuery<ShipmentDetailsDto>({
    queryKey: queryKeys.shipments.detail(id || ''),
    queryFn: ({ signal }) => {
      if (!id) throw new Error('Shipment ID is required');
      return shipmentsApi.getById(id, signal);
    },
    enabled: !!id,
  });
}

export function useCreateShipmentMutation() {
  const queryClient = useQueryClient();
  return useMutation<ShipmentDetailsDto, unknown, CreateShipmentCommand>({
    mutationFn: (command) => shipmentsApi.create(command),
    onSuccess: (newShipment) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
      queryClient.setQueryData(queryKeys.shipments.detail(newShipment.id), newShipment);
    },
  });
}

export function useOpenForQuotesMutation() {
  const queryClient = useQueryClient();
  return useMutation<ShipmentDetailsDto, unknown, string>({
    mutationFn: (id) => shipmentsApi.openForQuotes(id),
    onSuccess: (updated, id) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}

export function useCancelShipmentMutation() {
  const queryClient = useQueryClient();
  return useMutation<ShipmentDetailsDto, unknown, { id: string; command: CancelShipmentCommand }>({
    mutationFn: ({ id, command }) => shipmentsApi.cancel(id, command),
    onSuccess: (updated, { id }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.dispatch.assignment(id) });
    },
  });
}

export function useShipmentQuotesQuery(shipmentId: string | undefined) {
  return useQuery<QuoteDto[]>({
    queryKey: queryKeys.shipments.quotes(shipmentId || ''),
    queryFn: ({ signal }) => {
      if (!shipmentId) throw new Error('Shipment ID is required');
      return quotesApi.list(shipmentId, signal);
    },
    enabled: !!shipmentId,
  });
}

export function useSubmitQuoteMutation() {
  const queryClient = useQueryClient();
  return useMutation<QuoteDto, unknown, { shipmentId: string; command: SubmitQuoteCommand }>({
    mutationFn: ({ shipmentId, command }) => quotesApi.submit(shipmentId, command),
    onSuccess: (_, { shipmentId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.quotes(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}

export function useAcceptQuoteMutation() {
  const queryClient = useQueryClient();
  return useMutation<ShipmentDetailsDto, unknown, { shipmentId: string; quoteId: string }>({
    mutationFn: ({ shipmentId, quoteId }) => quotesApi.accept(shipmentId, quoteId),
    onSuccess: (updated, { shipmentId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.quotes(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}

export function useWithdrawQuoteMutation() {
  const queryClient = useQueryClient();
  return useMutation<QuoteDto, unknown, { shipmentId: string; quoteId: string }>({
    mutationFn: ({ shipmentId, quoteId }) => quotesApi.withdraw(shipmentId, quoteId),
    onSuccess: (_, { shipmentId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.quotes(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
    },
  });
}
