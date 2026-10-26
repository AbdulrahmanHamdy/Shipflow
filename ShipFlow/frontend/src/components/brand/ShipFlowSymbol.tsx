import React from 'react';

interface ShipFlowSymbolProps {
  size?: number | string;
  className?: string;
  variant?: 'light' | 'dark' | 'brand';
}

export const ShipFlowSymbol: React.FC<ShipFlowSymbolProps> = ({
  size = 32,
  className = '',
  variant = 'light',
}) => {
  const containerStroke = variant === 'dark' ? '#1C543F' : '#123B2B';
  const containerFill = variant === 'dark' ? '#0B2A1F' : 'none';

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 100 100"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={`flex-shrink-0 transition-transform duration-200 ${className}`}
      aria-hidden="true"
    >
      <defs>
        <linearGradient id="sf-symbol-grad" x1="10" y1="90" x2="90" y2="10" gradientUnits="userSpaceOnUse">
          <stop offset="0%" stopColor="#15803D" />
          <stop offset="100%" stopColor="#10B981" />
        </linearGradient>
      </defs>

      {/* Logistics container perimeter */}
      <polygon
        points="50,8 86,28 86,72 50,92 14,72 14,28"
        stroke={containerStroke}
        strokeWidth="6"
        strokeLinejoin="round"
        fill={containerFill}
        opacity={variant === 'dark' ? 0.8 : 0.25}
      />

      {/* Dynamic S-curve routing flow */}
      <path
        d="M 32 30 C 48 18, 70 30, 68 46 C 66 60, 34 52, 32 68 C 30 82, 54 86, 68 76"
        stroke="url(#sf-symbol-grad)"
        strokeWidth="7.5"
        strokeLinecap="round"
        fill="none"
      />

      {/* Waypoint Pins */}
      <circle cx="32" cy="30" r="6" fill="#15803D" stroke="#FFFFFF" strokeWidth="2.5" />
      <circle cx="68" cy="76" r="6" fill="#10B981" stroke="#FFFFFF" strokeWidth="2.5" />
      <circle cx="50" cy="51" r="3.5" fill="#DCFCE7" />
    </svg>
  );
};
