import React from 'react';
import { Link } from 'react-router-dom';
import { ChevronRight, Home } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface BreadcrumbItem {
  label: string;
  href?: string;
}

export interface BreadcrumbsProps {
  items: BreadcrumbItem[];
  className?: string;
}

export const Breadcrumbs: React.FC<BreadcrumbsProps> = ({ items, className }) => {
  return (
    <nav aria-label="Breadcrumb" className={cn('flex items-center space-x-1.5 text-xs text-neutral-secondary', className)}>
      <Link to="/" className="hover:text-neutral-primary transition-colors flex items-center">
        <Home className="w-3.5 h-3.5" />
      </Link>
      {items.map((item, idx) => {
        const isLast = idx === items.length - 1;
        return (
          <React.Fragment key={idx}>
            <ChevronRight className="w-3 h-3 text-neutral-muted flex-shrink-0" />
            {isLast || !item.href ? (
              <span className="font-semibold text-neutral-primary truncate max-w-[200px]">
                {item.label}
              </span>
            ) : (
              <Link to={item.href} className="hover:text-neutral-primary transition-colors truncate max-w-[150px]">
                {item.label}
              </Link>
            )}
          </React.Fragment>
        );
      })}
    </nav>
  );
};

export interface PageHeaderProps {
  title: string;
  description?: string;
  breadcrumbs?: BreadcrumbItem[];
  actions?: React.ReactNode;
  badge?: React.ReactNode;
  className?: string;
}

export const PageHeader: React.FC<PageHeaderProps> = ({
  title,
  description,
  breadcrumbs,
  actions,
  badge,
  className,
}) => {
  return (
    <div className={cn('mb-6 space-y-2', className)}>
      {breadcrumbs && <Breadcrumbs items={breadcrumbs} className="mb-2" />}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <h1 className="text-2xl font-black text-neutral-primary tracking-tight">
            {title}
          </h1>
          {badge}
        </div>
        {actions && <div className="flex items-center gap-2.5 flex-wrap">{actions}</div>}
      </div>
      {description && <p className="text-sm text-neutral-secondary">{description}</p>}
    </div>
  );
};
