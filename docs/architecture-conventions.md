# Architecture Conventions

Ground rules for the Ticket Management System codebase. These are enforced where possible by the `TicketSystem.ArchitectureTests` suite; the rest are code-review checklist items.

## Layer boundaries (enforced by tests)

- **Domain** → depends on nothing.
- **Application** → depends on **Domain** only. **Must not** reference EF Core, HTTP frameworks, or logging providers directly.
- **Infrastructure** → depends on **Application** + **Domain**. Implements interfaces defined in Application.
- **Api** → depends on **Application** + **Infrastructure**. Never on **Domain** directly.
- **Controllers** → must not return types from **Domain** (return DTOs from **Application**).

## Feature folder layout

Every feature lives under `Application/Features/<Feature>/`. Each Command/Query gets its own folder:

```
Features/
└── Health/
    └── Queries/
        └── GetDeepHealth/
            ├── GetDeepHealthQuery.cs       # record : IRequest<HealthDetailDto>
            ├── GetDeepHealthQueryHandler.cs
            ├── GetDeepHealthQueryValidator.cs   # (if any)
            └── HealthDetailDto.cs
```

## CQRS / MediatR

- **Every** endpoint dispatches to a Command or Query via `IMediator.Send`. Controllers contain no business logic.
- Commands mutate state, return a DTO (or `Unit`). Queries never mutate state, always return a DTO.
- Use `record` for Commands, Queries, and DTOs. Prefer immutability.
- Cross-cutting behaviors go in `Application/Common/Behaviors` (currently `ValidationBehavior`; later: `AuthorizationBehavior`, `LoggingBehavior`, `CachingBehavior`).

## Data access

- **Never** inject `AppDbContext` into controllers or Application handlers. Use `IUnitOfWork` + `IGenericRepository<T>`.
- **Read queries**: use `IGenericRepository<T>.QueryNoTracking()`. Read handlers must not track entities — this is what makes 300 ms P95 at 50 k rows achievable.
- **Write commands**: use `IGenericRepository<T>.Query()`, `AddAsync`, `Update`, or `Remove`. Always finish with `_uow.SaveChangesAsync(ct)`.
- **Entity configurations**: `IEntityTypeConfiguration<T>` under `Infrastructure/Persistence/Configurations/` (auto-discovered).

## About Generic Repository over EF Core

This is an **intentional constraint** from the PRD (NFR-M3), not a best practice. `DbContext` already provides UoW semantics and `DbSet<T>` is a repository. We add the wrapper to satisfy the requirement. **Do not refactor it away.**

## DTO discipline (enforced by tests)

- EF entities never leave the Application/Infrastructure boundary. Controllers and MediatR responses return DTOs.
- A DTO lives next to the Command/Query it serves. Shared DTOs (like `UserDto`) live in `Application/Common/Dtos/`.

## Validation

- FluentValidation `AbstractValidator<T>` in the same folder as the Command/Query it validates. Auto-discovered by `AddValidatorsFromAssembly`.
- The `ValidationBehavior` pipeline throws `ValidationException` on failure — the centralized exception handler maps it to 400 ProblemDetails with a `errors` extension.

## Exceptions

- Application throws domain-shaped exceptions from `Application/Common/Exceptions`: `NotFoundException`, `ForbiddenException`, `ConflictException`.
- **Never** return HTTP status codes from handlers. The middleware maps exceptions to status codes.
- **Never** leak stack traces in Production. The centralized handler enforces this.

## For data-isolation stories

When ownership mismatch occurs (Customer A requesting Customer B's ticket), throw `NotFoundException`, not `ForbiddenException`. This is deliberate: 403 leaks existence information; 404 does not. Only throw `ForbiddenException` when the caller legitimately knows the resource exists and lacks a specific right (e.g., a Customer trying to change priority — priority-change is an admin-only capability regardless of ownership).

## Logging

- Every request has a correlation ID. It appears in logs and in the `X-Correlation-Id` response header.
- Never log request bodies wholesale. Never log passwords, tokens, or password hashes — the `SensitivePropertyDestructuringPolicy` redacts common names, but structured logging with `{@Object}` on a credential payload is still risky.

## Testing

- **Unit tests** cover Application handlers. Mock `IUnitOfWork`, `IPasswordHasher`, etc. via NSubstitute.
- **Integration tests** boot the API via `WebApplicationFactory<Program>` against a Testcontainer SQL Server. Cover request → handler → DB round-trip.
- **Architecture tests** guard layer boundaries and DTO discipline. New violations fail CI.
- **Frontend unit tests** run under `ChromeHeadlessNoSandbox` for CI compatibility.

## Migrations

```bash
dotnet ef migrations add <SemanticName> \
  --project src/TicketSystem.Infrastructure \
  --startup-project src/TicketSystem.Api
```

Semantic name examples: `AddUsers`, `AddTickets`, `AddTicketRowVersion`. Never use auto-generated names like `Migration1`.

Dev auto-migrates on startup. Prod does not — run `dotnet ef database update` from your deployment pipeline.
