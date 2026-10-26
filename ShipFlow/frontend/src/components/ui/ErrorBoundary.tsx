import React, { Component, ErrorInfo, ReactNode } from 'react';
import { Button } from '@/components/ui/Button';
import { AlertOctagon, RotateCcw } from 'lucide-react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
    error: null,
  };

  public static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('Uncaught error caught by ErrorBoundary:', error, errorInfo);
  }

  public handleReset = () => {
    this.setState({ hasError: false, error: null });
    window.location.reload();
  };

  public render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }

      return (
        <div className="min-h-screen bg-neutral-appBg flex items-center justify-center p-6 text-center">
          <div className="max-w-md w-full bg-white rounded-3xl p-8 border border-neutral-border shadow-panel space-y-4">
            <div className="w-14 h-14 rounded-2xl bg-red-100 text-red-600 flex items-center justify-center mx-auto">
              <AlertOctagon className="w-7 h-7" />
            </div>

            <h2 className="text-xl font-bold text-neutral-primary">Application Exception</h2>
            <p className="text-xs text-neutral-secondary">
              An unexpected interface error interrupted execution. Your session data remains safe.
            </p>

            <div className="pt-2">
              <Button size="sm" variant="primary" onClick={this.handleReset} leftIcon={<RotateCcw className="w-4 h-4" />}>
                Reload Application
              </Button>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
