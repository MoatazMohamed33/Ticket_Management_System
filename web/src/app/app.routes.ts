import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { roleGuard } from './core/auth/role.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./auth/login/login.component').then(m => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () => import('./auth/register/register.component').then(m => m.RegisterComponent),
  },
  {
    path: 'forbidden',
    loadComponent: () => import('./forbidden/forbidden.component').then(m => m.ForbiddenComponent),
  },
  {
    // Authenticated shell — every protected page mounts inside it (header + logout).
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell.component').then(m => m.ShellComponent),
    children: [
      {
        path: '',
        loadComponent: () => import('./home/home.component').then(m => m.HomeComponent),
      },
      {
        path: 'admin/users',
        canActivate: [roleGuard(['Admin'])],
        loadComponent: () => import('./admin/users/users.component').then(m => m.UsersComponent),
      },
      {
        path: 'customer/tickets/new',
        canActivate: [roleGuard(['Customer'])],
        loadComponent: () => import('./customer/tickets/new-ticket.component').then(m => m.NewTicketComponent),
      },
      {
        path: 'customer/tickets/:id',
        // Any authenticated role can navigate; server responds 404 for unauthorized ownership (FR21).
        loadComponent: () => import('./customer/tickets/ticket-detail.component').then(m => m.TicketDetailComponent),
      },
      {
        path: 'customer/tickets',
        canActivate: [roleGuard(['Customer'])],
        loadComponent: () => import('./customer/tickets/my-tickets.component').then(m => m.MyTicketsComponent),
      },
      {
        path: 'agent/tickets',
        canActivate: [roleGuard(['SupportAgent'])],
        loadComponent: () => import('./agent/tickets/assigned-tickets.component').then(m => m.AssignedTicketsComponent),
      },
      {
        path: 'admin/tickets',
        canActivate: [roleGuard(['Admin'])],
        loadComponent: () => import('./admin/tickets/admin-tickets.component').then(m => m.AdminTicketsComponent),
      },
      {
        path: 'admin/dashboard',
        canActivate: [roleGuard(['Admin'])],
        loadComponent: () => import('./admin/dashboard/admin-dashboard.component').then(m => m.AdminDashboardComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
