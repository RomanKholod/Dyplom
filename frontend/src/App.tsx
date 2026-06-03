import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { useAuthStore } from './store/authStore';
import AppLayout from './components/layout/AppLayout';
import LoginPage from './features/auth/LoginPage';
import DashboardPage from './features/dashboard/DashboardPage';
import PropertiesPage from './features/properties/PropertiesPage';
import PropertyDetailPage from './features/properties/PropertyDetailPage';
import CrmPage from './features/crm/CrmPage';
import ContractsPage from './features/contracts/ContractsPage';
import FinancePage from './features/finance/FinancePage';
import UtilitiesPage from './features/utilities/UtilitiesPage';
import ReportsPage from './features/reports/ReportsPage';

const qc = new QueryClient({
  defaultOptions: { queries: { retry: 1, staleTime: 30_000, refetchOnWindowFocus: false } },
});

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore(s => s.isAuthenticated);
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
}

export default function App() {
  return (
    <QueryClientProvider client={qc}>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/" element={<PrivateRoute><AppLayout /></PrivateRoute>}>
            <Route index element={<Navigate to="/dashboard" replace />} />
            <Route path="dashboard"         element={<DashboardPage />} />
            <Route path="properties"        element={<PropertiesPage />} />
            <Route path="properties/:id"    element={<PropertyDetailPage />} />
            <Route path="contracts"         element={<ContractsPage />} />
            <Route path="crm"               element={<CrmPage />} />
            <Route path="finance"           element={<FinancePage />} />
            <Route path="utilities"         element={<UtilitiesPage />} />
            <Route path="reports"           element={<ReportsPage />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
      <ReactQueryDevtools />
    </QueryClientProvider>
  );
}
