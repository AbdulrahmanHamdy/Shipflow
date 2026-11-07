import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { dispatchApi, driverPortalApi } from '@/api';
import { MatchingStatusDto, AssignDriverVehicleCommand } from '@/api/types';
import { queryKeys } from '@/lib/queryClient';

export function useMatchingStatusQuery(shipmentId: string, enabled = true) {
  return useQuery<MatchingStatusDto>({
    queryKey: queryKeys.dispatch.matchingStatus(shipmentId),
    queryFn: ({ signal }) => dispatchApi.getMatchingStatus(shipmentId, signal),
    enabled: Boolean(shipmentId) && enabled,
    refetchInterval: (query) => {
      // Poll every 5s if status is SearchingForDriver as a fallback
      return query.state.data?.status === 'SearchingForDriver' ? 5000 : false;
    },
  });
}

export function useDispatchMatchingMutation() {
  const queryClient = useQueryClient();
  return useMutation<MatchingStatusDto, unknown, string>({
    mutationFn: (shipmentId: string) => dispatchApi.dispatchForMatching(shipmentId),
    onSuccess: (data, shipmentId) => {
      queryClient.setQueryData(queryKeys.dispatch.matchingStatus(shipmentId), data);
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}

export function useCancelDispatchMutation() {
  const queryClient = useQueryClient();
  return useMutation<void, unknown, string>({
    mutationFn: (shipmentId: string) => dispatchApi.cancelDispatchMatching(shipmentId),
    onSuccess: (_, shipmentId) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.dispatch.matchingStatus(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}

export function useAssignDriverVehicleMutation() {
  const queryClient = useQueryClient();
  return useMutation<void, unknown, { driverId: string; command: AssignDriverVehicleCommand }>({
    mutationFn: ({ driverId, command }) => driverPortalApi.assignDriverVehicle(driverId, command),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['fleet'] });
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.profile });
    },
  });
}
