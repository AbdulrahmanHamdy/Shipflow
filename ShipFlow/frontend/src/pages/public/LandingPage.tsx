import React from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/Button';
import { usePageTitle } from '@/hooks/usePageTitle';
import { useAuth } from '@/features/auth/AuthContext';
import {
  ShieldCheck,
  Compass,
  ArrowRight,
  Package,
  Truck,
  Users,
  CheckCircle2,
  FileCheck2,
  Cpu,
  Navigation,
  Clock,
  Layers,
  MapPin,
} from 'lucide-react';

export const LandingPage: React.FC = () => {
  usePageTitle('Smart Logistics & Shipment Management');
  const { isAuthenticated, user } = useAuth();

  const getDashboardLink = () => {
    if (user?.roles.includes('CompanyAdmin') || user?.roles.includes('Dispatcher')) return '/carrier';
    if (user?.roles.includes('Driver')) return '/driver';
    return '/app';
  };

  return (
    <div className="space-y-24 pb-20">
      {/* ─── HERO SECTION ────────────────────────────────────────────────────── */}
      <section className="relative pt-12 md:pt-20 pb-16 overflow-hidden">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
            {/* Left Column: Headline & Action */}
            <div className="lg:col-span-7 space-y-6">
              <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-brand-mint text-brand-mintText text-xs font-semibold tracking-wide border border-emerald-300">
                <span className="w-2 h-2 rounded-full bg-brand animate-pulse" />
                <span>Unified Logistics Control Tower</span>
              </div>

              <h1 className="text-4xl sm:text-5xl lg:text-6xl font-black text-neutral-primary tracking-tight leading-[1.1]">
                Every Shipment.{' '}
                <span className="text-transparent bg-clip-text bg-gradient-to-r from-brand to-brand-accent">
                  One Clear Journey.
                </span>
              </h1>

              <p className="text-base sm:text-lg text-neutral-secondary max-w-2xl leading-relaxed">
                Manage shipments, coordinate drivers, monitor deliveries, and keep every movement visible from dispatch to destination.
              </p>

              <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3 pt-2">
                {isAuthenticated ? (
                  <Button size="lg" variant="primary" asChild leftIcon={<ArrowRight className="w-5 h-5" />}>
                    <Link to={getDashboardLink()}>Enter Control Console</Link>
                  </Button>
                ) : (
                  <>
                    <Button size="lg" variant="primary" asChild rightIcon={<ArrowRight className="w-5 h-5" />}>
                      <Link to="/register">Get Started</Link>
                    </Button>
                    <Button size="lg" variant="outline" asChild leftIcon={<Navigation className="w-5 h-5" />}>
                      <Link to="/tracking">Track a Shipment</Link>
                    </Button>
                  </>
                )}
              </div>

              <div className="pt-4 flex items-center gap-6 text-xs text-neutral-secondary">
                <div className="flex items-center gap-1.5">
                  <CheckCircle2 className="w-4 h-4 text-brand" />
                  <span>Role-Based Multi-Tenant</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <CheckCircle2 className="w-4 h-4 text-brand" />
                  <span>GPS Tracking Integration</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <CheckCircle2 className="w-4 h-4 text-brand" />
                  <span>Secure Paymob Checkout</span>
                </div>
              </div>
            </div>

            {/* Right Column: Custom Authentic Platform Illustration */}
            <div className="lg:col-span-5 relative">
              <div className="relative rounded-3xl bg-forest-night border border-forest-border p-6 shadow-2xl text-white">
                {/* Visual Header */}
                <div className="flex items-center justify-between pb-4 border-b border-forest-border/80">
                  <div className="flex items-center gap-2">
                    <span className="w-3 h-3 rounded-full bg-red-500/80" />
                    <span className="w-3 h-3 rounded-full bg-amber-500/80" />
                    <span className="w-3 h-3 rounded-full bg-emerald-500/80" />
                  </div>
                  <span className="text-[11px] font-mono text-emerald-400 bg-forest-deep px-2.5 py-0.5 rounded-full border border-forest-border">
                    LIVE DISPATCH RADAR
                  </span>
                </div>

                {/* Simulated Real Map Polyline Vector */}
                <div className="py-6 space-y-4">
                  <div className="relative h-44 rounded-2xl bg-forest-deep/60 border border-forest-border overflow-hidden p-3 flex flex-col justify-between">
                    <div className="flex justify-between items-start text-[11px]">
                      <div className="bg-forest-night/90 px-2 py-1 rounded-md border border-forest-border flex items-center gap-1.5">
                        <MapPin className="w-3 h-3 text-brand-accent" />
                        <span>Port Said Hub &rarr; Cairo Cargo Center</span>
                      </div>
                      <span className="text-emerald-400 font-mono font-bold">142 km / 2h 15m</span>
                    </div>

                    <svg className="w-full h-24" viewBox="0 0 300 100" fill="none">
                      <path
                        d="M 30 70 Q 110 20, 180 50 T 270 30"
                        stroke="#10B981"
                        strokeWidth="3.5"
                        strokeDasharray="4 4"
                      />
                      <circle cx="30" cy="70" r="5" fill="#15803D" stroke="#FFFFFF" strokeWidth="1.5" />
                      <circle cx="180" cy="50" r="4" fill="#10B981" stroke="#FFFFFF" strokeWidth="1.5" />
                      <circle cx="270" cy="30" r="6" fill="#0284C7" stroke="#FFFFFF" strokeWidth="2" />
                    </svg>

                    <div className="flex items-center justify-between text-[10px] text-neutral-300">
                      <span>Status: In Transit</span>
                      <span className="text-emerald-400 flex items-center gap-1 font-mono">
                        <Clock className="w-3 h-3" /> GPS ping: 3 mins ago
                      </span>
                    </div>
                  </div>

                  {/* Active Cargo Info Pill */}
                  <div className="bg-forest-deep p-4 rounded-xl border border-forest-border space-y-2">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-semibold text-white">Cargo Manifest #SHP-84920</span>
                      <span className="text-[11px] font-bold text-brand-accent bg-brand/20 px-2 py-0.5 rounded">
                        Driver Assigned
                      </span>
                    </div>
                    <div className="grid grid-cols-3 gap-2 text-[11px] text-neutral-300 pt-1">
                      <div>
                        <span className="text-neutral-400 block text-[9px] uppercase">Payload</span>
                        <span className="font-semibold text-white">4,200 kg</span>
                      </div>
                      <div>
                        <span className="text-neutral-400 block text-[9px] uppercase">Volume</span>
                        <span className="font-semibold text-white">28.5 m³</span>
                      </div>
                      <div>
                        <span className="text-neutral-400 block text-[9px] uppercase">Agreed Rate</span>
                        <span className="font-semibold text-white">$1,850.00</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ─── CAPABILITIES SECTION ────────────────────────────────────────────── */}
      <section id="capabilities" className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center max-w-3xl mx-auto space-y-3 mb-16">
          <span className="text-xs font-bold uppercase tracking-wider text-brand">Core Platform Architecture</span>
          <h2 className="text-3xl sm:text-4xl font-black text-neutral-primary tracking-tight">
            Built for Real-World Freight Operations
          </h2>
          <p className="text-sm text-neutral-secondary">
            ShipFlow connects cargo owners, transport carriers, and drivers through strict state machine orchestration and real-time data integrity.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          <div className="bg-white p-7 rounded-2xl border border-neutral-border shadow-card space-y-3">
            <div className="w-12 h-12 rounded-xl bg-brand-mint text-brand-mintText flex items-center justify-center font-bold">
              <Package className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-neutral-primary">Shipment Lifecycle</h3>
            <p className="text-xs text-neutral-secondary leading-relaxed">
              Create multi-stop consignments with strict UTC pickup windows. Open shipments for carrier bids and lock in agreed quotes securely.
            </p>
          </div>

          <div className="bg-white p-7 rounded-2xl border border-neutral-border shadow-card space-y-3">
            <div className="w-12 h-12 rounded-xl bg-sky-50 text-sky-700 flex items-center justify-center font-bold border border-sky-200">
              <Truck className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-neutral-primary">Fleet &amp; Dispatch Tower</h3>
            <p className="text-xs text-neutral-secondary leading-relaxed">
              Transport companies manage fleets and drivers. Filter available vehicles by exact cargo weight and volume constraints without overlapping windows.
            </p>
          </div>

          <div className="bg-white p-7 rounded-2xl border border-neutral-border shadow-card space-y-3">
            <div className="w-12 h-12 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center font-bold border border-purple-200">
              <FileCheck2 className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-neutral-primary">Verifiable Proof of Delivery</h3>
            <p className="text-xs text-neutral-secondary leading-relaxed">
              Drivers report pickup, transit, and atomic delivery proof capturing recipient identity, timestamps, and signature references directly to database storage.
            </p>
          </div>
        </div>
      </section>

      {/* ─── ROLE-BASED SOLUTIONS SECTION ────────────────────────────────────── */}
      <section id="solutions" className="bg-forest-night text-white py-20 border-y border-forest-border">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 space-y-16">
          <div className="text-center max-w-2xl mx-auto space-y-3">
            <span className="text-xs font-bold uppercase tracking-wider text-emerald-400">Targeted Portals</span>
            <h2 className="text-3xl sm:text-4xl font-black text-white tracking-tight">
              A Tailored Experience for Every Role
            </h2>
            <p className="text-sm text-neutral-300">
              The platform enforces backend-derived authorization so each stakeholder interacts only with what matters to their workflow.
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
            {/* Customer Box */}
            <div className="bg-forest-deep p-6 rounded-2xl border border-forest-border space-y-4">
              <div className="flex items-center gap-2 text-emerald-400 font-bold text-sm">
                <Users className="w-5 h-5" />
                <span>For Shippers &amp; Customers</span>
              </div>
              <p className="text-xs text-neutral-300 leading-relaxed">
                Post shipments with detailed cargo specs, review multiple competitive quotes from verified carriers, accept bids with a single click, and pay via Paymob.
              </p>
              <ul className="text-xs space-y-2 text-neutral-300 pt-2 border-t border-forest-border/80">
                <li className="flex items-center gap-2"><CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" /> Full quotation visibility</li>
                <li className="flex items-center gap-2"><CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" /> Live shipment status timeline</li>
                <li className="flex items-center gap-2"><CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" /> Verified proof of delivery records</li>
              </ul>
            </div>

            {/* Carrier Box */}
            <div className="bg-forest-deep p-6 rounded-2xl border border-forest-border space-y-4">
              <div className="flex items-center gap-2 text-emerald-400 font-bold text-sm">
                <Layers className="w-5 h-5" />
                <span>For Carriers &amp; Dispatchers</span>
              </div>
              <p className="text-xs text-neutral-300 leading-relaxed">
                Browse open-for-quote shipments, submit competitive prices, schedule free drivers and vehicles within delivery windows, and monitor fleet utilization.
              </p>
              <ul className="text-xs space-y-2 text-neutral-300 pt-2 border-t border-forest-border/80">
                <li className="flex items-center gap-2"><CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" /> Overlap-conflict prevention</li>
                <li className="flex items-center gap-2"><CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" /> Weight &amp; volume capacity validation</li>
                <li className="flex items-center gap-2"><CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" /> Team member &amp; driver management</li>
              </ul>
            </div>

            {/* Driver Box */}
            <div className="bg-forest-deep p-6 rounded-2xl border border-forest-border space-y-4">
              <div className="flex items-center gap-2 text-emerald-400 font-bold text-sm">
                <Navigation className="w-5 h-5" />
                <span>For Assigned Drivers</span>
              </div>
              <p className="text-xs text-neutral-300 leading-relaxed">
                Mobile-optimized cockpit with high-contrast buttons. Confirm pickups, start transit, stream periodic GPS coordinates, and log recipient confirmations.
              </p>
              <ul className="text-xs space-y-2 text-neutral-300 pt-2 border-t border-forest-border/80">
                <li className="flex items-center gap-2"><CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" /> Touch-friendly operational buttons</li>
                <li className="flex items-center gap-2"><CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" /> Failure reporting with reason logging</li>
                <li className="flex items-center gap-2"><CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" /> Live assignment trip history</li>
              </ul>
            </div>
          </div>
        </div>
      </section>

      {/* ─── SECURITY & RELIABILITY SECTION ─────────────────────────────────── */}
      <section className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="bg-white rounded-3xl border border-neutral-border p-8 md:p-12 shadow-card space-y-8">
          <div className="space-y-3">
            <span className="text-xs font-bold uppercase tracking-wider text-brand">System Integrity</span>
            <h2 className="text-2xl sm:text-3xl font-black text-neutral-primary tracking-tight">
              Enterprise Governance by Design
            </h2>
            <p className="text-sm text-neutral-secondary max-w-2xl">
              ShipFlow strictly reflects what the server permits. No simulated transitions, no client-side price calculations, and no unauthenticated data leakage.
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6 pt-2">
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-brand font-bold text-sm">
                <Cpu className="w-4 h-4" />
                <span>Deterministic State</span>
              </div>
              <p className="text-xs text-neutral-secondary">
                Status changes occur solely through verified backend commands preventing invalid state transitions.
              </p>
            </div>

            <div className="space-y-2">
              <div className="flex items-center gap-2 text-brand font-bold text-sm">
                <ShieldCheck className="w-4 h-4" />
                <span>Tenant Isolation</span>
              </div>
              <p className="text-xs text-neutral-secondary">
                Anti-probing 404 responses ensure one company cannot inspect another's quotes, drivers, or vehicles.
              </p>
            </div>

            <div className="space-y-2">
              <div className="flex items-center gap-2 text-brand font-bold text-sm">
                <Clock className="w-4 h-4" />
                <span>Rate Limiting &amp; RFC 7807</span>
              </div>
              <p className="text-xs text-neutral-secondary">
                Integrated rate limiters protect auth and tracking endpoints with standard Problem Details diagnostics.
              </p>
            </div>

            <div className="space-y-2">
              <div className="flex items-center gap-2 text-brand font-bold text-sm">
                <Compass className="w-4 h-4" />
                <span>Audit Traceability</span>
              </div>
              <p className="text-xs text-neutral-secondary">
                All API interactions carry unique trace IDs linking frontend operations with server execution logs.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* ─── FINAL CALL TO ACTION ───────────────────────────────────────────── */}
      <section className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 text-center space-y-6">
        <h2 className="text-3xl sm:text-4xl font-black text-neutral-primary tracking-tight">
          Ready to Coordinate Your Freight?
        </h2>
        <p className="text-sm text-neutral-secondary max-w-xl mx-auto">
          Create a free shipper account or sign in to your transport company dispatch console.
        </p>
        <div className="flex items-center justify-center gap-3">
          <Button size="lg" variant="primary" asChild rightIcon={<ArrowRight className="w-5 h-5" />}>
            <Link to="/register">Create Customer Account</Link>
          </Button>
          <Button size="lg" variant="outline" asChild>
            <Link to="/login">Sign In</Link>
          </Button>
        </div>
      </section>
    </div>
  );
};
