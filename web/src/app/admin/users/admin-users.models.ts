import { UserRole } from '../../core/auth/auth.models';

export interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CreateStaffUserRequest {
  email: string;
  displayName: string;
  role: 'SupportAgent' | 'Admin';
  initialPassword: string;
}

export interface ListUsersParams {
  page?: number;
  pageSize?: number;
  role?: UserRole;
  search?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}
