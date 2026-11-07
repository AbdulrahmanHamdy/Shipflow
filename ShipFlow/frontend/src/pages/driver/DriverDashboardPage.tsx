import React from 'react';
import { Link } from 'react-router-dom';
import { useDriverCurrentAssignmentQuery } from '@/features/tracking/hooks';
import {
  useDriverProfileQuery,
  useAvailableShipmentsQuery,
  useUpdateAvailabilityMutation,
  useAcceptShipmentMutation,
} from '@/features/driver/portalHooks';
import { useDispatchHub } from '@/hooks/useDispatchHub';
import { usePageTitle } from '@/hooks/usePageTitle';
import { useAuth } from '@/features/auth/AuthContext';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/ui/EmptyState';
import { useToast } from '@/components/ui/Toast';
import { formatDateTime, formatWeight, formatCurrency } from '@/lib/formatters';
import { AppError } from '@/api/types';
import {
  Navigation,
  Truck,
  Clock,
  CalendarDays,
  ArrowRight,
  ShieldCheck,
  CheckCircle2,
  Radio,
  Power,
  MapPin,
  Package,
  AlertCircle,
  Sparkles,
} from 'lucide-react';

export const DriverDashboardPage: React.FC = () => {
  usePageTitle('Driver Cockpit');
  const { user } = useAuth();
  const toast = useToast();

  // SignalR live dispatch hub
  useDispatchHub();

  const { data: assignment, isLoading: isAssignmentLoading } = useDriverCurrentAssignmentQuery();
  const { data: profile, isLoading: isProfileLoading } = useDriverProfileQuery();
  const { data: availableShipments = [], isLoading: isShipmentsLoading } = useAvailableShipmentsQuery(
    profile?.isAvailable && !assignment
  );

  const updateAvailabilityMutation = useUpdateAvailabilityMutation();
  const acceptMutation = useAcceptShipmentMutation();

  const handleToggleAvailability = async () => {
    if (!profile) return;
    const newStatus = !profile.isAvailable;
    try {
      await updateAvailabilityMutation.mutateAsync({ isAvailable: newStatus });
      toast.success(
        newStatus ? 'You are now Online' : 'You are now Offline',
        newStatus
          ? 'Eligible open shipments will appear on your board.'
          : 'You will not receive new shipment offers.'
      );
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Failed to change status', appErr.message);
    }
  };

  const handleAcceptShipment = async (shipmentId: string) => {
    try {
      await acceptMutation.mutateAsync(shipmentId);
      toast.success('Shipment Accepted!', 'This consignment has been locked to your vehicle. Proceed to pickup.');
    } catch (err: unknown) {
      const appErr = err as AppError;
      if (appErr.status === 409) {
        toast.error('Already Taken', 'Another driver just accepted this shipment first.');
      } else {
        toast.error('Could not accept shipment', appErr.message || 'An unexpected error occurred.');
      }
    }
  };

  return (
    <div className="space-y-6 max-w-4xl mx-auto">
      <PageHeader
        title="Driver Cockpit"
        description={`Logged in as ${user?.email}. Drive safely and confirm stops sequentially.`}
        actions={
          <Button variant="outline" size="sm" asChild leftIcon={<CalendarDays className="w-4 h-4" />}>
            <Link to="/driver/assignments">View Trip History</Link>
          </Button>
        }
      />

      {/* Driver Status & Assigned Vehicle Bar */}
      {isProfileLoading ? (
        <Skeleton className="h-20 w-full rounded-2xl" />
      ) : profile ? (
        <Card className="border border-neutral-border shadow-sm overflow-hidden bg-white">
          <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div className="flex items-center gap-3.5">
              <div
                className={`p-3 rounded-2xl ${
                  profile.isAvailable
                    ? 'bg-emerald-50 text-emerald-600 ring-2 ring-emerald-500/20'
                    : 'bg-stone-100 text-stone-500'
                }`}
              >
                <Power className="w-5 h-5" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="font-bold text-neutral-primary text-sm">{profile.fullName}</h3>
                  <span
                    className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      profile.isAvailable
                        ? 'bg-emerald-100 text-emerald-800'
                        : 'bg-stone-200 text-stone-700'
                    }`}
                  >
                    <span
                      className={`w-2 h-2 rounded-full ${
                        profile.isAvailable ? 'bg-emerald-500 animate-pulse' : 'bg-stone-400'
                      }`}
                    />
                    {profile.isAvailable ? 'Online & Available' : 'Offline'}
                  </span>
                </div>

                <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-neutral-secondary mt-1">
                  <span>Carrier: <strong>{profile.companyName}</strong></span>
                  <span>&bull;</span>
                  {profile.assignedVehiclePlate ? (
                    <span className="flex items-center gap-1 text-neutral-primary font-medium">
                      <Truck className="w-3.5 h-3.5 text-brand" />
                      Vehicle: <strong>{profile.assignedVehiclePlate}</strong> ({profile.assignedVehicleType})
                    </span>
                  ) : (
                    <span className="text-amber-600 font-medium flex items-center gap-1">
                      <AlertCircle className="w-3.5 h-3.5" />
                      No vehicle assigned
                    </span>
                  )}
                </div>
              </div>
            </div>

            <Button
              variant={profile.isAvailable ? 'outline' : 'primary'}
              size="sm"
              onClick={handleToggleAvailability}
              isLoading={updateAvailabilityMutation.isPending}
              className="shrink-0 text-xs"
              leftIcon={<Power className="w-3.5 h-3.5" />}
            >
              {profile.isAvailable ? 'Go Offline' : 'Go Online'}
            </Button>
          </div>
        </Card>
      ) : null}

      {/* Active Job Alert / Card */}
      {isAssignmentLoading ? (
        <Skeleton className="h-64 w-full rounded-2xl" />
      ) : assignment ? (
        <Card className="border-2 border-brand bg-white shadow-panel overflow-hidden">
          <div className="bg-forest-night text-white p-5 flex items-center justify-between border-b border-forest-border">
            <div className="flex items-center gap-2">
              <span className="w-2.5 h-2.5 rounded-full bg-emerald-400 animate-ping" />
              <span className="font-bold text-xs uppercase tracking-wider text-emerald-400">
                ACTIVE ASSIGNMENT ENGAGED
              </span>
            </div>
            <span className="text-xs font-mono font-bold text-white bg-forest-deep px-2.5 py-1 rounded-md border border-forest-border">
              Status: {assignment.shipmentStatus}
            </span>
          </div>

          <CardContent className="p-6 space-y-5">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div>
                <span className="text-xs text-neutral-secondary block">Assigned Shipment</span>
                <h3 className="text-xl font-black text-neutral-primary">
                  Shipment #{assignment.shipmentId.substring(0, 8).toUpperCase()}
                </h3>
              </div>

              <StatusBadge status={assignment.shipmentStatus} size="md" />
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs pt-2">
              <div className="p-3.5 rounded-xl bg-neutral-appBg border border-neutral-border">
                <span className="text-neutral-secondary block text-[11px] mb-1">Window Schedule (UTC)</span>
                <span className="font-semibold text-neutral-primary">
                  {formatDateTime(assignment.windowStartUtc)} &rarr; {formatDateTime(assignment.windowEndUtc)}
                </span>
              </div>

              <div className="p-3.5 rounded-xl bg-neutral-appBg border border-neutral-border">
                <span className="text-neutral-secondary block text-[11px] mb-1">Assigned Vehicle</span>
                <span className="font-semibold text-neutral-primary font-mono">
                  Vehicle #{assignment.vehicleId.substring(0, 8)}
                </span>
              </div>
            </div>
          </CardContent>

          <CardFooter className="bg-neutral-appBg/60 flex items-center justify-between p-4">
            <span className="text-xs text-neutral-secondary">Ready for execution</span>
            <Button size="md" variant="primary" asChild rightIcon={<ArrowRight className="w-4 h-4" />}>
              <Link to="/driver/current-job">Open Delivery Cockpit</Link>
            </Button>
          </CardFooter>
        </Card>
      ) : (
        /* When No Active Assignment: Show Uber-style Available Shipments */
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Radio className="w-5 h-5 text-brand animate-pulse" />
              <h2 className="text-base font-bold text-neutral-primary">Available Dispatched Shipments</h2>
              {availableShipments.length > 0 && (
                <span className="px-2 py-0.5 rounded-full text-xs font-bold bg-brand text-white">
                  {availableShipments.length}
                </span>
              )}
            </div>
            <span className="text-xs text-neutral-secondary">Real-time matching active</span>
          </div>

          {!profile?.isAvailable ? (
            <Card className="border border-neutral-border bg-white text-center p-8">
              <div className="max-w-md mx-auto space-y-3">
                <div className="w-12 h-12 rounded-full bg-stone-100 text-stone-500 mx-auto flex items-center justify-center">
                  <Power className="w-6 h-6" />
                </div>
                <h3 className="font-bold text-neutral-primary text-sm">You are Currently Offline</h3>
                <p className="text-xs text-neutral-secondary">
                  Toggle your status to <strong>Online</strong> to receive instant open shipment offers matching your vehicle capacity.
                </p>
                <Button
                  variant="primary"
                  size="sm"
                  onClick={handleToggleAvailability}
                  isLoading={updateAvailabilityMutation.isPending}
                  leftIcon={<Power className="w-3.5 h-3.5" />}
                >
                  Go Online Now
                </Button>
              </div>
            </Card>
          ) : !profile?.assignedVehicleId ? (
            <Card className="border border-amber-200 bg-amber-50/40 text-center p-8">
              <div className="max-w-md mx-auto space-y-2">
                <AlertCircle className="w-10 h-10 text-amber-600 mx-auto" />
                <h3 className="font-bold text-neutral-primary text-sm">Vehicle Not Assigned</h3>
                <p className="text-xs text-neutral-secondary">
                  To view and accept matching shipments, ask your dispatcher to assign an active vehicle to your driver profile.
                </p>
              </div>
            </Card>
          ) : isShipmentsLoading ? (
            <div className="space-y-3">
              <Skeleton className="h-32 w-full rounded-2xl" />
              <Skeleton className="h-32 w-full rounded-2xl" />
            </div>
          ) : availableShipments.length === 0 ? (
            <EmptyState
              icon={Radio}
              title="No Dispatched Shipments Available"
              description="You are Online! Open shipments matching your vehicle's capacity and dimensions will appear here instantly. Shipments stay available until accepted."
            />
          ) : (
            <div className="space-y-3">
              {availableShipments.map((s) => (
                <Card
                  key={s.shipmentId}
                  className="border-2 border-brand/30 bg-white hover:border-brand transition-all shadow-sm overflow-hidden"
                >
                  <CardContent className="p-5 space-y-4">
                    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-neutral-border pb-3">
                      <div>
                        <div className="flex items-center gap-2">
                          <h4 className="font-bold text-neutral-primary text-sm">
                            {s.reference || `Shipment #${s.shipmentId.substring(0, 8).toUpperCase()}`}
                          </h4>
                          <span className="px-2 py-0.5 rounded text-[11px] font-semibold bg-brand-mint text-brand-mintText border border-brand/20">
                            {s.requiredVehicleType}
                          </span>
                        </div>
                        <span className="text-[11px] text-neutral-secondary">
                          Company: {s.carrierCompanyName}
                        </span>
                      </div>

                      <div className="text-right">
                        {s.priceAmount != null && (
                          <span className="text-base font-extrabold text-neutral-primary block">
                            {formatCurrency(s.priceAmount, s.priceCurrency || 'USD')}
                          </span>
                        )}
                        {s.estimatedDistanceKm != null && (
                          <span className="text-[11px] text-neutral-secondary font-medium">
                            ~{s.estimatedDistanceKm} km
                          </span>
                        )}
                      </div>
                    </div>

                    {/* Routing Details */}
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
                      <div className="flex items-start gap-2.5 p-2.5 rounded-xl bg-neutral-appBg border border-neutral-border">
                        <MapPin className="w-4 h-4 text-emerald-600 shrink-0 mt-0.5" />
                        <div className="min-w-0">
                          <span className="text-[10px] uppercase font-bold text-emerald-700 block">Pickup</span>
                          <span className="font-semibold text-neutral-primary truncate block">{s.pickupAddress}</span>
                        </div>
                      </div>

                      <div className="flex items-start gap-2.5 p-2.5 rounded-xl bg-neutral-appBg border border-neutral-border">
                        <MapPin className="w-4 h-4 text-red-500 shrink-0 mt-0.5" />
                        <div className="min-w-0">
                          <span className="text-[10px] uppercase font-bold text-red-700 block">Delivery</span>
                          <span className="font-semibold text-neutral-primary truncate block">{s.deliveryAddress}</span>
                        </div>
                      </div>
                    </div>

                    {/* Cargo specifications & Accept Button */}
                    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pt-1">
                      <div className="flex items-center gap-3 text-xs text-neutral-secondary">
                        <span className="flex items-center gap-1 font-medium text-neutral-primary">
                          <Package className="w-3.5 h-3.5 text-brand" /> {s.cargoDescription}
                        </span>
                        <span>&bull;</span>
                        <span>{formatWeight(s.cargoWeightKg)}</span>
                        <span>&bull;</span>
                        <span>{s.cargoVolumeM3} m³</span>
                      </div>

                      <Button
                        variant="primary"
                        size="sm"
                        onClick={() => handleAcceptShipment(s.shipmentId)}
                        isLoading={acceptMutation.isPending}
                        className="text-xs shrink-0"
                        rightIcon={<ArrowRight className="w-3.5 h-3.5" />}
                      >
                        Accept Consignment
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Driver Protocol Guidelines */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2 text-xs font-bold text-neutral-primary uppercase tracking-wider">
            <ShieldCheck className="w-4 h-4 text-brand" /> Driver Execution Guidelines
          </div>
        </CardHeader>
        <CardContent className="space-y-3 text-xs text-neutral-secondary leading-relaxed">
          <div className="flex items-start gap-3">
            <span className="w-5 h-5 rounded-full bg-brand-mint text-brand-mintText font-bold text-[10px] flex items-center justify-center flex-shrink-0 mt-0.5">
              1
            </span>
            <p>
              <strong>At Cargo Origin:</strong> Inspect payload specifications and tap <em>Confirm Pickup</em>.
            </p>
          </div>
          <div className="flex items-start gap-3">
            <span className="w-5 h-5 rounded-full bg-brand-mint text-brand-mintText font-bold text-[10px] flex items-center justify-center flex-shrink-0 mt-0.5">
              2
            </span>
            <p>
              <strong>Departing:</strong> Tap <em>Start Transit</em> to begin live customer tracking radar.
            </p>
          </div>
          <div className="flex items-start gap-3">
            <span className="w-5 h-5 rounded-full bg-brand-mint text-brand-mintText font-bold text-[10px] flex items-center justify-center flex-shrink-0 mt-0.5">
              3
            </span>
            <p>
              <strong>At Delivery Location:</strong> Capture recipient full name and notes to atomically seal the shipment.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};
