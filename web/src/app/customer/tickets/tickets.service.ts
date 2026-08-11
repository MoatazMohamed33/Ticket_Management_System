import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Comment,
  CreateTicketRequest,
  ListMyTicketsParams,
  PagedResult,
  TicketDetail,
  TicketDto,
  TicketListItem,
} from './tickets.models';

@Injectable({ providedIn: 'root' })
export class TicketsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/tickets`;

  create(body: CreateTicketRequest): Observable<TicketDto> {
    return this.http.post<TicketDto>(this.base, body);
  }

  listMine(params: ListMyTicketsParams = {}): Observable<PagedResult<TicketListItem>> {
    return this.http.get<PagedResult<TicketListItem>>(
      this.base, { params: this.buildListParams(params) });
  }

  listAssignedToMe(params: ListMyTicketsParams = {}): Observable<PagedResult<TicketListItem>> {
    return this.http.get<PagedResult<TicketListItem>>(
      `${this.base}/assigned-to-me`, { params: this.buildListParams(params) });
  }

  listAllForAdmin(params: ListMyTicketsParams = {}): Observable<PagedResult<TicketListItem>> {
    return this.http.get<PagedResult<TicketListItem>>(
      `${environment.apiBaseUrl}/admin/tickets`,
      { params: this.buildListParams(params) });
  }

  private buildListParams(params: ListMyTicketsParams): HttpParams {
    let p = new HttpParams();
    if (params.page != null) p = p.set('page', params.page);
    if (params.pageSize != null) p = p.set('pageSize', params.pageSize);
    if (params.status?.length) p = p.set('status', params.status.join(','));
    if (params.priority?.length) p = p.set('priority', params.priority.join(','));
    if (params.search) p = p.set('search', params.search);
    if (params.sortBy) p = p.set('sortBy', params.sortBy);
    if (params.sortDirection) p = p.set('sortDirection', params.sortDirection);
    if (params.assignedAgentId) p = p.set('assignedAgentId', params.assignedAgentId);
    if (params.customerId) p = p.set('customerId', params.customerId);
    return p;
  }

  getDetail(id: string): Observable<TicketDetail> {
    return this.http.get<TicketDetail>(`${this.base}/${id}`);
  }

  addComment(ticketId: string, body: { body: string }): Observable<Comment> {
    return this.http.post<Comment>(`${this.base}/${ticketId}/comments`, body);
  }

  close(ticketId: string, rowVersion: string): Observable<TicketDto> {
    return this.http.post<TicketDto>(`${this.base}/${ticketId}/close`, { rowVersion });
  }

  changeStatus(ticketId: string, status: string, rowVersion: string): Observable<TicketDto> {
    return this.http.patch<TicketDto>(
      `${this.base}/${ticketId}/status`, { status, rowVersion });
  }

  changePriority(ticketId: string, priority: string, rowVersion: string): Observable<TicketDto> {
    return this.http.patch<TicketDto>(
      `${environment.apiBaseUrl}/admin/tickets/${ticketId}/priority`, { priority, rowVersion });
  }

  assign(ticketId: string, assignedAgentId: string | null, rowVersion: string): Observable<TicketDto> {
    return this.http.patch<TicketDto>(
      `${environment.apiBaseUrl}/admin/tickets/${ticketId}/assign`,
      { assignedAgentId, rowVersion });
  }

  logTime(ticketId: string, body: { workedOn: string; durationMinutes: number; description: string }):
      Observable<import('./tickets.models').TimeEntry> {
    return this.http.post<import('./tickets.models').TimeEntry>(
      `${this.base}/${ticketId}/time-entries`, body);
  }
}
