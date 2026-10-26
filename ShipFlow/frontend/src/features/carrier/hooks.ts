import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { companiesApi, fleetApi, dispatchApi } from '@/api';
import {
  AddMemberCommand,
  AssignShipmentCommand,
  AssignmentDto,
  AvailabilityQuery,
  CompanyDto,
  CreateCompanyCommand,
  DriverDto,
  GetMembersQuery,
  ListFleetQuery,
  MemberDto,
  PagedResult,
  RegisterDriverCommand,
  RegisterVehicleCommand,
  ShipmentDetailsDto,
  UnassignShipmentCommand,
  UpdateDriverCommand,
  UpdateVehicleCapacityCommand,
  VehicleDto,
} from '@/api/types';
import { queryKeys } from '@/lib/queryClient';

// ─── Company Hooks ──────────────────────────────────────────────────────────

export function useMyCompanyQuery(enabled = true) {
  return useQuery<CompanyDto>({
    queryKey: queryKeys.companies.mine,
    queryFn: ({ signal }) => companiesApi.getMine(signal),
    enabled,
    retry: false,
  });
}

export function useCreateCompanyMutation() {
  const queryClient = useQueryClient();
  return useMutation<CompanyDto, unknown, CreateCompanyCommand>({
    mutationFn: (command) => companiesApi.create(command),
    onSuccess: (newCompany) => {
      queryClient.setQueryData(queryKeys.companies.mine, newCompany);
    },
  });
}

export function useCompanyMembersQuery(query?: GetMembersQuery) {
  return useQuery<MemberDto[]>({
    queryKey: queryKeys.companies.members(query?.includeFormer),
    queryFn: ({ signal }) => companiesApi.getMembers(query, signal),
  });
}

export function useAddCompanyMemberMutation() {
  const queryClient = useQueryClient();
  return useMutation<MemberDto, unknown, AddMemberCommand>({
    mutationFn: (command) => companiesApi.addMember(command),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['companies', 'members'] });
    },
  });
}

export function useRemoveCompanyMemberMutation() {
  const queryClient = useQueryClient();
  return useMutation<void, unknown, string>({
    mutationFn: (userId) => companiesApi.removeMember(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['companies', 'members'] });
      queryClient.invalidateQueries({ queryKey: ['fleet', 'drivers'] });
    },
  });
}

// ─── Fleet Hooks ────────────────────────────────────────────────────────────

export function useDriversListQuery(query?: ListFleetQuery) {
  return useQuery<PagedResult<DriverDto>>({
    queryKey: queryKeys.fleet.drivers(query),
    queryFn: ({ signal }) => fleetApi.listDrivers(query, signal),
  });
}

export function useDriverDetailQuery(id: string | undefined) {
  return useQuery<DriverDto>({
    queryKey: queryKeys.fleet.driverDetail(id || ''),
    queryFn: ({ signal }) => {
      if (!id) throw new Error('Driver ID required');
      return fleetApi.getDriverById(id, signal);
    },
    enabled: !!id,
  });
}

export function useRegisterDriverMutation() {
  const queryClient = useQueryClient();
  return useMutation<DriverDto, unknown, RegisterDriverCommand>({
    mutationFn: (command) => fleetApi.registerDriver(command),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['fleet', 'drivers'] });
    },
  });
}

export function useUpdateDriverMutation() {
  const queryClient = useQueryClient();
  return useMutation<DriverDto, unknown, { id: string; command: UpdateDriverCommand }>({
    mutationFn: ({ id, command }) => fleetApi.updateDriver(id, command),
    onSuccess: (updated, { id }) => {
      queryClient.setQueryData(queryKeys.fleet.driverDetail(id), updated);
      queryClient.invalidateQueries({ queryKey: ['fleet', 'drivers'] });
    },
  });
}

export function useActivateDriverMutation() {
  const queryClient = useQueryClient();
  return useMutation<DriverDto, unknown, string>({
    mutationFn: (id) => fleetApi.activateDriver(id),
    onSuccess: (updated, id) => {
      queryClient.setQueryData(queryKeys.fleet.driverDetail(id), updated);
      queryClient.invalidateQueries({ queryKey: ['fleet', 'drivers'] });
    },
  });
}

export function useDeactivateDriverMutation() {
  const queryClient = useQueryClient();
  return useMutation<DriverDto, unknown, string>({
    mutationFn: (id) => fleetApi.deactivateDriver(id),
    onSuccess: (updated, id) => {
      queryClient.setQueryData(queryKeys.fleet.driverDetail(id), updated);
      queryClient.invalidateQueries({ queryKey: ['fleet', 'drivers'] });
    },
  });
}

export function useVehiclesListQuery(query?: ListFleetQuery) {
  return useQuery<PagedResult<VehicleDto>>({
    queryKey: queryKeys.fleet.vehicles(query),
    queryFn: ({ signal }) => fleetApi.listVehicles(query, signal),
  });
}

export function useVehicleDetailQuery(id: string | undefined) {
  return useQuery<VehicleDto>({
    queryKey: queryKeys.fleet.vehicleDetail(id || ''),
    queryFn: ({ signal }) => {
      if (!id) throw new Error('Vehicle ID required');
      return fleetApi.getVehicleById(id, signal);
    },
    enabled: !!id,
  });
}

export function useRegisterVehicleMutation() {
  const queryClient = useQueryClient();
  return useMutation<VehicleDto, unknown, RegisterVehicleCommand>({
    mutationFn: (command) => fleetApi.registerVehicle(command),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['fleet', 'vehicles'] });
    },
  });
}

export function useUpdateVehicleCapacityMutation() {
  const queryClient = useQueryClient();
  return useMutation<VehicleDto, unknown, { id: string; command: UpdateVehicleCapacityCommand }>({
    mutationFn: ({ id, command }) => fleetApi.updateVehicleCapacity(id, command),
    onSuccess: (updated, { id }) => {
      queryClient.setQueryData(queryKeys.fleet.vehicleDetail(id), updated);
      queryClient.invalidateQueries({ queryKey: ['fleet', 'vehicles'] });
    },
  });
}

export function useActivateVehicleMutation() {
  const queryClient = useQueryClient();
  return useMutation<VehicleDto, unknown, string>({
    mutationFn: (id) => fleetApi.activateVehicle(id),
    onSuccess: (updated, id) => {
      queryClient.setQueryData(queryKeys.fleet.vehicleDetail(id), updated);
      queryClient.invalidateQueries({ queryKey: ['fleet', 'vehicles'] });
    },
  });
}

export function useDeactivateVehicleMutation() {
  const queryClient = useQueryClient();
  return useMutation<VehicleDto, unknown, string>({
    mutationFn: (id) => fleetApi.deactivateVehicle(id),
    onSuccess: (updated, id) => {
      queryClient.setQueryData(queryKeys.fleet.vehicleDetail(id), updated);
      queryClient.invalidateQueries({ queryKey: ['fleet', 'vehicles'] });
    },
  });
}

// ─── Dispatch Hooks ─────────────────────────────────────────────────────────

export function useAvailableVehiclesQuery(shipmentId: string, query: AvailabilityQuery, enabled = true) {
  return useQuery<VehicleDto[]>({
    queryKey: queryKeys.dispatch.availableVehicles(shipmentId, query),
    queryFn: ({ signal }) => dispatchApi.getAvailableVehicles(shipmentId, query, signal),
    enabled: !!shipmentId && !!query.windowStartUtc && !!query.windowEndUtc && enabled,
  });
}

export function useAvailableDriversQuery(shipmentId: string, query: AvailabilityQuery, enabled = true) {
  return useQuery<DriverDto[]>({
    queryKey: queryKeys.dispatch.availableDrivers(shipmentId, query),
    queryFn: ({ signal }) => dispatchApi.getAvailableDrivers(shipmentId, query, signal),
    enabled: !!shipmentId && !!query.windowStartUtc && !!query.windowEndUtc && enabled,
  });
}

export function useAssignShipmentMutation() {
  const queryClient = useQueryClient();
  return useMutation<AssignmentDto, unknown, { shipmentId: string; command: AssignShipmentCommand }>({
    mutationFn: ({ shipmentId, command }) => dispatchApi.assign(shipmentId, command),
    onSuccess: (assignment, { shipmentId }) => {
      queryClient.setQueryData(queryKeys.dispatch.assignment(shipmentId), assignment);
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}

export function useShipmentAssignmentQuery(shipmentId: string | undefined) {
  return useQuery<AssignmentDto>({
    queryKey: queryKeys.dispatch.assignment(shipmentId || ''),
    queryFn: ({ signal }) => {
      if (!shipmentId) throw new Error('Shipment ID required');
      return dispatchApi.getAssignment(shipmentId, signal);
    },
    enabled: !!shipmentId,
  });
}

export function useUnassignShipmentMutation() {
  const queryClient = useQueryClient();
  return useMutation<ShipmentDetailsDto, unknown, { shipmentId: string; command: UnassignShipmentCommand }>({
    mutationFn: ({ shipmentId, command }) => dispatchApi.unassign(shipmentId, command),
    onSuccess: (updated, { shipmentId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.dispatch.assignment(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
    },
  });
}
