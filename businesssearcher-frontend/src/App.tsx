import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Box, CircularProgress } from '@mui/material';

import { AuthProvider } from '@/context/AuthContext';
import { AppThemeProvider } from '@/context/ThemeContext';
import RequireAuth from '@/components/dashboard/RequireAuth';
import DashboardLayout from '@/components/dashboard/DashboardLayout';

const LoginPage = lazy(() => import('@/components/auth/LoginPage'));
const RegisterPage = lazy(() => import('@/components/auth/RegisterPage'));
const ForgotPasswordPage = lazy(() => import('@/components/auth/ForgotPasswordPage'));
const ResetPasswordPage = lazy(() => import('@/components/auth/ResetPasswordPage'));

const OverviewPage = lazy(() => import('@/components/dashboard/pages/OverviewPage'));
const AccountPage = lazy(() => import('@/components/dashboard/pages/AccountPage'));
const SearchPage = lazy(() => import('@/components/dashboard/pages/SearchPage'));
const WholesaleCatalogPage = lazy(() => import('@/components/dashboard/pages/WholesaleCatalogPage'));
const AdminPage = lazy(() => import('@/components/dashboard/pages/AdminPage'));
const VerifyEmailPage = lazy(() => import('@/components/auth/VerifyEmailPage'));

// Operaciones (TPV/ERP)
const PosPage = lazy(() => import('@/components/dashboard/pages/ops/PosPage'));
const SalesPage = lazy(() => import('@/components/dashboard/pages/ops/SalesPage'));
const TerminalsPage = lazy(() => import('@/components/dashboard/pages/ops/TerminalsPage'));
const InventoryPage = lazy(() => import('@/components/dashboard/pages/ops/InventoryPage'));
const CashRegisterPage = lazy(() => import('@/components/dashboard/pages/ops/CashRegisterPage'));
const OpsUsersPage = lazy(() => import('@/components/dashboard/pages/ops/UsersPage'));
const PurchaseRequestsPage = lazy(() => import('@/components/dashboard/pages/ops/PurchaseRequestsPage'));
const PurchasesPage = lazy(() => import('@/components/dashboard/pages/ops/PurchasesPage'));
const MovementsPage = lazy(() => import('@/components/dashboard/pages/ops/MovementsPage'));
const InventoryCountPage = lazy(() => import('@/components/dashboard/pages/ops/InventoryCountPage'));
const SuppliersPage = lazy(() => import('@/components/dashboard/pages/ops/SuppliersPage'));
const ExpensesPage = lazy(() => import('@/components/dashboard/pages/ops/ExpensesPage'));
const SettingsPage = lazy(() => import('@/components/dashboard/pages/ops/SettingsPage'));
const AuditLogPage = lazy(() => import('@/components/dashboard/pages/ops/AuditLogPage'));
const ReportsPage = lazy(() => import('@/components/dashboard/pages/ops/ReportsPage'));

function PageFallback() {
  return (
    <Box display="flex" justifyContent="center" py={8}>
      <CircularProgress />
    </Box>
  );
}

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30_000,
    },
  },
});

export default function App() {
  return (
    <AppThemeProvider>
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <BrowserRouter basename={import.meta.env.BASE_URL}>
          <Suspense fallback={<PageFallback />}>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />
              <Route path="/forgot-password" element={<ForgotPasswordPage />} />
              <Route path="/reset-password" element={<ResetPasswordPage />} />
              <Route path="/verify-email" element={<VerifyEmailPage />} />

              <Route element={<RequireAuth />}>
                <Route element={<DashboardLayout />}>
                  <Route path="/dashboard" element={<OverviewPage />} />
                  <Route path="/dashboard/search" element={<SearchPage />} />
                  <Route path="/dashboard/wholesale-catalog" element={<WholesaleCatalogPage />} />
                  <Route path="/dashboard/account" element={<AccountPage />} />
                  <Route path="/dashboard/ops/pos" element={<PosPage />} />
                  <Route path="/dashboard/ops/sales" element={<SalesPage />} />
                  <Route path="/dashboard/ops/terminals" element={<TerminalsPage />} />
                  <Route path="/dashboard/ops/inventory" element={<InventoryPage />} />
                  <Route path="/dashboard/ops/cash" element={<CashRegisterPage />} />
                  <Route path="/dashboard/ops/users" element={<OpsUsersPage />} />
                  <Route path="/dashboard/ops/purchase-requests" element={<PurchaseRequestsPage />} />
                  <Route path="/dashboard/ops/purchases" element={<PurchasesPage />} />
                  <Route path="/dashboard/ops/movements" element={<MovementsPage />} />
                  <Route path="/dashboard/ops/counts" element={<InventoryCountPage />} />
                  <Route path="/dashboard/ops/suppliers" element={<SuppliersPage />} />
                  <Route path="/dashboard/ops/expenses" element={<ExpensesPage />} />
                  <Route path="/dashboard/ops/settings" element={<SettingsPage />} />
                  <Route path="/dashboard/ops/audit-log" element={<AuditLogPage />} />
                  <Route path="/dashboard/ops/reports" element={<ReportsPage />} />
                  <Route path="/admin" element={<AdminPage />} />
                </Route>
              </Route>

              <Route path="/" element={<Navigate to="/dashboard" replace />} />
              <Route path="*" element={<Navigate to="/dashboard" replace />} />
            </Routes>
          </Suspense>
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
    </AppThemeProvider>
  );
}
