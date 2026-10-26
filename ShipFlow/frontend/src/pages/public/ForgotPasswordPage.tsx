import React from 'react';
import { Link } from 'react-router-dom';
import { ShipFlowLogo } from '@/components/brand';
import { Button } from '@/components/ui/Button';
import { usePageTitle } from '@/hooks/usePageTitle';
import { Info, ArrowLeft, Building2 } from 'lucide-react';

export const ForgotPasswordPage: React.FC = () => {
  usePageTitle('Reset Password Guidance');

  return (
    <div className="min-h-screen bg-neutral-appBg flex flex-col justify-center py-12 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md text-center space-y-4">
        <Link to="/" className="inline-block">
          <ShipFlowLogo size="xl" variant="dark" />
        </Link>
        <h2 className="text-2xl font-black text-neutral-primary tracking-tight">
          Password Recovery
        </h2>
      </div>

      <div className="mt-8 sm:mx-auto sm:w-full sm:max-w-md px-4 sm:px-0">
        <div className="bg-white py-8 px-6 sm:px-10 rounded-3xl border border-neutral-border shadow-panel space-y-6">
          <div className="p-4 rounded-2xl bg-amber-50 border border-amber-200 space-y-2">
            <div className="flex items-center gap-2 text-amber-800 font-bold text-xs uppercase tracking-wider">
              <Info className="w-4 h-4 text-amber-600 flex-shrink-0" />
              <span>Service Integration Notice</span>
            </div>
            <p className="text-xs text-amber-900 leading-relaxed">
              Automated self-service password reset is currently unavailable because the backend email delivery service is in staging mode.
            </p>
          </div>

          <div className="space-y-3 text-xs text-neutral-secondary leading-relaxed">
            <h4 className="font-bold text-neutral-primary text-sm">How to Restore Access:</h4>
            <div className="flex items-start gap-2.5 p-3 rounded-xl bg-neutral-appBg border border-neutral-border">
              <Building2 className="w-4 h-4 text-brand flex-shrink-0 mt-0.5" />
              <div>
                <span className="font-semibold text-neutral-primary block">Company Members &amp; Drivers:</span>
                Contact your transport company administrator to regenerate your credentials or re-assign your seat.
              </div>
            </div>
            <div className="flex items-start gap-2.5 p-3 rounded-xl bg-neutral-appBg border border-neutral-border">
              <Info className="w-4 h-4 text-brand flex-shrink-0 mt-0.5" />
              <div>
                <span className="font-semibold text-neutral-primary block">Customer Accounts:</span>
                If you have forgotten your password during development or evaluation, register a new customer account or use one of the pre-seeded demo accounts on the sign-in page.
              </div>
            </div>
          </div>

          <div className="pt-2">
            <Button variant="primary" size="md" asChild className="w-full" leftIcon={<ArrowLeft className="w-4 h-4" />}>
              <Link to="/login">Return to Sign In</Link>
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
};
