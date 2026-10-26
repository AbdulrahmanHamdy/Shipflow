import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useDriverAssignmentHistoryQuery } from '@/features/tracking/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Pagination } from '@/components/ui/Pagination';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/ui/EmptyState';
import { ErrorState } from '@/components/ui/ErrorState';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/Table';
import { formatDateTime } from '@/lib/formatters';
import { CalendarDays, Truck, ExternalLink, ArrowRight } from 'lucide-react';

export const DriverAssignmentsPage: React.FC = () => {
  usePageTitle('Trip & Assignment History');
  const [page, setPage] = useState(1);
  const pageSize = 15;

  const { data, isLoading, isError, error, refetch } = useDriverAssignmentHistoryQuery(page, pageSize);

  const assignments = data?.items || [];
  const totalCount = data?.totalCount || 0;
  const totalPages = data?.totalPages || 1;

  return (
    <div className="space-y-6 max-w-5xl mx-auto">
      <PageHeader
        title="Trip &amp; Assignment History"
        description="Historical log of all transport missions assigned to your driver profile."
        breadcrumbs={[{ label: 'Cockpit', href: '/driver' }, { label: 'Trip History' }]}
        actions={
          <Button variant="primary" size="sm" asChild rightIcon={<ArrowRight className="w-4 h-4" />}>
            <Link to="/driver/current-job">Active Cockpit</Link>
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
          ) : assignments.length === 0 ? (
            <div className="p-8">
              <EmptyState
                icon={CalendarDays}
                title="No Trip Records Found"
                description="You do not have any past completed or released assignments on file yet."
              />
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Shipment ID</TableHead>
                    <TableHead>Assignment Status</TableHead>
                    <TableHead>Shipment Stage</TableHead>
                    <TableHead>Scheduled Window (UTC)</TableHead>
                    <TableHead>Assigned Date</TableHead>
                    <TableHead>Ended Date</TableHead>
                    <TableHead className="text-right">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {assignments.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell className="font-mono text-xs font-semibold text-neutral-primary">
                        SHP-{a.shipmentId.substring(0, 8).toUpperCase()}
                      </TableCell>
                      <TableCell>
                        <span
                          className={`text-xs font-semibold px-2.5 py-0.5 rounded-full ${
                            a.status === 'Active'
                              ? 'bg-emerald-50 text-emerald-800 border border-emerald-300'
                              : 'bg-neutral-hover text-neutral-secondary'
                          }`}
                        >
                          {a.status}
                        </span>
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={a.shipmentStatus} />
                      </TableCell>
                      <TableCell className="text-xs text-neutral-secondary">
                        {formatDateTime(a.windowStartUtc)} &rarr; {formatDateTime(a.windowEndUtc)}
                      </TableCell>
                      <TableCell className="text-xs text-neutral-secondary">
                        {formatDateTime(a.createdAtUtc)}
                      </TableCell>
                      <TableCell className="text-xs text-neutral-secondary">
                        {a.endedAtUtc ? formatDateTime(a.endedAtUtc) : '—'}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button variant="outline" size="sm" asChild className="h-7 text-xs">
                          <Link to={`/app/shipments/${a.shipmentId}`}>Inspect</Link>
                        </Button>
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
    </div>
  );
};
