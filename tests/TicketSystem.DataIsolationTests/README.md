# TicketSystem.DataIsolationTests

Automated proof that a Customer cannot access, modify, or infer the existence of another Customer's data through any API path.

## What this suite proves

- **FR21** — cross-customer access to a ticket returns `404 Not Found`, never `403`. The response body carries no title, description, id, or `customerId` echo from the forbidden resource.
- **FR24** — `?customerId=<other>` query-string overrides and body-supplied `customerId` fields are silently ignored; caller identity always comes from the JWT `sub` claim.
- **NFR-S7** — JWTs signed with any key other than the server's are rejected as `401 Unauthorized` before authorization runs. Expired JWTs likewise 401.
- **FR50 / NFR-M5** — the suite runs on every push; any failure fails the pipeline.

## How to run locally

```bash
dotnet test tests/TicketSystem.DataIsolationTests --configuration Release
```

Requires Docker (the suite uses Testcontainers to spin up a real SQL Server 2022 instance).

First run pulls the ~1.5 GB `mcr.microsoft.com/mssql/server:2022-latest` image — subsequent runs are cached.

## What to do when a test fails

**A failure in this suite is a security regression, not a test flake.** Do not `[Skip]` the test or weaken the assertion to unblock CI. Instead:

1. Reproduce locally.
2. Trace the failing assertion back to the production code path that lost the isolation guarantee.
3. Fix the production code. Re-run the suite until green.
4. If the failure is a genuine timing/environment flake (e.g., Docker startup race), document it in the PR description AND file an issue to make the test deterministic — do not just retry-and-hope in CI.

## Coverage gaps intentionally deferred

The following scenarios from the story spec are called out but not yet implemented — they should be added as follow-up:

- **Refresh-token replay + family invalidation** — covered indirectly by `TicketSystem.IntegrationTests` today. Copy into this project when time permits so the security surface is story-complete here.
- **Path-enumeration + timing tests** — spec called for `[Trait("Category","Perf")]` timing assertion (median forbidden-vs-nonexistent within 20 ms). Deferred because the timing signal is noisy on shared CI runners. When adding, follow the warmup + `GC.Collect()` + median-of-50 protocol in the story spec.
- **Additional cross-role verifications** — Agent-can't-see-other-agent's-assigned-tickets; Admin-can-see-everything. Add alongside the current cross-customer tests as an `AgentCrossAccessTests.cs` and `AdminAccessTests.cs`.

## Fixture

`IsolationApiFactory` spins up a dedicated MSSQL container (separate from the general integration test suite so state can't leak between). Seeds:

- Customer A (`isolation-a@test.local`) — 2 tickets
- Customer B (`isolation-b@test.local`) — 3 tickets, all with `SECRET_B_*` sentinel titles/descriptions so no-leak assertions can grep for them
- Customer C — 1 ticket (present so a bug that returns "everyone except A" would still fail)
- 1 Admin, 2 Agents

Password for all seeded users: `Passw0rd!`.

Between test classes: Respawn wipes user tables + `IsolationApiFactory.ResetAndReseedAsync` re-seeds.
