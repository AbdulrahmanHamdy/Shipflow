import React from 'react';
import * as Dialog from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: React.ReactNode;
  maxWidth?: 'sm' | 'md' | 'lg' | 'xl' | '2xl';
}

export const Modal: React.FC<ModalProps> = ({
  isOpen,
  onClose,
  title,
  description,
  children,
  maxWidth = 'md',
}) => {
  const maxWClasses = {
    sm: 'max-w-sm',
    md: 'max-w-md',
    lg: 'max-w-lg',
    xl: 'max-w-xl',
    '2xl': 'max-w-2xl',
  };

  return (
    <Dialog.Root open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 bg-forest-night/60 backdrop-blur-xs z-50 animate-in fade-in duration-200" />
        <Dialog.Content
          className={cn(
            'fixed left-[50%] top-[50%] z-50 w-full translate-x-[-50%] translate-y-[-50%]',
            'bg-white rounded-2xl p-6 shadow-2xl border border-neutral-border',
            'focus:outline-none animate-in zoom-in-95 duration-200',
            maxWClasses[maxWidth],
            'max-h-[90vh] overflow-y-auto'
          )}
        >
          <div className="flex items-start justify-between pb-3 border-b border-neutral-border/60 mb-4">
            <div>
              <Dialog.Title className="text-lg font-bold text-neutral-primary tracking-tight">
                {title}
              </Dialog.Title>
              {description && (
                <Dialog.Description className="text-xs text-neutral-secondary mt-1">
                  {description}
                </Dialog.Description>
              )}
            </div>
            <Dialog.Close asChild>
              <button
                className="rounded-lg p-1 text-neutral-secondary hover:text-neutral-primary hover:bg-neutral-hover transition-colors"
                aria-label="Close"
              >
                <X className="w-5 h-5" />
              </button>
            </Dialog.Close>
          </div>

          <div className="mt-2">{children}</div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
};
