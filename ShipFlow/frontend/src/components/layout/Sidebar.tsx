import React, { useState, useEffect } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '@/features/auth/AuthContext';
import { ShipFlowLogo, ShipFlowSymbol } from '@/components/brand';
import {
  LayoutDashboard,
  Package,
  PlusCircle,
  Truck,
  Users,
  Building2,
  CalendarDays,
  FileCheck2,
  ChevronLeft,
  ChevronRight,
  LogOut,
  Navigation,
} from 'lucide-react';
import { cn } from '@/lib/utils';

interface NavItem {
  label: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  roles?: string[];
  exact?: boolean;
}

const customerNav: NavItem[] = [
  { label: 'Dashboard', href: '/app', icon: LayoutDashboard, exact: true },
  { label: 'My Shipments', href: '/app/shipments', icon: Package },
  { label: 'New Shipment', href: '/app/shipments/new', icon: PlusCircle },
];

const carrierNav: NavItem[] = [
  { label: 'Dispatch Tower', href: '/carrier', icon: LayoutDashboard, exact: true },
  { label: 'Shipment Board', href: '/carrier/shipments', icon: Package },
  { label: 'Fleet Vehicles', href: '/carrier/fleet', icon: Truck },
  { label: 'Drivers', href: '/carrier/drivers', icon: Users },
  { label: 'Company & Team', href: '/carrier/team', icon: Building2 },
];

const driverNav: NavItem[] = [
  { label: 'Driver Cockpit', href: '/driver', icon: LayoutDashboard, exact: true },
  { label: 'Active Job', href: '/driver/current-job', icon: Navigation },
  { label: 'Job History', href: '/driver/assignments', icon: CalendarDays },
];

export const Sidebar: React.FC = () => {
  const navigate = useNavigate();
  const { user, hasRole, logout } = useAuth();
  const [collapsed, setCollapsed] = useState<boolean>(() => {
    try {
      return localStorage.getItem('shipflow_sidebar_collapsed') === 'true';
    } catch {
      return false;
    }
  });

  useEffect(() => {
    try {
      localStorage.setItem('shipflow_sidebar_collapsed', String(collapsed));
    } catch {
      // Ignore
    }
  }, [collapsed]);

  const isPlatformAdmin = hasRole('PlatformAdmin');
  const isCustomer = hasRole('Customer') || isPlatformAdmin;
  const isCarrierStaff = hasRole('CompanyAdmin') || hasRole('Dispatcher') || isPlatformAdmin;
  const isDriver = hasRole('Driver') || isPlatformAdmin;

  const homeRoute = isDriver && !isCustomer
    ? '/driver'
    : (isCarrierStaff && !isCustomer) || isPlatformAdmin
    ? '/carrier'
    : '/app';

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <aside
      className={cn(
        'hidden md:flex flex-col bg-forest-night border-r border-forest-border text-white transition-all duration-300 z-30 select-none',
        collapsed ? 'w-20' : 'w-64'
      )}
    >
      {/* Brand Header */}
      <div className="h-16 flex items-center justify-between px-4 border-b border-forest-border/80">
        <NavLink to={homeRoute} className="flex items-center">
          {collapsed ? (
            <ShipFlowSymbol size={34} variant="dark" />
          ) : (
            <ShipFlowLogo size="md" variant="light" />
          )}
        </NavLink>

        <button
          onClick={() => setCollapsed(!collapsed)}
          className="p-1.5 rounded-lg text-emerald-400/80 hover:text-white hover:bg-forest-deep transition-colors"
          title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        >
          {collapsed ? <ChevronRight className="w-4 h-4" /> : <ChevronLeft className="w-4 h-4" />}
        </button>
      </div>

      {/* Navigation Sections */}
      <div className="flex-1 py-4 px-3 space-y-6 overflow-y-auto">
        {/* Customer Nav */}
        {isCustomer && (
          <div>
            {!collapsed && (
              <span className="px-3 text-[11px] font-bold uppercase tracking-wider text-emerald-500/70 block mb-2">
                Customer Portal
              </span>
            )}
            <nav className="space-y-1">
              {customerNav.map((item) => (
                <NavLink
                  key={item.href}
                  to={item.href}
                  end={item.exact}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all group',
                      isActive
                        ? 'bg-brand text-white shadow-sm font-semibold'
                        : 'text-neutral-300 hover:text-white hover:bg-forest-deep'
                    )
                  }
                >
                  <item.icon className="w-5 h-5 flex-shrink-0 text-emerald-400 group-hover:text-white transition-colors" />
                  {!collapsed && <span>{item.label}</span>}
                </NavLink>
              ))}
            </nav>
          </div>
        )}

        {/* Carrier Nav */}
        {isCarrierStaff && (
          <div>
            {!collapsed && (
              <span className="px-3 text-[11px] font-bold uppercase tracking-wider text-emerald-500/70 block mb-2">
                Carrier &amp; Dispatch
              </span>
            )}
            <nav className="space-y-1">
              {carrierNav.map((item) => (
                <NavLink
                  key={item.href}
                  to={item.href}
                  end={item.exact}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all group',
                      isActive
                        ? 'bg-brand text-white shadow-sm font-semibold'
                        : 'text-neutral-300 hover:text-white hover:bg-forest-deep'
                    )
                  }
                >
                  <item.icon className="w-5 h-5 flex-shrink-0 text-emerald-400 group-hover:text-white transition-colors" />
                  {!collapsed && <span>{item.label}</span>}
                </NavLink>
              ))}
            </nav>
          </div>
        )}

        {/* Driver Nav */}
        {isDriver && (
          <div>
            {!collapsed && (
              <span className="px-3 text-[11px] font-bold uppercase tracking-wider text-emerald-500/70 block mb-2">
                Driver Operations
              </span>
            )}
            <nav className="space-y-1">
              {driverNav.map((item) => (
                <NavLink
                  key={item.href}
                  to={item.href}
                  end={item.exact}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all group',
                      isActive
                        ? 'bg-brand text-white shadow-sm font-semibold'
                        : 'text-neutral-300 hover:text-white hover:bg-forest-deep'
                    )
                  }
                >
                  <item.icon className="w-5 h-5 flex-shrink-0 text-emerald-400 group-hover:text-white transition-colors" />
                  {!collapsed && <span>{item.label}</span>}
                </NavLink>
              ))}
            </nav>
          </div>
        )}
      </div>

      {/* User Footer */}
      <div className="p-3 border-t border-forest-border/80 bg-forest-night">
        <div className={cn('flex items-center gap-3 px-2 py-2 rounded-xl bg-forest-deep/60', collapsed && 'justify-center')}>
          <div className="w-8 h-8 rounded-full bg-brand flex items-center justify-center font-bold text-xs text-white flex-shrink-0">
            {user?.email?.[0]?.toUpperCase() || 'U'}
          </div>
          {!collapsed && (
            <div className="flex-1 min-w-0">
              <p className="text-xs font-semibold text-white truncate">{user?.email}</p>
              <p className="text-[10px] text-emerald-400 uppercase font-medium">
                {user?.roles?.join(', ') || 'User'}
              </p>
            </div>
          )}
          <button
            onClick={handleLogout}
            className="p-1.5 text-neutral-400 hover:text-red-400 hover:bg-forest-surface rounded-lg transition-colors"
            title="Sign out"
            aria-label="Sign out"
          >
            <LogOut className="w-4 h-4" />
          </button>
        </div>
      </div>
    </aside>
  );
};
