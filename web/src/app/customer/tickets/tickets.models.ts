export type TicketStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed';
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export interface TicketDto {
  id: string;
  title: string;
  description: string;
  status: TicketStatus;
  priority: TicketPriority;
  customerId: string;
  assignedAgentId: string | null;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
}

export interface CreateTicketRequest {
  title: string;
  description: string;
  priority: TicketPriority;
}

export interface TicketListItem {
  id: string;
  title: string;
  status: TicketStatus;
  priority: TicketPriority;
  createdAt: string;
  updatedAt: string;
  assignedAgentDisplayName: string | null;
  customerDisplayName: string | null;   // populated in agent/admin lists; null in customer list
  assignedAgentId: string | null;
  rowVersion: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ListMyTicketsParams {
  page?: number;
  pageSize?: number;
  status?: TicketStatus[];
  priority?: TicketPriority[];
  search?: string;
  sortBy?: 'updatedAt' | 'createdAt' | 'priority';
  sortDirection?: 'asc' | 'desc';
  // Admin-only filters (Story 6.1). Ignored by /api/tickets and /api/tickets/assigned-to-me.
  // assignedAgentId may also carry the string "unassigned" to filter for null-assignee rows.
  assignedAgentId?: string;
  customerId?: string;
}

// ---------------- Detail view (Story 4.3) ----------------

export interface UserSummary {
  id: string;
  displayName: string;
}

export type ActivityEventType =
  | 'TicketCreated'
  | 'StatusChanged'
  | 'PriorityChanged'
  | 'AgentAssigned'
  | 'AgentUnassigned'
  | 'TicketClosed';

export interface Comment {
  id: string;
  author: UserSummary;
  body: string;
  createdAt: string;
}

export interface ActivityEntry {
  id: string;
  actor: UserSummary;
  event: ActivityEventType;
  summary: string;
  at: string;
}

export interface TimeEntry {
  id: string;
  author: UserSummary;
  workedOn: string;         // ISO date (YYYY-MM-DD)
  durationMinutes: number;
  description: string;
  createdAt: string;
}

export interface TicketDetail {
  id: string;
  title: string;
  description: string;
  status: TicketStatus;
  priority: TicketPriority;
  customer: UserSummary;
  assignedAgent: UserSummary | null;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  comments: Comment[];
  activity: ActivityEntry[];
  timeEntries: TimeEntry[];
  totalLoggedMinutes: number;
}
