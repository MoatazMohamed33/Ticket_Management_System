# Support Ticket Management System

A full-stack ticketing application: ASP.NET Core 8 Web API + Angular 17, SQL Server, JWT auth with refresh-token rotation, SignalR real-time updates, and Docker Compose orchestration.

Built as an assessment-style demonstration of Clean Architecture + CQRS on the backend, and a lazy-loaded, guard-protected SPA on the frontend.

**Quick tour**: [docs/screenshots/](docs/screenshots/README.md) — 11 ordered screenshots walking through the Admin, Support Agent, and Customer flows against the seeded demo data.

---

## Setup

### Prerequisites

- Docker Desktop (Compose v2)
- Optional for local dev without Docker: .NET 8 SDK, Node 20, Angular CLI 17

### One-command startup (recommended)

```bash
cp .env.example .env
# Edit .env — set DB_SA_PASSWORD (8+ chars, mixed case + digit + symbol)
#              set JWT_SIGNING_KEY (base64 of >= 32 bytes; `openssl rand -base64 32`)

docker compose up --build
```

Services come up in order (`db` → healthy → `api` → healthy → `web`):

| Service | URL |
|---|---|
| Web (Angular) | http://localhost:4200 |
| API — Swagger UI | http://localhost:5080/swagger |
| API — OpenAPI spec (JSON) | http://localhost:5080/swagger/v1/swagger.json |
| API health | http://localhost:5080/api/health |
| API deep health | http://localhost:5080/api/health/deep |
| SQL Server | `localhost:1433` (loopback-only bind) |

For local dev (no Docker) the API runs on port **5097** instead of 5080 — e.g. Swagger UI at http://localhost:5097/swagger and OpenAPI JSON at http://localhost:5097/swagger/v1/swagger.json.

On first startup the API applies EF migrations and seeds demo data automatically.

### Local dev (no Docker)

```bash
# Backend
dotnet restore
dotnet ef database update --project src/TicketSystem.Infrastructure --startup-project src/TicketSystem.Api
dotnet run --project src/TicketSystem.Api    # http://localhost:5080

# Frontend
cd web
npm install
npm start                                    # http://localhost:4200
```

Set `ConnectionStrings__Default` and `Jwt__SigningKey` via user-secrets or environment vars if not using Docker.

### Migrations

```bash
dotnet ef migrations add <Name> \
  --project src/TicketSystem.Infrastructure \
  --startup-project src/TicketSystem.Api
```

In Development the API auto-applies pending migrations on startup. In Production, run `dotnet ef database update` explicitly from your deployment pipeline.

---

## Credentials (seed data)

All seed users share the password `Passw0rd!`. Weak dev passwords are intentional — seeding never runs in Production.

| Role | Email |
|---|---|
| Admin | `admin@demo.local` |
| Support Agent | `agent1@demo.local` |
| Support Agent | `agent2@demo.local` |
| Customer | `customer1@demo.local` |
| Customer | `customer2@demo.local` |
| Customer | `customer3@demo.local` |

The seeder creates 6 tickets spread across customers, assigns two per agent round-robin, flips one per agent to `InProgress`, one per customer to `Resolved`, and one to `Closed` (with a synthetic `ClosedAt` so the dashboard's `averageResolutionMinutes` is non-null).

Seeding is idempotent: users and tickets each have independent existence guards.

---

## Tests

```bash
# All test projects
dotnet test

# By project
dotnet test tests/TicketSystem.UnitTests            # 178 tests, no external deps, ~250 ms
dotnet test tests/TicketSystem.IntegrationTests     # Requires Docker (Testcontainers.MsSql); hits real DB via WebApplicationFactory
dotnet test tests/TicketSystem.DataIsolationTests   # FR21/FR24 customer data-isolation matrix
dotnet test tests/TicketSystem.ArchitectureTests    # NetArchTest layering rules (no EF in Application, etc.)

# Frontend
cd web && npm test -- --watch=false --browsers=ChromeHeadless
```

Filter by name:

```bash
dotnet test tests/TicketSystem.UnitTests --filter "FullyQualifiedName~ChangeTicketStatus"
```

---

## Architecture

### Backend — Clean Architecture + CQRS

```
src/
  TicketSystem.Domain           Aggregates, value objects, domain exceptions. No dependencies.
  TicketSystem.Application      Use cases (MediatR handlers), DTOs, abstractions (IUnitOfWork,
                                IGenericRepository<T>, ICurrentUser, IClock, ITicketBroadcaster,
                                IDashboardCacheInvalidator). References Domain only. NO EF Core.
  TicketSystem.Infrastructure   EF Core DbContext, generic repository + UoW impl, JWT & password
                                hashers, dashboard cache invalidator, migrations, seeder.
  TicketSystem.Api              Controllers, filters, SignalR TicketsHub, DI composition root,
                                Swagger, global exception handler, rate limiting.
```

**Patterns applied:**

- **CQRS via MediatR** — every controller action dispatches a `Command` or `Query`. Handlers own the business logic; controllers are thin.
- **Generic repository + Unit of Work** — Application layer stays EF-free. `IGenericRepository<T>` exposes `Query()` / `AddAsync` / async LINQ extensions. `IUnitOfWork.SaveChangesWithConcurrencyCheckAsync` centralises optimistic-concurrency handling.
- **DTOs everywhere** — EF entities never cross the API boundary (enforced by architecture tests).
- **Optimistic concurrency** — `Ticket.RowVersion` (SQL `rowversion`) round-tripped as base64. Mutating endpoints require the client's rowVersion in the body; conflicts return HTTP 409 with the current state.
- **Refresh-token rotation** — access token (15 min) + refresh token (7 days). Refresh returns a new pair and revokes the old refresh token; reuse is treated as compromise and revokes the whole family.
- **Real-time via SignalR** — `TicketsHub` broadcasts `TicketCreated / TicketUpdated / TicketAssigned / CommentAdded / TimeEntryLogged` to per-ticket, per-agent, per-customer, and `admins` groups. Broadcasts are fire-and-forget after `SaveChanges` — a failed broadcast never rolls back a committed write.
- **Dashboard cache** — versioned-prefix in-memory cache via `IDashboardCacheInvalidator`. Any ticket-mutation handler bumps the version; reads compose the version into the cache key, so prior entries become unreachable atomically.
- **Rate limiting** — `AspNetCoreRateLimit` on auth endpoints (login/refresh).
- **Structured logging** — Serilog with request-correlation enrichment and sensitive-property redaction.
- **Centralised exception handling** — one middleware translates `NotFoundException` → 404, `ValidationException` → 400 (RFC 7807), `ConcurrencyConflictException` → 409, `UnauthorizedAccessException` → 401.

### Frontend — Angular 17

- Standalone components, signal-based state
- Lazy-loaded feature routes: `customer/`, `agent/`, `admin/`
- Route guards for auth + role
- HTTP interceptor: attaches JWT, transparently refreshes on 401, redirects to login on refresh failure
- Reactive Forms + Angular Material
- Chart.js for the admin dashboard
- SignalR client subscribes to per-role groups and refetches lists on events (see `web/src/app/core/realtime/realtime.service.ts`)

### Authorization model

| Role | View | Mutate status | Assign | Priority | Dashboard |
|---|---|---|---|---|---|
| Admin | any ticket | any ticket | ✅ | ✅ | ✅ |
| SupportAgent | assigned OR unassigned | assigned only | ❌ | ❌ | ❌ |
| Customer | own tickets only | close resolved only | ❌ | ❌ | ❌ |

`TicketAccess.CanView` and `TicketAccess.CanMutateStatus` are the single source of truth (`src/TicketSystem.Application/Features/Tickets/TicketAccess.cs`). Agent-assigned-elsewhere returns **404** rather than 403 to prevent existence-leak (FR22).

### Ticket state machine

```
Open ──► InProgress ──► Resolved ──► Closed (terminal)
           ▲    │           ▲   │
           │    ▼           │   ▼
           └── Open         └── InProgress
```

Enforced by `Ticket.ChangeStatus` — illegal transitions throw `InvalidTicketTransitionException` (→ HTTP 400). Same-status calls are idempotent no-ops.

---

## Assumptions & limitations

**Deliberate scope cuts (assessment MVP):**

- **Single API instance.** In-memory dashboard cache and in-memory rate limiter assume one process. Growth path is documented on the interfaces (`IDashboardCacheInvalidator`, rate-limit store) — swap for Redis-backed versions without touching handlers.
- **SignalR uses the default in-process backplane.** Scaling out requires the Redis or Azure SignalR backplane.
- **No password reset.** The seed password (`Passw0rd!`) is intentionally weak because it's dev-only; production would enforce a real password policy and lockout on repeated failures.
- **No email delivery.** Notifications are real-time via SignalR only.
- **No file attachments on tickets.**
- **No soft-delete for tickets.** Users have `DeletedAt` but tickets are hard-deleted (not exposed via API in MVP).
- **Search is `EF.Functions.Like`** on title/description, not full-text. Fine for MVP-scale data; would want SQL Server full-text or an external index at scale.
- **Rate-limit counters reset on process restart** (in-memory store).
- **The admin dashboard chart uses Chart.js** with no drill-down — clicking a bar does not filter the ticket list.
- **JWT signing key is symmetric (HS256).** Sufficient for a single-issuer, single-audience deployment. Production with multiple services would want asymmetric (RS256) keys.

**Test coverage current state:**

- Unit tests: 178 passing — domain aggregates (state machine, invariants), application helpers (`TicketAccess`, `EnumCsvParser`, `RowVersionValidators`), and strategic MediatR handlers (`CreateTicket`, `ChangeTicketStatus`, `AssignTicket`, `GetDashboardSummary`, `ListMyTickets`).
- Integration, data-isolation, and architecture test projects exist and are runnable. Not every endpoint has an integration test yet — remaining test-debt is documented per-story in `_bmad-output/`.

**Environment assumptions:**

- Docker Compose binds SQL Server to `127.0.0.1:1433` (loopback only) — the container is not reachable from the LAN by default.
- CORS is locked to `http://localhost:4200` in Docker; override via `Cors__WebOrigin`.
- Docker requires `DB_SA_PASSWORD` and `JWT_SIGNING_KEY` to be set — the compose file fails fast rather than starting with insecure defaults.
