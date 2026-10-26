import React, { useEffect, useState } from 'react';
import { useLocation, Link, useNavigate } from 'react-router-dom';
import { usePaymentRedirectCallbackQuery } from '@/features/payments/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Skeleton } from '@/components/ui/Skeleton';
import { CheckCircle2, AlertCircle, Clock, RefreshCw, ArrowRight } from 'lucide-react';

export const PaymentCallbackPage: React.FC = () => {
  usePageTitle('Payment Verification');
  const location = useLocation();
  const navigate = useNavigate();

  // Extract query string without leading '?'
  const queryString = location.search.startsWith('?') ? location.search.slice(1) : location.search;

  const { data, isLoading, isError, error, refetch, isFetching } = usePaymentRedirectCallbackQuery(queryString, !!queryString);

  const [pollCount, setPollCount] = useState(0);

  // Poll status briefly (max 3 times with 4-second delay) if response is not yet final
  useEffect(() => {
    if (!data?.success && pollCount < 3 && !isLoading && !isError) {
      const timer = setTimeout(() => {
        setPollCount((prev) => prev + 1);
        refetch();
      }, 4000);
      return () => clearTimeout(timer);
    }
  }, [data, pollCount, isLoading, isError, refetch]);

  return (
    <div className="max-w-xl mx-auto py-12 px-4 space-y-6">
      <Card className="border border-neutral-border bg-white shadow-panel overflow-hidden">
        {isLoading ? (
          <div className="p-8 text-center space-y-4">
            <div className="w-12 h-12 border-3 border-brand border-t-transparent rounded-full animate-spin mx-auto" />
            <h3 className="text-lg font-bold text-neutral-primary">Verifying Payment with Paymob Gateway...</h3>
            <p className="text-xs text-neutral-secondary">
              Validating transaction signatures and server webhook confirmation. Please do not close this window.
            </p>
          </div>
        ) : data?.success ? (
          <>
            <div className="bg-forest-night text-white p-6 text-center space-y-2 border-b border-forest-border">
              <div className="w-14 h-14 rounded-2xl bg-emerald-500/20 text-emerald-400 flex items-center justify-center mx-auto border border-emerald-400/30">
                <CheckCircle2 className="w-8 h-8" />
              </div>
              <h2 className="text-xl font-black">Payment Verified Successfully!</h2>
              <p className="text-xs text-emerald-300">Transaction verified and recorded by server backend.</p>
            </div>

            <CardContent className="p-6 space-y-4 text-xs text-neutral-secondary">
              <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-950 space-y-1">
                <span className="font-bold block">Gateway Response:</span>
                <p>{data.message || 'Payment has been processed and credited to the transport booking.'}</p>
              </div>

              <p className="leading-relaxed">
                Your payment confirmation email has been dispatched. Carrier dispatch and shipment scheduling can now proceed.
              </p>
            </CardContent>

            <CardFooter className="bg-neutral-appBg/60 flex items-center justify-end p-4">
              <Button size="md" variant="primary" asChild rightIcon={<ArrowRight className="w-4 h-4" />}>
                <Link to="/app/shipments">Return to My Shipments</Link>
              </Button>
            </CardFooter>
          </>
        ) : (
          <>
            <div className="bg-amber-50 text-amber-950 p-6 text-center space-y-2 border-b border-amber-200">
              <div className="w-14 h-14 rounded-2xl bg-amber-100 text-amber-700 flex items-center justify-center mx-auto border border-amber-300">
                <Clock className="w-8 h-8 animate-pulse" />
              </div>
              <h2 className="text-xl font-black">Transaction Still Processing</h2>
              <p className="text-xs text-amber-800">
                The transaction is awaiting confirmation from Paymob or banking networks.
              </p>
            </div>

            <CardContent className="p-6 space-y-4 text-xs text-neutral-secondary">
              <p className="leading-relaxed">
                If you completed payment in the Paymob checkout frame, webhook confirmation may take a few moments to reconcile.
              </p>

              <div className="flex items-center justify-center pt-2">
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => refetch()}
                  isLoading={isFetching}
                  leftIcon={<RefreshCw className="w-3.5 h-3.5" />}
                >
                  Check Verification Again
                </Button>
              </div>
            </CardContent>

            <CardFooter className="bg-neutral-appBg/60 flex items-center justify-between p-4">
              <Button size="sm" variant="ghost" asChild>
                <Link to="/app">Dashboard</Link>
              </Button>
              <Button size="sm" variant="primary" asChild rightIcon={<ArrowRight className="w-4 h-4" />}>
                <Link to="/app/shipments">View Shipments</Link>
              </Button>
            </CardFooter>
          </>
        )}
      </Card>
    </div>
  );
};
