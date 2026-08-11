// Mirrors src/TicketSystem.Application/Features/Realtime/RealtimeEvents.cs
export interface TicketCreatedEvent {
  ticketId: string;
  customerId: string;
  assignedAgentId: string | null;
}

export interface TicketUpdatedEvent {
  ticketId: string;
  changeKind: 'Status' | 'Priority' | 'Closed';
  customerId: string;
  assignedAgentId: string | null;
}

export interface TicketAssignedEvent {
  ticketId: string;
  oldAssigneeId: string | null;
  newAssigneeId: string | null;
  customerId: string;
}

export interface CommentAddedEvent {
  ticketId: string;
  commentId: string;
}

export interface TimeEntryLoggedEvent {
  ticketId: string;
  timeEntryId: string;
}
