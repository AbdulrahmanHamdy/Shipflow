import { describe, it, expect } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { ShipmentsListPage } from './ShipmentsListPage';
import { renderWithProviders } from '@/test/test-utils';
import { server } from '@/test/mocks/server';
import { http, HttpResponse } from 'msw';

describe('ShipmentsListPage', () => {
  it('renders shipments table with loaded shipment from API', async () => {
    renderWithProviders(<ShipmentsListPage />);

    expect(screen.getByRole('heading', { name: /shipments/i })).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getAllByText(/REF-2026-0001/).length).toBeGreaterThan(0);
      expect(screen.getAllByText(/100 Industrial Pkwy/i).length).toBeGreaterThan(0);
      expect(screen.getAllByText(/500 Logistics Ave/i).length).toBeGreaterThan(0);
    });
  });

  it('renders empty state when no shipments match criteria', async () => {
    server.use(
      http.get('*/api/shipments', () => {
        return HttpResponse.json({
          items: [],
          totalCount: 0,
          page: 1,
          pageSize: 20,
          totalPages: 0,
        });
      })
    );

    renderWithProviders(<ShipmentsListPage />);

    await waitFor(() => {
      expect(screen.getByText(/no consignments found/i)).toBeInTheDocument();
    });
  });

  it('renders error state when API request fails', async () => {
    server.use(
      http.get('*/api/shipments', () => {
        return HttpResponse.json(
          { title: 'Server Error', status: 500, detail: 'Failed to query database.' },
          { status: 500 }
        );
      })
    );

    renderWithProviders(<ShipmentsListPage />);

    await waitFor(() => {
      expect(screen.getByText(/unable to load data/i)).toBeInTheDocument();
    });
  });
});
