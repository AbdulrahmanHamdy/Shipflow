import React, { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import {
  useShipmentDetailQuery,
  useOpenForQuotesMutation,
  useCancelShipmentMutation,
  useAcceptQuoteMutation,
} from '@/features/shipments/hooks';
import { useLatestTrackingPointQuery } from '@/features/tracking/hooks';
import { useInitiatePaymentMutation } from '@/features/payments/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { useAuth } from '@/features/auth/AuthContext';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Skeleton } from '@/components/ui/Skeleton';
import { ErrorState } from '@/components/ui/ErrorState';
import { Modal } from '@/components/ui/Modal';
import { FormField } from '@/components/ui/FormField';
import { Textarea } from '@/components/ui/Input';
import { useToast } from '@/components/ui/Toast';
import { ShipmentMap } from '@/components/map/ShipmentMap';
import { AppError } from '@/api/types';
import {
  formatDate,
  formatDateTime,
  formatWeight,
  formatVolume,
  formatCurrency,
} from '@/lib/formatters';
import {
  Package,
  MapPin,
  Clock,
  Truck,
  CreditCard,
  Ban,
  CheckCircle2,
  Navigation,
  FileCheck2,
  AlertCircle,
  ExternalLink,
  ChevronRight,
  Shield,
} from 'lucide-react';

export const ShipmentDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  usePageTitle('Shipment Details');
  const { user, hasRole } = useAuth();
  const toast = useToast();

  const { data: shipment, isLoading, isError, error, refetch } = useShipmentDetailQuery(id);
  const { data: latestGps } = useLatestTrackingPointQuery(id, !!shipment);

  // Mutations
  const openQuotesMutation = useOpenForQuotesMutation();
  const cancelMutation = useCancelShipmentMutation();
  const acceptQuoteMutation = useAcceptQuoteMutation();
  const initiatePaymentMutation = useInitiatePaymentMutation();

  // Dialog states
  const [cancelModalOpen, setCancelModalOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [paymentModalOpen, setPaymentModalOpen] = useState(false);
  const [paymentIframeUrl, setPaymentIframeUrl] = useState<string | null>(null);

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-72" />
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <Skeleton className="h-64 md:col-span-2" />
          <Skeleton className="h-64" />
        </div>
      </div>
    );
  }

  if (isError || !shipment) {
    return (
      <div className="py-12">
        <ErrorState
          title="Consignment Not Found"
          message={(error as any)?.message || 'The requested shipment does not exist or you do not have permission to view it.'}
          onRetry={() => refetch()}
        />
      </div>
    );
  }

  const isOwner = user?.id && shipment ? true : false;
  const isCustomer = hasRole('Customer');

  // Handle Open for Quotes
  const handleOpenForQuotes = async () => {
    try {
      await openQuotesMutation.mutateAsync(shipment.id);
      toast.success('Quotes Opened', 'Carriers can now review cargo and submit binding bids.');
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Unable to open quotes', appErr.message);
    }
  };

  // Handle Cancel Shipment
  const handleCancel = async () => {
    if (!cancelReason.trim()) {
      toast.error('Reason required', 'Please provide a justification for cancellation.');
      return;
    }
    try {
      await cancelMutation.mutateAsync({
        id: shipment.id,
        command: { reason: cancelReason.trim() },
      });
      toast.success('Shipment Cancelled', 'Consignment has been cancelled and any assigned resources freed.');
      setCancelModalOpen(false);
      setCancelReason('');
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Cancellation failed', appErr.message);
    }
  };

  // Handle Accept Quote
  const handleAcceptQuote = async (quoteId: string) => {
    try {
      await acceptQuoteMutation.mutateAsync({
        shipmentId: shipment.id,
        quoteId,
      });
      toast.success('Quote Accepted', 'Carrier has been awarded the contract. Agreed price is locked.');
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Failed to accept quote', appErr.message);
    }
  };

  // Handle Paymob Payment Initiation
  const handleInitiatePayment = async () => {
    try {
      const res = await initiatePaymentMutation.mutateAsync({
        shipmentId: shipment.id,
      });

      if (res.iframeUrl) {
        setPaymentIframeUrl(res.iframeUrl);
        setPaymentModalOpen(true);
      } else {
        toast.info('Payment Initiated', `Order ID: ${res.orderId}`);
      }
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Payment initiation failed', appErr.message);
    }
  };

  const canCancel =
    shipment.status !== 'Delivered' &&
    shipment.status !== 'Cancelled' &&
    shipment.status !== 'PickedUp' &&
    shipment.status !== 'InTransit';

  return (
    <div className="space-y-6">
      <PageHeader
        title={shipment.reference || `Shipment #${shipment.id.substring(0, 8).toUpperCase()}`}
        description={`Registered on ${formatDateTime(shipment.createdAtUtc)} &bull; ID: ${shipment.id}`}
        badge={<StatusBadge status={shipment.status} size="md" />}
        breadcrumbs={[
          { label: 'Dashboard', href: '/app' },
          { label: 'Shipments', href: '/app/shipments' },
          { label: shipment.reference || shipment.id.substring(0, 8) },
        ]}
        actions={
          <div className="flex items-center gap-2 flex-wrap">
            {shipment.status === 'Pending' && isCustomer && (
              <Button
                variant="primary"
                size="sm"
                onClick={handleOpenForQuotes}
                isLoading={openQuotesMutation.isPending}
                leftIcon={<Clock className="w-4 h-4" />}
              >
                Open For Carrier Quotes
              </Button>
            )}

            {shipment.status === 'Accepted' && isCustomer && (
              <Button
                variant="primary"
                size="sm"
                onClick={handleInitiatePayment}
                isLoading={initiatePaymentMutation.isPending}
                leftIcon={<CreditCard className="w-4 h-4" />}
              >
                Pay With Paymob
              </Button>
            )}

            {shipment.status === 'Delivered' && (
              <Button variant="secondary" size="sm" asChild leftIcon={<FileCheck2 className="w-4 h-4" />}>
                <Link to={`/app/shipments/${shipment.id}/proof`}>View Proof of Delivery</Link>
              </Button>
            )}

            <Button variant="outline" size="sm" asChild leftIcon={<Navigation className="w-4 h-4" />}>
              <Link to={`/app/shipments/${shipment.id}/tracking`}>Live Tracking Radar</Link>
            </Button>

            {canCancel && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setCancelModalOpen(true)}
                className="text-red-600 hover:text-red-700 hover:bg-red-50"
                leftIcon={<Ban className="w-4 h-4" />}
              >
                Cancel Consignment
              </Button>
            )}
          </div>
        }
      />

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left Column (2 Cols): Cargo Details & Stops Timeline */}
        <div className="lg:col-span-2 space-y-6">
          {/* Cargo Specs Card */}
          <Card>
            <CardHeader>
              <div className="flex items-center gap-2">
                <Package className="w-5 h-5 text-brand" />
                <CardTitle>Cargo Specifications</CardTitle>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-sm text-neutral-primary bg-neutral-appBg/60 p-3.5 rounded-xl border border-neutral-border">
                {shipment.cargo.description}
              </p>

              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-xs pt-1">
                <div className="p-3 rounded-xl bg-white border border-neutral-border">
                  <span className="text-neutral-secondary text-[11px] block">Payload Weight</span>
                  <span className="text-base font-bold text-neutral-primary tabular-nums">
                    {formatWeight(shipment.cargo.weightKg)}
                  </span>
                </div>

                <div className="p-3 rounded-xl bg-white border border-neutral-border">
                  <span className="text-neutral-secondary text-[11px] block">Cargo Volume</span>
                  <span className="text-base font-bold text-neutral-primary tabular-nums">
                    {formatVolume(shipment.cargo.volumeM3)}
                  </span>
                </div>

                <div className="p-3 rounded-xl bg-white border border-neutral-border">
                  <span className="text-neutral-secondary text-[11px] block">Agreed Freight Rate</span>
                  <span className="text-base font-bold text-neutral-primary tabular-nums">
                    {shipment.agreedAmount != null
                      ? formatCurrency(shipment.agreedAmount, shipment.agreedCurrency || 'USD')
                      : 'Unassigned'}
                  </span>
                </div>

                <div className="p-3 rounded-xl bg-white border border-neutral-border">
                  <span className="text-neutral-secondary text-[11px] block">Stops Plotted</span>
                  <span className="text-base font-bold text-neutral-primary tabular-nums">
                    {shipment.stops.length} Waypoints
                  </span>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Stops Timeline Card */}
          <Card>
            <CardHeader>
              <div className="flex items-center gap-2">
                <MapPin className="w-5 h-5 text-brand" />
                <CardTitle>Routing Stops &amp; Waypoints</CardTitle>
              </div>
              <CardDescription>Sequenced movement plan with UTC time windows</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {shipment.stops.map((stop, idx) => {
                  const isPickup = idx === 0;
                  const isDelivery = idx === shipment.stops.length - 1;

                  return (
                    <div
                      key={stop.id}
                      className="flex items-start gap-3 p-4 rounded-xl bg-neutral-appBg/60 border border-neutral-border"
                    >
                      <div className="w-7 h-7 rounded-full bg-brand text-white font-bold text-xs flex items-center justify-center flex-shrink-0 mt-0.5">
                        {idx + 1}
                      </div>
                      <div className="flex-1 space-y-1">
                        <div className="flex items-center justify-between">
                          <span className="text-xs font-bold text-neutral-primary uppercase tracking-wide">
                            {isPickup ? 'Origin (Pickup)' : isDelivery ? 'Destination (Delivery)' : `Waypoint ${idx + 1}`}
                          </span>
                          <span className="text-[11px] font-mono text-neutral-secondary">
                            {stop.latitude.toFixed(4)}, {stop.longitude.toFixed(4)}
                          </span>
                        </div>
                        <p className="text-sm font-semibold text-neutral-primary">{stop.address}</p>
                        {stop.windowStartUtc && (
                          <div className="flex items-center gap-2 text-xs text-neutral-secondary pt-1">
                            <Clock className="w-3.5 h-3.5 text-brand" />
                            <span>
                              Window: {formatDateTime(stop.windowStartUtc)} &rarr; {formatDateTime(stop.windowEndUtc)}
                            </span>
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </CardContent>
          </Card>

          {/* Quotations Board */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between w-full">
                <div className="flex items-center gap-2">
                  <CreditCard className="w-5 h-5 text-brand" />
                  <div>
                    <CardTitle>Carrier Quotations ({shipment.quotes?.length || 0})</CardTitle>
                    <CardDescription>
                      {shipment.status === 'Quoting'
                        ? 'Carriers have submitted the following bids. Accept one to award the freight contract.'
                        : 'Quotes associated with this consignment'}
                    </CardDescription>
                  </div>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              {!shipment.quotes || shipment.quotes.length === 0 ? (
                <div className="text-center py-6 text-xs text-neutral-secondary">
                  {shipment.status === 'Pending'
                    ? 'Shipment is Pending. Click "Open for Carrier Quotes" above to invite bids.'
                    : 'No carrier quotes submitted yet. Carriers browsing the freight exchange will place bids.'}
                </div>
              ) : (
                <div className="space-y-3">
                  {shipment.quotes.map((q) => {
                    const isAccepted = q.status === 'Accepted';

                    return (
                      <div
                        key={q.id}
                        className={`p-4 rounded-xl border flex flex-col sm:flex-row sm:items-center justify-between gap-3 ${
                          isAccepted
                            ? 'bg-emerald-50/60 border-emerald-300'
                            : 'bg-white border-neutral-border'
                        }`}
                      >
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <span className="text-lg font-black text-neutral-primary tabular-nums">
                              {formatCurrency(q.amount, q.currency)}
                            </span>
                            <span
                              className={`text-[10px] uppercase font-bold px-2 py-0.5 rounded-full ${
                                isAccepted
                                  ? 'bg-emerald-200 text-emerald-900'
                                  : 'bg-neutral-hover text-neutral-secondary'
                              }`}
                            >
                              {q.status}
                            </span>
                          </div>
                          <p className="text-xs text-neutral-secondary">
                            Terms: <span className="font-medium text-neutral-primary">{q.terms}</span>
                          </p>
                          <p className="text-[11px] text-neutral-muted">
                            Submitted: {formatDateTime(q.createdAtUtc)} &bull; Valid until: {formatDateTime(q.expiresAtUtc)}
                          </p>
                        </div>

                        {shipment.status === 'Quoting' && isCustomer && q.status === 'Submitted' && (
                          <Button
                            variant="primary"
                            size="sm"
                            onClick={() => handleAcceptQuote(q.id)}
                            isLoading={acceptQuoteMutation.isPending}
                            leftIcon={<CheckCircle2 className="w-4 h-4" />}
                          >
                            Accept Bid
                          </Button>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Right Column (1 Col): Live Map Preview & Actions */}
        <div className="space-y-6">
          {/* Map Preview Card */}
          <Card>
            <CardHeader>
              <div className="flex items-center gap-2">
                <Navigation className="w-5 h-5 text-brand" />
                <CardTitle>Route Map</CardTitle>
              </div>
            </CardHeader>
            <CardContent className="p-2">
              <ShipmentMap
                stops={shipment.stops}
                latestPosition={latestGps}
                height="320px"
              />
            </CardContent>
            <CardFooter>
              <Button variant="ghost" size="sm" asChild className="w-full text-xs" rightIcon={<ChevronRight className="w-4 h-4" />}>
                <Link to={`/app/shipments/${shipment.id}/tracking`}>Open Full Radar View</Link>
              </Button>
            </CardFooter>
          </Card>

          {/* Operational Integrity Info */}
          <Card>
            <CardHeader>
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-brand">
                <Shield className="w-4 h-4" /> State Machine Rules
              </div>
            </CardHeader>
            <CardContent className="text-xs text-neutral-secondary space-y-2 leading-relaxed">
              <p>
                &bull; Status is currently <strong>{shipment.status}</strong>.
              </p>
              <p>
                &bull; Carriers can submit quotes only during <em>Quoting</em>.
              </p>
              <p>
                &bull; Once a quote is accepted, carrier assignment and agreed rate are locked atomically in the database.
              </p>
              <p>
                &bull; Cancellation releases any active driver/vehicle schedule lock in the same commit.
              </p>
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Cancel Modal */}
      <Modal
        isOpen={cancelModalOpen}
        onClose={() => setCancelModalOpen(false)}
        title="Cancel Consignment"
        description="Provide a justification for cancellation. This will release any booked carrier assignments."
      >
        <div className="space-y-4">
          <FormField label="Cancellation Reason" required htmlFor="cancelReason">
            <Textarea
              id="cancelReason"
              rows={3}
              placeholder="e.g. Client requested delivery schedule change or supplier delay..."
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
            />
          </FormField>

          <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-neutral-border">
            <Button variant="outline" size="sm" onClick={() => setCancelModalOpen(false)}>
              Keep Shipment
            </Button>
            <Button
              variant="danger"
              size="sm"
              onClick={handleCancel}
              isLoading={cancelMutation.isPending}
            >
              Confirm Cancellation
            </Button>
          </div>
        </div>
      </Modal>

      {/* Paymob Iframe Modal */}
      <Modal
        isOpen={paymentModalOpen}
        onClose={() => setPaymentModalOpen(false)}
        title="Paymob Electronic Checkout"
        description="Complete payment through Paymob's PCI-DSS compliant payment frame."
        maxWidth="lg"
      >
        <div className="space-y-4">
          {paymentIframeUrl ? (
            <div className="w-full h-[520px] rounded-xl overflow-hidden border border-neutral-border bg-neutral-appBg">
              <iframe
                src={paymentIframeUrl}
                title="Paymob Checkout"
                className="w-full h-full border-none"
              />
            </div>
          ) : (
            <div className="p-8 text-center text-neutral-secondary">
              Initializing Paymob payment iframe...
            </div>
          )}

          <div className="flex items-center justify-between text-xs text-neutral-secondary pt-2">
            <span>Verified 256-bit encrypted checkout</span>
            <Button variant="outline" size="sm" onClick={() => setPaymentModalOpen(false)}>
              Close Checkout
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};
