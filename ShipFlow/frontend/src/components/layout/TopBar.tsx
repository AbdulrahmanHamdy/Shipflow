import React, { useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '@/features/auth/AuthContext';
import { ShipFlowSymbol } from '@/components/brand';
import { LogOut, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/Button';

interface TopBarProps {
  onToggleMobile?: () => void;
  onOpenProfile?: () => void;
}

export const TopBar: React.FC<TopBarProps> = ({ onOpenProfile }) => {
  const navigate = useNavigate();
  const { user, hasRole, logout } = useAuth();
  const [profileOpen, setProfileOpen] = useState(false);

  const isPlatformAdmin = hasRole('PlatformAdmin');
  const isCustomer = hasRole('Customer') || isPlatformAdmin;
  const isCarrierStaff = hasRole('CompanyAdmin') || hasRole('Dispatcher') || isPlatformAdmin;
  const isDriver = hasRole('Driver') || isPlatformAdmin;

  const homeRoute = isDriver && !isCustomer
    ? '/driver'
    : (isCarrierStaff && !isCustomer) || isPlatformAdmin
    ? '/carrier'
    : '/app';

  return (
    <header className="h-16 bg-white border-b border-neutral-border flex items-center justify-between px-4 sm:px-6 z-20">
      <div className="flex items-center gap-3">
        {/* Mobile Brand Logo */}
        <div className="md:hidden flex items-center">
          <NavLink to={homeRoute} className="flex items-center gap-2 select-none">
            <ShipFlowSymbol size={28} variant="dark" />
            <span className="font-extrabold text-base tracking-tight text-forest-night">
              Ship<span className="text-brand">Flow</span>
            </span>
          </NavLink>
        </div>

        <div className="hidden sm:flex items-center gap-2 text-xs text-neutral-secondary font-medium">
          <span className="w-2 h-2 rounded-full bg-emerald-500 inline-block" />
          <span>Connected to ShipFlow Control Engine</span>
        </div>
      </div>

      <div className="flex items-center gap-3">
        {/* User Profile dropdown */}
        <div className="relative">
          <button
            onClick={() => {
              // On mobile, trigger the full-height mobile profile sheet for native feel
              if (window.innerWidth < 768 && onOpenProfile) {
                onOpenProfile();
              } else {
                setProfileOpen(!profileOpen);
              }
            }}
            className="flex items-center gap-2 p-1.5 rounded-xl hover:bg-neutral-hover transition-colors focus:outline-none"
            aria-expanded={profileOpen}
            aria-label="User account menu"
          >
            <div className="w-8 h-8 rounded-full bg-brand-mint text-brand-mintText font-bold text-xs flex items-center justify-center border border-brand-accent/30">
              {user?.email?.[0]?.toUpperCase() || 'U'}
            </div>
            <div className="hidden md:flex flex-col text-left">
              <span className="text-xs font-semibold text-neutral-primary truncate max-w-[140px]">
                {user?.email}
              </span>
              <span className="text-[10px] text-neutral-secondary font-medium">
                {user?.roles?.[0] || 'Member'}
              </span>
            </div>
          </button>

          {profileOpen && (
            <>
              <div
                className="fixed inset-0 z-30"
                onClick={() => setProfileOpen(false)}
              />
              <div className="absolute right-0 mt-2 w-56 rounded-xl bg-white border border-neutral-border shadow-panel p-2 z-40 animate-in fade-in zoom-in-95 duration-150">
                <div className="px-3 py-2 border-b border-neutral-border/60 mb-1">
                  <p className="text-xs font-semibold text-neutral-primary truncate">{user?.email}</p>
                  <div className="flex items-center gap-1 text-[11px] text-neutral-secondary mt-1">
                    <ShieldCheck className="w-3.5 h-3.5 text-brand" />
                    <span>Roles: {user?.roles?.join(', ')}</span>
                  </div>
                </div>

                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => {
                    setProfileOpen(false);
                    logout();
                    navigate('/login', { replace: true });
                  }}
                  className="w-full justify-start text-xs text-red-600 hover:text-red-700 hover:bg-red-50"
                  leftIcon={<LogOut className="w-3.5 h-3.5" />}
                >
                  Sign Out
                </Button>
              </div>
            </>
          )}
        </div>
      </div>
    </header>
  );
};
