import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardSummary } from './dashboard.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  getSummary(window: string = '30d'): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(
      `${environment.apiBaseUrl}/admin/dashboard/summary`, { params: { window } });
  }
}
