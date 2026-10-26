import React from 'react';
import { usePageTitle } from '@/hooks/usePageTitle';
import { AlertTriangle, ShieldCheck } from 'lucide-react';

export const PrivacyPage: React.FC = () => {
  usePageTitle('Privacy Policy (Draft)');

  return (
    <div className="max-w-4xl mx-auto px-4 py-16 sm:px-6 lg:px-8 space-y-8">
      {/* Notice Banner */}
      <div className="p-4 rounded-2xl bg-amber-50 border border-amber-200 text-amber-900 flex items-start gap-3">
        <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
        <div className="text-xs leading-relaxed">
          <span className="font-bold uppercase tracking-wider block mb-0.5">Draft Document — Requires Legal Review</span>
          This document is a technical operational specification of ShipFlow's data handling practices and does not constitute formal legal counsel.
        </div>
      </div>

      <div className="space-y-3">
        <div className="flex items-center gap-2 text-xs font-bold text-brand uppercase tracking-wider">
          <ShieldCheck className="w-4 h-4" /> ShipFlow Platform Security
        </div>
        <h1 className="text-3xl sm:text-4xl font-black text-neutral-primary tracking-tight">
          Privacy &amp; Data Protection Policy
        </h1>
        <p className="text-xs text-neutral-secondary">Last updated: October 2026</p>
      </div>

      <div className="bg-white rounded-3xl p-8 border border-neutral-border shadow-panel space-y-8 text-xs text-neutral-secondary leading-relaxed">
        <section className="space-y-3">
          <h2 className="text-base font-bold text-neutral-primary">1. Information We Collect</h2>
          <p>
            ShipFlow collects identity, business contact, and operational freight data solely for executing logistics transactions:
          </p>
          <ul className="list-disc pl-5 space-y-1 text-neutral-secondary">
            <li><strong>User Account Details:</strong> Name, professional email address, and hashed credentials.</li>
            <li><strong>Shipment Consignment Data:</strong> Cargo descriptions, weight (kg), volume (m³), pickup and delivery addresses, and delivery time windows.</li>
            <li><strong>GPS &amp; Telemetry Data:</strong> Latitude, longitude, timestamp (UTC), and speed reported by assigned drivers during active transit.</li>
            <li><strong>Proof of Delivery:</strong> Recipient full name, delivery timestamp (UTC), optional operational notes, and digital signature/photo reference strings.</li>
          </ul>
        </section>

        <section className="space-y-3">
          <h2 className="text-base font-bold text-neutral-primary">2. Role-Based Access and Tenant Segregation</h2>
          <p>
            The backend engine enforces strict multi-tenant segregation. Shippers can only view their own consignments and quotations. Carriers and dispatchers can view only bids and fleet resources under their active company membership. Driver location updates are visible exclusively to authorized viewers of the active consignment.
          </p>
        </section>

        <section className="space-y-3">
          <h2 className="text-base font-bold text-neutral-primary">3. Payment Information Processing</h2>
          <p>
            ShipFlow integrates with Paymob for electronic transactions. Sensitive credit card numbers, CVVs, and banking secrets are entered directly through Paymob's PCI-DSS compliant checkout frame and are never stored on or transmitted through ShipFlow servers.
          </p>
        </section>

        <section className="space-y-3">
          <h2 className="text-base font-bold text-neutral-primary">4. Data Retention and Integrity</h2>
          <p>
            Consignment manifests, pricing agreements, and delivery proofs are maintained in database audit stores to ensure regulatory compliance and verifiable chain-of-custody.
          </p>
        </section>
      </div>
    </div>
  );
};
