import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { useAuth } from '@/features/auth/AuthContext';
import { usePageTitle } from '@/hooks/usePageTitle';
import { ShipFlowLogo } from '@/components/brand';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { FormField } from '@/components/ui/FormField';
import { useToast } from '@/components/ui/Toast';
import { AppError } from '@/api/types';
import { User, Mail, Lock, AlertTriangle, ArrowRight, Check } from 'lucide-react';

const registerSchema = z
  .object({
    fullName: z
      .string()
      .min(1, 'Full name is required')
      .max(150, 'Full name cannot exceed 150 characters'),
    email: z
      .string()
      .min(1, 'Email is required')
      .max(256, 'Email cannot exceed 256 characters')
      .email('Please enter a valid email address'),
    password: z
      .string()
      .min(10, 'Password must be at least 10 characters')
      .max(128, 'Password cannot exceed 128 characters')
      .regex(/[a-z]/, 'Password must contain a lowercase letter')
      .regex(/[A-Z]/, 'Password must contain an uppercase letter')
      .regex(/[0-9]/, 'Password must contain a digit'),
    confirmPassword: z.string().min(1, 'Please confirm your password'),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  });

type RegisterFormData = z.infer<typeof registerSchema>;

export const RegisterPage: React.FC = () => {
  usePageTitle('Create Customer Account');
  const navigate = useNavigate();
  const { register: registerUser } = useAuth();
  const toast = useToast();

  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      fullName: '',
      email: '',
      password: '',
      confirmPassword: '',
    },
  });

  const pwd = watch('password', '');
  const hasMinLength = pwd.length >= 10;
  const hasLower = /[a-z]/.test(pwd);
  const hasUpper = /[A-Z]/.test(pwd);
  const hasDigit = /[0-9]/.test(pwd);

  const onSubmit = async (data: RegisterFormData) => {
    setServerError(null);

    try {
      await registerUser({
        fullName: data.fullName.trim(),
        email: data.email.trim(),
        password: data.password,
      });

      toast.success('Account Created', 'Your Customer account is ready. Please sign in to proceed.');
      navigate('/login');
    } catch (err: unknown) {
      const appErr = err as AppError;
      if (appErr.status === 409) {
        setServerError('An account with this email address already exists.');
      } else {
        setServerError(appErr.message || 'Registration failed. Please check your details.');
      }
    }
  };

  return (
    <div className="min-h-screen bg-neutral-appBg flex flex-col justify-center py-12 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md text-center space-y-4">
        <Link to="/" className="inline-block">
          <ShipFlowLogo size="xl" variant="dark" />
        </Link>
        <h2 className="text-2xl font-black text-neutral-primary tracking-tight">
          Create Customer Account
        </h2>
        <p className="text-xs text-neutral-secondary">
          Self-registration creates a Customer account for creating consignments and managing shipments.
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
            <FormField label="Full Name" required error={errors.fullName?.message} htmlFor="fullName">
              <Input
                id="fullName"
                type="text"
                placeholder="Jane Doe"
                disabled={isSubmitting}
                leftIcon={<User className="w-4 h-4" />}
                {...register('fullName')}
              />
            </FormField>

            <FormField label="Email Address" required error={errors.email?.message} htmlFor="email">
              <Input
                id="email"
                type="email"
                placeholder="jane@company.com"
                disabled={isSubmitting}
                leftIcon={<Mail className="w-4 h-4" />}
                {...register('email')}
              />
            </FormField>

            <FormField label="Password" required error={errors.password?.message} htmlFor="password">
              <Input
                id="password"
                type="password"
                placeholder="••••••••••••"
                disabled={isSubmitting}
                leftIcon={<Lock className="w-4 h-4" />}
                {...register('password')}
              />
            </FormField>

            {/* Live Password Rules Indicator */}
            <div className="p-3 rounded-xl bg-neutral-appBg border border-neutral-border space-y-1.5 text-[11px]">
              <span className="font-semibold text-neutral-primary block mb-1">Password Requirements:</span>
              <div className="grid grid-cols-2 gap-1 text-neutral-secondary">
                <span className={`flex items-center gap-1 ${hasMinLength ? 'text-brand font-semibold' : ''}`}>
                  <Check className={`w-3 h-3 ${hasMinLength ? 'text-brand' : 'opacity-30'}`} /> 10+ characters
                </span>
                <span className={`flex items-center gap-1 ${hasLower ? 'text-brand font-semibold' : ''}`}>
                  <Check className={`w-3 h-3 ${hasLower ? 'text-brand' : 'opacity-30'}`} /> Lowercase letter
                </span>
                <span className={`flex items-center gap-1 ${hasUpper ? 'text-brand font-semibold' : ''}`}>
                  <Check className={`w-3 h-3 ${hasUpper ? 'text-brand' : 'opacity-30'}`} /> Uppercase letter
                </span>
                <span className={`flex items-center gap-1 ${hasDigit ? 'text-brand font-semibold' : ''}`}>
                  <Check className={`w-3 h-3 ${hasDigit ? 'text-brand' : 'opacity-30'}`} /> At least 1 number
                </span>
              </div>
            </div>

            <FormField label="Confirm Password" required error={errors.confirmPassword?.message} htmlFor="confirmPassword">
              <Input
                id="confirmPassword"
                type="password"
                placeholder="••••••••••••"
                disabled={isSubmitting}
                leftIcon={<Lock className="w-4 h-4" />}
                {...register('confirmPassword')}
              />
            </FormField>

            <div className="text-[11px] text-neutral-secondary leading-relaxed pt-1">
              By creating an account, you agree to our{' '}
              <Link to="/terms" className="font-semibold text-brand hover:underline">
                Terms of Service
              </Link>{' '}
              and{' '}
              <Link to="/privacy" className="font-semibold text-brand hover:underline">
                Privacy Policy
              </Link>.
            </div>

            <Button
              type="submit"
              variant="primary"
              size="md"
              className="w-full mt-2"
              isLoading={isSubmitting}
              rightIcon={<ArrowRight className="w-4 h-4" />}
            >
              Create Account
            </Button>
          </form>

          <div className="text-center text-xs text-neutral-secondary pt-2 border-t border-neutral-border">
            Already have an account?{' '}
            <Link to="/login" className="font-bold text-brand hover:underline">
              Sign In
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
};
