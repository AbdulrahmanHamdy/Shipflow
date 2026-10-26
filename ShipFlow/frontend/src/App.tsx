import React, { Suspense, lazy } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ProtectedRoute } from '@/features/auth/ProtectedRoute';
import { AppShell } from '@/components/layout/AppShell';
import { PublicLayout } from '@/components/layout/PublicLayout';

// Lazy-loaded pages for optimal performance and route-level code splitting
const LandingPage = lazy(() => import('@/pages/public/LandingPage').then((m) => ({ default: m.LandingPage })));
const LoginPage = lazy(() => import('@/pages/public/LoginPage').then((m) => ({ default: m.LoginPage })));
const RegisterPage = lazy(() => import('@/pages/public/RegisterPage').then((m) => ({ default: m.RegisterPage })));
const ForgotPasswordPage = lazy(() => import('@/pages/public/ForgotPasswordPage').then((m) => ({ default: m.ForgotPasswordPage })));
const ResetPasswordPage = lazy(() => import('@/pages/public/ResetPasswordPage').then((m) => ({ default: m.ResetPasswordPage })));
const PublicTrackingPage = lazy(() => import('@/pages/public/PublicTrackingPage').then((m) => ({ default: m.PublicTrackingPage })));
const PrivacyPage = lazy(() => import('@/pages/public/PrivacyPage').then((m) => ({ default: m.PrivacyPage })));
const TermsPage = lazy(() => import('@/pages/public/TermsPage').then((m) => ({ default: m.TermsPage })));
const NotFoundPage = lazy(() => import('@/pages/public/NotFoundPage').then((m) => ({ default: m.NotFoundPage })));

// Customer pages
const CustomerDashboardPage = lazy(() => import('@/pages/customer/CustomerDashboardPage').then((m) => ({ default: m.CustomerDashboardPage })));
const ShipmentsListPage = lazy(() => import('@/pages/customer/ShipmentsListPage').then((m) => ({ default: m.ShipmentsListPage })));
const CreateShipmentPage = lazy(() => import('@/pages/customer/CreateShipmentPage').then((m) => ({ default: m.CreateShipmentPage })));
const ShipmentDetailPage = lazy(() => import('@/pages/customer/ShipmentDetailPage').then((m) => ({ default: m.ShipmentDetailPage })));
const CustomerTrackingPage = lazy(() => import('@/pages/customer/CustomerTrackingPage').then((m) => ({ default: m.CustomerTrackingPage })));
const DeliveryProofPage = lazy(() => import('@/pages/customer/DeliveryProofPage').then((m) => ({ default: m.DeliveryProofPage })));

// Driver pages
const DriverDashboardPage = lazy(() => import('@/pages/driver/DriverDashboardPage').then((m) => ({ default: m.DriverDashboardPage })));
const DriverCurrentJobPage = lazy(() => import('@/pages/driver/DriverCurrentJobPage').then((m) => ({ default: m.DriverCurrentJobPage })));
const DriverAssignmentsPage = lazy(() => import('@/pages/driver/DriverAssignmentsPage').then((m) => ({ default: m.DriverAssignmentsPage })));

// Carrier pages
const CarrierDashboardPage = lazy(() => import('@/pages/carrier/CarrierDashboardPage').then((m) => ({ default: m.CarrierDashboardPage })));
const CarrierShipmentsPage = lazy(() => import('@/pages/carrier/CarrierShipmentsPage').then((m) => ({ default: m.CarrierShipmentsPage })));
const CarrierDispatchPage = lazy(() => import('@/pages/carrier/CarrierDispatchPage').then((m) => ({ default: m.CarrierDispatchPage })));
const CarrierFleetPage = lazy(() => import('@/pages/carrier/CarrierFleetPage').then((m) => ({ default: m.CarrierFleetPage })));
const CarrierDriversPage = lazy(() => import('@/pages/carrier/CarrierDriversPage').then((m) => ({ default: m.CarrierDriversPage })));
const CarrierTeamPage = lazy(() => import('@/pages/carrier/CarrierTeamPage').then((m) => ({ default: m.CarrierTeamPage })));
const CarrierOnboardingPage = lazy(() => import('@/pages/carrier/CarrierOnboardingPage').then((m) => ({ default: m.CarrierOnboardingPage })));

// Shared pages
const PaymentCallbackPage = lazy(() => import('@/pages/shared/PaymentCallbackPage').then((m) => ({ default: m.PaymentCallbackPage })));
const DevComponentsPage = lazy(() => import('@/pages/shared/DevComponentsPage').then((m) => ({ default: m.DevComponentsPage })));

const LoadingFallback: React.FC = () => (
  <div className="min-h-[50vh] flex items-center justify-center">
    <div className="w-8 h-8 border-2 border-brand border-t-transparent rounded-full animate-spin" />
  </div>
);

const App: React.FC = () => {
  return (
    <BrowserRouter>
      <Suspense fallback={<LoadingFallback />}>
        <Routes>
          {/* Public Website Routes */}
          <Route element={<PublicLayout />}>
            <Route path="/" element={<LandingPage />} />
            <Route path="/tracking" element={<PublicTrackingPage />} />
            <Route path="/privacy" element={<PrivacyPage />} />
            <Route path="/terms" element={<TermsPage />} />
          </Route>

          {/* Auth Flow Pages */}
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/payments/callback" element={<PaymentCallbackPage />} />

          {/* Customer / Shipper Workspace Routes */}
          <Route
            path="/app"
            element={
              <ProtectedRoute>
                <AppShell />
              </ProtectedRoute>
            }
          >
            <Route index element={<CustomerDashboardPage />} />
            <Route path="shipments" element={<ShipmentsListPage />} />
            <Route path="shipments/new" element={<CreateShipmentPage />} />
            <Route path="shipments/:id" element={<ShipmentDetailPage />} />
            <Route path="shipments/:id/tracking" element={<CustomerTrackingPage />} />
            <Route path="shipments/:id/proof" element={<DeliveryProofPage />} />
          </Route>

          {/* Driver Workspace Routes */}
          <Route
            path="/driver"
            element={
              <ProtectedRoute allowedRoles={['Driver', 'PlatformAdmin']}>
                <AppShell />
              </ProtectedRoute>
            }
          >
            <Route index element={<DriverDashboardPage />} />
            <Route path="current-job" element={<DriverCurrentJobPage />} />
            <Route path="assignments" element={<DriverAssignmentsPage />} />
          </Route>

          {/* Carrier & Dispatcher Workspace Routes */}
          <Route
            path="/carrier"
            element={
              <ProtectedRoute allowedRoles={['CompanyAdmin', 'Dispatcher', 'PlatformAdmin', 'Customer']}>
                <AppShell />
              </ProtectedRoute>
            }
          >
            <Route index element={<CarrierDashboardPage />} />
            <Route path="shipments" element={<CarrierShipmentsPage />} />
            <Route path="dispatch/:shipmentId" element={<CarrierDispatchPage />} />
            <Route path="fleet" element={<CarrierFleetPage />} />
            <Route path="drivers" element={<CarrierDriversPage />} />
            <Route path="team" element={<CarrierTeamPage />} />
            <Route path="onboarding" element={<CarrierOnboardingPage />} />
          </Route>

          {/* Development Showcase */}
          <Route path="/dev/components" element={<DevComponentsPage />} />

          {/* Fallback 404 */}
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  );
};

export default App;
