// ─── AUTH ─────────────────────────────────────────────────────

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  email: string;
  fullName: string;
  role: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export interface RegisterPayload {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  role: string;
}

// ─── COMMON ───────────────────────────────────────────────────

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ─── PROPERTIES ───────────────────────────────────────────────

export type PropertyType = 'Residential' | 'Commercial' | 'Industrial' | 'Land';
export type PropertyStatus = 'Available' | 'Rented' | 'UnderMaintenance' | 'Reserved';
export type UnitStatus = 'Available' | 'Occupied' | 'UnderRepair' | 'Reserved';

export interface PropertyDto {
  id: string;
  name: string;
  address: string;
  city: string;
  type: PropertyType;
  status: PropertyStatus;
  totalArea: number;
  floorsCount: number;
  description?: string;
  ownerId: string;
  ownerName: string;
  unitsCount: number;
  occupiedUnitsCount: number;
  createdAt: string;
}

export interface UnitDto {
  id: string;
  number: string;
  floor: number;
  area: number;
  roomsCount: number;
  baseRentPrice: number;
  status: UnitStatus;
  propertyId: string;
  propertyName: string;
}

export interface CreatePropertyPayload {
  name: string;
  address: string;
  city: string;
  type: PropertyType;
  totalArea: number;
  floorsCount: number;
  ownerId: string;
  description?: string;
}

// ─── CRM ──────────────────────────────────────────────────────

export interface TenantDto {
  id: string;
  firstName: string;
  lastName: string;
  middleName?: string;
  taxCode?: string;
  isCompany: boolean;
  companyName?: string;
  email: string;
  phone: string;
  notes?: string;
  fullName: string;
}

export interface OwnerDto {
  id: string;
  firstName: string;
  lastName: string;
  isCompany: boolean;
  companyName?: string;
  email: string;
  phone: string;
  managementFeePercent?: number;
  fullName: string;
}

// ─── CONTRACTS ────────────────────────────────────────────────

export type ContractStatus = 'Draft' | 'Active' | 'Expired' | 'Terminated' | 'Suspended';

export interface ContractDto {
  id: string;
  number: string;
  unitId: string;
  tenantId: string;
  tenantName: string;
  unitNumber: string;
  propertyName: string;
  startDate: string;
  endDate: string;
  monthlyRent: number;
  securityDeposit: number;
  paymentDayOfMonth: number;
  status: ContractStatus;
  notes?: string;
}

// ─── FINANCE ──────────────────────────────────────────────────

export type PaymentStatus = 'Pending' | 'Paid' | 'Overdue' | 'PartiallyPaid' | 'Cancelled';
export type InvoiceType = 'Rent' | 'Utility' | 'Maintenance' | 'Deposit' | 'Fine';

export interface InvoiceDto {
  id: string;
  number: string;
  contractId: string;
  tenantName: string;
  type: InvoiceType;
  amount: number;
  paidAmount: number;
  debtAmount: number;
  dueDate: string;
  status: PaymentStatus;
  description?: string;
}

// ─── DASHBOARD ────────────────────────────────────────────────

export interface DashboardStats {
  totalProperties: number;
  totalUnits: number;
  occupiedUnits: number;
  occupancyRate: number;
  activeContracts: number;
  expiringContracts: number;
  monthlyRevenue: number;
  totalDebt: number;
  overdueInvoices: number;
}
