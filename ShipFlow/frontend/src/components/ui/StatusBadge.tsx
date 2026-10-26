import React from 'react';
import { cn } from '@/lib/utils';
import { ShipmentStatus } from '@/api/types';
import {
  Clock,
  MessageSquare,
  CheckCircle2,
  UserCheck,
  PackageCheck,
  Truck,
  AlertTriangle,
  XCircle,
  HelpCircle,
} from 'lucide-react';

interface StatusBadgeProps {
  status: ShipmentStatus | string;
  size?: 'sm' | 'md';
  className?: string;
}

interface StatusConfig {
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  badgeClass: string;
  iconClass: string;
}

const statusConfigs: Record<ShipmentStatus, StatusConfig> = {
  Pending: {
    label: 'Pending',
    icon: Clock,
    badgeClass: 'bg-amber-50 text-amber-800 border-amber-200',
    iconClass: 'text-amber-600',
  },
  Quoting: {
    label: 'Open for Quotes',
    icon: MessageSquare,
    badgeClass: 'bg-purple-50 text-purple-800 border-purple-200',
    iconClass: 'text-purple-600',
  },
  Accepted: {
    label: 'Quote Accepted',
    icon: CheckCircle2,
    badgeClass: 'bg-sky-50 text-sky-800 border-sky-200',
    iconClass: 'text-sky-600',
  },
  Assigned: {
    label: 'Assigned to Driver',
    icon: UserCheck,
    badgeClass: 'bg-teal-50 text-teal-800 border-teal-200',
    iconClass: 'text-teal-600',
  },
  PickedUp: {
    label: 'Picked Up',
    icon: PackageCheck,
    badgeClass: 'bg-blue-50 text-blue-800 border-blue-200',
    iconClass: 'text-blue-600',
  },
  InTransit: {
    label: 'In Transit',
    icon: Truck,
    badgeClass: 'bg-sky-50 text-sky-800 border-sky-300 font-medium',
    iconClass: 'text-sky-600 animate-pulse',
  },
  DeliveryFailed: {
    label: 'Delivery Attempt Failed',
    icon: AlertTriangle,
    badgeClass: 'bg-red-50 text-red-800 border-red-200',
    iconClass: 'text-red-600',
  },
  Delivered: {
    label: 'Delivered',
    icon: CheckCircle2,
    badgeClass: 'bg-emerald-50 text-emerald-900 border-emerald-300 font-semibold',
    iconClass: 'text-emerald-700',
  },
  Cancelled: {
    label: 'Cancelled',
    icon: XCircle,
    badgeClass: 'bg-stone-100 text-stone-700 border-stone-200',
    iconClass: 'text-stone-500',
  },
};

export const StatusBadge: React.FC<StatusBadgeProps> = ({ status, size = 'sm', className }) => {
  const config =
    statusConfigs[status as ShipmentStatus] || {
      label: status,
      icon: HelpCircle,
      badgeClass: 'bg-gray-100 text-gray-800 border-gray-200',
      iconClass: 'text-gray-500',
    };

  const Icon = config.icon;

  const sizeClasses = {
    sm: 'text-xs px-2.5 py-0.5 gap-1.5',
    md: 'text-sm px-3 py-1 gap-2',
  };

  const iconSizes = {
    sm: 'w-3.5 h-3.5',
    md: 'w-4 h-4',
  };

  return (
    <span
      className={cn(
        'inline-flex items-center font-medium rounded-full border shadow-2xs select-none transition-colors',
        sizeClasses[size],
        config.badgeClass,
        className
      )}
    >
      <Icon className={cn(iconSizes[size], config.iconClass, 'flex-shrink-0')} />
      <span>{config.label}</span>
    </span>
  );
};
