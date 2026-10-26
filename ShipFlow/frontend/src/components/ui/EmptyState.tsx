import React from 'react';
import { Button } from './Button';
import { PackageOpen, AlertOctagon, RefreshCw } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface EmptyStateProps {
  title: string;
  description?: string;
  icon?: React.ComponentType<{ className?: string }>;
  actionLabel?: string;
  onAction?: () => void;
  className?: string;
}

export const EmptyState: React.FC<EmptyStateProps> = ({
  title,
  description,
  icon: Icon = PackageOpen,
  actionLabel,
  onAction,
  className,
}) => {
  return (
    <div className={cn('flex flex-col items-center justify-center p-8 text-center rounded-2xl border-2 border-dashed border-neutral-border bg-white/50', className)}>
      <div className="w-12 h-12 rounded-full bg-neutral-hover flex items-center justify-center text-neutral-secondary mb-3">
        <Icon className="w-6 h-6" />
      </div>
      <h3 className="text-base font-bold text-neutral-primary">{title}</h3>
      {description && <p className="text-xs text-neutral-secondary max-w-sm mt-1 mb-4">{description}</p>}
      {actionLabel && onAction && (
        <Button size="sm" onClick={onAction} variant="secondary">
          {actionLabel}
        </Button>
      )}
    </div>
  );
};

export interface ErrorStateProps {
  title?: string;
  message?: string;
  onRetry?: () => void;
  className?: string;
}

export const ErrorState: React.FC<ErrorStateProps> = ({
  title = 'Unable to load data',
  message = 'A problem occurred while retrieving information from the server.',
  onRetry,
  className,
}) => {
  return (
    <div className={cn('flex flex-col items-center justify-center p-8 text-center rounded-2xl border border-red-200 bg-red-50/40', className)}>
      <div className="w-12 h-12 rounded-full bg-red-100 flex items-center justify-center text-red-600 mb-3">
        <AlertOctagon className="w-6 h-6" />
      </div>
      <h3 className="text-base font-bold text-neutral-primary">{title}</h3>
      <p className="text-xs text-neutral-secondary max-w-sm mt-1 mb-4">{message}</p>
      {onRetry && (
        <Button size="sm" variant="outline" onClick={onRetry} leftIcon={<RefreshCw className="w-3.5 h-3.5" />}>
          Retry Request
        </Button>
      )}
    </div>
  );
};
