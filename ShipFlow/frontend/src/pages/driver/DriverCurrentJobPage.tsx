import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  useDriverCurrentAssignmentQuery,
  useDriverPickupMutation,
  useDriverStartTransitMutation,
  useDriverDeliverMutation,
  useDriverFailDeliveryMutation,
  useDriverRetryDeliveryMutation,
  useSubmitTrackingPointMutation,
} from '@/features/tracking/hooks';
import { useShipmentDetailQuery } from '@/features/shipments/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/ui/EmptyState';
import { Modal } from '@/components/ui/Modal';
import { FormField } from '@/components/ui/FormField';
import { Input, Textarea } from '@/components/ui/Input';
import { useToast } from '@/components/ui/Toast';
import { AppError } from '@/api/types';
import { formatDateTime, formatWeight, formatVolume } from '@/lib/formatters';
import {
  Truck,
  Package,
  MapPin,
  Navigation,
  CheckCircle2,
  AlertTriangle,
  RotateCcw,
  Send,
  ArrowRight,
  Clock,
  ShieldCheck,
} from 'lucide-react';

export const DriverCurrentJobPage: React.FC = () => {
  usePageTitle('Active Delivery Cockpit');
  const toast = useToast();

  const { data: assignment, isLoading: isAssignmentLoading, refetch: refetchAssignment } =
    useDriverCurrentAssignmentQuery();

  const { data: shipment, isLoading: isShipmentLoading, refetch: refetchShipment } =
    useShipmentDetailQuery(assignment?.shipmentId);

  // Driver action mutations
  const pickupMutation = useDriverPickupMutation();
  const startTransitMutation = useDriverStartTransitMutation();
  const deliverMutation = useDriverDeliverMutation();
  const failDeliveryMutation = useDriverFailDeliveryMutation();
  const retryDeliveryMutation = useDriverRetryDeliveryMutation();
  const submitGpsMutation = useSubmitTrackingPointMutation();

  // Modals
  const [deliverModalOpen, setDeliverModalOpen] = useState(false);
  const [failModalOpen, setFailModalOpen] = useState(false);
  const [gpsModalOpen, setGpsModalOpen] = useState(false);

  // Deliver form
  const [recipientName, setRecipientName] = useState('');
  const [deliveryNotes, setDeliveryNotes] = useState('');
  const [signatureRef, setSignatureRef] = useState('');

  // Fail form
  const [failureReason, setFailureReason] = useState('');

  // GPS form
  const [manualLat, setManualLat] = useState('30.0444');
  const [manualLng, setManualLng] = useState('31.2357');
  const [manualSpeed, setManualSpeed] = useState('65');

  const isLoading = isAssignmentLoading || isShipmentLoading;

  if (isLoading) {
    return (
      <div className="space-y-6 max-w-3xl mx-auto">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-96 w-full rounded-2xl" />
      </div>
    );
  }

  if (!assignment || !shipment) {
    return (
      <div className="max-w-2xl mx-auto py-12">
        <EmptyState
          icon={Truck}
          title="No Active Delivery Assignment"
          description="You do not have a currently active trip. When your dispatcher assigns you a consignment, it will automatically activate your cockpit."
          actionLabel="Check Assigned Trips"
          onAction={() => window.location.assign('/driver/assignments')}
        />
      </div>
    );
  }

  const status = assignment.shipmentStatus;

  // Actions
  const handlePickup = async () => {
    try {
      await pickupMutation.mutateAsync(shipment.id);
      toast.success('Pickup Confirmed', 'Cargo is now in Picked Up state.');
      refetchAssignment();
      refetchShipment();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Pickup failed', appErr.message);
    }
  };

  const handleStartTransit = async () => {
    try {
      await startTransitMutation.mutateAsync(shipment.id);
      toast.success('In Transit', 'Delivery route initiated. Telemetry radar is now active.');
      refetchAssignment();
      refetchShipment();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Failed to start transit', appErr.message);
    }
  };

  const handleDeliver = async () => {
    if (!recipientName.trim()) {
      toast.error('Recipient required', 'Please provide the recipient full name.');
      return;
    }
    try {
      await deliverMutation.mutateAsync({
        shipmentId: shipment.id,
        command: {
          recipientName: recipientName.trim(),
          deliveredAtUtc: new Date().toISOString(),
          notes: deliveryNotes.trim() || null,
          signatureReference: signatureRef.trim() || null,
          photoReference: null,
        },
      });
      toast.success('Delivery Completed', 'Consignment atomically sealed and closed.');
      setDeliverModalOpen(false);
      refetchAssignment();
      refetchShipment();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Delivery confirmation failed', appErr.message);
    }
  };

  const handleFailDelivery = async () => {
    if (!failureReason.trim()) {
      toast.error('Reason required', 'Please specify why delivery could not be completed.');
      return;
    }
    try {
      await failDeliveryMutation.mutateAsync({
        shipmentId: shipment.id,
        command: { reason: failureReason.trim() },
      });
      toast.info('Delivery Marked Failed', 'Reason recorded. You may retry or return to depot.');
      setFailModalOpen(false);
      refetchAssignment();
      refetchShipment();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Failed to report issue', appErr.message);
    }
  };

  const handleRetryDelivery = async () => {
    try {
      await retryDeliveryMutation.mutateAsync(shipment.id);
      toast.success('Delivery Retried', 'Consignment has resumed In Transit state.');
      refetchAssignment();
      refetchShipment();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Retry failed', appErr.message);
    }
  };

  // Submit GPS Ping
  const handleSendGps = async () => {
    const lat = parseFloat(manualLat);
    const lng = parseFloat(manualLng);
    const speed = manualSpeed ? parseFloat(manualSpeed) : undefined;

    try {
      await submitGpsMutation.mutateAsync({
        shipmentId: shipment.id,
        latitude: lat,
        longitude: lng,
        recordedAtUtc: new Date().toISOString(),
        speedKph: speed,
      });
      toast.success('GPS Reported', `Ping transmitted: ${lat.toFixed(4)}, ${lng.toFixed(4)}`);
      setGpsModalOpen(false);
    } catch (err: unknown) {
      const appErr = err as AppError;
      if (appErr.status === 429) {
        toast.error('Rate limited', `Please wait ${appErr.retryAfterSeconds || 60}s before next ping.`);
      } else {
        toast.error('GPS transmission failed', appErr.message);
      }
    }
  };

  return (
    <div className="max-w-2xl mx-auto space-y-6 pb-28">
      <PageHeader
        title={shipment.reference || `Shipment #${shipment.id.substring(0, 8).toUpperCase()}`}
        description="Mobile Driver Terminal &bull; Sequential Stop Actions"
        badge={<StatusBadge status={status} size="md" />}
        breadcrumbs={[{ label: 'Cockpit', href: '/driver' }, { label: 'Active Delivery' }]}
      />

      {/* Primary State Action Box (Large Touch Target) */}
      <Card className="border-2 border-brand overflow-hidden shadow-panel">
        <div className="bg-forest-night text-white p-4 flex items-center justify-between border-b border-forest-border">
          <span className="text-xs font-bold uppercase tracking-wider text-emerald-400 flex items-center gap-2">
            <Navigation className="w-4 h-4" /> Next Sequential Driver Action
          </span>
          <span className="text-xs font-mono font-bold text-neutral-300">Stage: {status}</span>
        </div>

        <CardContent className="p-5 space-y-4">
          {status === 'Assigned' && (
            <div className="space-y-3">
              <p className="text-xs text-neutral-secondary">
                You have arrived at the pickup location. Confirm payload receipt to proceed.
              </p>
              <Button
                variant="primary"
                size="lg"
                className="w-full h-14 text-base font-bold shadow-md"
                onClick={handlePickup}
                isLoading={pickupMutation.isPending}
                leftIcon={<Package className="w-5 h-5" />}
              >
                Confirm Cargo Pickup
              </Button>
            </div>
          )}

          {status === 'PickedUp' && (
            <div className="space-y-3">
              <p className="text-xs text-neutral-secondary">
                Cargo loaded securely. Tap to begin driving toward destination.
              </p>
              <Button
                variant="primary"
                size="lg"
                className="w-full h-14 text-base font-bold shadow-md"
                onClick={handleStartTransit}
                isLoading={startTransitMutation.isPending}
                leftIcon={<Truck className="w-5 h-5" />}
              >
                Start Transit to Destination
              </Button>
            </div>
          )}

          {status === 'InTransit' && (
            <div className="space-y-3">
              <p className="text-xs text-neutral-secondary">
                Currently en route. Transmit GPS updates or seal delivery upon destination arrival.
              </p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <Button
                  variant="primary"
                  size="lg"
                  className="w-full h-14 text-sm font-bold shadow-md bg-emerald-700 hover:bg-emerald-800"
                  onClick={() => setDeliverModalOpen(true)}
                  leftIcon={<CheckCircle2 className="w-5 h-5" />}
                >
                  Deliver &amp; Seal Proof
                </Button>
                <Button
                  variant="outline"
                  size="lg"
                  className="w-full h-14 text-sm font-bold border-red-300 text-red-600 hover:bg-red-50"
                  onClick={() => setFailModalOpen(true)}
                  leftIcon={<AlertTriangle className="w-5 h-5" />}
                >
                  Report Delivery Issue
                </Button>
              </div>
            </div>
          )}

          {status === 'DeliveryFailed' && (
            <div className="space-y-3">
              <div className="p-3 rounded-xl bg-red-50 border border-red-200 text-xs text-red-800">
                A delivery attempt failed previously. You may attempt redelivery or coordinate with dispatch.
              </div>
              <Button
                variant="primary"
                size="lg"
                className="w-full h-14 text-base font-bold"
                onClick={handleRetryDelivery}
                isLoading={retryDeliveryMutation.isPending}
                leftIcon={<RotateCcw className="w-5 h-5" />}
              >
                Retry Delivery Attempt
              </Button>
            </div>
          )}

          {status === 'Delivered' && (
            <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-center space-y-2">
              <CheckCircle2 className="w-8 h-8 text-emerald-600 mx-auto" />
              <h4 className="font-bold text-sm text-emerald-950">Consignment Fully Delivered</h4>
              <p className="text-xs text-emerald-800">
                Proof of delivery has been sealed. This trip is complete.
              </p>
            </div>
          )}

          {/* Quick GPS Transmit Button */}
          {status === 'InTransit' && (
            <div className="pt-2 border-t border-neutral-border flex items-center justify-between">
              <span className="text-xs text-neutral-secondary flex items-center gap-1.5">
                <Navigation className="w-4 h-4 text-brand" /> GPS Telemetry Transmitter
              </span>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setGpsModalOpen(true)}
                leftIcon={<Send className="w-3.5 h-3.5" />}
              >
                Send GPS Ping
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Cargo Specs Summary */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Package className="w-4 h-4 text-brand" />
            <CardTitle>Cargo Manifest</CardTitle>
          </div>
        </CardHeader>
        <CardContent className="space-y-3 text-xs">
          <p className="p-3 bg-neutral-appBg rounded-xl border border-neutral-border text-neutral-primary font-medium">
            {shipment.cargo.description}
          </p>
          <div className="grid grid-cols-2 gap-3 text-neutral-secondary">
            <div className="p-3 rounded-xl bg-white border border-neutral-border">
              <span className="block text-[11px]">Weight</span>
              <span className="text-sm font-bold text-neutral-primary">{formatWeight(shipment.cargo.weightKg)}</span>
            </div>
            <div className="p-3 rounded-xl bg-white border border-neutral-border">
              <span className="block text-[11px]">Volume</span>
              <span className="text-sm font-bold text-neutral-primary">{formatVolume(shipment.cargo.volumeM3)}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Destination Addresses */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <MapPin className="w-4 h-4 text-brand" />
            <CardTitle>Stops &amp; Navigation</CardTitle>
          </div>
        </CardHeader>
        <CardContent className="space-y-3 text-xs">
          {shipment.stops.map((stop, idx) => (
            <div key={stop.id} className="p-3.5 rounded-xl bg-neutral-appBg/60 border border-neutral-border space-y-1">
              <div className="flex items-center justify-between">
                <span className="font-bold text-brand uppercase text-[11px]">
                  {idx === 0 ? 'Pickup Location' : idx === shipment.stops.length - 1 ? 'Destination Drop' : `Stop ${idx + 1}`}
                </span>
                <span className="font-mono text-[10px] text-neutral-secondary">
                  {stop.latitude.toFixed(4)}, {stop.longitude.toFixed(4)}
                </span>
              </div>
              <p className="text-xs font-semibold text-neutral-primary">{stop.address}</p>
            </div>
          ))}
        </CardContent>
      </Card>

      {/* Deliver Modal */}
      <Modal
        isOpen={deliverModalOpen}
        onClose={() => setDeliverModalOpen(false)}
        title="Complete Delivery &amp; Seal Proof"
        description="Verify recipient details. This will atomically seal delivery proof."
      >
        <div className="space-y-4">
          <FormField label="Recipient Full Name" required htmlFor="recipientName">
            <Input
              id="recipientName"
              placeholder="e.g. Ahmed Mahmoud (Warehouse Lead)"
              value={recipientName}
              onChange={(e) => setRecipientName(e.target.value)}
            />
          </FormField>

          <FormField label="Delivery Notes (Optional)" htmlFor="deliveryNotes">
            <Textarea
              id="deliveryNotes"
              rows={2}
              placeholder="e.g. Delivered to Bay 4, signed cargo receipt manifest..."
              value={deliveryNotes}
              onChange={(e) => setDeliveryNotes(e.target.value)}
            />
          </FormField>

          <FormField label="Signature / Manifest Reference (Optional)" htmlFor="sigRef">
            <Input
              id="sigRef"
              placeholder="e.g. SIG-2026-9812 or Physical Waybill #482"
              value={signatureRef}
              onChange={(e) => setSignatureRef(e.target.value)}
            />
          </FormField>

          <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-neutral-border">
            <Button variant="outline" size="sm" onClick={() => setDeliverModalOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={handleDeliver}
              isLoading={deliverMutation.isPending}
            >
              Confirm Delivery
            </Button>
          </div>
        </div>
      </Modal>

      {/* Fail Modal */}
      <Modal
        isOpen={failModalOpen}
        onClose={() => setFailModalOpen(false)}
        title="Report Delivery Issue"
        description="Provide a justification why delivery could not be completed at this time."
      >
        <div className="space-y-4">
          <FormField label="Reason for Failure" required htmlFor="failureReason">
            <Textarea
              id="failureReason"
              rows={3}
              placeholder="e.g. Recipient facility was closed, cargo refused, or inaccessible entrance..."
              value={failureReason}
              onChange={(e) => setFailureReason(e.target.value)}
            />
          </FormField>

          <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-neutral-border">
            <Button variant="outline" size="sm" onClick={() => setFailModalOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="danger"
              size="sm"
              onClick={handleFailDelivery}
              isLoading={failDeliveryMutation.isPending}
            >
              Submit Issue Report
            </Button>
          </div>
        </div>
      </Modal>

      {/* GPS Transmission Modal */}
      <Modal
        isOpen={gpsModalOpen}
        onClose={() => setGpsModalOpen(false)}
        title="Transmit GPS Telemetry"
        description="Send your current location coordinates to the live tracking radar."
      >
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <FormField label="Latitude" required htmlFor="lat">
              <Input
                id="lat"
                type="number"
                step="0.0001"
                value={manualLat}
                onChange={(e) => setManualLat(e.target.value)}
              />
            </FormField>
            <FormField label="Longitude" required htmlFor="lng">
              <Input
                id="lng"
                type="number"
                step="0.0001"
                value={manualLng}
                onChange={(e) => setManualLng(e.target.value)}
              />
            </FormField>
          </div>

          <FormField label="Vehicle Speed (km/h, optional)" htmlFor="spd">
            <Input
              id="spd"
              type="number"
              step="1"
              value={manualSpeed}
              onChange={(e) => setManualSpeed(e.target.value)}
            />
          </FormField>

          <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-neutral-border">
            <Button variant="outline" size="sm" onClick={() => setGpsModalOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={handleSendGps}
              isLoading={submitGpsMutation.isPending}
              leftIcon={<Send className="w-3.5 h-3.5" />}
            >
              Transmit Coordinate
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};
