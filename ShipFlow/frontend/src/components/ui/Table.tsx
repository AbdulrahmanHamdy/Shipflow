import React from 'react';
import { cn } from '@/lib/utils';

export const Table: React.FC<React.TableHTMLAttributes<HTMLTableElement>> = ({ className, children, ...props }) => {
  return (
    <div className="w-full overflow-x-auto rounded-xl border border-neutral-border bg-white shadow-card">
      <table className={cn('w-full text-left text-sm text-neutral-primary border-collapse', className)} {...props}>
        {children}
      </table>
    </div>
  );
};

export const TableHeader: React.FC<React.HTMLAttributes<HTMLTableSectionElement>> = ({ className, children, ...props }) => {
  return (
    <thead className={cn('bg-neutral-appBg/60 text-xs uppercase font-semibold text-neutral-secondary border-b border-neutral-border', className)} {...props}>
      {children}
    </thead>
  );
};

export const TableBody: React.FC<React.HTMLAttributes<HTMLTableSectionElement>> = ({ className, children, ...props }) => {
  return (
    <tbody className={cn('divide-y divide-neutral-border/60', className)} {...props}>
      {children}
    </tbody>
  );
};

export const TableRow: React.FC<React.HTMLAttributes<HTMLTableRowElement>> = ({ className, children, ...props }) => {
  return (
    <tr className={cn('hover:bg-neutral-hover/50 transition-colors', className)} {...props}>
      {children}
    </tr>
  );
};

export const TableHead: React.FC<React.ThHTMLAttributes<HTMLTableCellElement>> = ({ className, children, ...props }) => {
  return (
    <th className={cn('px-4 py-3 font-semibold tracking-wider text-xs', className)} {...props}>
      {children}
    </th>
  );
};

export const TableCell: React.FC<React.TdHTMLAttributes<HTMLTableCellElement>> = ({ className, children, ...props }) => {
  return (
    <td className={cn('px-4 py-3.5 align-middle', className)} {...props}>
      {children}
    </td>
  );
};
