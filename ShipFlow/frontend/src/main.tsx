import React, { useEffect } from 'react';
import ReactDOM from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { queryClient } from '@/lib/queryClient';
import { AuthProvider } from '@/features/auth/AuthContext';
import { ToastProvider } from '@/components/ui/Toast';
import { ErrorBoundary } from '@/components/ui/ErrorBoundary';
import { initializeAppBoot } from '@/lib/boot';
import App from './App';
import './index.css';

const RootComponent: React.FC = () => {
  useEffect(() => {
    initializeAppBoot();
  }, []);

  return (
    <React.StrictMode>
      <ErrorBoundary>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <ToastProvider>
              <App />
            </ToastProvider>
          </AuthProvider>
        </QueryClientProvider>
      </ErrorBoundary>
    </React.StrictMode>
  );
};

ReactDOM.createRoot(document.getElementById('root')!).render(<RootComponent />);
