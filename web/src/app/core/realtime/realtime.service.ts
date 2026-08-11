import { Injectable, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import {
  CommentAddedEvent,
  TicketAssignedEvent,
  TicketCreatedEvent,
  TicketUpdatedEvent,
  TimeEntryLoggedEvent,
} from './realtime.events';

type ConnState = 'disconnected' | 'connecting' | 'connected' | 'offline';

@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;

  private readonly state = signal<ConnState>('disconnected');
  readonly connectionState = this.state.asReadonly();

  readonly reconnected = new Subject<void>();

  // Persistent event subjects — decoupled from connection lifecycle.
  // Consumers can subscribe synchronously (BEFORE ensureConnected completes) and
  // still receive events once the connection materializes. Story 7.2 review fix CR1:
  // the previous design attached handlers via connection?.on() at consumer-subscribe
  // time; if the connection wasn't up yet, the ?. short-circuited and handlers were
  // silently dropped forever.
  private readonly ticketCreated$    = new Subject<TicketCreatedEvent>();
  private readonly ticketUpdated$    = new Subject<TicketUpdatedEvent>();
  private readonly ticketAssigned$   = new Subject<TicketAssignedEvent>();
  private readonly commentAdded$     = new Subject<CommentAddedEvent>();
  private readonly timeEntryLogged$  = new Subject<TimeEntryLoggedEvent>();
  private handlersAttached = false;

  async ensureConnected(): Promise<HubConnection> {
    if (this.connection?.state === HubConnectionState.Connected) return this.connection;

    if (!this.connection) {
      this.connection = new HubConnectionBuilder()
        .withUrl(`${environment.hubBaseUrl}/tickets`, {
          // Awaited factory — on reconnect (10s+ after disconnect) the current JWT may be
          // expired. getFreshAccessToken() triggers refresh via Story 2.4's refresh-token
          // pipeline if the current one is stale. Empty string on failure → server 401s
          // → reconnect stops until user action.
          accessTokenFactory: async () =>
            (await this.auth.getFreshAccessToken()) ?? '',
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(LogLevel.Warning)
        .build();

      this.connection.onreconnecting(() => this.state.set('connecting'));
      this.connection.onreconnected(() => {
        this.state.set('connected');
        this.reconnected.next();
      });
      this.connection.onclose(err => {
        this.state.set(err ? 'offline' : 'disconnected');
      });

      // Attach hub → subject bridges ONCE per connection instance. Subjects survive
      // reconnects because SignalR's auto-reconnect reuses the same HubConnection.
      this.attachHandlers(this.connection);
    }

    if (this.connection.state !== HubConnectionState.Connected) {
      this.state.set('connecting');
      try {
        await this.connection.start();
        this.state.set('connected');
      } catch (e) {
        this.state.set('offline');
        throw e;
      }
    }
    return this.connection;
  }

  private attachHandlers(conn: HubConnection): void {
    if (this.handlersAttached) return;
    conn.on('TicketCreated',    (p: TicketCreatedEvent)    => this.ticketCreated$.next(p));
    conn.on('TicketUpdated',    (p: TicketUpdatedEvent)    => this.ticketUpdated$.next(p));
    conn.on('TicketAssigned',   (p: TicketAssignedEvent)   => this.ticketAssigned$.next(p));
    conn.on('CommentAdded',     (p: CommentAddedEvent)     => this.commentAdded$.next(p));
    conn.on('TimeEntryLogged',  (p: TimeEntryLoggedEvent)  => this.timeEntryLogged$.next(p));
    this.handlersAttached = true;
  }

  async subscribeToTicket(ticketId: string): Promise<void> {
    const conn = await this.ensureConnected();
    await conn.invoke('SubscribeToTicket', ticketId);
  }

  async unsubscribeFromTicket(ticketId: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      try { await this.connection.invoke('UnsubscribeFromTicket', ticketId); }
      catch { /* silent — disconnected mid-teardown is fine */ }
    }
  }

  async subscribeToMyTickets(): Promise<void> {
    const conn = await this.ensureConnected();
    await conn.invoke('SubscribeToMyTickets');
  }

  // Consumers subscribe to these Observables at any time — the underlying Subjects
  // are persistent, so subscribing BEFORE ensureConnected completes still receives
  // events once the connection materializes. No historical replay (Subject not
  // ReplaySubject) — live-forward only, which is the desired UX.
  onTicketCreated():    Observable<TicketCreatedEvent>    { return this.ticketCreated$.asObservable(); }
  onTicketUpdated():    Observable<TicketUpdatedEvent>    { return this.ticketUpdated$.asObservable(); }
  onTicketAssigned():   Observable<TicketAssignedEvent>   { return this.ticketAssigned$.asObservable(); }
  onCommentAdded():     Observable<CommentAddedEvent>     { return this.commentAdded$.asObservable(); }
  onTimeEntryLogged():  Observable<TimeEntryLoggedEvent>  { return this.timeEntryLogged$.asObservable(); }
}
