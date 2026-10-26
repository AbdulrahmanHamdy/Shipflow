import React, { useState, useEffect } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useShipmentsListQuery } from '@/features/shipments/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { ShipmentStatus } from '@/api/types';
import { PageHeader } from '@/components/ui/PageHeader';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Pagination } from '@/components/ui/Pagination';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/Table';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/ui/EmptyState';
import { ErrorState } from '@/components/ui/ErrorState';
import { Card, CardContent } from '@/components/ui/Card';
import { formatDate, formatWeight, formatCurrency } from '@/lib/formatters';
import { PlusCircle, Search, Filter, ArrowRight, Package } from 'lucide-react';

const STATUS_FILTERS: { label: string; value: ShipmentStatus | 'ALL' }[] = [
  { label: 'All Shipments', value: 'ALL' },
  { label: 'Pending', value: 'Pending' },
  { label: 'Open for Quotes', value: 'Quoting' },
  { label: 'Accepted', value: 'Accepted' },
  { label: 'Assigned', value: 'Assigned' },
  { label: 'In Transit', value: 'InTransit' },
  { label: 'Delivered', value: 'Delivered' },
  { label: 'Cancelled', value: 'Cancelled' },
];

export const ShipmentsListPage: React.FC = () => {
  usePageTitle('My Shipments');
  const [searchParams, setSearchParams] = useSearchParams();

  const searchParam = searchParams.get('search') || '';
  const statusParam = (searchParams.get('status') as (ShipmentStatus | 'ALL') | null) || 'ALL';
  const pageParam = parseInt(searchParams.get('page') || '1', 10);

  const [searchTerm, setSearchTerm] = useState(searchParam);

  const queryStatuses = statusParam !== 'ALL' ? [statusParam] : undefined;

  const { data, isLoading, isError, error, refetch } = useShipmentsListQuery({
    page: pageParam,
    pageSize: 15,
    search: searchParam || undefined,
    statuses: queryStatuses,
    scope: 'Mine',
  });

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const newParams = new URLSearchParams(searchParams);
    if (searchTerm.trim()) {
      newParams.set('search', searchTerm.trim());
    } else {
      newParams.delete('search');
    }
    newParams.set('page', '1');
    setSearchParams(newParams);
  };

  const handleStatusChange = (status: ShipmentStatus | 'ALL') => {
    const newParams = new URLSearchParams(searchParams);
    if (status !== 'ALL') {
      newParams.set('status', status);
    } else {
      newParams.delete('status');
    }
    newParams.set('page', '1');
    setSearchParams(newParams);
  };

  const handlePageChange = (newPage: number) => {
    const newParams = new URLSearchParams(searchParams);
    newParams.set('page', newPage.toString());
    setSearchParams(newParams);
  };

  const shipments = data?.items || [];
  const totalCount = data?.totalCount || 0;
  const totalPages = data?.totalPages || 1;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Consignments &amp; Shipments"
        description="Comprehensive manifest list of your registered cargo movements."
        breadcrumbs={[{ label: 'Dashboard', href: '/app' }, { label: 'Shipments' }]}
        actions={
          <Button size="md" variant="primary" asChild leftIcon={<PlusCircle className="w-4 h-4" />}>
            <Link to="/app/shipments/new">Create Shipment</Link>
          </Button>
        }
      />

      {/* Search and Filters Bar */}
      <Card>
        <CardContent className="p-4 space-y-4">
          <form onSubmit={handleSearchSubmit} className="flex flex-col sm:flex-row gap-3">
            <Input
              placeholder="Search by reference, cargo description or address..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              leftIcon={<Search className="w-4 h-4" />}
              className="flex-1"
            />
            <Button type="submit" variant="secondary" size="md">
              Search
            </Button>
          </form>

          {/* Status Pills */}
          <div className="flex items-center gap-1.5 overflow-x-auto pb-1 text-xs">
            {STATUS_FILTERS.map((f) => (
              <button
                key={f.value}
                type="button"
                onClick={() => handleStatusChange(f.value)}
                className={`px-3 py-1.5 rounded-lg font-medium whitespace-nowrap transition-colors ${
                  statusParam === f.value
                    ? 'bg-brand text-white font-semibold shadow-xs'
                    : 'bg-neutral-hover text-neutral-secondary hover:text-neutral-primary hover:bg-neutral-border'
                }`}
              >
                {f.label}
              </button>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Main Table / Grid */}
      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="p-6 space-y-4">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-14 w-full" />
              <Skeleton className="h-14 w-full" />
              <Skeleton className="h-14 w-full" />
            </div>
          ) : isError ? (
            <div className="p-6">
              <ErrorState message={(error as any)?.message} onRetry={() => refetch()} />
            </div>
          ) : shipments.length === 0 ? (
            <div className="p-8">
              <EmptyState
                title="No consignments found"
                description={
                  searchParam || statusParam !== 'ALL'
                    ? 'No shipments match the selected filters. Try clearing your query.'
                    : 'You do not have any registered shipments yet.'
                }
                actionLabel={searchParam || statusParam !== 'ALL' ? 'Reset Filters' : 'Create Shipment'}
                onAction={() => {
                  if (searchParam || statusParam !== 'ALL') {
                    setSearchTerm('');
                    setSearchParams(new URLSearchParams());
                  } else {
                    window.location.assign('/app/shipments/new');
                  }
                }}
              />
            </div>
          ) : (
            <>
              {/* Desktop Table View */}
              <div className="hidden md:block">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Reference / ID</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Route (Pickup &rarr; Drop)</TableHead>
                      <TableHead>Cargo Specs</TableHead>
                      <TableHead>Agreed Rate</TableHead>
                      <TableHead>Created</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {shipments.map((s) => (
                      <TableRow key={s.id}>
                        <TableCell>
                          <Link to={`/app/shipments/${s.id}`} className="font-bold text-brand hover:underline block">
                            {s.reference || `SHP-${s.id.substring(0, 8).toUpperCase()}`}
                          </Link>
                          <span className="text-[10px] text-neutral-secondary font-mono">{s.id.substring(0, 16)}...</span>
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={s.status} />
                        </TableCell>
                        <TableCell className="max-w-[240px]">
                          <div className="text-xs truncate font-medium" title={s.pickupAddress}>
                            <span className="text-neutral-secondary">From:</span> {s.pickupAddress}
                          </div>
                          <div className="text-xs truncate text-neutral-secondary" title={s.deliveryAddress}>
                            <span className="font-medium">To:</span> {s.deliveryAddress}
                          </div>
                        </TableCell>
                        <TableCell className="text-xs tabular-nums">
                          <span className="font-semibold">{formatWeight(s.cargoWeightKg)}</span>
                          <span className="text-neutral-secondary block text-[11px]">{s.cargoVolumeM3} m³</span>
                        </TableCell>
                        <TableCell className="text-xs font-semibold tabular-nums">
                          {s.agreedAmount != null ? formatCurrency(s.agreedAmount, s.agreedCurrency || 'USD') : '—'}
                        </TableCell>
                        <TableCell className="text-xs text-neutral-secondary">
                          {formatDate(s.createdAtUtc)}
                        </TableCell>
                        <TableCell className="text-right">
                          <Button variant="outline" size="sm" asChild className="h-8 text-xs">
                            <Link to={`/app/shipments/${s.id}`}>Manage</Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>

              {/* Mobile Card List View */}
              <div className="md:hidden divide-y divide-neutral-border">
                {shipments.map((s) => (
                  <div key={s.id} className="p-4 space-y-3">
                    <div className="flex items-start justify-between gap-2">
                      <div>
                        <Link to={`/app/shipments/${s.id}`} className="font-bold text-sm text-brand hover:underline">
                          {s.reference || `SHP-${s.id.substring(0, 8).toUpperCase()}`}
                        </Link>
                        <span className="text-[10px] text-neutral-secondary font-mono block">
                          {formatDate(s.createdAtUtc)}
                        </span>
                      </div>
                      <StatusBadge status={s.status} />
                    </div>

                    <div className="text-xs space-y-1 text-neutral-secondary">
                      <p className="truncate"><strong className="text-neutral-primary">From:</strong> {s.pickupAddress}</p>
                      <p className="truncate"><strong className="text-neutral-primary">To:</strong> {s.deliveryAddress}</p>
                    </div>

                    <div className="flex items-center justify-between text-xs pt-1">
                      <span className="tabular-nums">
                        {formatWeight(s.cargoWeightKg)} &bull; {s.cargoVolumeM3} m³
                      </span>
                      <Button variant="outline" size="sm" asChild className="h-7 text-xs">
                        <Link to={`/app/shipments/${s.id}`}>Details</Link>
                      </Button>
                    </div>
                  </div>
                ))}
              </div>

              {/* Pagination */}
              <div className="p-4 border-t border-neutral-border">
                <Pagination
                  page={pageParam}
                  totalPages={totalPages}
                  totalCount={totalCount}
                  pageSize={15}
                  onPageChange={handlePageChange}
                />
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
};
