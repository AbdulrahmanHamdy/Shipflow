import React from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { useAuth } from '@/features/auth/AuthContext';
import {
  LayoutDashboard,
  Package,
  PlusCircle,
  Truck,
  Users,
  Building2,
  CalendarDays,
  Navigation,
  Compass,
  User,
} from 'lucide-react';
import { cn } from '@/lib/utils';

interface TabItem {
  id: string;
  label: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  exact?: boolean;
}

interface MobileBottomNavProps {
  onOpenProfile: () => void;
  isProfileOpen?: boolean;
}

export const MobileBottomNav: React.FC<MobileBottomNavProps> = ({
  onOpenProfile,
  isProfileOpen = false,
}) => {
  const location = useLocation();
  const { hasRole } = useAuth();

  const isPlatformAdmin = hasRole('PlatformAdmin');
  const isCustomer = hasRole('Customer') || isPlatformAdmin;
  const isCarrierStaff = hasRole('CompanyAdmin') || hasRole('Dispatcher') || isPlatformAdmin;
  const isDriver = hasRole('Driver') || isPlatformAdmin;

  let tabs: TabItem[] = [];

  if (isDriver && !isCustomer && !isCarrierStaff) {
    // Driver role tabs
    tabs = [
      { id: 'home', label: 'Cockpit', href: '/driver', icon: LayoutDashboard, exact: true },
      { id: 'job', label: 'My Task', href: '/driver/current-job', icon: Navigation },
      { id: 'history', label: 'History', href: '/driver/assignments', icon: CalendarDays },
      { id: 'track', label: 'Track', href: '/track', icon: Compass },
    ];
  } else if (isCarrierStaff && !isCustomer) {
    // Carrier Admin / Dispatcher tabs
    tabs = [
      { id: 'tower', label: 'Tower', href: '/carrier', icon: LayoutDashboard, exact: true },
      { id: 'board', label: 'Shipments', href: '/carrier/shipments', icon: Package },
      { id: 'fleet', label: 'Fleet', href: '/carrier/fleet', icon: Truck },
      { id: 'drivers', label: 'Drivers', href: '/carrier/drivers', icon: Users },
    ];
  } else if (isPlatformAdmin) {
    // Platform Admin tabs
    tabs = [
      { id: 'portal', label: 'Portal', href: '/app', icon: LayoutDashboard, exact: true },
      { id: 'shipments', label: 'Shipments', href: '/app/shipments', icon: Package },
      { id: 'carrier', label: 'Carrier', href: '/carrier', icon: Building2 },
      { id: 'driver', label: 'Driver', href: '/driver', icon: Truck },
    ];
  } else {
    // Customer role tabs (default)
    tabs = [
      { id: 'home', label: 'Home', href: '/app', icon: LayoutDashboard, exact: true },
      { id: 'shipments', label: 'Shipments', href: '/app/shipments', icon: Package, exact: true },
      { id: 'new', label: 'New', href: '/app/shipments/new', icon: PlusCircle },
      { id: 'track', label: 'Track', href: '/track', icon: Compass },
    ];
  }

  const isTabActive = (tab: TabItem) => {
    if (isProfileOpen) return false;
    if (tab.exact) {
      return location.pathname === tab.href;
    }
    return location.pathname === tab.href || location.pathname.startsWith(`${tab.href}/`);
  };

  return (
    <nav
      aria-label="Mobile Navigation"
      className="md:hidden fixed bottom-0 left-0 right-0 z-40 bg-forest-night/95 backdrop-blur-md border-t border-forest-border shadow-[0_-4px_25px_rgba(0,0,0,0.4)] pb-[env(safe-area-inset-bottom,0px)]"
    >
      <div className="grid grid-cols-5 h-16 items-center px-1">
        {tabs.map((tab) => {
          const active = isTabActive(tab);
          const IconComponent = tab.icon;

          return (
            <NavLink
              key={tab.id}
              to={tab.href}
              end={tab.exact}
              className={cn(
                'group flex flex-col items-center justify-center h-full min-h-[48px] py-1 select-none transition-all touch-manipulation focus:outline-none',
                active ? 'text-white' : 'text-neutral-400 hover:text-neutral-200'
              )}
            >
              <div
                className={cn(
                  'flex items-center justify-center w-10 h-7 rounded-full transition-all duration-200',
                  active
                    ? 'bg-brand/25 text-emerald-400 scale-105'
                    : 'text-neutral-400 group-hover:text-neutral-200 group-active:scale-95'
                )}
              >
                <IconComponent className={cn('w-5 h-5 transition-colors', active && 'stroke-[2.5]')} />
              </div>
              <span
                className={cn(
                  'text-[10px] font-medium tracking-tight mt-0.5 truncate max-w-[64px]',
                  active ? 'text-white font-bold' : 'text-neutral-400'
                )}
              >
                {tab.label}
              </span>
            </NavLink>
          );
        })}

        {/* 5th Tab: Profile / Account Sheet trigger */}
        <button
          type="button"
          onClick={onOpenProfile}
          aria-label="Account and Profile"
          className={cn(
            'group flex flex-col items-center justify-center h-full min-h-[48px] py-1 select-none transition-all touch-manipulation focus:outline-none',
            isProfileOpen ? 'text-white' : 'text-neutral-400 hover:text-neutral-200'
          )}
        >
          <div
            className={cn(
              'flex items-center justify-center w-10 h-7 rounded-full transition-all duration-200',
              isProfileOpen
                ? 'bg-brand/25 text-emerald-400 scale-105'
                : 'text-neutral-400 group-hover:text-neutral-200 group-active:scale-95'
            )}
          >
            <User className={cn('w-5 h-5 transition-colors', isProfileOpen && 'stroke-[2.5]')} />
          </div>
          <span
            className={cn(
              'text-[10px] font-medium tracking-tight mt-0.5',
              isProfileOpen ? 'text-white font-bold' : 'text-neutral-400'
            )}
          >
            Profile
          </span>
        </button>
      </div>
    </nav>
  );
};
