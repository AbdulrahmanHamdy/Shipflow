import React, { useState, useEffect } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useShipmentDetailQuery } from '@/features/shipments/hooks';
import {
  useAvailableVehiclesQuery,
  useAvailableDriversQuery,
  useAssignShipmentMutation,
  useShipmentAssignmentQuery,
  useUnassignShipmentMutation,
} from '@/features/carrier/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Skeleton } from '@/components/ui/Skeleton';
import { ErrorState } from '@/components/ui/ErrorState';
import { FormField } from '@/components/ui/FormField';
import { Input, Textarea } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/Toast';
import { AppError } from '@/api/types';
import { formatDateTime, formatWeight, formatVolume } from '@/lib/formatters';
import { Truck, Users, Clock, ShieldCheck, ArrowRight, UserCheck, AlertOctagon, RotateCcw } from 'lucide-react';

export const CarrierDispatchPage: React.FC = () => {
  const { shipmentId } = useParams<{ shipmentId: string }>();
  usePageTitle('Dispatch & Fleet Assignment');
  const toast = useToast();
  const navigate = useNavigate();

  const { data: shipment, isLoading: isShipmentLoading, refetch: refetchShipment } = useShipmentDetailQuery(shipmentId);
  const { data: activeAssignment, refetch: refetchAssignment } = useShipmentAssignmentQuery(shipmentId);

  // Time window states
  const [windowStart, setWindowStart] = useState('');
  const [windowEnd, setWindowEnd] = useState('');

  // Selected resources
  const [selectedDriverId, setSelectedDriverId] = useState<string>('');
  const [selectedVehicleId, setSelectedVehicleId] = useState<string>('');

  // Unassign dialog
  const [unassignModalOpen, setUnassignModalOpen] = useState(false);
  const [unassignReason, setUnassignReason] = useState('');

  const assignMutation = useAssignShipmentMutation();
  const unassignMutation = useUnassignShipmentMutation();

  // Initialize windows from shipment pickup stop
  useEffect(() => {
    if (shipment?.stops?.[0]?.windowStartUtc) {
      setWindowStart(shipment.stops[0].windowStartUtc.slice(0, 16));
    } else {
      const d = new Date();
      setWindowStart(d.toISOString().slice(0, 16));
    }

    if (shipment?.stops?.[0]?.windowEndUtc) {
      setWindowEnd(shipment.stops[0].windowEndUtc.slice(0, 16));
    } else {
      const d2 = new Date();
      d2.setHours(d2.getHours() + 8);
      setWindowEnd(d2.toISOString().slice(0, 16));
    }
  }, [shipment]);

  const availabilityQuery = {
    windowStartUtc: windowStart ? new Date(windowStart).toISOString() : '',
    windowEndUtc: windowEnd ? new Date(windowEnd).toISOString() : '',
  };

  const hasValidWindow = !!availabilityQuery.windowStartUtc && !!availabilityQuery.windowEndUtc;

  // Query free resources
  const { data: vehicles = [], isLoading: isVehiclesLoading } = useAvailableVehiclesQuery(
    shipmentId || '',
    availabilityQuery,
    hasValidWindow && !activeAssignment
  );

  const { data: drivers = [], isLoading: isDriversLoading } = useAvailableDriversQuery(
    shipmentId || '',
    availabilityQuery,
    hasValidWindow && !activeAssignment
  );

  if (isShipmentLoading) {
    return (
      <div className="space-y-6 max-w-4xl mx-auto">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-96 w-full rounded-2xl" />
      </div>
    );
  }

  if (!shipment) {
    return (
      <div className="py-12">
        <ErrorState title="Shipment Not Found" message="Could not locate the requested consignment for dispatch." />
      </div>
    );
  }

  const handleAssign = async () => {
    if (!selectedDriverId || !selectedVehicleId) {
      toast.error('Selection incomplete', 'Please select both an available driver and a vehicle.');
      return;
    }

    try {
      await assignMutation.mutateAsync({
        shipmentId: shipment.id,
        command: {
          driverId: selectedDriverId,
          vehicleId: selectedVehicleId,
          windowStartUtc: new Date(windowStart).toISOString(),
          windowEndUtc: new Date(windowEnd).toISOString(),
        },
      });

      toast.success('Assignment Dispatched', 'Driver and vehicle locked for this consignment.');
      refetchAssignment();
      refetchShipment();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Assignment rejected', appErr.message);
    }
  };

  const handleUnassign = async () => {
    if (!unassignReason.trim()) {
      toast.error('Reason required', 'Please provide a justification for releasing the assignment.');
      return;
    }

    try {
      await unassignMutation.mutateAsync({
        shipmentId: shipment.id,
        command: { reason: unassignReason.trim() },
      });

      toast.success('Assignment Released', 'The driver and vehicle are now free to take other jobs.');
      setUnassignModalOpen(false);
      setUnassignReason('');
      refetchAssignment();
      refetchShipment();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Unassign failed', appErr.message);
    }
  };

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <PageHeader
        title={`Dispatch Workbench: ${shipment.reference || shipment.id.substring(0, 8)}`}
        description="Schedule free drivers and vehicles matching cargo weight and volume limits."
        badge={<StatusBadge status={shipment.status} size="md" />}
        breadcrumbs={[
          { label: 'Control Tower', href: '/carrier' },
          { label: 'Shipments', href: '/carrier/shipments' },
          { label: 'Dispatch' },
        ]}
      />

      {/* Cargo Specifications Card */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Truck className="w-5 h-5 text-brand" />
            <CardTitle>Consignment Requirements</CardTitle>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs">
            <div className="p-3 rounded-xl bg-neutral-appBg border border-neutral-border">
              <span className="text-neutral-secondary block text-[11px]">Weight</span>
              <span className="text-sm font-bold text-neutral-primary">{formatWeight(shipment.cargo.weightKg)}</span>
            </div>
            <div className="p-3 rounded-xl bg-neutral-appBg border border-neutral-border">
              <span className="text-neutral-secondary block text-[11px]">Volume</span>
              <span className="text-sm font-bold text-neutral-primary">{formatVolume(shipment.cargo.volumeM3)}</span>
            </div>
            <div className="p-3 rounded-xl bg-neutral-appBg border border-neutral-border">
              <span className="text-neutral-secondary block text-[11px]">Pickup Stop</span>
              <span className="text-xs font-semibold text-neutral-primary truncate block">{shipment.stops[0]?.address}</span>
            </div>
            <div className="p-3 rounded-xl bg-neutral-appBg border border-neutral-border">
              <span className="text-neutral-secondary block text-[11px]">Destination</span>
              <span className="text-xs font-semibold text-neutral-primary truncate block">
                {shipment.stops[shipment.stops.length - 1]?.address}
              </span>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Active Assignment Display */}
      {activeAssignment ? (
        <Card className="border-2 border-emerald-300 bg-emerald-50/20">
          <CardHeader>
            <div className="flex items-center justify-between w-full">
              <div className="flex items-center gap-2">
                <ShieldCheck className="w-5 h-5 text-emerald-700" />
                <div>
                  <CardTitle>Active Assignment Dispatched</CardTitle>
                  <CardDescription>Locked in database &bull; State: {activeAssignment.shipmentStatus}</CardDescription>
                </div>
              </div>
              <Button
                variant="outline"
                size="sm"
                className="text-red-600 border-red-200 hover:bg-red-50"
                onClick={() => setUnassignModalOpen(true)}
              >
                Release Assignment
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3 text-xs">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div className="p-3.5 rounded-xl bg-white border border-neutral-border space-y-1">
                <span className="text-neutral-secondary block text-[11px]">Assigned Driver ID</span>
                <span className="font-mono font-bold text-neutral-primary">{activeAssignment.driverId}</span>
              </div>
              <div className="p-3.5 rounded-xl bg-white border border-neutral-border space-y-1">
                <span className="text-neutral-secondary block text-[11px]">Assigned Vehicle ID</span>
                <span className="font-mono font-bold text-neutral-primary">{activeAssignment.vehicleId}</span>
              </div>
            </div>

            <p className="text-[11px] text-neutral-secondary">
              Window: {formatDateTime(activeAssignment.windowStartUtc)} &rarr; {formatDateTime(activeAssignment.windowEndUtc)}
            </p>
          </CardContent>
        </Card>
      ) : (
        /* Dispatch Assignment Workspace */
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Assign Driver &amp; Vehicle</CardTitle>
              <CardDescription>
                Select execution window to filter available carrier resources without overlapping commitments
              </CardDescription>
            </div>
          </CardHeader>

          <CardContent className="space-y-6">
            {/* Window selection */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <FormField label="Window Start (UTC)" required htmlFor="winStart">
                <Input
                  id="winStart"
                  type="datetime-local"
                  value={windowStart}
                  onChange={(e) => setWindowStart(e.target.value)}
                />
              </FormField>

              <FormField label="Window End (UTC)" required htmlFor="winEnd">
                <Input
                  id="winEnd"
                  type="datetime-local"
                  value={windowEnd}
                  onChange={(e) => setWindowEnd(e.target.value)}
                />
              </FormField>
            </div>

            {/* Vehicle Selection */}
            <div className="space-y-2">
              <div className="flex items-center justify-between text-xs">
                <span className="font-bold text-neutral-primary flex items-center gap-1.5">
                  <Truck className="w-4 h-4 text-brand" /> Available Fleet Vehicles ({vehicles.length})
                </span>
                <span className="text-[11px] text-neutral-secondary">Capacity-checked for cargo</span>
              </div>

              {isVehiclesLoading ? (
                <Skeleton className="h-12 w-full" />
              ) : vehicles.length === 0 ? (
                <div className="p-4 rounded-xl bg-neutral-appBg border border-neutral-border text-xs text-neutral-secondary text-center">
                  No vehicles available that satisfy the cargo weight ({shipment.cargo.weightKg} kg) and volume (
                  {shipment.cargo.volumeM3} m³) within this window.
                </div>
              ) : (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs">
                  {vehicles.map((v) => (
                    <label
                      key={v.id}
                      className={`flex items-center justify-between p-3 rounded-xl border cursor-pointer transition-all ${
                        selectedVehicleId === v.id
                          ? 'border-brand bg-brand-mint/30 ring-1 ring-brand'
                          : 'border-neutral-border bg-white hover:bg-neutral-hover'
                      }`}
                    >
                      <div>
                        <span className="font-bold text-neutral-primary block">{v.plateNumber}</span>
                        <span className="text-[11px] text-neutral-secondary">
                          {v.type} &bull; Max: {formatWeight(v.maxWeightKg)} / {v.maxVolumeM3} m³
                        </span>
                      </div>
                      <input
                        type="radio"
                        name="vehicleChoice"
                        checked={selectedVehicleId === v.id}
                        onChange={() => setSelectedVehicleId(v.id)}
                        className="text-brand focus:ring-brand"
                      />
                    </label>
                  ))}
                </div>
              )}
            </div>

            {/* Driver Selection */}
            <div className="space-y-2">
              <div className="flex items-center justify-between text-xs">
                <span className="font-bold text-neutral-primary flex items-center gap-1.5">
                  <Users className="w-4 h-4 text-brand" /> Available Drivers ({drivers.length})
                </span>
                <span className="text-[11px] text-neutral-secondary">Free during window</span>
              </div>

              {isDriversLoading ? (
                <Skeleton className="h-12 w-full" />
              ) : drivers.length === 0 ? (
                <div className="p-4 rounded-xl bg-neutral-appBg border border-neutral-border text-xs text-neutral-secondary text-center">
                  No drivers available without an overlapping assignment window.
                </div>
              ) : (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs">
                  {drivers.map((d) => (
                    <label
                      key={d.id}
                      className={`flex items-center justify-between p-3 rounded-xl border cursor-pointer transition-all ${
                        selectedDriverId === d.id
                          ? 'border-brand bg-brand-mint/30 ring-1 ring-brand'
                          : 'border-neutral-border bg-white hover:bg-neutral-hover'
                      }`}
                    >
                      <div>
                        <span className="font-bold text-neutral-primary block">{d.fullName}</span>
                        <span className="text-[11px] text-neutral-secondary">License: {d.licenseNumber}</span>
                      </div>
                      <input
                        type="radio"
                        name="driverChoice"
                        checked={selectedDriverId === d.id}
                        onChange={() => setSelectedDriverId(d.id)}
                        className="text-brand focus:ring-brand"
                      />
                    </label>
                  ))}
                </div>
              )}
            </div>
          </CardContent>

          <CardFooter>
            <Button variant="outline" size="sm" asChild>
              <Link to="/carrier/shipments">Back to Board</Link>
            </Button>
            <Button
              variant="primary"
              size="md"
              onClick={handleAssign}
              isLoading={assignMutation.isPending}
              disabled={!selectedDriverId || !selectedVehicleId}
              rightIcon={<ArrowRight className="w-4 h-4" />}
            >
              Lock &amp; Dispatch Assignment
            </Button>
          </CardFooter>
        </Card>
      )}

      {/* Unassign Modal */}
      <Modal
        isOpen={unassignModalOpen}
        onClose={() => setUnassignModalOpen(false)}
        title="Release Shipment Assignment"
        description="Releasing will transition the shipment back to Accepted, freeing the driver and vehicle."
      >
        <div className="space-y-4">
          <FormField label="Reason for Unassigning" required htmlFor="unassignReason">
            <Textarea
              id="unassignReason"
              rows={3}
              placeholder="e.g. Vehicle mechanical maintenance, driver schedule change..."
              value={unassignReason}
              onChange={(e) => setUnassignReason(e.target.value)}
            />
          </FormField>

          <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-neutral-border">
            <Button variant="outline" size="sm" onClick={() => setUnassignModalOpen(false)}>
              Keep Assignment
            </Button>
            <Button
              variant="danger"
              size="sm"
              onClick={handleUnassign}
              isLoading={unassignMutation.isPending}
            >
              Release Assignment
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};
