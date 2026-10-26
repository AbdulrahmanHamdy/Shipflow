import React, { forwardRef } from 'react';
import { cn } from '@/lib/utils';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  error?: string;
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ className, error, leftIcon, rightIcon, disabled, ...props }, ref) => {
    return (
      <div className="relative w-full">
        {leftIcon && (
          <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-neutral-secondary">
            {leftIcon}
          </div>
        )}
        <input
          ref={ref}
          disabled={disabled}
          className={cn(
            'flex h-10 w-full rounded-lg border bg-white px-3 py-2 text-sm text-neutral-primary placeholder:text-neutral-muted transition-colors',
            'border-neutral-border hover:border-neutral-muted focus:border-brand focus:outline-none focus:ring-1 focus:ring-brand',
            'disabled:cursor-not-allowed disabled:bg-neutral-hover disabled:text-neutral-muted',
            leftIcon && 'pl-9',
            rightIcon && 'pr-9',
            error && 'border-red-500 focus:border-red-500 focus:ring-red-500',
            className
          )}
          {...props}
        />
        {rightIcon && (
          <div className="absolute inset-y-0 right-0 pr-3 flex items-center pointer-events-none text-neutral-secondary">
            {rightIcon}
          </div>
        )}
      </div>
    );
  }
);

Input.displayName = 'Input';

export interface TextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  error?: string;
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(
  ({ className, error, disabled, rows = 3, ...props }, ref) => {
    return (
      <textarea
        ref={ref}
        rows={rows}
        disabled={disabled}
        className={cn(
          'flex w-full rounded-lg border bg-white px-3 py-2 text-sm text-neutral-primary placeholder:text-neutral-muted transition-colors',
          'border-neutral-border hover:border-neutral-muted focus:border-brand focus:outline-none focus:ring-1 focus:ring-brand',
          'disabled:cursor-not-allowed disabled:bg-neutral-hover disabled:text-neutral-muted',
          error && 'border-red-500 focus:border-red-500 focus:ring-red-500',
          className
        )}
        {...props}
      />
    );
  }
);

Textarea.displayName = 'Textarea';
