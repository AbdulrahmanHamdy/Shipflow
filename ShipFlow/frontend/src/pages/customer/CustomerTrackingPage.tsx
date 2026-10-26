import React from 'react';
import { useParams, Link } from 'react-router-dom';
import { useShipmentDetailQuery } from '@/features/shipments/hooks';
import { useLatestTrackingPointQuery, useTrackingHistoryQuery } from '@/features/tracking/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Skeleton } from '@/components/ui/Skeleton';
import { ErrorState } from '@/components/ui/ErrorState';
import { ShipmentMap } from '@/components/map/ShipmentMap';
import { formatDateTime, formatRelativeTime } from '@/lib/formatters';
import { Navigation, Clock, ShieldCheck, ArrowLeft, RefreshCw, MapPin } from 'lucide-react';

export const CustomerTrackingPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  usePageTitle('Live GPS Tracking');

  const { data: shipment, isLoading: isShipmentLoading, isError, error, refetch: refetchShipment } = useShipmentDetailQuery(id);
  const { data: latestPosition, refetch: refetchLatest, isFetching: isFetchingLatest } = useLatestTrackingPointQuery(id, !!shipment);
  const { data: historyData, refetch: refetchHistory } = useTrackingHistoryQuery(id, { page: 1, pageSize: 100 });

  const handleManualRefresh = () => {
    refetchShipment();
    refetchLatest();
    refetchHistory();
  };

  if (isShipmentLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-[500px] w-full rounded-2xl" />
      </div>
    );
  }

  if (isError || !shipment) {
    return (
      <div className="py-12">
        <ErrorState
          title="Tracking Stream Unavailable"
          message={(error as any)?.message || 'Shipment telemetry records could not be retrieved.'}
          onRetry={handleManualRefresh}
        />
      </div>
    );
  }

  const trackingPoints = historyData?.items || [];

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Live Tracking: ${shipment.reference || shipment.id.substring(0, 8)}`}
        description="Real-time GPS telemetry radar streamed directly from the assigned driver."
        badge={<StatusBadge status={shipment.status} size="md" />}
        breadcrumbs={[
          { label: 'Dashboard', href: '/app' },
          { label: 'Shipments', href: '/app/shipments' },
          { label: shipment.reference || shipment.id.substring(0, 8), href: `/app/shipments/${shipment.id}` },
          { label: 'Live Tracking' },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={handleManualRefresh}
              isLoading={isFetchingLatest}
              leftIcon={<RefreshCw className="w-3.5 h-3.5" />}
            >
              Refresh Telemetry
            </Button>
            <Button variant="secondary" size="sm" asChild leftIcon={<ArrowLeft className="w-3.5 h-3.5" />}>
              <Link to={`/app/shipments/${shipment.id}`}>Back to Overview</Link>
            </Button>
          </div>
        }
      />

      {/* Main Interactive Map Component */}
      <ShipmentMap
        stops={shipment.stops}
        trackingPoints={trackingPoints}
        latestPosition={latestPosition}
        height="550px"
      />

      {/* Telemetry Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card>
          <CardHeader>
            <div className="flex items-center gap-2 text-xs font-bold text-neutral-primary uppercase tracking-wider">
              <Navigation className="w-4 h-4 text-brand" /> Driver Telemetry Status
            </div>
          </CardHeader>
          <CardContent className="space-y-2 text-xs text-neutral-secondary">
            {latestPosition ? (
              <>
                <p>
                  <strong>Reported At:</strong> {formatDateTime(latestPosition.recordedAtUtc)} (
                  {formatRelativeTime(latestPosition.recordedAtUtc)})
                </p>
                <p>
                  <strong>Coordinates:</strong> {latestPosition.latitude.toFixed(5)}, {latestPosition.longitude.toFixed(5)}
                </p>
                <p>
                  <strong>Speed:</strong> {latestPosition.speedKph != null ? `${latestPosition.speedKph} km/h` : 'Stationary / Not reported'}
                </p>
              </>
            ) : (
              <p className="text-neutral-muted italic">
                Driver has not yet submitted real-time GPS coordinates. Position updates will stream once in transit.
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center gap-2 text-xs font-bold text-neutral-primary uppercase tracking-wider">
              <Clock className="w-4 h-4 text-brand" /> Polling &amp; Freshness
            </div>
          </CardHeader>
          <CardContent className="space-y-2 text-xs text-neutral-secondary">
            <p>
              &bull; Automatic refresh interval: <strong>20 seconds</strong> while window is in focus.
            </p>
            <p>
              &bull; Polling automatically suspends when switching browser tabs to conserve network bandwidth.
            </p>
            <p>
              &bull; Driver submission rate-limited to 60 coordinates / minute to guarantee network stability.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center gap-2 text-xs font-bold text-neutral-primary uppercase tracking-wider">
              <ShieldCheck className="w-4 h-4 text-brand" /> Anti-Fabrication Guarantee
            </div>
          </CardHeader>
          <CardContent className="space-y-2 text-xs text-neutral-secondary">
            <p>
              Route lines connect verified driver pings. ShipFlow never manufactures synthetic road tracks or simulated positions.
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
};
