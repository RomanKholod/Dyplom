import { api } from './client';
import type {
  AuthResponse, LoginPayload, RegisterPayload,
  PagedResult, PropertyDto, UnitDto, CreatePropertyPayload,
  TenantDto, OwnerDto, ContractDto, InvoiceDto, DashboardStats
} from '../types';

// ─── AUTH ─────────────────────────────────────────────────────

export const authApi = {
  login: (payload: LoginPayload) =>
    api.post<AuthResponse>('/auth/login', payload).then(r => r.data),

  register: (payload: RegisterPayload) =>
    api.post<AuthResponse>('/auth/register', payload).then(r => r.data),

  refresh: (refreshToken: string) =>
    api.post<AuthResponse>('/auth/refresh', { refreshToken }).then(r => r.data),
};

// ─── PROPERTIES ───────────────────────────────────────────────

export const propertiesApi = {
  getAll: (params?: { page?: number; pageSize?: number; search?: string; status?: string }) =>
    api.get<PagedResult<PropertyDto>>('/properties', { params }).then(r => r.data),

  getById: (id: string) =>
    api.get<PropertyDto>(`/properties/${id}`).then(r => r.data),

  create: (payload: any) =>
    api.post<PropertyDto>('/properties', payload).then(r => r.data),

  // Передаємо payload як обгорнутий об'єкт "data", якщо контролер побудований аналогічно до Owner
  update: (id: string, payload: any) =>
    api.put<PropertyDto>(`/properties/${id}`, { data: payload }).then(r => r.data),

  delete: (id: string) =>
    api.delete(`/properties/${id}`),

  getUnits: (propertyId: string) =>
    api.get<UnitDto[]>(`/properties/${propertyId}/units`).then(r => r.data),

  createUnit: (payload: any) =>
    api.post<UnitDto>('/properties/units', payload).then(r => r.data),
};

// ─── CRM ──────────────────────────────────────────────────────

export const tenantsApi = {
  getAll: (params?: { page?: number; search?: string }) =>
    api.get<PagedResult<TenantDto>>('/tenants', { params }).then(r => r.data),

  getById: (id: string) =>
    api.get<TenantDto>(`/tenants/${id}`).then(r => r.data),

  create: (payload: Omit<TenantDto, 'id' | 'fullName'>) =>
    api.post<TenantDto>('/tenants', payload).then(r => r.data),

  update: (id: string, payload: Partial<TenantDto>) =>
    api.put<TenantDto>(`/tenants/${id}`, payload).then(r => r.data),

  delete: (id: string) => api.delete(`/tenants/${id}`),
};

export const ownersApi = {
  getAll: () => api.get<OwnerDto[]>('/owners').then(r => r.data),
  getById: (id: string) => api.get<OwnerDto>(`/owners/${id}`).then(r => r.data),
  create: (payload: Omit<OwnerDto, 'id' | 'fullName'>) =>
    api.post<OwnerDto>('/owners', payload).then(r => r.data),
};

// ─── CONTRACTS ────────────────────────────────────────────────

export const contractsApi = {
  getAll: (params?: { page?: number; pageSize?: number; search?: string; status?: string; tenantId?: string; propertyId?: string }) =>
    api.get<PagedResult<ContractDto>>('/contracts', { params }).then(r => r.data),

  getById: (id: string) =>
    api.get<ContractDto>(`/contracts/${id}`).then(r => r.data),

  getExpiring: (daysAhead = 30) =>
    api.get<ContractDto[]>('/contracts/expiring', { params: { daysAhead } }).then(r => r.data),

  create: (payload: Omit<ContractDto, 'id' | 'tenantName' | 'unitNumber' | 'propertyName' | 'propertyAddress' | 'tenantPhone' | 'daysUntilExpiry' | 'createdAt' | 'status' | 'number' | 'terminationReason' | 'terminatedAt'>) =>
    api.post<ContractDto>('/contracts', payload).then(r => r.data),

  activate: (id: string) =>
    api.post<ContractDto>(`/contracts/${id}/activate`).then(r => r.data),

  terminate: (id: string, reason: string) =>
    api.post<ContractDto>(`/contracts/${id}/terminate`, { reason }).then(r => r.data),

  renew: (id: string, newEndDate: Date, newMonthlyRent?: number) =>
    api.post<ContractDto>(`/contracts/${id}/renew`, { newEndDate: newEndDate.toISOString(), newMonthlyRent }).then(r => r.data),
};

// ─── FINANCE ──────────────────────────────────────────────────

export const invoicesApi = {
  getAll: (params?: { page?: number; status?: string; contractId?: string }) =>
    api.get<PagedResult<InvoiceDto>>('/invoices', { params }).then(r => r.data),

  getById: (id: string) =>
    api.get<InvoiceDto>(`/invoices/${id}`).then(r => r.data),

  addPayment: (invoiceId: string, payload: { amount: number; paymentDate: string; paymentMethod?: string; reference?: string }) =>
    api.post(`/invoices/${invoiceId}/payments`, payload).then(r => r.data),

  // ─── ОНОВЛЕНО: Передаємо Month з великої літери ───────────────────
 generateRentInvoices: (payload: { month: string }) =>
    api.post<{ isSuccess: boolean; value: number; error?: string }>('/invoices/generate-rent', null, { params: payload }).then(r => r.data),
};

// ─── DASHBOARD ────────────────────────────────────────────────

export const dashboardApi = {
  getStats: () =>
    api.get<DashboardStats>('/dashboard/stats').then(r => r.data),
};

// ─── RE-EXPORTS ───────────────────────────────────────────────
export { utilitiesApi } from './utilities';
export { exportApi } from './export';