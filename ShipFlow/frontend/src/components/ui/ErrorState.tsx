import React from 'react';
import { Button } from './Button';
import { AlertOctagon, RefreshCw } from 'lucide-react';
import { cn } from '@/lib/utils';

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
