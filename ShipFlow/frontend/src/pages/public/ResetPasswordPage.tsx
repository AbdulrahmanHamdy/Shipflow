import React from 'react';
import { Link } from 'react-router-dom';
import { ShipFlowLogo } from '@/components/brand';
import { Button } from '@/components/ui/Button';
import { usePageTitle } from '@/hooks/usePageTitle';
import { Info, ArrowLeft } from 'lucide-react';

export const ResetPasswordPage: React.FC = () => {
  usePageTitle('Reset Password');

  return (
    <div className="min-h-screen bg-neutral-appBg flex flex-col justify-center py-12 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md text-center space-y-4">
        <Link to="/" className="inline-block">
          <ShipFlowLogo size="xl" variant="dark" />
        </Link>
        <h2 className="text-2xl font-black text-neutral-primary tracking-tight">
          Set New Password
        </h2>
      </div>

      <div className="mt-8 sm:mx-auto sm:w-full sm:max-w-md px-4 sm:px-0">
        <div className="bg-white py-8 px-6 sm:px-10 rounded-3xl border border-neutral-border shadow-panel space-y-6">
          <div className="p-4 rounded-2xl bg-amber-50 border border-amber-200 space-y-2">
            <div className="flex items-center gap-2 text-amber-800 font-bold text-xs uppercase tracking-wider">
              <Info className="w-4 h-4 text-amber-600 flex-shrink-0" />
              <span>Integration Notice</span>
            </div>
            <p className="text-xs text-amber-900 leading-relaxed">
              Token-based password reset completion is not implemented on the current backend Web API release. Please refer to your company administrator or register a new account.
            </p>
          </div>

          <div className="pt-2">
            <Button variant="primary" size="md" asChild className="w-full" leftIcon={<ArrowLeft className="w-4 h-4" />}>
              <Link to="/login">Back to Sign In</Link>
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
};
