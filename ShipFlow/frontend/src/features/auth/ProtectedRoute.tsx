import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';
import { sanitizeReturnUrl } from '@/lib/utils';
import { ForbiddenPage } from '@/pages/public/ForbiddenPage';

interface ProtectedRouteProps {
  children: React.ReactNode;
  allowedRoles?: string[];
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children, allowedRoles }) => {
  const { isAuthenticated, isLoading, hasAnyRole } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <div className="min-h-screen bg-neutral-appBg flex items-center justify-center">
        <div className="flex flex-col items-center gap-3">
          <div className="w-8 h-8 border-2 border-brand border-t-transparent rounded-full animate-spin" />
          <span className="text-sm font-medium text-neutral-secondary">Verifying session...</span>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    const returnUrl = encodeURIComponent(sanitizeReturnUrl(location.pathname + location.search));
    return <Navigate to={`/login?returnUrl=${returnUrl}`} replace />;
  }

  if (allowedRoles && allowedRoles.length > 0 && !hasAnyRole(allowedRoles)) {
    return <ForbiddenPage requiredRoles={allowedRoles} />;
  }

  return <>{children}</>;
};
