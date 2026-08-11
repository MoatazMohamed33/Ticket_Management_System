import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgChartsModule } from 'ng2-charts';
import type { ChartConfiguration, ChartData, ActiveElement } from 'chart.js';
import { DashboardService } from './dashboard.service';
import { AgentWorkload, DashboardSummary } from './dashboard.models';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [RouterLink, NgChartsModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.scss',
})
export class AdminDashboardComponent implements OnInit {
  private readonly service = inject(DashboardService);
  private readonly router = inject(Router);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly isDesktop = signal(
    typeof window !== 'undefined' && window.matchMedia?.('(min-width: 1024px)').matches);

  readonly totalOpen = computed(() => {
    const s = this.summary();
    if (!s) return 0;
    return (s.ticketCountsByStatus.open ?? 0) + (s.ticketCountsByStatus.inProgress ?? 0);
  });

  // Alphabetical tiebreak for deterministic rendering on repeat loads.
  readonly topLoadedAgent = computed<AgentWorkload | null>(() => {
    const s = this.summary();
    if (!s || s.agentWorkload.length === 0) return null;
    const sorted = [...s.agentWorkload].sort((a, b) => {
      const loadDiff = (b.openCount + b.inProgressCount) - (a.openCount + a.inProgressCount);
      return loadDiff !== 0 ? loadDiff : a.agentDisplayName.localeCompare(b.agentDisplayName);
    });
    const top = sorted[0];
    return top.openCount + top.inProgressCount === 0 ? null : top;
  });

  readonly avgResolutionDisplay = computed(() => {
    const s = this.summary();
    if (!s || s.averageResolutionMinutes === null) return 'n/a';
    const m = s.averageResolutionMinutes;
    const h = Math.floor(m / 60);
    const mm = (m % 60).toString().padStart(2, '0');
    return `${h}:${mm}`;
  });

  readonly chartData = computed<ChartData<'bar'>>(() => {
    const w = this.summary()?.agentWorkload ?? [];
    return {
      labels: w.map(a => a.agentDisplayName),
      datasets: [
        { label: 'Open',        data: w.map(a => a.openCount),       backgroundColor: '#3b82f6' },
        { label: 'In Progress', data: w.map(a => a.inProgressCount), backgroundColor: '#f59e0b' },
      ],
    };
  });

  readonly chartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: { stacked: true },
      y: { stacked: true, beginAtZero: true, ticks: { stepSize: 1 } },
    },
    plugins: {
      legend: { position: 'bottom' },
      tooltip: { mode: 'index', intersect: false },
    },
    onClick: (_evt, elements) => this.onChartElementClick(elements),
  };

  readonly chartAriaLabel = computed(() => {
    const top = this.topLoadedAgent();
    if (!top) return 'Bar chart of agent workload. No workload currently.';
    return `Bar chart of agent workload. ${top.agentDisplayName} has the highest load: ${top.openCount} open, ${top.inProgressCount} in progress.`;
  });

  ngOnInit(): void { this.load(); }
  refresh(): void { this.load(); }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.getSummary('30d').subscribe({
      next: s => { this.summary.set(s); this.loading.set(false); },
      error: () => { this.error.set("Couldn't load dashboard."); this.loading.set(false); },
    });
  }

  navigate(queryParams: Record<string, string>): void {
    this.router.navigate(['/admin/tickets'], { queryParams });
  }

  private onChartElementClick(elements: ActiveElement[]): void {
    if (elements.length === 0) return;
    const idx = elements[0].index;
    const agent = this.summary()?.agentWorkload[idx];
    if (agent) {
      this.navigate({ assignedAgentId: agent.agentId, status: 'Open,InProgress' });
    }
  }
}
