import React from 'react';
import { ShipFlowSymbol } from './ShipFlowSymbol';

interface ShipFlowLogoProps {
  size?: 'sm' | 'md' | 'lg' | 'xl';
  variant?: 'light' | 'dark'; // 'light' means light text for dark backgrounds; 'dark' means dark text for light backgrounds
  showWordmark?: boolean;
  className?: string;
}

const sizes = {
  sm: { symbol: 24, text: 'text-base', sub: 'text-[9px]' },
  md: { symbol: 32, text: 'text-lg', sub: 'text-[10px]' },
  lg: { symbol: 40, text: 'text-xl', sub: 'text-[11px]' },
  xl: { symbol: 52, text: 'text-2xl', sub: 'text-xs' },
};

export const ShipFlowLogo: React.FC<ShipFlowLogoProps> = ({
  size = 'md',
  variant = 'dark',
  showWordmark = true,
  className = '',
}) => {
  const currentSize = sizes[size];
  const textColor = variant === 'light' ? 'text-white' : 'text-neutral-primary';
  const subtitleColor = variant === 'light' ? 'text-brand-accent' : 'text-brand-hover';

  return (
    <div className={`inline-flex items-center gap-2.5 select-none ${className}`}>
      <ShipFlowSymbol
        size={currentSize.symbol}
        variant={variant === 'light' ? 'dark' : 'light'}
      />
      {showWordmark && (
        <div className="flex flex-col leading-none">
          <span className={`font-black tracking-tight ${currentSize.text} ${textColor}`}>
            Ship<span className="text-brand-accent">Flow</span>
          </span>
          <span className={`font-semibold uppercase tracking-wider ${currentSize.sub} ${subtitleColor} mt-0.5`}>
            Logistics
          </span>
        </div>
      )}
    </div>
  );
};
