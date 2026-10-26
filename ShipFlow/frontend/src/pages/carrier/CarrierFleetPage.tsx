import React, { useState } from 'react';
import {
  useVehiclesListQuery,
  useRegisterVehicleMutation,
  useUpdateVehicleCapacityMutation,
  useActivateVehicleMutation,
  useDeactivateVehicleMutation,
} from '@/features/carrier/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { VehicleDto, VehicleType, AppError } from '@/api/types';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/Table';
import { Modal } from '@/components/ui/Modal';
import { FormField } from '@/components/ui/FormField';
import { Input } from '@/components/ui/Input';
import { Pagination } from '@/components/ui/Pagination';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/ui/EmptyState';
import { ErrorState } from '@/components/ui/ErrorState';
import { useToast } from '@/components/ui/Toast';
import { formatWeight, formatVolume } from '@/lib/formatters';
import { Truck, Plus, ShieldCheck, Power, Settings2, AlertTriangle } from 'lucide-react';

const VEHICLE_TYPES: VehicleType[] = ['Van', 'Pickup', 'Truck', 'Trailer'];

export const CarrierFleetPage: React.FC = () => {
  usePageTitle('Fleet Vehicles');
  const toast = useToast();
  const [page, setPage] = useState(1);
  const pageSize = 15;

  const { data, isLoading, isError, error, refetch } = useVehiclesListQuery({ page, pageSize });

  const registerMutation = useRegisterVehicleMutation();
  const updateCapacityMutation = useUpdateVehicleCapacityMutation();
  const activateMutation = useActivateVehicleMutation();
  const deactivateMutation = useDeactivateVehicleMutation();

  // Modals
  const [registerModalOpen, setRegisterModalOpen] = useState(false);
  const [capacityModalVehicle, setCapacityModalVehicle] = useState<VehicleDto | null>(null);

  // Register form
  const [plateNumber, setPlateNumber] = useState('');
  const [vehicleType, setVehicleType] = useState<VehicleType>('Truck');
  const [maxWeightKg, setMaxWeightKg] = useState('12000');
  const [maxVolumeM3, setMaxVolumeM3] = useState('45');

  // Capacity update form
  const [updateWeight, setUpdateWeight] = useState('');
  const [updateVolume, setUpdateVolume] = useState('');

  const handleRegister = async () => {
    if (!plateNumber.trim()) {
      toast.error('Plate required', 'Please provide a valid vehicle license plate number.');
      return;
    }

    try {
      await registerMutation.mutateAsync({
        plateNumber: plateNumber.trim().toUpperCase(),
        type: vehicleType,
        maxWeightKg: parseFloat(maxWeightKg),
        maxVolumeM3: parseFloat(maxVolumeM3),
      });

      toast.success('Vehicle Registered', `Vehicle ${plateNumber.toUpperCase()} added to active fleet.`);
      setRegisterModalOpen(false);
      setPlateNumber('');
      refetch();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Registration failed', appErr.message);
    }
  };

  const handleOpenCapacityModal = (v: VehicleDto) => {
    setCapacityModalVehicle(v);
    setUpdateWeight(v.maxWeightKg.toString());
    setUpdateVolume(v.maxVolumeM3.toString());
  };

  const handleUpdateCapacity = async () => {
    if (!capacityModalVehicle) return;

    try {
      await updateCapacityMutation.mutateAsync({
        id: capacityModalVehicle.id,
        command: {
          maxWeightKg: parseFloat(updateWeight),
          maxVolumeM3: parseFloat(updateVolume),
        },
      });

      toast.success('Capacity Updated', 'Payload limits updated successfully.');
      setCapacityModalVehicle(null);
      refetch();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Update refused', appErr.message);
    }
  };

  const handleToggleActive = async (v: VehicleDto) => {
    try {
      if (v.isActive) {
        await deactivateMutation.mutateAsync(v.id);
        toast.info('Vehicle Deactivated', `${v.plateNumber} is now marked inactive.`);
      } else {
        await activateMutation.mutateAsync(v.id);
        toast.success('Vehicle Activated', `${v.plateNumber} is now active.`);
      }
      refetch();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Status change refused', appErr.message);
    }
  };

  const vehicles = data?.items || [];
  const totalCount = data?.totalCount || 0;
  const totalPages = data?.totalPages || 1;

  return (
    <div className="space-y-6 max-w-5xl mx-auto">
      <PageHeader
        title="Fleet Vehicles"
        description="Register and manage transport assets, type categories, and verified payload capacities."
        breadcrumbs={[{ label: 'Control Tower', href: '/carrier' }, { label: 'Fleet' }]}
        actions={
          <Button
            variant="primary"
            size="sm"
            onClick={() => setRegisterModalOpen(true)}
            leftIcon={<Plus className="w-4 h-4" />}
          >
            Register Vehicle
          </Button>
        }
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="p-6 space-y-3">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
            </div>
          ) : isError ? (
            <div className="p-6">
              <ErrorState message={(error as any)?.message} onRetry={() => refetch()} />
            </div>
          ) : vehicles.length === 0 ? (
            <div className="p-8">
              <EmptyState
                icon={Truck}
                title="No Vehicles Registered"
                description="Register your company's transport vehicles to enable shipment assignment and dispatching."
                actionLabel="Register Vehicle"
                onAction={() => setRegisterModalOpen(true)}
              />
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Plate Number</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Max Weight (kg)</TableHead>
                    <TableHead>Max Volume (m³)</TableHead>
                    <TableHead>Active Status</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {vehicles.map((v) => (
                    <TableRow key={v.id}>
                      <TableCell className="font-mono font-bold text-neutral-primary">
                        {v.plateNumber}
                      </TableCell>
                      <TableCell className="text-xs font-semibold text-neutral-secondary">
                        {v.type}
                      </TableCell>
                      <TableCell className="text-xs tabular-nums font-semibold">
                        {formatWeight(v.maxWeightKg)}
                      </TableCell>
                      <TableCell className="text-xs tabular-nums text-neutral-secondary">
                        {v.maxVolumeM3} m³
                      </TableCell>
                      <TableCell>
                        <span
                          className={`text-xs font-semibold px-2.5 py-0.5 rounded-full ${
                            v.isActive
                              ? 'bg-emerald-50 text-emerald-800 border border-emerald-300'
                              : 'bg-neutral-hover text-neutral-muted'
                          }`}
                        >
                          {v.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          <Button
                            variant="outline"
                            size="sm"
                            className="h-7 text-xs"
                            onClick={() => handleOpenCapacityModal(v)}
                            leftIcon={<Settings2 className="w-3 h-3" />}
                          >
                            Capacity
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            className={`h-7 text-xs ${v.isActive ? 'text-amber-700 hover:bg-amber-50' : 'text-emerald-700 hover:bg-emerald-50'}`}
                            onClick={() => handleToggleActive(v)}
                          >
                            {v.isActive ? 'Deactivate' : 'Activate'}
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              <div className="p-4 border-t border-neutral-border">
                <Pagination
                  page={page}
                  totalPages={totalPages}
                  totalCount={totalCount}
                  pageSize={pageSize}
                  onPageChange={setPage}
                />
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {/* Register Vehicle Modal */}
      <Modal
        isOpen={registerModalOpen}
        onClose={() => setRegisterModalOpen(false)}
        title="Register Fleet Vehicle"
        description="Add a new commercial vehicle to your transport fleet roster."
      >
        <div className="space-y-4">
          <FormField label="Plate Number" required hint="Spaces will be trimmed and formatted" htmlFor="plate">
            <Input
              id="plate"
              placeholder="e.g. TRK-8921-CAI"
              value={plateNumber}
              onChange={(e) => setPlateNumber(e.target.value)}
            />
          </FormField>

          <FormField label="Vehicle Category / Type" required htmlFor="vtype">
            <select
              id="vtype"
              className="w-full h-10 px-3 py-2 rounded-lg border border-neutral-border bg-white text-sm text-neutral-primary focus:border-brand focus:ring-1 focus:ring-brand"
              value={vehicleType}
              onChange={(e) => setVehicleType(e.target.value as VehicleType)}
            >
              {VEHICLE_TYPES.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </FormField>

          <div className="grid grid-cols-2 gap-4">
            <FormField label="Max Weight (kg)" required htmlFor="weight">
              <Input
                id="weight"
                type="number"
                step="0.1"
                min="1"
                value={maxWeightKg}
                onChange={(e) => setMaxWeightKg(e.target.value)}
              />
            </FormField>

            <FormField label="Max Volume (m³)" required htmlFor="volume">
              <Input
                id="volume"
                type="number"
                step="0.1"
                min="0.1"
                value={maxVolumeM3}
                onChange={(e) => setMaxVolumeM3(e.target.value)}
              />
            </FormField>
          </div>

          <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-neutral-border">
            <Button variant="outline" size="sm" onClick={() => setRegisterModalOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={handleRegister}
              isLoading={registerMutation.isPending}
            >
              Register Asset
            </Button>
          </div>
        </div>
      </Modal>

      {/* Capacity Modal */}
      <Modal
        isOpen={!!capacityModalVehicle}
        onClose={() => setCapacityModalVehicle(null)}
        title="Update Vehicle Capacity"
        description={`Modify weight or volume limits for vehicle ${capacityModalVehicle?.plateNumber}. Cannot shrink during an active assignment.`}
      >
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <FormField label="Max Weight (kg)" required htmlFor="upWeight">
              <Input
                id="upWeight"
                type="number"
                step="0.1"
                value={updateWeight}
                onChange={(e) => setUpdateWeight(e.target.value)}
              />
            </FormField>

            <FormField label="Max Volume (m³)" required htmlFor="upVolume">
              <Input
                id="upVolume"
                type="number"
                step="0.1"
                value={updateVolume}
                onChange={(e) => setUpdateVolume(e.target.value)}
              />
            </FormField>
          </div>

          <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-neutral-border">
            <Button variant="outline" size="sm" onClick={() => setCapacityModalVehicle(null)}>
              Cancel
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={handleUpdateCapacity}
              isLoading={updateCapacityMutation.isPending}
            >
              Save Capacity Limits
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};
