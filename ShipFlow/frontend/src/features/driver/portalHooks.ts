import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { driverPortalApi } from '@/api';
import {
  AssignmentDto,
  AvailableShipmentDto,
  DriverProfileDto,
  UpdateDriverAvailabilityCommand,
} from '@/api/types';
import { queryKeys } from '@/lib/queryClient';

export function useDriverProfileQuery(enabled = true) {
  return useQuery<DriverProfileDto>({
    queryKey: queryKeys.driver.profile,
    queryFn: ({ signal }) => driverPortalApi.getProfile(signal),
    enabled,
    retry: false,
  });
}

export function useAvailableShipmentsQuery(enabled = true) {
  return useQuery<AvailableShipmentDto[]>({
    queryKey: queryKeys.driver.availableShipments,
    queryFn: ({ signal }) => driverPortalApi.getAvailableShipments(signal),
    enabled,
    refetchInterval: 10000, // Fallback poll every 10s alongside SignalR
  });
}

export function useUpdateAvailabilityMutation() {
  const queryClient = useQueryClient();
  return useMutation<DriverProfileDto, unknown, UpdateDriverAvailabilityCommand>({
    mutationFn: (command) => driverPortalApi.updateAvailability(command),
    onSuccess: (data) => {
      queryClient.setQueryData(queryKeys.driver.profile, data);
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.availableShipments });
    },
  });
}

export function useAcceptShipmentMutation() {
  const queryClient = useQueryClient();
  return useMutation<AssignmentDto, unknown, string>({
    mutationFn: (shipmentId: string) => driverPortalApi.acceptShipment(shipmentId),
    onSuccess: (_, shipmentId) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.availableShipments });
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.currentAssignment });
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.profile });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}
