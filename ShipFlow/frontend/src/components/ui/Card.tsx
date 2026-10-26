import React from 'react';
import { cn } from '@/lib/utils';

export interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?: 'default' | 'flat' | 'forest';
}

export const Card: React.FC<CardProps> = ({ className, variant = 'default', children, ...props }) => {
  const variants = {
    default: 'bg-white border border-neutral-border shadow-card rounded-xl',
    flat: 'bg-white border border-neutral-border rounded-xl',
    forest: 'bg-forest-night border border-forest-border text-white rounded-xl shadow-panel',
  };

  return (
    <div className={cn(variants[variant], className)} {...props}>
      {children}
    </div>
  );
};

export const CardHeader: React.FC<React.HTMLAttributes<HTMLDivElement>> = ({ className, children, ...props }) => {
  return (
    <div className={cn('px-5 py-4 border-b border-neutral-border/60 flex items-center justify-between', className)} {...props}>
      {children}
    </div>
  );
};

export const CardTitle: React.FC<React.HTMLAttributes<HTMLHeadingElement>> = ({ className, children, ...props }) => {
  return (
    <h3 className={cn('text-base font-bold text-neutral-primary tracking-tight', className)} {...props}>
      {children}
    </h3>
  );
};

export const CardDescription: React.FC<React.HTMLAttributes<HTMLParagraphElement>> = ({
  className,
  children,
  ...props
}) => {
  return (
    <p className={cn('text-xs text-neutral-secondary mt-0.5', className)} {...props}>
      {children}
    </p>
  );
};

export const CardContent: React.FC<React.HTMLAttributes<HTMLDivElement>> = ({ className, children, ...props }) => {
  return (
    <div className={cn('p-5', className)} {...props}>
      {children}
    </div>
  );
};

export const CardFooter: React.FC<React.HTMLAttributes<HTMLDivElement>> = ({ className, children, ...props }) => {
  return (
    <div className={cn('px-5 py-3 border-t border-neutral-border/60 bg-neutral-appBg/50 rounded-b-xl flex items-center justify-between', className)} {...props}>
      {children}
    </div>
  );
};
