import { describe, it, expect } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { DriverCurrentJobPage } from './DriverCurrentJobPage';
import { renderWithProviders } from '@/test/test-utils';
import { server } from '@/test/mocks/server';
import { http, HttpResponse } from 'msw';

describe('DriverCurrentJobPage', () => {
  it('renders active trip mission when driver has an assigned shipment', async () => {
    renderWithProviders(<DriverCurrentJobPage />);

    await waitFor(() => {
      expect(screen.getByText('REF-2026-0001')).toBeInTheDocument();
      expect(screen.getByText(/100 Industrial Pkwy, Chicago, IL/i)).toBeInTheDocument();
      expect(screen.getByText(/500 Logistics Ave, Indianapolis, IN/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /confirm cargo pickup/i })).toBeInTheDocument();
    });
  });

  it('renders idle state when driver has no active assignment', async () => {
    server.use(
      http.get('*/api/driver/assignments/current', () => {
        return new HttpResponse(null, { status: 204 });
      })
    );

    renderWithProviders(<DriverCurrentJobPage />);

    await waitFor(() => {
      expect(screen.getByText(/no active delivery assignment/i)).toBeInTheDocument();
      expect(screen.getByText(/you do not have a currently active trip/i)).toBeInTheDocument();
    });
  });
});
