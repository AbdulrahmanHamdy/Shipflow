import React from 'react';
import { cn } from '@/lib/utils';
import { CheckCircle2, Circle, Clock } from 'lucide-react';

export interface TimelineEvent {
  id: string;
  title: string;
  description?: string;
  timestamp?: string;
  isCompleted?: boolean;
  isCurrent?: boolean;
}

export interface TimelineProps {
  events: TimelineEvent[];
  className?: string;
}

export const Timeline: React.FC<TimelineProps> = ({ events, className }) => {
  return (
    <div className={cn('relative pl-6 space-y-6', className)}>
      <div className="absolute left-[11px] top-2 bottom-2 w-0.5 bg-neutral-border" />
      {events.map((evt, idx) => {
        return (
          <div key={evt.id || idx} className="relative flex items-start gap-3">
            <div
              className={cn(
                'absolute -left-6 mt-0.5 flex items-center justify-center w-6 h-6 rounded-full bg-white border-2 transition-colors',
                evt.isCompleted
                  ? 'border-emerald-600 text-emerald-600'
                  : evt.isCurrent
                  ? 'border-brand text-brand bg-brand-tint ring-4 ring-brand-tint'
                  : 'border-neutral-muted text-neutral-muted'
              )}
            >
              {evt.isCompleted ? (
                <CheckCircle2 className="w-3.5 h-3.5 fill-emerald-100" />
              ) : evt.isCurrent ? (
                <Clock className="w-3.5 h-3.5" />
              ) : (
                <Circle className="w-2.5 h-2.5" />
              )}
            </div>

            <div className="flex-1">
              <div className="flex items-baseline justify-between gap-2">
                <h4
                  className={cn(
                    'text-sm font-semibold',
                    evt.isCurrent ? 'text-brand' : 'text-neutral-primary'
                  )}
                >
                  {evt.title}
                </h4>
                {evt.timestamp && (
                  <span className="text-xs text-neutral-secondary tabular-nums flex-shrink-0">
                    {evt.timestamp}
                  </span>
                )}
              </div>
              {evt.description && (
                <p className="text-xs text-neutral-secondary mt-0.5">{evt.description}</p>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
};
