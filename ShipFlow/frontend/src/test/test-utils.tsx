import React, { PropsWithChildren } from 'react';
import { render, RenderOptions } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, MemoryRouterProps } from 'react-router-dom';
import { AuthProvider } from '@/features/auth/AuthContext';
import { ToastProvider } from '@/components/ui/Toast';

export function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: Infinity,
      },
      mutations: {
        retry: false,
      },
    },
  });
}

interface WrapperProps extends PropsWithChildren {
  routerProps?: MemoryRouterProps;
  queryClient?: QueryClient;
}

export function AllTheProviders({
  children,
  routerProps = {},
  queryClient = createTestQueryClient(),
}: WrapperProps) {
  return (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter {...routerProps}>
        <ToastProvider>
          <AuthProvider>{children}</AuthProvider>
        </ToastProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

export function renderWithProviders(
  ui: React.ReactElement,
  options?: Omit<RenderOptions, 'wrapper'> & {
    routerProps?: MemoryRouterProps;
    queryClient?: QueryClient;
  }
) {
  const { routerProps, queryClient, ...renderOptions } = options || {};
  return {
    ...render(ui, {
      wrapper: ({ children }) => (
        <AllTheProviders routerProps={routerProps} queryClient={queryClient}>
          {children}
        </AllTheProviders>
      ),
      ...renderOptions,
    }),
    queryClient: queryClient || createTestQueryClient(),
  };
}
