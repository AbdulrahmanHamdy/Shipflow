import React from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/Button';
import { ShipFlowLogo } from '@/components/brand';
import { Compass, Home, ArrowLeft } from 'lucide-react';
import { usePageTitle } from '@/hooks/usePageTitle';

export const NotFoundPage: React.FC = () => {
  usePageTitle('Page Not Found');

  return (
    <div className="min-h-screen bg-neutral-appBg flex flex-col items-center justify-center p-6 text-center select-none">
      <div className="max-w-md w-full bg-white rounded-3xl p-8 border border-neutral-border shadow-panel space-y-6">
        <div className="flex justify-center">
          <ShipFlowLogo size="lg" variant="dark" />
        </div>

        <div className="w-16 h-16 rounded-2xl bg-brand-mint text-brand-mintText flex items-center justify-center mx-auto shadow-inner">
          <Compass className="w-8 h-8 animate-spin" style={{ animationDuration: '8s' }} />
        </div>

        <div className="space-y-2">
          <span className="text-xs font-mono font-bold tracking-wider text-brand uppercase bg-brand-tint px-2.5 py-1 rounded-md">
            HTTP 404
          </span>
          <h1 className="text-2xl font-black text-neutral-primary tracking-tight">
            Route Uncharted
          </h1>
          <p className="text-xs text-neutral-secondary leading-relaxed">
            The page or cargo record you are searching for does not exist, has been archived, or was relocated.
          </p>
        </div>

        <div className="flex items-center justify-center gap-3 pt-2">
          <Button variant="outline" size="sm" onClick={() => window.history.back()} leftIcon={<ArrowLeft className="w-4 h-4" />}>
            Back
          </Button>
          <Button variant="primary" size="sm" asChild leftIcon={<Home className="w-4 h-4" />}>
            <Link to="/">Home Overview</Link>
          </Button>
        </div>
      </div>
    </div>
  );
};
