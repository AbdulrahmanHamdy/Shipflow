import React, { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useShipmentsListQuery, useSubmitQuoteMutation } from '@/features/shipments/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { ShipmentScope, ShipmentStatus, AppError } from '@/api/types';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Pagination } from '@/components/ui/Pagination';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/Table';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/ui/EmptyState';
import { ErrorState } from '@/components/ui/ErrorState';
import { Modal } from '@/components/ui/Modal';
import { FormField } from '@/components/ui/FormField';
import { Input, Textarea } from '@/components/ui/Input';
import { useToast } from '@/components/ui/Toast';
import { formatDate, formatWeight, formatCurrency } from '@/lib/formatters';
import { Send, Truck, ArrowRight, Clock, CheckCircle2, Navigation } from 'lucide-react';

export const CarrierShipmentsPage: React.FC = () => {
  usePageTitle('Carrier Shipment Board');
  const [searchParams, setSearchParams] = useSearchParams();
  const toast = useToast();

  const scopeParam = (searchParams.get('scope') as ShipmentScope) || 'Mine';
  const pageParam = parseInt(searchParams.get('page') || '1', 10);

  const { data, isLoading, isError, error, refetch } = useShipmentsListQuery({
    scope: scopeParam,
    page: pageParam,
    pageSize: 15,
  });

  const submitQuoteMutation = useSubmitQuoteMutation();

  // Quote Submission Modal
  const [selectedShipmentId, setSelectedShipmentId] = useState<string | null>(null);
  const [quoteAmount, setQuoteAmount] = useState('1250.00');
  const [quoteCurrency, setQuoteCurrency] = useState('USD');
  const [quoteTerms, setQuoteTerms] = useState('Net 15 days upon proof of delivery');

  // Tomorrow UTC expiry
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 2);
  const defaultExpiry = tomorrow.toISOString().slice(0, 16);
  const [quoteExpiry, setQuoteExpiry] = useState(defaultExpiry);

  const handleScopeToggle = (newScope: ShipmentScope) => {
    const newParams = new URLSearchParams(searchParams);
    newParams.set('scope', newScope);
    newParams.set('page', '1');
    setSearchParams(newParams);
  };

  const handlePageChange = (newPage: number) => {
    const newParams = new URLSearchParams(searchParams);
    newParams.set('page', newPage.toString());
    setSearchParams(newParams);
  };

  const handleOpenQuoteModal = (shipmentId: string) => {
    setSelectedShipmentId(shipmentId);
  };

  const handleSubmitQuote = async () => {
    if (!selectedShipmentId) return;
    const amount = parseFloat(quoteAmount);
    if (isNaN(amount) || amount <= 0) {
      toast.error('Invalid amount', 'Please enter a positive numeric quote amount.');
      return;
    }

    try {
      await submitQuoteMutation.mutateAsync({
        shipmentId: selectedShipmentId,
        command: {
          amount,
          currency: quoteCurrency.trim().toUpperCase(),
          expiresAtUtc: new Date(quoteExpiry).toISOString(),
          terms: quoteTerms.trim(),
        },
      });

      toast.success('Bid Submitted', 'Your binding freight quotation has been recorded.');
      setSelectedShipmentId(null);
      refetch();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Quote submission rejected', appErr.message);
    }
  };

  const shipments = data?.items || [];
  const totalCount = data?.totalCount || 0;
  const totalPages = data?.totalPages || 1;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Freight &amp; Shipment Board"
        description="Browse awarded company consignments or place bids on open exchange freight."
        breadcrumbs={[{ label: 'Control Tower', href: '/carrier' }, { label: 'Shipment Board' }]}
      />

      {/* Scope Selector Tabs */}
      <div className="flex items-center gap-2 border-b border-neutral-border pb-3">
        <Button
          variant={scopeParam === 'Mine' ? 'primary' : 'outline'}
          size="sm"
          onClick={() => handleScopeToggle('Mine')}
        >
          Carried by My Company
        </Button>
        <Button
          variant={scopeParam === 'OpenForQuotes' ? 'primary' : 'outline'}
          size="sm"
          onClick={() => handleScopeToggle('OpenForQuotes')}
          leftIcon={<Clock className="w-3.5 h-3.5" />}
        >
          Open Freight Bidding Exchange
        </Button>
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="p-6 space-y-4">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
            </div>
          ) : isError ? (
            <div className="p-6">
              <ErrorState message={(error as any)?.message} onRetry={() => refetch()} />
            </div>
          ) : shipments.length === 0 ? (
            <div className="p-8">
              <EmptyState
                title={scopeParam === 'Mine' ? 'No company shipments found' : 'No consignments currently open for quotes'}
                description={
                  scopeParam === 'Mine'
                    ? 'Your company has not yet been awarded shipments. Place bids on the freight exchange to secure jobs.'
                    : 'All open consignments have closed or been accepted. Please check back shortly.'
                }
                actionLabel={scopeParam === 'Mine' ? 'Browse Freight Exchange' : undefined}
                onAction={() => handleScopeToggle('OpenForQuotes')}
              />
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Reference / ID</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Route (Pickup &rarr; Destination)</TableHead>
                    <TableHead>Payload Specs</TableHead>
                    <TableHead>Agreed Rate</TableHead>
                    <TableHead>Date</TableHead>
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
                        <span className="text-[10px] text-neutral-secondary font-mono">{s.id.substring(0, 14)}...</span>
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={s.status} />
                      </TableCell>
                      <TableCell className="max-w-[240px]">
                        <div className="text-xs truncate font-medium">{s.pickupAddress}</div>
                        <div className="text-xs truncate text-neutral-secondary">&rarr; {s.deliveryAddress}</div>
                      </TableCell>
                      <TableCell className="text-xs tabular-nums">
                        <span className="font-semibold">{formatWeight(s.cargoWeightKg)}</span>
                        <span className="text-[10px] text-neutral-secondary block">{s.cargoVolumeM3} m³</span>
                      </TableCell>
                      <TableCell className="text-xs font-semibold tabular-nums">
                        {s.agreedAmount != null ? formatCurrency(s.agreedAmount, s.agreedCurrency || 'USD') : '—'}
                      </TableCell>
                      <TableCell className="text-xs text-neutral-secondary">
                        {formatDate(s.createdAtUtc)}
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          {s.status === 'Quoting' && (
                            <Button
                              variant="primary"
                              size="sm"
                              className="h-7 text-xs"
                              onClick={() => handleOpenQuoteModal(s.id)}
                              leftIcon={<Send className="w-3 h-3" />}
                            >
                              Bid
                            </Button>
                          )}

                          {s.status === 'Accepted' && (
                            <Button
                              variant="secondary"
                              size="sm"
                              asChild
                              className="h-7 text-xs"
                              leftIcon={<Truck className="w-3 h-3" />}
                            >
                              <Link to={`/carrier/dispatch/${s.id}`}>Dispatch</Link>
                            </Button>
                          )}

                          {s.status === 'Assigned' && (
                            <Button
                              variant="outline"
                              size="sm"
                              asChild
                              className="h-7 text-xs"
                              leftIcon={<Truck className="w-3 h-3" />}
                            >
                              <Link to={`/carrier/dispatch/${s.id}`}>Reassign</Link>
                            </Button>
                          )}

                          <Button variant="ghost" size="sm" asChild className="h-7 text-xs">
                            <Link to={`/app/shipments/${s.id}`}>Details</Link>
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

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

      {/* Submit Quote Modal */}
      <Modal
        isOpen={!!selectedShipmentId}
        onClose={() => setSelectedShipmentId(null)}
        title="Submit Binding Freight Quote"
        description="State your pricing and terms. The customer will review your bid alongside competing carriers."
      >
        <div className="space-y-4">
          <div className="grid grid-cols-3 gap-3">
            <div className="col-span-2">
              <FormField label="Quote Amount" required htmlFor="amount">
                <Input
                  id="amount"
                  type="number"
                  step="0.01"
                  min="0.01"
                  value={quoteAmount}
                  onChange={(e) => setQuoteAmount(e.target.value)}
                />
              </FormField>
            </div>
            <div>
              <FormField label="Currency" required htmlFor="curr">
                <Input
                  id="curr"
                  maxLength={3}
                  value={quoteCurrency}
                  onChange={(e) => setQuoteCurrency(e.target.value.toUpperCase())}
                />
              </FormField>
            </div>
          </div>

          <FormField label="Quote Expiration Time (UTC)" required htmlFor="expiry">
            <Input
              id="expiry"
              type="datetime-local"
              value={quoteExpiry}
              onChange={(e) => setQuoteExpiry(e.target.value)}
            />
          </FormField>

          <FormField label="Carrier Terms &amp; Conditions" required htmlFor="terms">
            <Textarea
              id="terms"
              rows={2}
              placeholder="e.g. Standard freight terms, payment net 15 days upon verified POD..."
              value={quoteTerms}
              onChange={(e) => setQuoteTerms(e.target.value)}
            />
          </FormField>

          <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-neutral-border">
            <Button variant="outline" size="sm" onClick={() => setSelectedShipmentId(null)}>
              Cancel
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={handleSubmitQuote}
              isLoading={submitQuoteMutation.isPending}
              leftIcon={<Send className="w-3.5 h-3.5" />}
            >
              Submit Binding Quote
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};
