import React, { useState } from 'react';
import { Outlet, Link, NavLink } from 'react-router-dom';
import { ShipFlowLogo } from '@/components/brand';
import { Button } from '@/components/ui/Button';
import { useAuth } from '@/features/auth/AuthContext';
import { Menu, X, ArrowRight, ShieldCheck, ExternalLink } from 'lucide-react';
import { cn } from '@/lib/utils';

export const PublicLayout: React.FC = () => {
  const { isAuthenticated, user } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const getDashboardLink = () => {
    if (user?.roles.includes('CompanyAdmin') || user?.roles.includes('Dispatcher')) return '/carrier';
    if (user?.roles.includes('Driver')) return '/driver';
    return '/app';
  };

  return (
    <div className="min-h-screen flex flex-col bg-neutral-appBg text-neutral-primary">
      {/* Header */}
      <header className="sticky top-0 z-40 bg-white/90 backdrop-blur-md border-b border-neutral-border">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-18 flex items-center justify-between">
          <Link to="/" className="flex items-center">
            <ShipFlowLogo size="md" variant="dark" />
          </Link>

          {/* Desktop Nav */}
          <nav className="hidden md:flex items-center gap-8 text-sm font-medium text-neutral-secondary">
            <NavLink to="/" className={({ isActive }) => cn('hover:text-brand transition-colors', isActive && 'text-brand font-semibold')}>
              Platform
            </NavLink>
            <a href="/#capabilities" className="hover:text-brand transition-colors">
              Capabilities
            </a>
            <a href="/#solutions" className="hover:text-brand transition-colors">
              Solutions
            </a>
            <NavLink to="/tracking" className={({ isActive }) => cn('hover:text-brand transition-colors', isActive && 'text-brand font-semibold')}>
              Tracking Portal
            </NavLink>
            <a href="/#preview" className="hover:text-brand transition-colors">
              Product Tour
            </a>
          </nav>

          {/* CTA Buttons */}
          <div className="hidden md:flex items-center gap-3">
            {isAuthenticated ? (
              <Button size="sm" variant="primary" asChild>
                <Link to={getDashboardLink()} className="flex items-center gap-2">
                  <span>Open Console</span>
                  <ArrowRight className="w-4 h-4" />
                </Link>
              </Button>
            ) : (
              <>
                <Button size="sm" variant="ghost" asChild>
                  <Link to="/login">Sign In</Link>
                </Button>
                <Button size="sm" variant="primary" asChild>
                  <Link to="/register">Get Started</Link>
                </Button>
              </>
            )}
          </div>

          {/* Mobile hamburger */}
          <div className="md:hidden flex items-center">
            <button
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="p-2 text-neutral-secondary hover:text-neutral-primary rounded-lg"
              aria-label="Toggle navigation"
            >
              {mobileMenuOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
            </button>
          </div>
        </div>

        {/* Mobile menu panel */}
        {mobileMenuOpen && (
          <div className="md:hidden border-b border-neutral-border bg-white p-4 space-y-3 animate-in slide-in-from-top duration-200">
            <NavLink
              to="/"
              onClick={() => setMobileMenuOpen(false)}
              className="block px-3 py-2 rounded-lg text-sm font-medium hover:bg-neutral-hover"
            >
              Platform
            </NavLink>
            <NavLink
              to="/tracking"
              onClick={() => setMobileMenuOpen(false)}
              className="block px-3 py-2 rounded-lg text-sm font-medium hover:bg-neutral-hover"
            >
              Tracking Portal
            </NavLink>
            <a
              href="/#capabilities"
              onClick={() => setMobileMenuOpen(false)}
              className="block px-3 py-2 rounded-lg text-sm font-medium hover:bg-neutral-hover"
            >
              Capabilities
            </a>
            <a
              href="/#solutions"
              onClick={() => setMobileMenuOpen(false)}
              className="block px-3 py-2 rounded-lg text-sm font-medium hover:bg-neutral-hover"
            >
              Solutions
            </a>

            <div className="pt-3 border-t border-neutral-border flex flex-col gap-2">
              {isAuthenticated ? (
                <Button size="sm" variant="primary" asChild className="w-full">
                  <Link to={getDashboardLink()} onClick={() => setMobileMenuOpen(false)}>
                    Open Console
                  </Link>
                </Button>
              ) : (
                <>
                  <Button size="sm" variant="outline" asChild className="w-full">
                    <Link to="/login" onClick={() => setMobileMenuOpen(false)}>
                      Sign In
                    </Link>
                  </Button>
                  <Button size="sm" variant="primary" asChild className="w-full">
                    <Link to="/register" onClick={() => setMobileMenuOpen(false)}>
                      Get Started
                    </Link>
                  </Button>
                </>
              )}
            </div>
          </div>
        )}
      </header>

      {/* Page Content */}
      <main className="flex-1">
        <Outlet />
      </main>

      {/* Enterprise Footer */}
      <footer className="bg-forest-night text-neutral-300 border-t border-forest-border pt-12 pb-8">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-8 pb-10 border-b border-forest-border/80">
            {/* Col 1: Identity */}
            <div className="md:col-span-1 space-y-3">
              <ShipFlowLogo size="md" variant="light" />
              <p className="text-xs text-neutral-300 leading-relaxed max-w-xs mt-2">
                Enterprise control-tower platform for end-to-end freight visibility, carrier dispatching, and verified driver deliveries.
              </p>
              <div className="flex items-center gap-2 text-xs text-emerald-400 pt-2">
                <ShieldCheck className="w-4 h-4" />
                <span>Encrypted .NET 8 Core Web API</span>
              </div>
            </div>

            {/* Col 2: Solutions */}
            <div className="space-y-2">
              <h4 className="text-xs font-bold uppercase tracking-wider text-white">Platform Modules</h4>
              <ul className="space-y-1.5 text-xs text-neutral-300">
                <li><Link to="/login" className="hover:text-emerald-400 transition-colors">Shipper Dashboard</Link></li>
                <li><Link to="/login" className="hover:text-emerald-400 transition-colors">Carrier Dispatch Tower</Link></li>
                <li><Link to="/login" className="hover:text-emerald-400 transition-colors">Driver Mobile Cockpit</Link></li>
                <li><Link to="/tracking" className="hover:text-emerald-400 transition-colors">Shipment Tracking</Link></li>
              </ul>
            </div>

            {/* Col 3: Resources & Trust */}
            <div className="space-y-2">
              <h4 className="text-xs font-bold uppercase tracking-wider text-white">Integrations &amp; Trust</h4>
              <ul className="space-y-1.5 text-xs text-neutral-300">
                <li className="flex items-center gap-1.5">
                  <span>Paymob Payment Gateway</span>
                </li>
                <li><span>OpenStreetMap Routing Service</span></li>
                <li><span>FluentValidation RFC 7807</span></li>
                <li><span>JWT Bearer Identity</span></li>
              </ul>
            </div>

            {/* Col 4: Legal & Notice */}
            <div className="space-y-2">
              <h4 className="text-xs font-bold uppercase tracking-wider text-white">Compliance &amp; Legal</h4>
              <ul className="space-y-1.5 text-xs text-neutral-300">
                <li><Link to="/privacy" className="hover:text-emerald-400 transition-colors">Privacy Policy (Draft)</Link></li>
                <li><Link to="/terms" className="hover:text-emerald-400 transition-colors">Terms of Service (Draft)</Link></li>
                <li className="text-[11px] text-neutral-400 leading-normal pt-2">
                  ShipFlow is designed with strict tenant segregation, preventing cross-company information disclosure.
                </li>
              </ul>
            </div>
          </div>

          <div className="pt-6 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-neutral-400">
            <p>&copy; {new Date().getFullYear()} ShipFlow Logistics Platform. All rights reserved.</p>
            <p className="flex items-center gap-1">
              <span>Operating with .NET 8 ASP.NET Core Engine</span>
            </p>
          </div>
        </div>
      </footer>
    </div>
  );
};
