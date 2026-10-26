import React from 'react';
import { Link } from 'react-router-dom';
import { useMyCompanyQuery, useVehiclesListQuery, useDriversListQuery } from '@/features/carrier/hooks';
import { useShipmentsListQuery } from '@/features/shipments/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { useAuth } from '@/features/auth/AuthContext';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Skeleton } from '@/components/ui/Skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/Table';
import { formatDate, formatWeight, formatCurrency } from '@/lib/formatters';
import {
  Building2,
  Truck,
  Users,
  Package,
  ArrowRight,
  PlusCircle,
  Clock,
  Layers,
  Send,
  ShieldAlert,
} from 'lucide-react';

export const CarrierDashboardPage: React.FC = () => {
  usePageTitle('Carrier Control Tower');
  const { hasRole } = useAuth();

  const { data: company, isLoading: isCompanyLoading } = useMyCompanyQuery();
  const { data: vehiclesData } = useVehiclesListQuery({ page: 1, pageSize: 50 });
  const { data: driversData } = useDriversListQuery({ page: 1, pageSize: 50 });

  // Paged open-for-quote shipments to monitor bidding opportunities
  const { data: openBidsData } = useShipmentsListQuery({
    scope: 'OpenForQuotes',
    page: 1,
    pageSize: 5,
  });

  const vehicles = vehiclesData?.items || [];
  const drivers = driversData?.items || [];
  const openBids = openBidsData?.items || [];

  if (isCompanyLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-72" />
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <Skeleton className="h-28" />
          <Skeleton className="h-28" />
          <Skeleton className="h-28" />
          <Skeleton className="h-28" />
        </div>
      </div>
    );
  }

  if (!company) {
    return (
      <div className="max-w-xl mx-auto py-12 text-center space-y-4">
        <div className="w-16 h-16 rounded-2xl bg-amber-50 text-amber-600 flex items-center justify-center mx-auto border border-amber-200">
          <Building2 className="w-8 h-8" />
        </div>
        <h2 className="text-2xl font-black text-neutral-primary">Carrier Company Profile Needed</h2>
        <p className="text-xs text-neutral-secondary">
          You are not currently linked to an active transport company. Create a company or contact your administrator.
        </p>
        <Button variant="primary" size="md" asChild>
          <Link to="/carrier/onboarding">Establish Transport Company</Link>
        </Button>
      </div>
    );
  }

  const activeVehiclesCount = vehicles.filter((v) => v.isActive).length;
  const activeDriversCount = drivers.filter((d) => d.isActive).length;

  return (
    <div className="space-y-8">
      <PageHeader
        title={`${company.name} Control Tower`}
        description={`Fleet operations and freight exchange &bull; Role: ${company.myRole}`}
        badge={
          <span className="text-xs font-bold text-emerald-800 bg-emerald-100 px-3 py-1 rounded-full border border-emerald-300">
            Active Carrier
          </span>
        }
        actions={
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" asChild leftIcon={<Truck className="w-4 h-4" />}>
              <Link to="/carrier/fleet">Manage Fleet</Link>
            </Button>
            <Button variant="primary" size="sm" asChild rightIcon={<ArrowRight className="w-4 h-4" />}>
              <Link to="/carrier/shipments">Browse Freight Exchange</Link>
            </Button>
          </div>
        }
      />

      {/* Metrics Row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardContent className="p-5 flex items-center justify-between">
            <div className="space-y-1">
              <span className="text-xs font-semibold text-neutral-secondary">Active Vehicles</span>
              <div className="text-2xl font-black text-neutral-primary">{activeVehiclesCount}</div>
              <span className="text-[10px] text-neutral-muted block">{vehicles.length} total registered</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-neutral-hover text-neutral-primary flex items-center justify-center font-bold">
              <Truck className="w-6 h-6" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-5 flex items-center justify-between">
            <div className="space-y-1">
              <span className="text-xs font-semibold text-neutral-secondary">Active Drivers</span>
              <div className="text-2xl font-black text-neutral-primary">{activeDriversCount}</div>
              <span className="text-[10px] text-neutral-muted block">{drivers.length} company profiles</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-sky-50 text-sky-700 flex items-center justify-center font-bold border border-sky-200">
              <Users className="w-6 h-6" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-5 flex items-center justify-between">
            <div className="space-y-1">
              <span className="text-xs font-semibold text-neutral-secondary">Bidding Opportunities</span>
              <div className="text-2xl font-black text-purple-700">{openBidsData?.totalCount || 0}</div>
              <span className="text-[10px] text-neutral-muted block">Open on freight board</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center font-bold border border-purple-200">
              <Send className="w-6 h-6" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-5 flex items-center justify-between">
            <div className="space-y-1">
              <span className="text-xs font-semibold text-neutral-secondary">Company Governance</span>
              <div className="text-sm font-bold text-brand">{company.myRole}</div>
              <span className="text-[10px] text-neutral-muted block">Direct database role</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-brand-mint text-brand-mintText flex items-center justify-center font-bold">
              <Building2 className="w-6 h-6" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Open Bidding Opportunities Table */}
      <Card>
        <CardHeader>
          <div>
            <CardTitle>Open Consignments for Carrier Bids</CardTitle>
            <CardDescription>Live consignments in Quoting status ready for competitive quotes</CardDescription>
          </div>
          <Button variant="ghost" size="sm" asChild rightIcon={<ArrowRight className="w-4 h-4" />}>
            <Link to="/carrier/shipments?scope=OpenForQuotes">View All Opportunities</Link>
          </Button>
        </CardHeader>

        <CardContent className="p-0">
          {openBids.length === 0 ? (
            <div className="p-8 text-center text-xs text-neutral-secondary">
              No new shipments are currently open for quotes. Check back shortly.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Reference / ID</TableHead>
                  <TableHead>Route (Pickup &rarr; Destination)</TableHead>
                  <TableHead>Cargo Payload</TableHead>
                  <TableHead>Published Date</TableHead>
                  <TableHead className="text-right">Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {openBids.map((b) => (
                  <TableRow key={b.id}>
                    <TableCell>
                      <span className="font-bold text-neutral-primary block">
                        {b.reference || `SHP-${b.id.substring(0, 8).toUpperCase()}`}
                      </span>
                      <span className="text-[10px] font-mono text-neutral-secondary">{b.id.substring(0, 14)}...</span>
                    </TableCell>
                    <TableCell className="max-w-[240px]">
                      <div className="text-xs truncate font-medium">{b.pickupAddress}</div>
                      <div className="text-xs truncate text-neutral-secondary">&rarr; {b.deliveryAddress}</div>
                    </TableCell>
                    <TableCell className="text-xs tabular-nums">
                      <span className="font-semibold">{formatWeight(b.cargoWeightKg)}</span>
                      <span className="text-neutral-secondary block text-[11px]">{b.cargoVolumeM3} m³</span>
                    </TableCell>
                    <TableCell className="text-xs text-neutral-secondary">{formatDate(b.createdAtUtc)}</TableCell>
                    <TableCell className="text-right">
                      <Button variant="primary" size="sm" asChild className="h-8 text-xs">
                        <Link to={`/app/shipments/${b.id}`}>Submit Quote</Link>
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
