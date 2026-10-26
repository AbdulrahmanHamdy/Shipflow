import React from 'react';
import { Link } from 'react-router-dom';
import { useShipmentsListQuery } from '@/features/shipments/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { useAuth } from '@/features/auth/AuthContext';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/ui/EmptyState';
import { ErrorState } from '@/components/ui/ErrorState';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/Table';
import { formatDate, formatWeight, formatCurrency } from '@/lib/formatters';
import {
  Package,
  PlusCircle,
  Clock,
  Truck,
  CheckCircle2,
  ArrowRight,
  TrendingUp,
  MapPin,
  ExternalLink,
} from 'lucide-react';

export const CustomerDashboardPage: React.FC = () => {
  usePageTitle('Shipper Dashboard');
  const { user } = useAuth();

  const { data, isLoading, isError, error, refetch } = useShipmentsListQuery({
    page: 1,
    pageSize: 20,
    scope: 'Mine',
  });

  const shipments = data?.items || [];
  const totalCount = data?.totalCount || 0;

  // Compute operational aggregates strictly from real fetched data
  const quotingCount = shipments.filter((s) => s.status === 'Quoting').length;
  const inTransitCount = shipments.filter((s) => s.status === 'InTransit' || s.status === 'PickedUp').length;
  const deliveredCount = shipments.filter((s) => s.status === 'Delivered').length;

  return (
    <div className="space-y-8">
      <PageHeader
        title="Shipper Control Center"
        description={`Welcome back, ${user?.email}. Monitor and dispatch your consignments.`}
        actions={
          <Button size="md" variant="primary" asChild leftIcon={<PlusCircle className="w-4 h-4" />}>
            <Link to="/app/shipments/new">Create New Shipment</Link>
          </Button>
        }
      />

      {/* Metrics Row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardContent className="p-5 flex items-center justify-between">
            <div className="space-y-1">
              <span className="text-xs font-semibold text-neutral-secondary">Total Consignments</span>
              <div className="text-2xl font-black text-neutral-primary">{totalCount}</div>
              <span className="text-[10px] text-neutral-muted block">Registered in account</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-neutral-hover text-neutral-primary flex items-center justify-center font-bold">
              <Package className="w-6 h-6" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-5 flex items-center justify-between">
            <div className="space-y-1">
              <span className="text-xs font-semibold text-neutral-secondary">Open for Quotes</span>
              <div className="text-2xl font-black text-purple-700">{quotingCount}</div>
              <span className="text-[10px] text-neutral-muted block">Awaiting carrier bids</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center font-bold border border-purple-200">
              <Clock className="w-6 h-6" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-5 flex items-center justify-between">
            <div className="space-y-1">
              <span className="text-xs font-semibold text-neutral-secondary">Active In Transit</span>
              <div className="text-2xl font-black text-sky-700">{inTransitCount}</div>
              <span className="text-[10px] text-neutral-muted block">Real-time driver transit</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-sky-50 text-sky-700 flex items-center justify-center font-bold border border-sky-200">
              <Truck className="w-6 h-6" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-5 flex items-center justify-between">
            <div className="space-y-1">
              <span className="text-xs font-semibold text-neutral-secondary">Completed Deliveries</span>
              <div className="text-2xl font-black text-brand">{deliveredCount}</div>
              <span className="text-[10px] text-neutral-muted block">Verified proof on file</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-brand-mint text-brand-mintText flex items-center justify-center font-bold">
              <CheckCircle2 className="w-6 h-6" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Recent Shipments Table Card */}
      <Card>
        <CardHeader>
          <div>
            <CardTitle>Recent Consignments</CardTitle>
            <CardDescription>Live status of your latest active and completed shipments</CardDescription>
          </div>
          <Button variant="ghost" size="sm" asChild rightIcon={<ArrowRight className="w-4 h-4" />}>
            <Link to="/app/shipments">View All</Link>
          </Button>
        </CardHeader>

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
          ) : shipments.length === 0 ? (
            <div className="p-6">
              <EmptyState
                title="No consignments recorded yet"
                description="Create your first shipment to request carrier bids and start tracking freight."
                actionLabel="Create Shipment"
                onAction={() => window.location.assign('/app/shipments/new')}
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Reference / ID</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Pickup &rarr; Destination</TableHead>
                  <TableHead>Payload</TableHead>
                  <TableHead>Rate</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead className="text-right">Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {shipments.slice(0, 5).map((s) => (
                  <TableRow key={s.id}>
                    <TableCell>
                      <Link to={`/app/shipments/${s.id}`} className="font-bold text-brand hover:underline block">
                        {s.reference || `SHP-${s.id.substring(0, 8).toUpperCase()}`}
                      </Link>
                      <span className="text-[10px] text-neutral-secondary font-mono">
                        {s.id.substring(0, 13)}...
                      </span>
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={s.status} />
                    </TableCell>
                    <TableCell className="max-w-[220px]">
                      <div className="text-xs truncate" title={s.pickupAddress}>
                        <span className="font-semibold text-neutral-secondary">From:</span> {s.pickupAddress}
                      </div>
                      <div className="text-xs truncate text-neutral-secondary" title={s.deliveryAddress}>
                        <span className="font-semibold">To:</span> {s.deliveryAddress}
                      </div>
                    </TableCell>
                    <TableCell className="text-xs tabular-nums">
                      {formatWeight(s.cargoWeightKg)}
                      <span className="text-[10px] text-neutral-secondary block">{s.cargoVolumeM3} m³</span>
                    </TableCell>
                    <TableCell className="text-xs font-semibold tabular-nums">
                      {s.agreedAmount != null ? formatCurrency(s.agreedAmount, s.agreedCurrency || 'USD') : 'Pending bid'}
                    </TableCell>
                    <TableCell className="text-xs text-neutral-secondary">
                      {formatDate(s.createdAtUtc)}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="outline" size="sm" asChild className="h-8 text-xs">
                        <Link to={`/app/shipments/${s.id}`}>Details</Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
};
