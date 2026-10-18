# FEATURES.md

The plan for this work and its single source of truth. Every requirement, assumption and change lands here before any code. PROCESS-LOG.md records how the plan was carried out.

Base commit: `fde7dac` · Repo state: greenfield · Model: Claude Opus 5.5, effort low

## How this file works

- Features are numbered in build order. Each has a priority, a status, a branch, and testable acceptance criteria.
- Tasks are numbered `F0X.Y`. One task is one commit, about 20 minutes of work.
- A feature is Done when every criterion has a passing test (or a written reason why not) and it's merged to `main`.
- Statuses: `Todo`, `In Progress`, `Done`, `Cut`. A cut feature stays listed with the reason.
- Done work is never unticked or rewritten. Changes are added as new tasks (`(CR)` or `fix:`) or new features (`CR:` or `Fix:`, with `amends F0X`). Replaced criteria are struck through and marked `superseded`.

## Brief summary

The brief (TICKET-APP-SPEC.md, TICKET-APP-ARCHITECTURE.md) describes an event registration and check-in tool: clients, events, attendees, signed QR invitations, volunteer check-in at counters, and live dashboards. Its P0 has 15 features, and its stated risks are email deliverability, check-in races and cross-client data leaks.
The repo started greenfield, with only the brief and the workflow docs. This session builds only the .NET API, with a 2-hour budget. Local stand-ins replace AWS, and there is no frontend or infrastructure code.
Must: Foundation, staff auth with roles and tenant scope, and clients (the first client-scoped resource, which proves isolation). Stretch, in order: events, manual attendees, QR tokens, QR check-in with the race guard.
Every other brief feature stays in the brief's backlog and is not planned for this session.

## Solution outline

**Domain model.** `Client` (name unique ignoring case, archivable; archived means read-only) owns `Event`s. An `Event` has a venue, start and end stored in UTC plus an IANA time zone ID (end not before start), a status of Draft, Live or Closed (Closed is read-only), and a per-event ECDSA signing key. An `Event` owns `Attendee`s: email or phone required, unique per event on normalised email and then phone, plus a token version and a `CheckedInAt` projection. `CheckInEvent` is append-only (D1), has a UUIDv7 ID (D4), records a type (`CheckedIn`, `DuplicateScan`, `CheckInReverted`), the actor, the source and both times, and is the source of truth for check-in history. Staff users are ASP.NET Core Identity users with one role and an optional client scope.

**Request flow.** Minimal API endpoint → built-in validation for input shape → a handler class per use case → domain entity methods guard the invariants and return a result → EF Core `DbContext` saves → the handler's result maps to `TypedResults` or ProblemDetails (400, 404, 409, 422). Check-in decides the winner with a conditional `ExecuteUpdateAsync` on `CheckedInAt IS NULL` in the same transaction as the event insert (ADR-004).

**Cross-cutting.** Correlation ID middleware (`X-Correlation-ID`, pushed into the logging scope and into ProblemDetails). Serilog writes JSON request logs and structured logs. One global `IExceptionHandler` returns 500 ProblemDetails. Tenant scope comes from EF global query filters on `ClientId`, read from the user's claims, and a request filter rejects deactivated users. `TimeProvider` is injected everywhere. `/health` and `/health/ready` (the latter checks the database).

**Structure.** `SecuPass.Api` holds endpoints, DI, middleware, hosted services, and `Features/<Module>/<UseCase>`, with the modules Identity, ClientsEvents, Attendees, Messaging and CheckIn. `SecuPass.Domain` holds entities, value objects and domain errors, with no framework references. `SecuPass.Infrastructure` holds the EF Core Npgsql `DbContext`, migrations, the signer and the email stand-ins. `SecuPass.Worker` is added with the first queued job. `tests/SecuPass.UnitTests` and `tests/SecuPass.IntegrationTests` use `WebApplicationFactory` with Testcontainers Postgres.

**Variation points.** `IQrTokenSigner` (local ECDSA now, KMS later) and `IEmailSender` (fake now, SES later). They are the only interfaces planned beyond `DbContext` and `TimeProvider`. AWS SQS and S3 are replaced by an outbox table with a `BackgroundService`, and by direct upload to the API.

## Assumptions

Only assumptions I approved. Code that depends on one cites its ID.

| ID | Assumption | Reason | Approved |
|---|---|---|---|
| A1 | Payment is out of scope. | Taken from the brief's A1. It avoids gateways, refunds and invoices. | Chandan, 17:24 |
| A2 | Counters are open: any volunteer can check in any attendee, and the counter is recorded for reporting only. | Taken from the brief's A2. Assigned segments would need routing rules. | Chandan, 17:24 |
| A3 | Rack position is optional per attendee (row and column may be blank). | Taken from the brief's A3. Auto-assignment needs a rack layout feature. | Chandan, 17:24 |
| A5 | Every list endpoint is page-based, default page size 50, maximum 200. A larger size is rejected with 400. | Unbounded lists fail at the 10,000-attendee design target. | Chandan, 17:24 |
| A6 | Clients, events and attendees carry a Postgres `xmin` row version. An update with a stale version returns 409. | Two staff members editing the same record must not silently overwrite each other. | Chandan, 17:24 |
| A7 | Event start and end are stored as UTC instants plus an IANA time zone ID. | Times stay comparable, and the event's local time can still be shown. | Chandan, 17:24 |
| A8 | Duplicate checks compare emails trimmed and case-insensitive, and phones with spaces, dashes and brackets removed. Duplicates are rejected, never auto-fixed. | Matches the brief's `lower(email)` unique index and catches the same phone written differently. | Chandan, 17:24 |
| A9 | A Client Viewer who requests another client's record by ID gets 404, not 403. | A 403 would confirm the record exists, which lets a viewer probe other clients' IDs. | Chandan, 17:30 |
| A10 | Changing an archived client, or anything under it, returns 422 with ProblemDetails. | The request is well formed but breaks a business rule. Closed events are treated the same way. | Chandan, 17:30 |
| A11 | The client logo is left out of F02. | Image upload and storage is its own feature, and nothing in Must uses it. | Chandan, 17:30 |
| A12 | An incoming `X-Correlation-ID` is used only if it is 1 to 64 characters of letters, digits, `-`, `_`, `.` and `:`. Otherwise the API generates a new ID, returns that one, and logs a Warning. The request still proceeds. | The value is logged and echoed, so an unchecked one allows log and header injection. A tracing header should never fail a request. | Chandan, 22:05 |
| A13 | Error responses without a body, such as an unmatched route (404) or a wrong method (405), are returned as ProblemDetails with the correlation ID. | Clients then handle one error shape for every failure. | Chandan, 22:05 |
| A14 | ProblemDetails error bodies are written whatever the request's `Accept` header says. Amends A13. | Without it, `Accept: text/plain` gets a plain-text 404 and an empty 500, so clients would see more than one error shape. | Chandan, 23:43 (F00 review N3) |
| A15 | A malformed request (`BadHttpRequestException`, such as unreadable JSON or a body over the size limit) returns ProblemDetails with the exception's status and its standard title, with no `detail`. The framework's message goes only to the Warning log. | The response is the same in every environment, and parameter or type names don't leak. Field-level errors come from validation. | Chandan, 00:14 |

A4 (volunteer PWA) is frontend only and doesn't apply to this API.

## Decisions

The brief's D1 to D4 apply as written: the append-only check-in log, a personal identity per volunteer, asymmetrically signed QR tokens, and UUIDv7 IDs for device-created records.

| ID | Decision | Alternatives considered | Reason |
|---|---|---|---|
| D5 | PostgreSQL through EF Core with Npgsql. Integration tests run against Testcontainers Postgres, which needs Docker Desktop. | SQLite (the CLAUDE.md default), or a native Postgres install. | The brief names PostgreSQL. The check-in race guard and the `lower(email)` unique index must be proven on the production engine. |
| D6 | Projects are `SecuPass.Api`, `SecuPass.Domain` and `SecuPass.Infrastructure`, with the brief's modules as folders under `Api/Features`. `SecuPass.Worker` is added with the first feature that queues work. That makes four projects plus tests, one over the CLAUDE.md ceiling. | One API project, or creating the Worker in F00. | The brief asks for an API and a worker built from one codebase. Creating the Worker later keeps Foundation within its 25 minutes. |
| D7 | AWS services are replaced locally: an outbox table and `BackgroundService` for SQS, `IEmailSender` with a recording fake for SES, `IQrTokenSigner` with in-process ECDSA P-256 for KMS, and direct upload to the API for S3. | Real AWS SDKs, or LocalStack. | There is no AWS account in the 2-hour budget. The interfaces are real boundaries, so the AWS versions can be added later without touching features. |
| D8 | Serilog with JSON console output. | Built-in `ILogger` console logging. | The brief names Serilog. Code still logs through `ILogger<T>`. |
| D9 | ASP.NET Core Identity with `MapIdentityApi` bearer tokens and roles as claims. Every authenticated request checks the user's active flag. | A development-only header auth scheme, or a hand-rolled JWT. | The architecture assumes Identity. The per-request check locks out a deactivated user immediately, which meets the brief's one-minute rule. |
| D10 | No HTTP resilience handler for now. | The standard resilience handler on every `HttpClient`. | There are no outbound HTTP calls while D7's stand-ins are in place. |
| D11 | Local development and Testcontainers both use `postgres:17-alpine`. A `compose.yaml` runs the local database. | A `docker run` line in the README, or bringing your own Postgres. Postgres 16 or 18. | One command gives a database matching the test engine. Postgres 17 is a mature major version that RDS supports. |
| D12 | EF Core logs at Warning and above. SQL commands are not logged at Information in any environment. | Keep EF command logs at Information, which is the CLAUDE.md default of logging every dependency call with its duration. | Chandan's call at the F00 review (N4). Overrides the CLAUDE.md dependency-logging default for database calls. Failed commands still log at Error. |

## Features

Budget: 2 hours. Must comes to 80 minutes, which is 20 over half; Chandan approved this at 17:30. A checkpoint after F01 decides whether F02 stays in Must. Stretch features are built in the order listed, only if time remains.

### F00 Foundation
Priority: Must · Status: Done · Branch: feature/F00-foundation · Estimate: 25 min

Approach: `SecuPass.slnx` with `src/SecuPass.Api`, `src/SecuPass.Domain` and `src/SecuPass.Infrastructure` on net10.0. Api references Infrastructure and Domain, Infrastructure references Domain, and Domain references nothing. `Directory.Build.props` sets nullable, `TreatWarningsAsErrors` and `LangVersion` 14 for every project. Cross-cutting code lives in `SecuPass.Api/Infrastructure/` (correlation ID middleware, `GlobalExceptionHandler`, health check registration), and `Program.cs` stays a short list of `Add…`/`Map…` calls. `SecuPass.Infrastructure` holds `SecuPassDbContext` (Npgsql) and its migrations. Tests: xUnit v3 with `Assert`. `tests/SecuPass.IntegrationTests` drives the app through `WebApplicationFactory<Program>`, and from F00.3 against a Testcontainers Postgres fixture. Risk: Testcontainers needs Docker running, so integration tests fail without it. Docker 29.8.2 is now installed, which the setup table doesn't yet reflect.

Acceptance criteria:
- [x] The solution builds with no warnings under `TreatWarningsAsErrors`. `bin/` and `obj/` are git-ignored.
- [x] `GET /health` returns 200 without touching the database. An HTTP integration test proves it.
- [x] `GET /health/ready` returns 200 when Postgres is reachable and 503 when it isn't.
- [x] An unhandled exception returns 500 ProblemDetails with the correlation ID and no stack trace or internals, and is logged once at Error.
- [x] Every response carries `X-Correlation-ID`. An incoming value is echoed back, otherwise one is generated.
- [x] Every request is logged as JSON with method, path, status, duration and correlation ID. (Tests check the logged properties through a capturing sink. The JSON console format was checked by running the app, because the test host doesn't expose console output.)
- [x] OpenAPI and Scalar are served in Development only.

Tasks:
- [x] F00.1 Solution, Api, Domain and Infrastructure projects, `.gitignore`, `Directory.Build.props`, `.editorconfig`, the integration test project, and `/health` with a passing HTTP test. `SecuPass.UnitTests` is added with the first domain logic, because an empty test project fails `dotnet test`.
- [x] F00.2 Correlation ID middleware, Serilog JSON request logging, global `IExceptionHandler` with ProblemDetails, OpenAPI with Scalar.
- [x] F00.3 Npgsql `DbContext` with migrations auto-applied in Development, a Testcontainers Postgres fixture for integration tests, and `/health/ready`.
- [x] F00.4 fix: replace the flaky startup options test with a direct `IStartupValidator` test, and add the whitespace connection string, repeated `X-Correlation-ID` and Staging cases (review M1, N6).
- [x] F00.5 fix: `BadHttpRequestException` returns its own 4xx ProblemDetails logged at Warning, an exception after the response started is logged at Error with the correlation ID (S2: no code change, because ASP.NET Core already does this, and a test now pins it), and ProblemDetails is written whatever the `Accept` header (review S1, S2, N3, A14).
- [x] F00.6 fix: the migration log names only migrations actually applied and is skipped when none were, and EF Core logs at Warning (review N2, D12).

Wrap-up note: the README says production connection strings must set `Timeout`, so `/health/ready` fails fast instead of waiting for Npgsql's 15-second default (review N5).

### F01 Staff authentication and roles
Priority: Must · Status: Todo · Branch: feature/F01-staff-auth · Estimate: 30 min

Acceptance criteria:
- [ ] A user logs in with email and password and gets a bearer token. A wrong password or unknown email returns 401.
- [ ] Any non-health endpoint called without a valid token returns 401.
- [ ] Public self-registration isn't exposed: `POST /register` returns 404.
- [ ] An Admin can create an Admin or Staff user with email, password and exactly one role. A missing or unknown role returns 400. A weak password returns 400 with Identity's reasons. A duplicate email returns 409.
- [ ] Staff and Client Viewers calling user management get 403.
- [ ] An Admin can deactivate and reactivate a user. After deactivation, that user's existing token gets 401 on the next request, and login fails. After reactivation, login works again (D9).
- [ ] Deactivating an unknown user returns 404.
- [ ] In Development, an Admin is seeded from validated options. No user is seeded in tests.
- [ ] The Volunteer role isn't part of this feature. It comes with the brief's F09.

Tasks:
- [ ] F01.1 Identity with bearer login, the Admin, Staff and Client Viewer roles, `/register` not mapped, 401 for anonymous calls, and the Development Admin seed. Also rewrite `GetHealth_WithoutAuthorizationHeader_Returns200` so it proves `/health` stays anonymous, and add a 405 ProblemDetails test for the first method-specific endpoint (F00 review N1, A13).
- [ ] F01.2 Admin user management (create, deactivate, reactivate) and the active-flag check on every authenticated request.

### F02 Clients
Priority: Must · Status: Todo · Branch: feature/F02-clients · Estimate: 25 min

Acceptance criteria:
- [ ] Admin and Staff create a client with name, contact person, email and phone. A blank name or a badly formatted email returns 400 with field errors.
- [ ] Client names are unique ignoring case, enforced by a unique index. A duplicate returns 409.
- [ ] Get by ID returns the client. An unknown ID returns 404.
- [ ] The list is paginated (A5) and hides archived clients unless `includeArchived=true` is passed.
- [ ] An edit with a stale row version returns 409 (A6).
- [ ] Admin and Staff can archive a client. Editing an archived client returns 422 (A10).
- [ ] A Client Viewer sees only their own client. Requesting another client's ID returns 404 (A9), and the list contains only their own client.
- [ ] A Client Viewer who tries to create, edit or archive a client gets 403.
- [ ] An Admin can create a Client Viewer user scoped to one existing client. A missing or unknown client ID returns 400. (Amends F01.)
- [ ] The logo is out of scope (A11).

Tasks:
- [ ] F02.1 Create, get and list clients, with the `ClientId` tenant query filter and the Client Viewer scope tests.
- [ ] F02.2 Edit with row version, archive, and creating Client Viewer users scoped to a client.

### F03 Events
Priority: Stretch · Status: Todo · Branch: feature/F03-events · Estimate: 20 min

Acceptance criteria:
- [ ] Admin and Staff create an event under a client with name, venue, start, end and an IANA time zone (A7). Status starts as Draft. An auto-email setting defaults to on.
- [ ] An end before the start, or an unknown time zone, returns 400.
- [ ] Status moves Draft → Live → Closed only. Any other transition returns 422.
- [ ] Editing a Closed event, or any event under an archived client, returns 422 (A10).
- [ ] An unknown event, or another client's event requested by a Client Viewer, returns 404 (A9).
- [ ] The event list for a client is paginated (A5).

Tasks:
- [ ] F03.1 Event entity with its invariants, create, edit, status transition, get and list endpoints, with tests.

### F04 Attendees: manual entry
Priority: Stretch · Status: Todo · Branch: feature/F04-attendees · Estimate: 30 min

Acceptance criteria:
- [ ] Staff add an attendee with name, email, phone, company, designation, kit type, rack row and column (optional, A3) and notes.
- [ ] An attendee with neither email nor phone, or with a badly formatted email, returns 400.
- [ ] A duplicate within the event, matched on normalised email and then phone (A8), returns 409 with the existing attendee's location. Unique indexes back the check.
- [ ] Edit with row version (A6). Removing an attendee who is checked in returns 422.
- [ ] Changes under a Closed event or an archived client return 422. Another client's event returns 404 to a Client Viewer.
- [ ] The attendee list is paginated (A5).

Tasks:
- [ ] F04.1 Attendee entity, the add endpoint, and the duplicate check with unique indexes.
- [ ] F04.2 Edit, remove (blocked when checked in), get and list.

### F06 QR tokens
Priority: Stretch · Status: Todo · Branch: feature/F06-qr-tokens · Estimate: 25 min

Acceptance criteria:
- [ ] Each attendee gets a token when created. It carries the event ID, attendee ID, issue time and token version, and is signed with the event's ECDSA P-256 key through `IQrTokenSigner` (D3, D7).
- [ ] Staff can regenerate a token. This bumps the version, so the old token is revoked.
- [ ] Verification gives distinct outcomes for a tampered token, a token for the wrong event, and a revoked token.
- [ ] Private keys never appear in API responses or logs.

Tasks:
- [ ] F06.1 Per-event key creation, the local ECDSA signer, and token issue and verify with unit tests.
- [ ] F06.2 Issue on attendee creation, the regenerate endpoint, and the get-token endpoint.

### F11 QR check-in
Priority: Stretch · Status: Todo · Branch: feature/F11-qr-check-in · Estimate: 30 min

Acceptance criteria:
- [ ] A valid scan writes a `CheckedIn` event (UUIDv7, D4) with attendee, actor, counter (optional until the brief's F10), device ID, device time and server time. It returns the attendee's name, kit type and rack position.
- [ ] Scanning an attendee who is already checked in writes a `DuplicateScan` event and returns who checked them in, at which counter, and when. No second check-in is created.
- [ ] Two simultaneous scans of the same token produce exactly one `CheckedIn` and one `DuplicateScan`. The conditional update decides the winner (ADR-004), and a test against Postgres proves it.
- [ ] Invalid, revoked and wrong-event tokens each return a distinct 422 ProblemDetails.
- [ ] Staff can revert a check-in with a mandatory reason, which writes `CheckInReverted` and clears `CheckedInAt`. A missing reason returns 400.
- [ ] Staff perform scans in this session, because volunteer identity is the brief's F09.

Tasks:
- [ ] F11.1 The scan endpoint with the conditional update race guard and the `CheckedIn` and `DuplicateScan` events.
- [ ] F11.2 Revert with reason, distinct token errors, and the concurrency test.

### Not planned this session
The brief's F05, F07 to F10, F12 and F14 to F19, the client logo (A11), the KMS and SES implementations (D7), and the Worker project (D6). They stay in TICKET-APP-SPEC.md and can come back through `/pm-drill`.

## Change log

| Time | Change | Source |
|---|---|---|
| 17:30 | Plan created at kickoff: F00 to F02 are Must, and F03, F04, F06 and F11 are Stretch. | /kickoff, Chandan's answers |
| 18:26 | F00.1 no longer creates `SecuPass.UnitTests`. It comes with the first domain logic, because Microsoft.Testing.Platform fails a run when a test project has zero tests. | Chandan, F00.1 approach gate |
| 23:43 | F00 review added F00.4 to F00.6 as fix tasks, A14 (amends A13), D12, a README note, and two test additions to F01.1. | /review, Chandan |
| 00:26 | F00.5 drops review item S2's code change. A probe showed ASP.NET Core already logs a started-response exception at Error with the correlation ID, so the change would have logged it twice. The test stays and pins this. | Chandan, F00.5 |
