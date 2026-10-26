import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatusBadge } from './StatusBadge';
import { ShipmentStatus } from '@/api/types';

describe('StatusBadge', () => {
  const statuses: ShipmentStatus[] = [
    'Pending',
    'Quoting',
    'Accepted',
    'Assigned',
    'PickedUp',
    'InTransit',
    'Delivered',
    'DeliveryFailed',
    'Cancelled',
  ];

  it.each(statuses)('renders correct label and icon for status "%s"', (status) => {
    const { container } = render(<StatusBadge status={status} />);
    const badge = container.querySelector('span');
    expect(badge).toBeInTheDocument();
    expect(badge?.textContent).toBeTruthy();
  });

  it('renders Delivered state with checkmark icon and green accent tint', () => {
    const { container } = render(<StatusBadge status="Delivered" />);
    expect(screen.getByText('Delivered')).toBeInTheDocument();
    const svg = container.querySelector('svg');
    expect(svg).toBeInTheDocument();
  });

  it('renders InTransit state with moving arrow icon and active styling', () => {
    const { container } = render(<StatusBadge status="InTransit" />);
    expect(screen.getByText('In Transit')).toBeInTheDocument();
    const svg = container.querySelector('svg');
    expect(svg).toBeInTheDocument();
  });

  it('renders DeliveryFailed state with red warning tint and cross icon', () => {
    const { container } = render(<StatusBadge status="DeliveryFailed" />);
    expect(screen.getByText(/delivery.*failed/i)).toBeInTheDocument();
    const svg = container.querySelector('svg');
    expect(svg).toBeInTheDocument();
  });
});
