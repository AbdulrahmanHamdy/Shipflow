import React from 'react';
import { cn } from '@/lib/utils';

export interface FormFieldProps {
  label?: string;
  required?: boolean;
  error?: string;
  hint?: string;
  children: React.ReactNode;
  className?: string;
  htmlFor?: string;
}

export const FormField: React.FC<FormFieldProps> = ({
  label,
  required,
  error,
  hint,
  children,
  className,
  htmlFor,
}) => {
  return (
    <div className={cn('flex flex-col gap-1.5 w-full', className)}>
      {label && (
        <label htmlFor={htmlFor} className="text-xs font-semibold text-neutral-primary flex items-center gap-1">
          {label}
          {required && <span className="text-red-500 font-bold">*</span>}
        </label>
      )}
      {children}
      {error ? (
        <p className="text-xs text-red-600 font-medium flex items-center gap-1 mt-0.5" role="alert">
          {error}
        </p>
      ) : hint ? (
        <p className="text-xs text-neutral-secondary">{hint}</p>
      ) : null}
    </div>
  );
};
