import React, { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { useAuth } from '@/features/auth/AuthContext';
import { usePageTitle } from '@/hooks/usePageTitle';
import { sanitizeReturnUrl } from '@/lib/utils';
import { ShipFlowLogo } from '@/components/brand';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { FormField } from '@/components/ui/FormField';
import { useToast } from '@/components/ui/Toast';
import { AppError } from '@/api/types';
import { Lock, Mail, AlertTriangle, KeyRound, ArrowRight, ShieldCheck } from 'lucide-react';

const loginSchema = z.object({
  email: z.string().min(1, 'Email is required').email('Please enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
});

type LoginFormData = z.infer<typeof loginSchema>;

export const LoginPage: React.FC = () => {
  usePageTitle('Sign In');
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { login } = useAuth();
  const toast = useToast();

  const [serverError, setServerError] = useState<string | null>(null);
  const [retryAfter, setRetryAfter] = useState<number | null>(null);

  const returnUrl = sanitizeReturnUrl(searchParams.get('returnUrl'), '/app');

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  });

  const onSubmit = async (data: LoginFormData) => {
    setServerError(null);
    setRetryAfter(null);

    try {
      const res = await login({
        email: data.email.trim(),
        password: data.password,
      });

      toast.success('Welcome back', `Signed in as ${res.user.fullName}`);

      // Determine proper role-based route if no specific returnUrl was requested
      if (returnUrl === '/app') {
        if (res.user.roles.includes('CompanyAdmin') || res.user.roles.includes('Dispatcher')) {
          navigate('/carrier');
          return;
        }
        if (res.user.roles.includes('Driver')) {
          navigate('/driver');
          return;
        }
      }

      navigate(returnUrl);
    } catch (err: unknown) {
      const appErr = err as AppError;
      if (appErr.status === 429) {
        setRetryAfter(appErr.retryAfterSeconds || 60);
        setServerError(`Too many attempts. Rate limit active. Please wait ${appErr.retryAfterSeconds || 60}s.`);
      } else if (appErr.status === 401) {
        setServerError('Invalid email or password. Please verify your credentials.');
      } else {
        setServerError(appErr.message || 'Unable to sign in. Please try again.');
      }
    }
  };

  const handleDemoFill = (email: string) => {
    setValue('email', email);
    setValue('password', 'DevPass1234!');
    setServerError(null);
  };

  return (
    <div className="min-h-screen bg-neutral-appBg flex flex-col justify-center py-12 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md text-center space-y-4">
        <Link to="/" className="inline-block">
          <ShipFlowLogo size="xl" variant="dark" />
        </Link>
        <h2 className="text-2xl font-black text-neutral-primary tracking-tight">
          Sign In to Your Workspace
        </h2>
        <p className="text-xs text-neutral-secondary">
          Enter your registered email and password to access your logistics console.
        </p>
      </div>

      <div className="mt-8 sm:mx-auto sm:w-full sm:max-w-md px-4 sm:px-0">
        <div className="bg-white py-8 px-6 sm:px-10 rounded-3xl border border-neutral-border shadow-panel space-y-6">
          {serverError && (
            <div className="p-3.5 rounded-xl bg-red-50 border border-red-200 text-xs text-red-700 flex items-start gap-2.5">
              <AlertTriangle className="w-4 h-4 text-red-600 flex-shrink-0 mt-0.5" />
              <span>{serverError}</span>
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
            <FormField label="Email Address" required error={errors.email?.message} htmlFor="email">
              <Input
                id="email"
                type="email"
                autoComplete="email"
                placeholder="name@company.com"
                disabled={isSubmitting || !!retryAfter}
                leftIcon={<Mail className="w-4 h-4" />}
                {...register('email')}
              />
            </FormField>

            <FormField label="Password" required error={errors.password?.message} htmlFor="password">
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                placeholder="••••••••••••"
                disabled={isSubmitting || !!retryAfter}
                leftIcon={<Lock className="w-4 h-4" />}
                {...register('password')}
              />
            </FormField>

            <div className="flex items-center justify-between text-xs">
              <span className="text-neutral-secondary">Stateless JWT session (15m)</span>
              <Link to="/forgot-password" className="font-semibold text-brand hover:underline">
                Forgot password?
              </Link>
            </div>

            <Button
              type="submit"
              variant="primary"
              size="md"
              className="w-full mt-2"
              isLoading={isSubmitting}
              disabled={!!retryAfter}
              rightIcon={<ArrowRight className="w-4 h-4" />}
            >
              Sign In
            </Button>
          </form>

          {/* Seed Accounts Helper (For local dev / testing) */}
          <div className="pt-4 border-t border-neutral-border/80 space-y-2.5">
            <div className="flex items-center justify-between text-[11px] font-bold text-neutral-secondary uppercase tracking-wider">
              <span className="flex items-center gap-1">
                <KeyRound className="w-3.5 h-3.5 text-brand" /> Demo Seed Accounts
              </span>
              <span className="text-[10px] lowercase text-neutral-muted">click to fill</span>
            </div>

            <div className="grid grid-cols-2 gap-1.5 text-[11px]">
              <button
                type="button"
                onClick={() => handleDemoFill('customer@shipflow.test')}
                className="text-left p-2 rounded-lg bg-neutral-hover hover:bg-brand-mint/40 transition-colors border border-transparent hover:border-brand-accent/30"
              >
                <span className="font-bold text-neutral-primary block">Customer</span>
                <span className="text-[10px] text-neutral-secondary truncate block">customer@shipflow.test</span>
              </button>

              <button
                type="button"
                onClick={() => handleDemoFill('company.admin@shipflow.test')}
                className="text-left p-2 rounded-lg bg-neutral-hover hover:bg-brand-mint/40 transition-colors border border-transparent hover:border-brand-accent/30"
              >
                <span className="font-bold text-neutral-primary block">Carrier Admin</span>
                <span className="text-[10px] text-neutral-secondary truncate block">company.admin@shipflow.test</span>
              </button>

              <button
                type="button"
                onClick={() => handleDemoFill('dispatcher@shipflow.test')}
                className="text-left p-2 rounded-lg bg-neutral-hover hover:bg-brand-mint/40 transition-colors border border-transparent hover:border-brand-accent/30"
              >
                <span className="font-bold text-neutral-primary block">Dispatcher</span>
                <span className="text-[10px] text-neutral-secondary truncate block">dispatcher@shipflow.test</span>
              </button>

              <button
                type="button"
                onClick={() => handleDemoFill('driver@shipflow.test')}
                className="text-left p-2 rounded-lg bg-neutral-hover hover:bg-brand-mint/40 transition-colors border border-transparent hover:border-brand-accent/30"
              >
                <span className="font-bold text-neutral-primary block">Driver</span>
                <span className="text-[10px] text-neutral-secondary truncate block">driver@shipflow.test</span>
              </button>
            </div>
          </div>

          <div className="text-center text-xs text-neutral-secondary pt-2">
            Don't have an account yet?{' '}
            <Link to="/register" className="font-bold text-brand hover:underline">
              Create Customer Account
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
};
