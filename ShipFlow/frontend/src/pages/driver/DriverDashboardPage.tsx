import React from 'react';
import { Link } from 'react-router-dom';
import { useDriverCurrentAssignmentQuery } from '@/features/tracking/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { useAuth } from '@/features/auth/AuthContext';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/ui/EmptyState';
import { formatDateTime } from '@/lib/formatters';
import { Navigation, Truck, Clock, CalendarDays, ArrowRight, ShieldCheck, CheckCircle2 } from 'lucide-react';

export const DriverDashboardPage: React.FC = () => {
  usePageTitle('Driver Cockpit');
  const { user } = useAuth();
  const { data: assignment, isLoading, refetch } = useDriverCurrentAssignmentQuery();

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

      {/* Active Job Alert / Card */}
      {isLoading ? (
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
        <EmptyState
          icon={Truck}
          title="No Active Assignment"
          description="You are currently available in the carrier driver pool. When your dispatcher assigns you a shipment, it will appear here instantly."
          actionLabel="Check Past Trips"
          onAction={() => window.location.assign('/driver/assignments')}
        />
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
