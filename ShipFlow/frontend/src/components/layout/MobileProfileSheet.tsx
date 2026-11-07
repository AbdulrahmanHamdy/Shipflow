import React from 'react';
import { useNavigate, NavLink } from 'react-router-dom';
import { useAuth } from '@/features/auth/AuthContext';
import {
  X,
  ShieldCheck,
  LogOut,
  Building2,
  Truck,
  Users,
  Compass,
  PlusCircle,
  Package,
  Navigation,
  CalendarDays,
  ExternalLink,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';

interface MobileProfileSheetProps {
  isOpen: boolean;
  onClose: () => void;
}

export const MobileProfileSheet: React.FC<MobileProfileSheetProps> = ({
  isOpen,
  onClose,
}) => {
  const navigate = useNavigate();
  const { user, hasRole, logout } = useAuth();

  if (!isOpen) return null;

  const isPlatformAdmin = hasRole('PlatformAdmin');
  const isCustomer = hasRole('Customer') || isPlatformAdmin;
  const isCarrierStaff = hasRole('CompanyAdmin') || hasRole('Dispatcher') || isPlatformAdmin;
  const isDriver = hasRole('Driver') || isPlatformAdmin;

  const handleLogout = () => {
    onClose();
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div className="fixed inset-0 z-50 md:hidden flex flex-col justify-end">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-forest-night/80 backdrop-blur-xs transition-opacity animate-in fade-in duration-200"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Slide-up Bottom Sheet */}
      <div className="relative bg-white rounded-t-3xl border-t border-neutral-border p-6 shadow-2xl max-h-[85vh] overflow-y-auto animate-in slide-in-from-bottom duration-250 z-10 pb-[calc(1.5rem+env(safe-area-inset-bottom,0px))]">
        {/* Pull handle bar */}
        <div className="w-12 h-1.5 bg-neutral-300 rounded-full mx-auto mb-4" />

        {/* Header with Close */}
        <div className="flex items-start justify-between pb-4 border-b border-neutral-border/70">
          <div className="flex items-center gap-3">
            <div className="w-12 h-12 rounded-2xl bg-brand text-white font-bold text-base flex items-center justify-center shadow-md">
              {user?.email?.[0]?.toUpperCase() || 'U'}
            </div>
            <div>
              <p className="text-sm font-bold text-neutral-primary truncate max-w-[220px]">
                {user?.email}
              </p>
              <div className="flex items-center gap-1.5 mt-0.5">
                <ShieldCheck className="w-3.5 h-3.5 text-brand" />
                <span className="text-xs font-semibold text-brand">
                  {user?.roles?.join(', ') || 'Customer'}
                </span>
              </div>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-1.5 rounded-full text-neutral-400 hover:text-neutral-700 hover:bg-neutral-100 transition-colors"
            aria-label="Close profile sheet"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Engine status indicator */}
        <div className="my-4 px-3.5 py-2.5 rounded-xl bg-emerald-50 border border-emerald-200 flex items-center gap-2.5 text-xs text-emerald-900 font-medium">
          <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse inline-block" />
          <span>Connected to ShipFlow Control Engine (Production)</span>
        </div>

        {/* Quick Links Section */}
        <div className="space-y-1.5 py-2">
          <span className="text-[11px] font-bold uppercase tracking-wider text-neutral-secondary block px-1 mb-2">
            Quick Actions
          </span>

          {isCustomer && (
            <>
              <NavLink
                to="/app/shipments/new"
                onClick={onClose}
                className="flex items-center justify-between p-3 rounded-xl hover:bg-neutral-50 border border-neutral-border/60 text-xs font-semibold text-neutral-primary transition-colors"
              >
                <div className="flex items-center gap-2.5">
                  <PlusCircle className="w-4 h-4 text-brand" />
                  <span>Register New Consignment</span>
                </div>
                <ExternalLink className="w-3.5 h-3.5 text-neutral-400" />
              </NavLink>

              <NavLink
                to="/track"
                onClick={onClose}
                className="flex items-center justify-between p-3 rounded-xl hover:bg-neutral-50 border border-neutral-border/60 text-xs font-semibold text-neutral-primary transition-colors"
              >
                <div className="flex items-center gap-2.5">
                  <Compass className="w-4 h-4 text-brand" />
                  <span>Public Live Tracking Portal</span>
                </div>
                <ExternalLink className="w-3.5 h-3.5 text-neutral-400" />
              </NavLink>
            </>
          )}

          {isCarrierStaff && (
            <>
              <NavLink
                to="/carrier/team"
                onClick={onClose}
                className="flex items-center justify-between p-3 rounded-xl hover:bg-neutral-50 border border-neutral-border/60 text-xs font-semibold text-neutral-primary transition-colors"
              >
                <div className="flex items-center gap-2.5">
                  <Building2 className="w-4 h-4 text-brand" />
                  <span>Company Members &amp; Dispatchers</span>
                </div>
                <ExternalLink className="w-3.5 h-3.5 text-neutral-400" />
              </NavLink>

              <NavLink
                to="/carrier/fleet"
                onClick={onClose}
                className="flex items-center justify-between p-3 rounded-xl hover:bg-neutral-50 border border-neutral-border/60 text-xs font-semibold text-neutral-primary transition-colors"
              >
                <div className="flex items-center gap-2.5">
                  <Truck className="w-4 h-4 text-brand" />
                  <span>Fleet Vehicles Registry</span>
                </div>
                <ExternalLink className="w-3.5 h-3.5 text-neutral-400" />
              </NavLink>

              <NavLink
                to="/carrier/drivers"
                onClick={onClose}
                className="flex items-center justify-between p-3 rounded-xl hover:bg-neutral-50 border border-neutral-border/60 text-xs font-semibold text-neutral-primary transition-colors"
              >
                <div className="flex items-center gap-2.5">
                  <Users className="w-4 h-4 text-brand" />
                  <span>Licensed Drivers Directory</span>
                </div>
                <ExternalLink className="w-3.5 h-3.5 text-neutral-400" />
              </NavLink>
            </>
          )}

          {isDriver && (
            <>
              <NavLink
                to="/driver/current-job"
                onClick={onClose}
                className="flex items-center justify-between p-3 rounded-xl hover:bg-neutral-50 border border-neutral-border/60 text-xs font-semibold text-neutral-primary transition-colors"
              >
                <div className="flex items-center gap-2.5">
                  <Navigation className="w-4 h-4 text-brand" />
                  <span>Active Trip Mission</span>
                </div>
                <ExternalLink className="w-3.5 h-3.5 text-neutral-400" />
              </NavLink>

              <NavLink
                to="/driver/assignments"
                onClick={onClose}
                className="flex items-center justify-between p-3 rounded-xl hover:bg-neutral-50 border border-neutral-border/60 text-xs font-semibold text-neutral-primary transition-colors"
              >
                <div className="flex items-center gap-2.5">
                  <CalendarDays className="w-4 h-4 text-brand" />
                  <span>Trip Assignment History</span>
                </div>
                <ExternalLink className="w-3.5 h-3.5 text-neutral-400" />
              </NavLink>
            </>
          )}
        </div>

        {/* Sign Out Action Button */}
        <div className="mt-5 pt-4 border-t border-neutral-border">
          <Button
            variant="danger"
            size="md"
            onClick={handleLogout}
            className="w-full justify-center text-xs py-3 rounded-xl shadow-sm"
            leftIcon={<LogOut className="w-4 h-4" />}
          >
            Sign Out of ShipFlow
          </Button>
        </div>
      </div>
    </div>
  );
};
