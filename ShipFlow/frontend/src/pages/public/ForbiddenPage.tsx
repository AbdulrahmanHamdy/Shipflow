import React from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/Button';
import { ShipFlowLogo } from '@/components/brand';
import { ShieldAlert, ArrowLeft, Home, LogOut } from 'lucide-react';
import { useAuth } from '@/features/auth/AuthContext';
import { usePageTitle } from '@/hooks/usePageTitle';

interface ForbiddenPageProps {
  requiredRoles?: string[];
}

export const ForbiddenPage: React.FC<ForbiddenPageProps> = ({ requiredRoles }) => {
  usePageTitle('Access Denied');
  const { user, logout } = useAuth();

  return (
    <div className="min-h-screen bg-neutral-appBg flex flex-col items-center justify-center p-6 text-center select-none">
      <div className="max-w-md w-full bg-white rounded-3xl p-8 border border-red-200 shadow-panel space-y-6">
        <div className="flex justify-center">
          <ShipFlowLogo size="lg" variant="dark" />
        </div>

        <div className="w-16 h-16 rounded-2xl bg-red-50 text-red-600 flex items-center justify-center mx-auto border border-red-200">
          <ShieldAlert className="w-8 h-8" />
        </div>

        <div className="space-y-2">
          <span className="text-xs font-mono font-bold tracking-wider text-red-600 uppercase bg-red-100 px-2.5 py-1 rounded-md">
            HTTP 403 Forbidden
          </span>
          <h1 className="text-2xl font-black text-neutral-primary tracking-tight">
            Restricted Operational Area
          </h1>
          <p className="text-xs text-neutral-secondary leading-relaxed">
            Your current account credentials do not hold the required permissions to inspect this operational section.
          </p>
          {requiredRoles && requiredRoles.length > 0 && (
            <p className="text-[11px] font-medium text-neutral-secondary bg-neutral-hover py-1.5 px-3 rounded-lg mt-2">
              Required Role: <span className="font-semibold text-neutral-primary">{requiredRoles.join(' or ')}</span>
              <br />
              Your Role: <span className="font-semibold text-neutral-primary">{user?.roles.join(', ') || 'None'}</span>
            </p>
          )}
        </div>

        <div className="flex flex-col sm:flex-row items-center justify-center gap-2 pt-2">
          <Button variant="outline" size="sm" onClick={() => window.history.back()} leftIcon={<ArrowLeft className="w-4 h-4" />}>
            Go Back
          </Button>
          <Button variant="ghost" size="sm" onClick={logout} leftIcon={<LogOut className="w-4 h-4 text-red-500" />}>
            Switch Account
          </Button>
          <Button variant="primary" size="sm" asChild leftIcon={<Home className="w-4 h-4" />}>
            <Link to="/">Dashboard</Link>
          </Button>
        </div>
      </div>
    </div>
  );
};
