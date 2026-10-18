# CLAUDE.md

Instructions for Claude Code in this repository. Read this fully before any task.

## Delivery

`main` is the single source of truth. Work isn't done until it's merged to `main`, builds and passes. Nothing important lives only on a branch or a laptop. The commit history is part of the deliverable: it shows how the work was done, not only the result.

## Sources of truth

After a `/clear`, re-read FEATURES.md and the last 15 lines of PROCESS-LOG.md before doing anything. They hold all the state.

- `FEATURES.md`: what we build. Features, tasks, acceptance criteria, status.
- `PROCESS-LOG.md`: how we worked. Append only.
- `README.md`: how to run it, assumptions, trade-offs, what's left. Written at wrap-up.
- The brief or requirements, wherever they are in the repo. If the brief and FEATURES.md disagree, stop and ask me.
- Any checker, test harness or script the brief provides (Python, shell, Docker). If one exists, it is part of every verify step alongside `dotnet test`.

## Commands

| Command | When |
|---|---|
| `/kickoff` | Once, right after the content lands |
| `/pm-drill <text>` | New requirement, change, or anything unclear |
| `/next-task` | Build the next task: one task, one commit |
| `/review` | After a feature's last task. Required before `/ship-feature` |
| `/ship-feature` | Merge a finished feature to main |
| `/feedback <text>` | When I correct you |
| `/checkpoint [minutes left]` | Progress against estimates, and a scope call |
| `/wrap-up` | To close out: freeze code, finish docs, final push |

## What wins when sources disagree

1. The brief. Explicit instructions in it override everything below.
2. The existing repo's conventions, when there is existing code.
3. Decisions I approve during the session, recorded in FEATURES.md.
4. The defaults in this file.

The .NET, structure and testing rules below are defaults. DEFAULTS.md gives the reason, strength and override condition for each. /kickoff checks each against the brief and repo, and I approve any override. A new default added during the session needs a row in DEFAULTS.md too. Git rules, approval gates and the no-silent-assumptions rule are not defaults: they always apply.

## Git rules

- One feature per branch: `feature/F0X-short-name`, cut from the latest `main`.
- One task per commit. Message format: `F0X.Y: imperative summary`.
- Push after every commit, including on branches.
- Ship each feature to `main` as soon as its tasks pass, with a merge commit (never squash). Never more than one unmerged branch.
- `main` always builds and passes tests.
- Never create, move or delete tags. Never force-push. Never rewrite pushed history. Never delete branches.
- FEATURES.md and PROCESS-LOG.md changes go in the same commit as the work they describe.

## If the repo has existing code

- Read the structure, conventions and tests before writing anything. Follow existing conventions even where I'd choose differently, and note the disagreement in README instead of rewriting.
- Before changing untested existing code, add characterisation tests that pin its current behaviour.
- No mass reformatting, renaming or moving files. Diffs stay small and on-task.
- If the existing build or tests are broken at the start, log it in PROCESS-LOG.md before touching anything.

## If the repo is greenfield

```
<App>.sln
src/
  <App>.Api/              endpoints, DI, middleware, ProblemDetails, health checks
    Features/<Feature>/   endpoint, request/response, handler, validator per use case
  <App>.Domain/           entities, value objects, domain errors. No framework dependencies.
  <App>.Infrastructure/   EF Core, external clients, persistence
tests/
  <App>.UnitTests/        domain and handler logic
  <App>.IntegrationTests/ HTTP-level tests through WebApplicationFactory
Directory.Build.props     nullable, warnings as errors, LangVersion
```

Organise by feature, not by technical layer. Don't create projects or abstractions the brief doesn't need. Three projects plus tests is the ceiling, and one API project plus tests is fine for a small brief.

## .NET conventions

- .NET 10, nullable reference types on, `TreatWarningsAsErrors` in new projects.
- Async all the way down. Pass `CancellationToken` through every async call.
- Inject `TimeProvider`. Never call `DateTime.Now` or `DateTime.UtcNow` directly. Store and compare times in UTC.
- Configuration through the options pattern, validated at startup with `ValidateOnStart`.
- `decimal` for money, never `double`. Be explicit about rounding.
- Domain rules live in the domain: guard invariants in constructors and methods, not in controllers.
- Value objects for concepts with rules (amounts, identifiers, date ranges) where it clarifies the code. Records are fine for this.
- No MediatR, AutoMapper or FluentAssertions v8+, all commercially licensed now. Use plain handler classes, hand-written mapping, and xUnit `Assert` or Shouldly.
- No generic repository over EF Core. Use `DbContext` directly or a focused query service.
- Prefer minimal APIs with endpoint groups for new code. Match the existing style for existing code.

## Modern C# and .NET

Target .NET 10 and C# 14 in new code. Use a modern feature when it makes the code shorter and clearer, never just because it exists. In an existing repo, stay within its target framework and language version.

Use by default:
- File-scoped namespaces, global usings for the few namespaces every file needs, nullable reference types.
- Primary constructors for dependency injection in services and handlers.
- Records for DTOs, requests, responses and value objects. `required` and `init` for properties that must be set.
- Collection expressions (`[]`, `[..items]`), pattern matching and switch expressions over if-else chains.
- Raw string literals for multi-line text such as SQL or JSON in tests.
- `TypedResults` in minimal API endpoints, so return types show up in OpenAPI and tests.
- The built-in minimal API validation in .NET 10 (`AddValidation()` with data annotations) for simple input rules. Domain rules stay in the domain.
- `TimeProvider`, `IExceptionHandler`, `ProblemDetails`, keyed services where two implementations of one interface exist.

Use when it fits:
- C# 14 `field` keyword for a property that needs light validation, instead of a separate backing field.
- C# 14 extension members for grouping related extension methods.
- Null-conditional assignment (`order?.Status = ...`) where it reads better than an if.
- `HybridCache` if the brief needs caching.

Avoid:
- Primary constructors on domain entities that guard invariants. Their parameters are mutable captures, so use a normal constructor with guards.
- New syntax in a file that otherwise uses the old style. Consistency within a file wins.

If you're not sure a feature exists in the target version, check by building, don't guess.

## API documentation and manual testing

- OpenAPI through the built-in `Microsoft.AspNetCore.OpenApi` (`AddOpenApi()`, `MapOpenApi()`), with Scalar as the UI (`Scalar.AspNetCore`, `MapScalarApiReference()`). Development environment only. No Swashbuckle.
- Every endpoint declares its metadata: `WithName`, `WithSummary`, the success response type, and `ProducesProblem` for each error status it can return. The OpenAPI document must match real behaviour.
- Each feature folder has a `<Feature>.http` file with runnable requests: the happy path and the main failure cases, using a `@baseUrl` variable.
- Scalar and `.http` files are for exploring and demonstrating. Automated tests remain the proof.
- New projects get an `.editorconfig`. Run `dotnet format` before each commit. In an existing repo, use its formatting rules.

## Data access and caching

The brief and the existing repo decide first. These defaults apply only when neither does, and /kickoff proposes them in the fit check for my approval.

**Database**
- No database named: SQLite file through EF Core. It needs no install or Docker, so anyone can clone and run it.
- Watch for SQLite's limits with EF Core: `decimal` is stored as text, so sorting or summing money in the database fails to translate. If the brief sorts, filters or totals money in queries, raise it in the fit check and propose either PostgreSQL in Docker (with Testcontainers in tests) or storing money as whole minor units (`long` cents) with a value object.
- The brief needs real concurrency, JSON querying or several services sharing data: propose PostgreSQL.
- Never the EF InMemory provider, in the app or in tests.

**EF Core or Dapper**
- EF Core by default: migrations, LINQ, change tracking and concurrency tokens get to correct data access fastest.
- Dapper only where the repo already uses it, or for a read query EF can't express cleanly or that the brief marks as performance-critical. Say why in the feature's Approach.
- Reads use `AsNoTracking` and project to response types with `Select`. No loading whole entities to return three fields.

**Schema**
- EF migrations, committed. Applied automatically at startup in Development only, so the app runs on first launch. Note in README that production would apply them as a deployment step.
- Uniqueness and required fields enforced in the database (unique indexes, non-null columns), not only in code. Code checks give good error messages. The database guarantees the rule.
- An index for every column used in a lookup or filter.
- A concurrency token on records two users could edit at once.

**Lists**
- Every list endpoint is bounded. Propose pagination (page size with a maximum) at kickoff as an ambiguity, not as a silent choice.

**Caching**
- No cache by default. A cache adds invalidation bugs and stale data, and most scopes don't need it unless the brief says so.
- Add one only when the brief signals it: read-heavy data that rarely changes, an expensive external call, or a stated latency target. Then `HybridCache` with explicit expiry and invalidation on write, named in the feature's Approach.

**Seed data**
- Development-only seed data so the app has something to show in Scalar and in a demo. Never seeded in tests; tests arrange their own data.

## Readable code

- Names say what something is or does in the domain's language: `claimAmount`, `ArchiveClient`, `IsWithinCoverPeriod`. No `data`, `temp`, `obj`, `mgr`, `helper`, `util`, `Process()`, or single letters outside short lambdas.
- Methods are verbs, booleans read as questions (`isArchived`, `hasExpired`), collections are plural.
- One level of abstraction per method. Extract a well-named private method rather than writing a comment that explains a block.
- Short methods, early returns, no nesting deeper than two levels.
- No magic numbers or strings. Use named constants or options.
- Comments explain why, never what. Assumption references (`// A2: ...`) are the exception.
- Code should read top to bottom like the acceptance criteria it implements.

## Extensibility

Extensibility comes from structure, not from abstractions added in advance.

- Adding a feature means adding a feature folder plus one registration line. It must not require editing other features.
- Each feature registers its own endpoints and services through one extension method (`app.MapClientEndpoints()`, `services.AddClientFeature()`). `Program.cs` stays a short list of these calls.
- Cross-cutting concerns (errors, logging, correlation ID, validation) live in middleware, filters or shared infrastructure, so new features get them for free.
- Configuration through options, so behaviour changes without code changes where the brief hints at it.
- Introduce an interface or strategy only where the brief signals real variation (several providers, rule sets, formats). Name that variation point in the feature's Approach.

## Errors

- Expected failures (validation, not found, conflict, business rule broken) return a result type and map to 400, 404, 409 or 422 with ProblemDetails. Don't use exceptions for control flow.
- Unexpected failures throw. One global `IExceptionHandler` logs them with the correlation ID and returns a 500 ProblemDetails with no stack trace or internals.
- Never catch `Exception` outside the global handler. Never swallow an exception.
- Validate every input at the boundary. Reject bad input with a clear message, don't silently correct it.

## Observability

Comprehensive means every event that matters, with context. It doesn't mean logging every method.

- Log every business event (created, updated, archived, rejected) at Information, with the entity ID as a structured property.
- Log every handled failure at Warning, with the reason and the input that caused it (minus personal data).
- Log every unexpected failure once, in the global handler, at Error, with the exception.
- Log every outbound dependency call with its duration and outcome.
- Request logging for every HTTP request: method, path, status, duration.
- Use logging scopes to attach the correlation ID and key entity IDs to everything inside a request.
- Use `LoggerMessage` source-generated methods for logs on hot paths.
- Structured logging with message templates. Never string interpolation in a log call.
- A correlation ID on every request, taken from the `X-Correlation-ID` header or generated, echoed in the response header, included in logs and ProblemDetails.
- `/health` for liveness and `/health/ready` for dependencies.
- Information for business events, Warning for handled problems, Error for failures. No logging of personal data or secrets.

## Resilience

Only where there's an outbound dependency:
- `Microsoft.Extensions.Http.Resilience` standard handler on HttpClients: timeout, retry with backoff and jitter, circuit breaker.
- Idempotency keys or natural keys for any write a client might retry.
- Optimistic concurrency (row version) on records two people could edit at once.

## Tests

- Every acceptance criterion in FEATURES.md has at least one test, named after the behaviour.
- Unhappy paths first: invalid input, missing data, not found, duplicates, boundaries, concurrency where relevant.
- Integration tests go through HTTP with `WebApplicationFactory`. Use SQLite in-memory or Testcontainers, not the EF InMemory provider.
- Don't mock what you own. Mock only at real boundaries (HTTP, clock, external services).
- Test names: `Method_Scenario_ExpectedResult`.
- Run `dotnet build`, then `dotnet test --no-build`, before every commit. Never commit red. Build after any multi-file change before moving on, so errors surface early.

## No silent assumptions

- You never decide a requirement, business rule, default value, error behaviour or design choice on your own.
- When something is unclear, stop and ask. Offer your recommended answer so I can approve it quickly.
- An assumption exists only after I approve it. Then it goes into FEATURES.md (Assumptions) and PROCESS-LOG.md (ASSUME).
- In code, every approved assumption that shapes behaviour is referenced by its ID in a short comment, e.g. `// A2: negative amounts rejected`.
- If you notice code relying on something nobody approved, flag it.

## Human approval

Nothing changes without my approval. There are two gates per task, and no more:

1. **Approach gate**, before any code: the approach, files, tests and risks. I approve or correct.
2. **Commit gate**, before committing: the audited diff. I approve or ask for changes.

A new feature also gets an approach, written into FEATURES.md and approved, before its first task.

When I run a command, that's my approval for its mechanical steps: updating FEATURES.md and PROCESS-LOG.md, committing them, pushing, merging. Any change to code, scope, assumptions or rules still goes through a gate.

## Working style

- A task is about 20 minutes. If it's bigger, split it and update FEATURES.md first.
- When I correct you, I'll use `/feedback`. Repeated corrections become rules in this file.

## Simplicity over abstraction

- Write the simplest code that meets the acceptance criteria and is easy to extend at the obvious seams.
- No interface with one implementation unless it's a real boundary (database, clock, HTTP, external service).
- No pattern, base class or layer the current features don't need. Duplication twice is fine. Extract on the third.
- Apply SOLID to keep classes small and focused, not as a reason to add indirection.

## Writing style for docs

FEATURES.md, PROCESS-LOG.md, README.md and commit messages are read by other engineers. Write them like a careful engineer, not like a template.
- Plain, specific sentences. Vary their length. Say the concrete thing: "rejects end dates before start dates", not "robust validation".
- No filler openers, no hype words (robust, seamless, comprehensive, leverage), no summaries of what was just said.
- Few dashes. Use full stops and commas.
- Log entries: one or two sentences, facts first, then the reason.
- Record only what has happened. Never write a log entry, summary or README claim before the work it describes is done.

## Rules learned during this session

<!-- /feedback appends rules here as R1, R2... -->
