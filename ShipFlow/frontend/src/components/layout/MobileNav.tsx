import React from 'react';
import { NavLink } from 'react-router-dom';
import { useAuth } from '@/features/auth/AuthContext';
import { ShipFlowLogo } from '@/components/brand';
import { X, LayoutDashboard, Package, PlusCircle, Truck, Users, Building2, Navigation, CalendarDays, LogOut } from 'lucide-react';
import { cn } from '@/lib/utils';

interface MobileNavProps {
  isOpen: boolean;
  onClose: () => void;
}

export const MobileNav: React.FC<MobileNavProps> = ({ isOpen, onClose }) => {
  const { hasRole, logout } = useAuth();

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 md:hidden">
      <div className="fixed inset-0 bg-forest-night/70 backdrop-blur-xs" onClick={onClose} />
      <div className="fixed inset-y-0 left-0 w-72 bg-forest-night border-r border-forest-border p-5 flex flex-col text-white shadow-2xl animate-in slide-in-from-left duration-200">
        <div className="flex items-center justify-between pb-4 border-b border-forest-border/80">
          <ShipFlowLogo size="md" variant="light" />
          <button onClick={onClose} className="p-1 text-neutral-400 hover:text-white" aria-label="Close drawer">
            <X className="w-5 h-5" />
          </button>
        </div>

        <nav className="flex-1 py-4 space-y-4 overflow-y-auto">
          <div>
            <span className="text-[10px] font-bold uppercase tracking-wider text-emerald-400/80 block mb-2 px-2">
              Customer
            </span>
            <div className="space-y-1">
              <NavLink
                to="/app"
                end
                onClick={onClose}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium',
                    isActive ? 'bg-brand text-white font-semibold' : 'text-neutral-300 hover:bg-forest-deep'
                  )
                }
              >
                <LayoutDashboard className="w-4 h-4 text-emerald-400" />
                <span>Dashboard</span>
              </NavLink>
              <NavLink
                to="/app/shipments"
                onClick={onClose}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium',
                    isActive ? 'bg-brand text-white font-semibold' : 'text-neutral-300 hover:bg-forest-deep'
                  )
                }
              >
                <Package className="w-4 h-4 text-emerald-400" />
                <span>Shipments</span>
              </NavLink>
              <NavLink
                to="/app/shipments/new"
                onClick={onClose}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium',
                    isActive ? 'bg-brand text-white font-semibold' : 'text-neutral-300 hover:bg-forest-deep'
                  )
                }
              >
                <PlusCircle className="w-4 h-4 text-emerald-400" />
                <span>New Shipment</span>
              </NavLink>
            </div>
          </div>

          {(hasRole('CompanyAdmin') || hasRole('Dispatcher')) && (
            <div>
              <span className="text-[10px] font-bold uppercase tracking-wider text-emerald-400/80 block mb-2 px-2">
                Carrier &amp; Dispatch
              </span>
              <div className="space-y-1">
                <NavLink
                  to="/carrier"
                  end
                  onClick={onClose}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium',
                      isActive ? 'bg-brand text-white font-semibold' : 'text-neutral-300 hover:bg-forest-deep'
                    )
                  }
                >
                  <LayoutDashboard className="w-4 h-4 text-emerald-400" />
                  <span>Control Tower</span>
                </NavLink>
                <NavLink
                  to="/carrier/shipments"
                  onClick={onClose}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium',
                      isActive ? 'bg-brand text-white font-semibold' : 'text-neutral-300 hover:bg-forest-deep'
                    )
                  }
                >
                  <Package className="w-4 h-4 text-emerald-400" />
                  <span>Shipment Board</span>
                </NavLink>
                <NavLink
                  to="/carrier/fleet"
                  onClick={onClose}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium',
                      isActive ? 'bg-brand text-white font-semibold' : 'text-neutral-300 hover:bg-forest-deep'
                    )
                  }
                >
                  <Truck className="w-4 h-4 text-emerald-400" />
                  <span>Fleet Vehicles</span>
                </NavLink>
                <NavLink
                  to="/carrier/drivers"
                  onClick={onClose}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium',
                      isActive ? 'bg-brand text-white font-semibold' : 'text-neutral-300 hover:bg-forest-deep'
                    )
                  }
                >
                  <Users className="w-4 h-4 text-emerald-400" />
                  <span>Drivers</span>
                </NavLink>
                <NavLink
                  to="/carrier/team"
                  onClick={onClose}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium',
                      isActive ? 'bg-brand text-white font-semibold' : 'text-neutral-300 hover:bg-forest-deep'
                    )
                  }
                >
                  <Building2 className="w-4 h-4 text-emerald-400" />
                  <span>Company Members</span>
                </NavLink>
              </div>
            </div>
          )}

          {hasRole('Driver') && (
            <div>
              <span className="text-[10px] font-bold uppercase tracking-wider text-emerald-400/80 block mb-2 px-2">
                Driver Operations
              </span>
              <div className="space-y-1">
                <NavLink
                  to="/driver"
                  end
                  onClick={onClose}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium',
                      isActive ? 'bg-brand text-white font-semibold' : 'text-neutral-300 hover:bg-forest-deep'
                    )
                  }
                >
                  <LayoutDashboard className="w-4 h-4 text-emerald-400" />
                  <span>Cockpit</span>
                </NavLink>
                <NavLink
                  to="/driver/current-job"
                  onClick={onClose}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium',
                      isActive ? 'bg-brand text-white font-semibold' : 'text-neutral-300 hover:bg-forest-deep'
                    )
                  }
                >
                  <Navigation className="w-4 h-4 text-emerald-400" />
                  <span>Current Delivery</span>
                </NavLink>
                <NavLink
                  to="/driver/assignments"
                  onClick={onClose}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium',
                      isActive ? 'bg-brand text-white font-semibold' : 'text-neutral-300 hover:bg-forest-deep'
                    )
                  }
                >
                  <CalendarDays className="w-4 h-4 text-emerald-400" />
                  <span>Trip History</span>
                </NavLink>
              </div>
            </div>
          )}
        </nav>

        <div className="pt-3 border-t border-forest-border">
          <button
            onClick={() => {
              onClose();
              logout();
            }}
            className="w-full flex items-center gap-2.5 px-3 py-2 text-xs text-red-400 hover:text-white hover:bg-red-950/40 rounded-xl transition-colors"
          >
            <LogOut className="w-4 h-4" />
            <span>Sign Out</span>
          </button>
        </div>
      </div>
    </div>
  );
};
