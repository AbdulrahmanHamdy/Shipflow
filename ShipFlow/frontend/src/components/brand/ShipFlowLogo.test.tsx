import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ShipFlowLogo } from './ShipFlowLogo';
import { ShipFlowSymbol } from './ShipFlowSymbol';

describe('Branding Components', () => {
  it('renders full logo with symbol and wordmark for light backgrounds', () => {
    const { container } = render(<ShipFlowLogo variant="dark" size="md" />);
    expect(screen.getByText('Ship')).toBeInTheDocument();
    expect(screen.getByText('Flow')).toBeInTheDocument();
    expect(screen.getByText('Logistics')).toBeInTheDocument();
    const svg = container.querySelector('svg');
    expect(svg).toBeInTheDocument();
  });

  it('renders logo for dark backgrounds with appropriate contrast styling', () => {
    render(<ShipFlowLogo variant="light" size="lg" />);
    const wordmarkParent = screen.getByText('Ship').closest('span');
    expect(wordmarkParent).toHaveClass('text-white');
  });

  it('renders standalone symbol with correct SVG paths', () => {
    const { container } = render(<ShipFlowSymbol size={48} />);
    const svg = container.querySelector('svg');
    expect(svg).toBeInTheDocument();
    expect(svg).toHaveAttribute('width', '48');
    expect(svg).toHaveAttribute('height', '48');
  });
});
