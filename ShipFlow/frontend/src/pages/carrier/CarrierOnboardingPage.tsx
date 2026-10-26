import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useCreateCompanyMutation } from '@/features/carrier/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { useAuth } from '@/features/auth/AuthContext';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { FormField } from '@/components/ui/FormField';
import { Input } from '@/components/ui/Input';
import { useToast } from '@/components/ui/Toast';
import { AppError } from '@/api/types';
import { Building2, ArrowRight, ShieldCheck, CheckCircle2 } from 'lucide-react';

export const CarrierOnboardingPage: React.FC = () => {
  usePageTitle('Register Transport Carrier');
  const navigate = useNavigate();
  const toast = useToast();
  const { logout } = useAuth();
  const createMutation = useCreateCompanyMutation();

  const [companyName, setCompanyName] = useState('');
  const [createdCompany, setCreatedCompany] = useState<boolean>(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!companyName.trim()) {
      toast.error('Name required', 'Please provide a registered business or carrier name.');
      return;
    }

    try {
      await createMutation.mutateAsync({ name: companyName.trim() });
      setCreatedCompany(true);
      toast.success('Company Established', 'Your transport company has been registered in the database.');
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Registration failed', appErr.message);
    }
  };

  if (createdCompany) {
    return (
      <div className="max-w-xl mx-auto py-12 space-y-6">
        <Card className="border-2 border-brand bg-white shadow-panel p-8 text-center space-y-4">
          <div className="w-16 h-16 rounded-2xl bg-brand-mint text-brand-mintText flex items-center justify-center mx-auto">
            <CheckCircle2 className="w-8 h-8" />
          </div>

          <h2 className="text-2xl font-black text-neutral-primary">Company Registered Successfully!</h2>

          <div className="p-4 rounded-xl bg-neutral-appBg border border-neutral-border text-xs text-neutral-secondary text-left space-y-2 leading-relaxed">
            <p>
              <strong>Security Protocol Notice (D-023):</strong> You have been granted the <code>CompanyAdmin</code> role in the database.
            </p>
            <p>
              In accordance with stateless JWT security, token claims are fixed upon creation. You must sign out and sign in again to receive your new CompanyAdmin permissions in a fresh token.
            </p>
          </div>

          <div className="pt-2">
            <Button
              size="lg"
              variant="primary"
              className="w-full"
              onClick={() => {
                logout();
                navigate('/login');
              }}
              rightIcon={<ArrowRight className="w-5 h-5" />}
            >
              Sign In Again to Activate Role
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div className="max-w-xl mx-auto space-y-6">
      <PageHeader
        title="Establish Transport Carrier Organization"
        description="Register your fleet business entity to browse open freight bids and dispatch drivers."
        breadcrumbs={[{ label: 'Dashboard', href: '/app' }, { label: 'Register Carrier' }]}
      />

      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Building2 className="w-5 h-5 text-brand" />
            <CardTitle>Carrier Business Details</CardTitle>
          </div>
          <CardDescription>You will become the designated Company Administrator</CardDescription>
        </CardHeader>

        <form onSubmit={handleSubmit}>
          <CardContent className="space-y-4">
            <FormField
              label="Legal Company Name"
              required
              hint="Your logistics fleet or transport organization brand name"
              htmlFor="compName"
            >
              <Input
                id="compName"
                placeholder="e.g. Nile Valley Express Freight"
                value={companyName}
                onChange={(e) => setCompanyName(e.target.value)}
              />
            </FormField>

            <div className="p-3.5 rounded-xl bg-neutral-appBg border border-neutral-border text-xs text-neutral-secondary space-y-1.5 leading-relaxed">
              <span className="font-bold text-neutral-primary flex items-center gap-1.5">
                <ShieldCheck className="w-4 h-4 text-brand" /> Company Admin Permissions
              </span>
              <p>
                As CompanyAdmin, you will be able to invite dispatchers, onboard drivers, register vehicles, and submit binding freight quotes on the platform exchange.
              </p>
            </div>
          </CardContent>

          <CardFooter>
            <Button variant="outline" size="sm" asChild>
              <Link to="/app">Cancel</Link>
            </Button>
            <Button
              type="submit"
              variant="primary"
              size="md"
              isLoading={createMutation.isPending}
              rightIcon={<ArrowRight className="w-4 h-4" />}
            >
              Register &amp; Establish
            </Button>
          </CardFooter>
        </form>
      </Card>
    </div>
  );
};
