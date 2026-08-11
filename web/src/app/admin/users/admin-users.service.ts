import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UserRole } from '../../core/auth/auth.models';
import {
  AdminUser,
  CreateStaffUserRequest,
  ListUsersParams,
  PagedResult,
} from './admin-users.models';

@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin/users`;

  list(params: ListUsersParams = {}): Observable<PagedResult<AdminUser>> {
    let httpParams = new HttpParams();
    if (params.page != null) httpParams = httpParams.set('page', params.page);
    if (params.pageSize != null) httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.role) httpParams = httpParams.set('role', params.role);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDirection) httpParams = httpParams.set('sortDirection', params.sortDirection);
    return this.http.get<PagedResult<AdminUser>>(this.base, { params: httpParams });
  }

  create(body: CreateStaffUserRequest): Observable<AdminUser> {
    return this.http.post<AdminUser>(this.base, body);
  }

  deactivate(userId: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/${userId}/deactivate`, {});
  }

  activate(userId: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/${userId}/activate`, {});
  }

  changeRole(userId: string, role: UserRole): Observable<void> {
    return this.http.patch<void>(`${this.base}/${userId}/role`, { role });
  }
}
