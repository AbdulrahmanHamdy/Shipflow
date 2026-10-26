import React from 'react';
import { usePageTitle } from '@/hooks/usePageTitle';
import { AlertTriangle, FileText } from 'lucide-react';

export const TermsPage: React.FC = () => {
  usePageTitle('Terms of Service (Draft)');

  return (
    <div className="max-w-4xl mx-auto px-4 py-16 sm:px-6 lg:px-8 space-y-8">
      {/* Notice Banner */}
      <div className="p-4 rounded-2xl bg-amber-50 border border-amber-200 text-amber-900 flex items-start gap-3">
        <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
        <div className="text-xs leading-relaxed">
          <span className="font-bold uppercase tracking-wider block mb-0.5">Draft Document — Requires Legal Review</span>
          These operational terms describe the contractual state transitions of the ShipFlow platform. Subject to official legal finalization.
        </div>
      </div>

      <div className="space-y-3">
        <div className="flex items-center gap-2 text-xs font-bold text-brand uppercase tracking-wider">
          <FileText className="w-4 h-4" /> Operational Agreement
        </div>
        <h1 className="text-3xl sm:text-4xl font-black text-neutral-primary tracking-tight">
          Terms of Service
        </h1>
        <p className="text-xs text-neutral-secondary">Last updated: October 2026</p>
      </div>

      <div className="bg-white rounded-3xl p-8 border border-neutral-border shadow-panel space-y-8 text-xs text-neutral-secondary leading-relaxed">
        <section className="space-y-3">
          <h2 className="text-base font-bold text-neutral-primary">1. Acceptance of Terms</h2>
          <p>
            By accessing or using the ShipFlow Smart Logistics platform, you agree to comply with and be bound by these Terms of Service.
          </p>
        </section>

        <section className="space-y-3">
          <h2 className="text-base font-bold text-neutral-primary">2. Quotation and Contract Formation</h2>
          <p>
            Carriers submit binding price quotes for shipments in the Quoting state. Upon acceptance by the customer, a binding freight agreement is established, establishing the agreed carrier, rate, and currency. Competing quotes are automatically expired.
          </p>
        </section>

        <section className="space-y-3">
          <h2 className="text-base font-bold text-neutral-primary">3. Dispatch and Capacity Obligations</h2>
          <p>
            Carriers must ensure that assigned vehicles satisfy the verified cargo weight and volume specifications. Vehicle capacities may not be downsized while an active shipment assignment is underway.
          </p>
        </section>

        <section className="space-y-3">
          <h2 className="text-base font-bold text-neutral-primary">4. Driver Execution &amp; Proof of Delivery</h2>
          <p>
            Drivers must execute delivery state transitions sequentially (Assigned &rarr; PickedUp &rarr; InTransit &rarr; Delivered). Submission of valid delivery proof constitutes conclusive verification of cargo transfer.
          </p>
        </section>
      </div>
    </div>
  );
};
