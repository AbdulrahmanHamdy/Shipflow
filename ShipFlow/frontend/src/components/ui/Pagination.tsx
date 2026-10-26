import React from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from './Button';

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  onPageChange: (newPage: number) => void;
  className?: string;
}

export const Pagination: React.FC<PaginationProps> = ({
  page,
  totalPages,
  totalCount,
  pageSize,
  onPageChange,
  className = '',
}) => {
  if (totalPages <= 1 && totalCount <= pageSize) {
    return null;
  }

  const startItem = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const endItem = Math.min(page * pageSize, totalCount);

  return (
    <div className={`flex flex-col sm:flex-row items-center justify-between gap-3 py-3 px-1 text-sm text-neutral-secondary ${className}`}>
      <div>
        Showing <span className="font-semibold text-neutral-primary">{startItem}</span> to{' '}
        <span className="font-semibold text-neutral-primary">{endItem}</span> of{' '}
        <span className="font-semibold text-neutral-primary">{totalCount}</span> results
      </div>

      <div className="flex items-center gap-1.5">
        <Button
          variant="outline"
          size="sm"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
          aria-label="Previous page"
          leftIcon={<ChevronLeft className="w-4 h-4" />}
        >
          Previous
        </Button>

        <span className="px-3 py-1 text-xs font-semibold text-neutral-primary bg-neutral-hover rounded-md">
          {page} / {Math.max(1, totalPages)}
        </span>

        <Button
          variant="outline"
          size="sm"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
          aria-label="Next page"
          rightIcon={<ChevronRight className="w-4 h-4" />}
        >
          Next
        </Button>
      </div>
    </div>
  );
};
