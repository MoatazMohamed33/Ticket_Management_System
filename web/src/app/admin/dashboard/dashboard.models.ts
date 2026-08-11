export interface DashboardSummary {
  // API JSON config camelCases dictionary keys → "Open" becomes "open" on the wire.
  ticketCountsByStatus: Record<'open' | 'inProgress' | 'resolved' | 'closed', number>;
  openCriticalCount: number;
  averageResolutionMinutes: number | null;
  agentWorkload: AgentWorkload[];
}

export interface AgentWorkload {
  agentId: string;
  agentDisplayName: string;
  openCount: number;
  inProgressCount: number;
}
