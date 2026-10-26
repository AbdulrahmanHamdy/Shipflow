import '@testing-library/jest-dom';
import { server } from './test/mocks/server';
import { tokenStore } from './api/client';

// Establish API mocking before all tests
beforeAll(() => {
  server.listen({ onUnhandledRequest: 'bypass' });
});

// Reset any request handlers that we may add during the tests,
// and clear memory tokenStore
afterEach(() => {
  server.resetHandlers();
  tokenStore.clear();
  localStorage.clear();
});

// Clean up once the tests are finished
afterAll(() => {
  server.close();
});

// Polyfill window.matchMedia for jsdom
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});

// Polyfill ResizeObserver
global.ResizeObserver = class ResizeObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
};

// Polyfill IntersectionObserver
global.IntersectionObserver = class IntersectionObserver {
  root = null;
  rootMargin = '';
  thresholds = [];
  observe() {}
  unobserve() {}
  disconnect() {}
  takeRecords() {
    return [];
  }
} as any;
