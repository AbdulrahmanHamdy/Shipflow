import React, { useState } from 'react';
import { Button } from '@/components/ui/Button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Input, Textarea } from '@/components/ui/Input';
import { FormField } from '@/components/ui/FormField';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/Card';
import { Modal } from '@/components/ui/Modal';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { useToast } from '@/components/ui/Toast';
import { ShipmentStatus } from '@/api/types';
import { ShipFlowLogo, ShipFlowSymbol } from '@/components/brand';
import { usePageTitle } from '@/hooks/usePageTitle';

export const DevComponentsPage: React.FC = () => {
  usePageTitle('Design System Components');
  const toast = useToast();
  const [modalOpen, setModalOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);

  const statuses: ShipmentStatus[] = [
    'Pending',
    'Quoting',
    'Accepted',
    'Assigned',
    'PickedUp',
    'InTransit',
    'DeliveryFailed',
    'Delivered',
    'Cancelled',
  ];

  return (
    <div className="max-w-5xl mx-auto py-8 space-y-10">
      <div>
        <h1 className="text-3xl font-black text-neutral-primary">ShipFlow Design System Showcase</h1>
        <p className="text-sm text-neutral-secondary">Enterprise UI primitives, design tokens, and state indicators</p>
      </div>

      {/* Brand & Logos */}
      <Card>
        <CardHeader>
          <CardTitle>Brand Assets &amp; Logo Variants</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-8 flex-wrap p-4 bg-white rounded-xl border border-neutral-border">
            <ShipFlowLogo size="sm" variant="dark" />
            <ShipFlowLogo size="md" variant="dark" />
            <ShipFlowLogo size="lg" variant="dark" />
            <ShipFlowLogo size="xl" variant="dark" />
          </div>

          <div className="flex items-center gap-8 flex-wrap p-4 bg-forest-night rounded-xl border border-forest-border">
            <ShipFlowLogo size="sm" variant="light" />
            <ShipFlowLogo size="md" variant="light" />
            <ShipFlowLogo size="lg" variant="light" />
            <ShipFlowLogo size="xl" variant="light" />
          </div>
        </CardContent>
      </Card>

      {/* Buttons */}
      <Card>
        <CardHeader>
          <CardTitle>Buttons &amp; Variants</CardTitle>
        </CardHeader>
        <CardContent className="flex items-center gap-3 flex-wrap">
          <Button variant="primary">Primary Action</Button>
          <Button variant="secondary">Secondary Mint</Button>
          <Button variant="outline">Outline</Button>
          <Button variant="ghost">Ghost</Button>
          <Button variant="danger">Destructive Danger</Button>
          <Button variant="forest">Forest Dark</Button>
          <Button variant="primary" isLoading>
            Loading State
          </Button>
          <Button variant="primary" disabled>
            Disabled
          </Button>
        </CardContent>
      </Card>

      {/* Status Badges */}
      <Card>
        <CardHeader>
          <CardTitle>Shipment Status State Machine Badges</CardTitle>
        </CardHeader>
        <CardContent className="flex items-center gap-2.5 flex-wrap">
          {statuses.map((s) => (
            <StatusBadge key={s} status={s} size="md" />
          ))}
        </CardContent>
      </Card>

      {/* Interactive Toasts & Dialogs */}
      <Card>
        <CardHeader>
          <CardTitle>Feedback &amp; Modals</CardTitle>
        </CardHeader>
        <CardContent className="flex items-center gap-3 flex-wrap">
          <Button size="sm" variant="outline" onClick={() => toast.success('Operation Successful', 'Database record was committed.')}>
            Trigger Success Toast
          </Button>
          <Button size="sm" variant="outline" onClick={() => toast.error('Validation Error', 'Field inputs are missing.')}>
            Trigger Error Toast
          </Button>
          <Button size="sm" variant="outline" onClick={() => setModalOpen(true)}>
            Open Radix Modal
          </Button>
          <Button size="sm" variant="danger" onClick={() => setConfirmOpen(true)}>
            Open Confirm Dialog
          </Button>
        </CardContent>
      </Card>

      <Modal isOpen={modalOpen} onClose={() => setModalOpen(false)} title="Accessible Radix Modal">
        <p className="text-xs text-neutral-secondary">This modal enforces WCAG focus trapping, escape dismissal, and accessible ARIA attributes.</p>
        <div className="mt-4 flex justify-end">
          <Button size="sm" variant="primary" onClick={() => setModalOpen(false)}>
            Close Modal
          </Button>
        </div>
      </Modal>

      <ConfirmDialog
        isOpen={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        onConfirm={() => {
          toast.info('Action Confirmed', 'The dangerous action was executed.');
          setConfirmOpen(false);
        }}
        title="Execute Destructive Action"
        message="Are you sure you want to proceed with this operation? This will modify the database state."
        variant="danger"
      />
    </div>
  );
};
