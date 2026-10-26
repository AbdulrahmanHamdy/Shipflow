import { describe, it, expect } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { AuthProvider, useAuth } from './AuthContext';
import { tokenStore } from '@/api/client';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AuthProvider>{children}</AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('AuthContext', () => {
  it('initializes with null user when no token exists', async () => {
    const { result } = renderHook(() => useAuth(), { wrapper: createWrapper() });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.isAuthenticated).toBe(false);
    expect(result.current.user).toBeNull();
  });

  it('restores session when valid token exists in storage', async () => {
    tokenStore.setToken('initial-valid-token');

    const { result } = renderHook(() => useAuth(), { wrapper: createWrapper() });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.user?.email).toBe('customer@shipflow.local');
    expect(result.current.hasRole('Customer')).toBe(true);
  });

  it('handles login successfully and updates state and token', async () => {
    const { result } = renderHook(() => useAuth(), { wrapper: createWrapper() });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    await act(async () => {
      await result.current.login({
        email: 'customer@shipflow.local',
        password: 'ValidPassword123!',
      });
    });

    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.user?.email).toBe('customer@shipflow.local');
    expect(tokenStore.getToken()).toBe('mock-valid-jwt-token-xyz');
  });

  it('throws an error on invalid credentials', async () => {
    const { result } = renderHook(() => useAuth(), { wrapper: createWrapper() });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    await expect(
      result.current.login({
        email: 'customer@shipflow.local',
        password: 'WrongPassword!1',
      })
    ).rejects.toBeDefined();

    expect(result.current.isAuthenticated).toBe(false);
  });

  it('clears state on logout', async () => {
    tokenStore.setToken('valid-token');
    const { result } = renderHook(() => useAuth(), { wrapper: createWrapper() });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.isAuthenticated).toBe(true);

    act(() => {
      result.current.logout();
    });

    expect(result.current.isAuthenticated).toBe(false);
    expect(result.current.user).toBeNull();
    expect(tokenStore.getToken()).toBeNull();
  });
});
