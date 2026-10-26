import React from 'react';
import { useParams, Link } from 'react-router-dom';
import { useShipmentDetailQuery } from '@/features/shipments/hooks';
import { useDeliveryProofQuery } from '@/features/tracking/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Skeleton } from '@/components/ui/Skeleton';
import { ErrorState } from '@/components/ui/ErrorState';
import { EmptyState } from '@/components/ui/EmptyState';
import { formatDateTime } from '@/lib/formatters';
import { FileCheck2, UserCheck, Clock, FileText, CheckCircle2, ArrowLeft, ShieldCheck } from 'lucide-react';

export const DeliveryProofPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  usePageTitle('Proof of Delivery');

  const { data: shipment, isLoading: isShipmentLoading } = useShipmentDetailQuery(id);
  const { data: proof, isLoading: isProofLoading, isError, error, refetch } = useDeliveryProofQuery(id);

  if (isShipmentLoading || isProofLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-96 w-full max-w-2xl mx-auto rounded-3xl" />
      </div>
    );
  }

  if (isError || !proof) {
    return (
      <div className="py-12">
        <EmptyState
          icon={FileCheck2}
          title="Delivery Proof Not Yet Filed"
          description="Proof of delivery is atomically captured when the assigned driver executes the delivery command."
          actionLabel="Return to Shipment"
          onAction={() => window.location.assign(`/app/shipments/${id}`)}
        />
      </div>
    );
  }

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <PageHeader
        title="Proof of Delivery Certificate"
        description="Official atomic verification record stored in ShipFlow audit persistence."
        breadcrumbs={[
          { label: 'Dashboard', href: '/app' },
          { label: 'Shipments', href: '/app/shipments' },
          { label: shipment?.reference || id?.substring(0, 8) || '', href: `/app/shipments/${id}` },
          { label: 'Delivery Proof' },
        ]}
        actions={
          <Button variant="outline" size="sm" asChild leftIcon={<ArrowLeft className="w-3.5 h-3.5" />}>
            <Link to={`/app/shipments/${id}`}>Back</Link>
          </Button>
        }
      />

      <Card className="border-emerald-200 bg-white shadow-panel overflow-hidden">
        {/* Certificate Header Banner */}
        <div className="bg-forest-night text-white p-6 flex items-center justify-between border-b border-forest-border">
          <div className="space-y-1">
            <span className="text-[10px] font-mono uppercase tracking-widest text-emerald-400">
              OFFICIAL VERIFICATION MANIFEST
            </span>
            <h2 className="text-xl font-black tracking-tight">Cargo Successfully Delivered</h2>
          </div>
          <div className="w-12 h-12 rounded-2xl bg-emerald-500/20 text-emerald-400 flex items-center justify-center border border-emerald-400/30">
            <CheckCircle2 className="w-7 h-7" />
          </div>
        </div>

        <CardContent className="p-6 space-y-6 text-xs">
          <div className="grid grid-cols-2 gap-4">
            <div className="p-3.5 rounded-xl bg-neutral-appBg border border-neutral-border">
              <span className="text-neutral-secondary block text-[11px] mb-1 flex items-center gap-1">
                <UserCheck className="w-3.5 h-3.5 text-brand" /> Recipient Full Name
              </span>
              <span className="text-sm font-bold text-neutral-primary">{proof.recipientName}</span>
            </div>

            <div className="p-3.5 rounded-xl bg-neutral-appBg border border-neutral-border">
              <span className="text-neutral-secondary block text-[11px] mb-1 flex items-center gap-1">
                <Clock className="w-3.5 h-3.5 text-brand" /> Delivered At (UTC)
              </span>
              <span className="text-sm font-bold text-neutral-primary tabular-nums">
                {formatDateTime(proof.deliveredAtUtc)}
              </span>
            </div>
          </div>

          {proof.notes && (
            <div className="p-4 rounded-xl bg-neutral-appBg border border-neutral-border space-y-1">
              <span className="font-semibold text-neutral-primary block flex items-center gap-1.5">
                <FileText className="w-3.5 h-3.5 text-brand" /> Driver Delivery Notes
              </span>
              <p className="text-neutral-secondary leading-relaxed">{proof.notes}</p>
            </div>
          )}

          <div className="space-y-3 pt-2">
            <h4 className="font-bold text-neutral-primary text-xs uppercase tracking-wider">
              Verification References
            </h4>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div className="p-3 rounded-xl border border-neutral-border bg-white">
                <span className="text-neutral-secondary text-[11px] block">Digital Signature Reference</span>
                <span className="font-mono text-xs font-semibold text-neutral-primary">
                  {proof.signatureReference || 'Signed on physical receipt manifest'}
                </span>
              </div>

              <div className="p-3 rounded-xl border border-neutral-border bg-white">
                <span className="text-neutral-secondary text-[11px] block">Photographic Inspection Hash</span>
                <span className="font-mono text-xs font-semibold text-neutral-primary">
                  {proof.photoReference || 'Verified in person without damage'}
                </span>
              </div>
            </div>
          </div>

          <div className="p-3.5 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-950 flex items-center gap-2.5">
            <ShieldCheck className="w-5 h-5 text-emerald-700 flex-shrink-0" />
            <p className="text-[11px] leading-relaxed">
              This proof of delivery was sealed atomically with database transaction integrity. The shipment state is permanently closed as <strong>Delivered</strong>.
            </p>
          </div>
        </CardContent>

        <CardFooter className="bg-neutral-appBg/60 flex items-center justify-between">
          <span className="text-[11px] text-neutral-secondary font-mono">Proof ID: {proof.id}</span>
          <Button size="sm" variant="outline" onClick={() => window.print()}>
            Print Manifest
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
};
