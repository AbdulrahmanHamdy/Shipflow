import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';
import { mapApiError } from './errorMapper';

// In-memory token storage (preferred for XSS resilience)
let currentAccessToken: string | null = null;
let unauthorizedListener: (() => void) | null = null;

// Local storage key for optional session restoration fallback
const TOKEN_STORAGE_KEY = 'shipflow_auth_token';

// Initialize from session/localStorage if available
try {
  const persisted = localStorage.getItem(TOKEN_STORAGE_KEY);
  if (persisted) {
    currentAccessToken = persisted;
  }
} catch {
  // Ignore storage access errors in private/sandboxed modes
}

export function setAccessToken(token: string | null): void {
  currentAccessToken = token;
  try {
    if (token) {
      localStorage.setItem(TOKEN_STORAGE_KEY, token);
    } else {
      localStorage.removeItem(TOKEN_STORAGE_KEY);
    }
  } catch {
    // Ignore
  }
}

export function getAccessToken(): string | null {
  return currentAccessToken;
}

export const tokenStore = {
  getToken: getAccessToken,
  setToken: setAccessToken,
  clear: () => setAccessToken(null),
};

export function setUnauthorizedListener(listener: (() => void) | null): void {
  unauthorizedListener = listener;
}

const baseURL = import.meta.env.VITE_API_BASE_URL || '';

export const apiClient = axios.create({
  baseURL,
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json, application/problem+json',
  },
  timeout: 20000,
});

apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = getAccessToken();
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    // 401 Unauthorized handling
    if (error.response?.status === 401) {
      // If we had a token and got 401, token is expired or revoked
      if (currentAccessToken) {
        setAccessToken(null);
        if (unauthorizedListener) {
          unauthorizedListener();
        }
      }
    }

    const appError = mapApiError(error);
    return Promise.reject(appError);
  }
);
