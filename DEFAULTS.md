# DEFAULTS.md

Why each default in CLAUDE.md exists, how strong the reason is, and when to drop it. CLAUDE.md holds the rules. This file holds the reasoning, so the rules stay short for the AI and the reasons stay available for review.

Strength:
- **Strong**: backed by platform guidance, correctness, or the delivery rules of the repo. Override only if the brief or repo demands it.
- **Preference**: a sound choice among several good ones. Override freely for a fitting reason.

## Process

| Default | Reason | Strength | Override when |
|---|---|---|---|
| Merge each feature to main as soon as it passes; one open branch at a time | `main` is the single source of truth. Long-lived branches drift, conflict, and hide unfinished work. | Strong | Never. |
| Merge commits, never squash; no force-push, rebase or amend | The full history shows how the work was done. Squashing or rewriting erases that record. | Strong | Never. |
| Never create tags | Tags belong to the release process, not to feature work. | Strong | Never. |
| Push after every commit | Unpushed work is invisible to everyone else and lost if the machine fails. | Strong | Never. |
| One task per commit, about 20 minutes each | Small commits make the history readable and each step reviewable. Twenty minutes keeps each step small enough to review in a minute or two. | Preference | A task that can't be split without leaving a broken build. |
| FEATURES.md as the single plan, PROCESS-LOG.md as the record | One place for scope prevents drift between chat, code and docs, and lets anyone see the plan and its status. | Strong | Never. |
| Two approval gates per task: approach, then audited diff | Catches wrong direction before code is written and wrong code before it's committed. A third gate costs more time than it saves. | Preference | Trivial changes such as a typo can share one gate. |
| Done work is never unticked or rewritten; changes are new tasks or new `CR:` features | The history must show what changed and why. Unticking hides that a requirement moved, which is exactly what anyone reading the history needs to see. | Strong | Never. |
| No silent assumptions | An unapproved assumption is a requirement nobody agreed to and I can't defend. | Strong | Never. |
| Running a command approves its mechanical steps; content changes need a gate | Approving every log update and push would double the gates without adding judgement. Code, scope and rules are where judgement matters. | Preference | Never loosen it for code. |
| Provided checkers run with every verify step | If the brief ships a test harness, it's probably the definition of done. Finding a failure at the end is too late. | Strong | None. |
| Plain writing in all docs and commits | Other engineers read FEATURES.md, PROCESS-LOG.md and README.md. Specific, unpadded writing reads as considered work. Template language reads as generated. | Strong | None. |
| `.gitignore` before the first build | Committed `bin/` and `obj/` folders bloat the repo and look careless in the history. | Strong | Repo already has one. |
| Clear the AI's context after each feature | State lives in files, so nothing is lost. A fresh context costs fewer tokens and drifts less. | Preference | Mid-feature, never. |
| Solution outline approved before features; F00 Foundation first, capped at 25 minutes | Agreeing the big picture first stops each feature inventing its own structure. Building cross-cutting concerns once means every feature gets them for free. The cap stops setup from eating feature time. | Strong | Existing repo already has the foundation. F00 shrinks to characterisation tests. |
| `/review` of all branch commits required before merge | Per-task audits miss problems that only show across tasks: inconsistency, duplication, untested criteria. Independent agents haven't seen the work being produced. | Strong | F00 Foundation, where it's recommended but optional. |
| Regular progress checks against estimates, with cuts agreed early | Scope decisions are cheap early and expensive late. Comparing actuals with estimates shows drift while there's still room to act. | Preference | Very short pieces of work. |

## Structure

| Default | Reason | Strength | Override when |
|---|---|---|---|
| Follow existing conventions in an existing repo | Consistency beats personal preference in a shared codebase. Rewriting style is noise in the diff. | Strong | The brief asks for a refactor. |
| Characterisation tests before changing untested code | Without them, there's no proof a change preserved existing behaviour. | Strong | The code being changed is being deleted. |
| Organise by feature (vertical slices), not by technical layer | A feature's code lives together, so adding or changing one touches one folder. Established practice in .NET (Jimmy Bogard's vertical slice architecture). | Preference | The repo already uses layered or clean architecture. |
| Api, Domain and Infrastructure projects at most; one project is fine for small briefs | Separate projects let the compiler enforce dependency direction. More projects add ceremony a small scope can't repay. | Preference | The brief's scope is clearly tiny, or clearly larger. |
| Per-feature `AddX()` and `MapX()` registration | Microsoft's own extension method convention. Keeps Program.cs short and features independent. | Strong | The repo registers services another established way. |
| Cross-cutting concerns in middleware | The ASP.NET Core pipeline is built for this. New features get error handling, logging and correlation without extra code. | Strong | None in practice. |
| Interfaces only at real boundaries or real variation | An interface with one implementation adds indirection without flexibility. Boundaries (database, clock, HTTP) need them for testing. | Strong | The brief signals multiple implementations. |
| Extract on the third duplicate, not the second | Martin Fowler's rule of three. Two similar pieces of code often turn out to differ. Abstracting early guesses wrong. | Preference | The duplication is a business rule that must stay identical. Extract it immediately. |
| Minimal APIs with endpoint groups for new code | Less ceremony than controllers, pairs naturally with feature folders. Microsoft's recommended default for new APIs. | Preference | The repo uses controllers, or the brief expects them. |

## .NET

| Default | Reason | Strength | Override when |
|---|---|---|---|
| .NET 10 | The current LTS release, with the newest language and platform features. | Strong | The repo targets another version. Match it. |
| Nullable reference types on | Turns null bugs into compile-time warnings. | Strong | The existing repo has it off. Don't flip it as a side effect of feature work. |
| `TreatWarningsAsErrors` in new projects | Stops warnings piling up unnoticed. Risk: it can block the build under time pressure. | Preference | Repeated warning fixes are eating time. Drop it and note it in README. |
| Async with `CancellationToken` everywhere | Cancelled requests stop doing work. Sync-over-async risks thread starvation. | Strong | None. |
| Inject `TimeProvider`, store UTC | Built into .NET since version 8. Makes time-dependent logic testable. UTC avoids time zone and daylight-saving bugs. | Strong | None. |
| Options pattern with `ValidateOnStart` | Bad configuration fails at startup, not on the first request in production. | Strong | None. |
| `decimal` for money | `double` can't represent most decimal fractions exactly, so sums drift. | Strong | None. |
| Invariants guarded in the domain | Rules live in one place and can't be bypassed by a new endpoint. | Strong | The repo puts rules in services. Follow it. |
| No MediatR, AutoMapper or FluentAssertions v8+ | All three moved to commercial licences in 2025. Adding them to a new codebase is a licensing decision for the codebase owner, not a default. MediatR's main benefit, decoupling, is covered by plain handlers. | Strong for new code | The repo already uses them. Keep them. |
| No generic repository over EF Core | `DbContext` is already a unit of work and its `DbSet`s are already repositories. A generic wrapper hides EF features and adds a layer. | Preference, widely held | The repo already has one. |

## Data access and caching

| Default | Reason | Strength | Override when |
|---|---|---|---|
| SQLite through EF Core when no database is named | Zero install, so anyone can clone and run it. A real relational engine, unlike EF InMemory. | Preference | The brief or repo names a database, or the queries sort or total money (SQLite stores decimal as text and EF can't translate those queries). |
| PostgreSQL in Docker with Testcontainers when SQLite won't do | Full relational behaviour, real concurrency, and tests against the same engine as the app. | Preference | Docker isn't available. Then SQLite with money as integer minor units. |
| EF Core over Dapper | Migrations, LINQ, change tracking and concurrency tokens are the fastest route to correct data access. Dapper wins on raw read performance, which most scopes rarely need. | Preference | The repo uses Dapper, or a specific read query is performance-critical. |
| `AsNoTracking` and `Select` projections on reads | Skips change-tracking overhead and loads only the columns returned. | Strong | None. |
| Committed EF migrations, auto-applied in Development only | The app runs on first launch for anyone who clones it. Applying migrations at startup in production is risky with several instances, so it stays a deployment step. | Strong | The repo has its own migration approach. |
| Constraints and indexes in the database, not only in code | Application checks can race. A unique index can't. | Strong | None. |
| Bounded list endpoints, pagination agreed at kickoff | Unbounded lists fail as data grows. The page size is a product decision, so it's asked, not assumed. | Strong | The brief defines it. |
| No caching unless the brief signals a need | Caching brings stale data and invalidation bugs. Without a stated read-heavy load or latency target, it's complexity with no measured benefit. | Strong | The brief describes read-heavy data, slow dependencies or latency targets. Then `HybridCache` with explicit invalidation. |
| Development-only seed data | Gives the demo and Scalar something real to show. Tests set up their own data so they stay independent. | Preference | The brief supplies data. |

## Modern C# and .NET

| Default | Reason | Strength | Override when |
|---|---|---|---|
| .NET 10 and C# 14 features where they improve clarity | Shows current platform knowledge, and modern features (records, pattern matching, collection expressions) usually mean less code and fewer bugs. Using them only where they help avoids novelty for its own sake. | Preference | Existing repo on an older version. Match it. |
| Primary constructors for DI, normal constructors for entities | Primary constructors remove constructor and field boilerplate in services. Their parameters are mutable and can't hold guard logic, so entities with invariants need a normal constructor. | Strong | None. |
| Records with `required`/`init` for DTOs and value objects | Value equality and immutability by default. The compiler enforces that required data is set. | Strong | None. |
| `TypedResults` in endpoints | Return types become visible to OpenAPI and to tests, without extra attributes. | Strong | Repo uses controllers. |
| Built-in .NET 10 minimal API validation for simple input rules | No extra package, integrates with ProblemDetails. FluentValidation stays an option for conditional or cross-field rules. | Preference | Complex conditional rules, or repo already uses FluentValidation. |

## API documentation

| Default | Reason | Strength | Override when |
|---|---|---|---|
| Built-in OpenAPI with Scalar, development only, no Swashbuckle | Microsoft removed Swashbuckle from the templates in .NET 9 in favour of the built-in package. Scalar is a maintained, modern UI for that document. Exposing API docs in production widens the attack surface. | Preference | The repo already uses Swashbuckle. Keep it. |
| Full endpoint metadata | Without response types and ProblemDetails declarations, the generated document misleads its readers. | Strong | None. |
| A `.http` file per feature | Runnable, version-controlled examples that work in VS Code and Rider. They double as the demo script. | Preference | The repo uses another request collection format. |
| `.editorconfig` and `dotnet format` | Consistent formatting keeps diffs about behaviour, not whitespace. | Strong for new code | The repo has its own rules. Use them. |
| No CI workflow added | Existing Actions workflows may run the repo owner's automation. Adding workflows uninvited risks interfering with it. | Strong | The brief asks for CI. |

## Errors and observability

| Default | Reason | Strength | Override when |
|---|---|---|---|
| ProblemDetails for every error response | RFC 9457 standard, built into ASP.NET Core. Clients get one consistent error shape. | Strong | The brief specifies another error format. |
| Result types for expected failures, exceptions for unexpected ones | Expected failures are part of the contract and should be visible in method signatures. Exceptions are expensive and hide control flow. | Preference, defensible | The repo uses exceptions mapped by middleware. Follow it. |
| One global `IExceptionHandler`; never catch `Exception` elsewhere | One place to log and shape unexpected errors. Scattered catch-all blocks hide bugs. | Strong | None. |
| Validate at the boundary, reject rather than fix | Bad data never reaches the domain. Silently fixing input hides client bugs. | Strong | None. |
| Log business events, handled failures, unexpected failures, dependency calls and requests | Enough to reconstruct what happened to any request, without logging noise. | Strong | None. |
| Structured logs with templates, no string interpolation | Properties stay queryable. Interpolation loses structure and allocates even when the log level is off. | Strong | None. |
| Correlation ID on every request | Ties every log line from one request together, across services. | Strong | None. |
| `LoggerMessage` source generation on hot paths | Avoids allocations on paths called thousands of times. | Preference | No hot paths exist, which is likely. Skip it. |
| `/health` and `/health/ready` | The standard liveness and readiness split used by load balancers and orchestrators. | Strong | The brief names other endpoints. |
| No personal data or secrets in logs | Logs are widely readable and kept a long time. | Strong | None. |

## Resilience

| Default | Reason | Strength | Override when |
|---|---|---|---|
| Standard resilience handler on HttpClients | Microsoft's packaged timeout, retry with jitter, and circuit breaker. Avoids hand-rolled retry bugs. | Strong when there's an outbound call | No outbound calls. Then skip it entirely. |
| Idempotency for retryable writes | Retries happen. Without idempotency they create duplicates. | Strong where relevant | Writes that are naturally idempotent. |
| Optimistic concurrency on contested records | Prevents silent lost updates when two people edit at once. | Strong where relevant | Single-writer data. |

## Tests

| Default | Reason | Strength | Override when |
|---|---|---|---|
| Every acceptance criterion has a test | Proves the feature does what was agreed. | Strong | Untestable criteria, with the reason written down. |
| Unhappy paths first | Happy paths usually work. Bugs live in edges and failures. | Strong | None. |
| Integration tests through HTTP with `WebApplicationFactory` | Tests the real pipeline: routing, validation, serialisation, error handling. | Strong | The brief has no HTTP layer. |
| SQLite in-memory or Testcontainers, not EF InMemory | Microsoft's own docs advise against the InMemory provider for testing: it isn't relational and hides real database behaviour. | Strong | The repo already uses InMemory. Keep it and note the limitation. |
| Mock only at real boundaries | Mocking your own classes tests the mocks, not the code. | Strong | None. |
| `Method_Scenario_ExpectedResult` names | Failing test names read as bug reports. | Preference | The repo has another convention. |
| xUnit `Assert` or Shouldly | Free, maintained, readable. | Preference | The repo uses another library. |
| Build and test before every commit | Keeps main green and every commit a working checkpoint. | Strong | None. |
