import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '@/features/auth/AuthContext';
import { usePageTitle } from '@/hooks/usePageTitle';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { ShieldCheck, Search, Navigation, Lock, ArrowRight, Info } from 'lucide-react';

export const PublicTrackingPage: React.FC = () => {
  usePageTitle('Shipment Tracking Portal');
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const [reference, setReference] = useState('');

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (!isAuthenticated) {
      navigate('/login?returnUrl=' + encodeURIComponent('/app/shipments'));
      return;
    }
    // If authenticated, navigate to shipments search
    navigate(`/app/shipments?search=${encodeURIComponent(reference.trim())}`);
  };

  return (
    <div className="max-w-4xl mx-auto px-4 py-16 sm:px-6 lg:px-8 space-y-12">
      <div className="text-center space-y-3">
        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-brand-mint text-brand-mintText text-xs font-semibold border border-emerald-300">
          <Navigation className="w-3.5 h-3.5 text-brand" />
          <span>Cargo Visibility Engine</span>
        </div>
        <h1 className="text-3xl sm:text-4xl font-black text-neutral-primary tracking-tight">
          Track Your Freight Consignment
        </h1>
        <p className="text-sm text-neutral-secondary max-w-xl mx-auto">
          Monitor your shipment's journey, driver location updates, and delivery verification milestones in real time.
        </p>
      </div>

      {/* Lookup Card */}
      <div className="bg-white rounded-3xl p-6 sm:p-8 border border-neutral-border shadow-panel space-y-6">
        <form onSubmit={handleSearch} className="flex flex-col sm:flex-row gap-3">
          <Input
            placeholder="Enter tracking reference (e.g. SHP-84920 or Shipment ID)"
            value={reference}
            onChange={(e) => setReference(e.target.value)}
            leftIcon={<Search className="w-4 h-4" />}
            className="h-12 text-base"
          />
          <Button type="submit" size="lg" variant="primary" className="sm:w-44 h-12 flex-shrink-0" rightIcon={<ArrowRight className="w-4 h-4" />}>
            Track Freight
          </Button>
        </form>

        {/* Security & Access Notice */}
        <div className="p-4 rounded-2xl bg-neutral-appBg border border-neutral-border flex items-start gap-3">
          <Lock className="w-5 h-5 text-brand flex-shrink-0 mt-0.5" />
          <div className="space-y-1 text-xs text-neutral-secondary">
            <span className="font-bold text-neutral-primary text-xs block">
              Authorized Stakeholder Access Only
            </span>
            <p className="leading-relaxed">
              In accordance with enterprise freight security and cargo privacy protocols, real-time GPS telemetry and delivery records are restricted to authenticated shipment owners, transport dispatchers, and assigned drivers.
            </p>
            {!isAuthenticated && (
              <p className="pt-1 font-semibold text-brand">
                Please <Link to="/login" className="underline hover:text-brand-hover">sign in to your account</Link> to view detailed location trails and proof of delivery.
              </p>
            )}
          </div>
        </div>
      </div>

      {/* Feature Highlights */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 pt-4 text-xs">
        <div className="bg-white p-5 rounded-2xl border border-neutral-border shadow-card space-y-2">
          <span className="font-bold text-neutral-primary text-sm flex items-center gap-1.5">
            <ShieldCheck className="w-4 h-4 text-brand" /> Tenant Protection
          </span>
          <p className="text-neutral-secondary leading-relaxed">
            Consignment details are securely sequestered. Competitors and unauthorized viewers receive a neutral 404 response to eliminate cargo probing.
          </p>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-neutral-border shadow-card space-y-2">
          <span className="font-bold text-neutral-primary text-sm flex items-center gap-1.5">
            <Navigation className="w-4 h-4 text-brand" /> Real Recorded GPS
          </span>
          <p className="text-neutral-secondary leading-relaxed">
            Route paths are rendered exclusively from authenticated driver GPS telemetry points, never synthetic or simulated roadway approximations.
          </p>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-neutral-border shadow-card space-y-2">
          <span className="font-bold text-neutral-primary text-sm flex items-center gap-1.5">
            <Info className="w-4 h-4 text-brand" /> Atomic Proof of Delivery
          </span>
          <p className="text-neutral-secondary leading-relaxed">
            Upon completion, the assigned driver atomically seals delivery with recipient confirmation, UTC timestamp, and digital signature references.
          </p>
        </div>
      </div>
    </div>
  );
};
