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
| A16 | Identity's defaults apply. A password needs at least 6 characters with an upper-case letter, a lower-case letter, a digit and a symbol. Five failed logins lock the account for five minutes, and a locked account gets the same 401 as a wrong password. Access tokens last 1 hour and refresh tokens 14 days. | They are standard and need no configuration. Lockout slows password guessing, and the matching 401 doesn't reveal that an account exists. | Chandan, 01:01 |
| A17 | The Development Admin's email and password (`admin@secupass.local`, `Dev-Admin-Passw0rd!`) are committed in `appsettings.Development.json`, like the dev database password. They are for local use only. | A fresh clone runs on the first try, with no user-secrets setup. | Chandan, 01:26 |
| A18 | If a user with the configured email already exists, the seed leaves it unchanged: no password reset and no role change. | Changes made through the API survive restarts. | Chandan, 01:26 |
| A19 | If the seed can't create the Admin, for example because the configured password breaks A16's rules, startup fails with Identity's reasons. | A misconfigured dev environment shows up at once, not as a missing login later. It only runs in Development. | Chandan, 01:26 |
| A20 | Creating a user accepts only the exact role names `Admin` and `Staff`. `ClientViewer` returns 400 until F02.2 adds the client scope, and so does a name in another case, such as `admin`. | The criterion lists Admin and Staff, and a Client Viewer without a client would break A9's scoping. | Chandan, 01:55 |
| A21 | A created user is returned as 201 with the user ID, email and role, and no `Location` header. | There is no endpoint that gets a user by ID yet, so a Location would point nowhere. | Chandan, 01:55 |
| A22 | Any email that passes the email format check can be a user's email, including characters such as `'`. Identity's user-name character list is cleared, because the user name is the email. | Identity's default list would reject valid addresses like `o'brien@client.com`. | Chandan, 02:01 |
| A23 | Every authenticated request checks that the user still exists, is active, and has the same security stamp as the token. Otherwise the token no longer counts. | Deactivation takes effect on the next request (D9), and a token issued before deactivation, or before any later stamp change, never works again, even after reactivation. | Chandan, 02:10 |
| A24 | A request whose token no longer counts is treated as anonymous: protected endpoints return 401, and anonymous endpoints such as `/health` still work. | It is the same outcome as sending no token, so there is no extra error case. | Chandan, 02:10 |
| A25 | An Admin can't deactivate their own account. The request returns 422 with ProblemDetails. | It stops an Admin locking themselves out by accident or leaving no active Admin. 422 matches A10 for a well-formed request that breaks a business rule. | Chandan, 02:22 |
| A26 | Deactivating an inactive user, or reactivating an active one, returns 204 and changes nothing. | The end state is what was asked for, so a retried request is safe. | Chandan, 02:22 |
| A27 | An email containing whitespace, including leading or trailing spaces, is rejected with 400. Amends A22. | Otherwise `" bob@x "` becomes a second account beside `bob@x` that can't log in with the address typed normally. | Chandan, 02:42 (F01 review S4) |
| A28 | Login takes about as long for an unknown email, an inactive user or a locked-out account as for a wrong password, so timing doesn't reveal which accounts exist. Amends A16. | A probe showed about 1 ms against 48 ms, which revealed which emails exist despite the matching 401. | Chandan, 02:42 (F01 review S5). Locked-out accounts added at 03:10, F01.8 build. |
| A29 | When a deactivate or reactivate loses a concurrency race, the user is reloaded and the request applied once more, returning 204 as A26 does. A second conflict throws. Amends A26. | Retries sent at the same moment are then as safe as retries sent one after another. | Chandan, 02:42 (F01 review S3) |
| A30 | The active-user check (A23) runs only on endpoints that require a logged-in user. Anonymous endpoints, unknown routes and wrong methods never query the user, whatever token is sent. Amends A24. | `/health` must never touch the database (F00), and a token sent to an endpoint that needs none changes nothing there. | Chandan, 02:53 (F01 review S1) |
| A31 | Deactivating an Admin is refused with 422 when no other active Admin would remain, so two Admins deactivating each other at once leave one active. | With A25 a single request can't remove the last Admin, but two simultaneous ones could. 422 matches A25 and A10. | Chandan, 03:31 (F01 review N2) |

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
| D13 | Login and refresh are our own endpoints, `POST /auth/login` and `POST /auth/refresh`, which call `SignInManager` with Identity's bearer token scheme. `MapIdentityApi` isn't used. Amends D9. | `MapIdentityApi` with a filter that returns 404 for `/register`, or login only with no refresh. | A probe showed `MapIdentityApi` has no options and always maps `/register` and eight other endpoints, which would leave untested and undocumented endpoints exposed. The tokens are the same ones it would issue. |
| D14 | Endpoints are closed by default: every feature maps onto a root route group with `RequireAuthorization()`, and anonymous endpoints say `AllowAnonymous`. A test fails if any mapped endpoint declares neither. | A fallback authorization policy, either as it is or with an exemption for requests that match no endpoint. | The fallback policy also applies when no endpoint matches and to the framework's 405 endpoint, so anonymous 404s and 405s became 401s, against A13 (seen in F01.1). An exemption for unmatched routes still leaves the 405. The test gives back the safety the fallback policy gave. Chandan, 01:16. |
| D15 | Deactivations run one at a time under a Postgres advisory transaction lock (`pg_advisory_xact_lock`), taken before the target and the Admin count are read. | A SERIALIZABLE transaction with a retry on serialization failure. | The lock is released at commit or rollback, needs no retry path, and makes the A31 check see every deactivation that committed before it. Deactivation is rare, so serialising it costs nothing that matters. Chandan, 03:31. |
| D16 | EF Core's four failure events (`CommandError`, `SaveChangesFailed`, `ConnectionError`, `QueryIterationFailed`) log at Debug, so EF Core's Warning floor hides them. Each comes with an exception that the app either handles and logs at Warning, or the global handler logs once at Error. Amends D12. | Serilog overrides setting EF Core's Database, Update and Query categories to Fatal. | A probe showed a handled 409 logging EF Errors in 9 of 19 races, and one database-down 500 logging three Errors. Moving only these events keeps EF's other Warnings. The failing SQL text is still logged at Debug. Chandan, 04:06 (second F01 review S1). |

## Features

Budget: 2 hours. Must comes to 80 minutes, which is 20 over half; Chandan approved this at 17:30. A checkpoint after F01 decides whether F02 stays in Must. Stretch features are built in the order listed, only if time remains. At the 00:55 checkpoint, after F00 shipped at about 224 active minutes, Chandan lifted the fixed budget and kept the full plan, Stretch included.

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
Priority: Must · Status: Done · Branch: feature/F01-staff-auth · Estimate: 30 min

Approach: `StaffUser : IdentityUser<Guid>` lives in `SecuPass.Infrastructure/Identity`, and `SecuPassDbContext` becomes an `IdentityDbContext` with Guid keys. The Admin, Staff and Client Viewer roles are seeded by the migration (`HasData`), so they exist in every environment, and their names are constants in `SecuPass.Domain/Identity/StaffRoles`. Identity's bearer token handler (`AddBearerToken`) is the only authentication scheme. `AddIdentityApiEndpoints` isn't used, because it also registers Identity's cookie scheme, and `MapIdentityApi` isn't called, because it has no options and always maps `/register` and eight other endpoints (probe, R5). Login and refresh are endpoints in `Api/Features/Identity` that call `SignInManager` with the bearer scheme (D13). `GET /auth/me` returns the caller's user ID, email and role. Features map onto a root route group that requires an authenticated user. Login, refresh, health and OpenAPI are marked `AllowAnonymous`, and a test fails if any mapped endpoint declares neither (D14). User management in F01.3 to F01.5 uses `UserManager` behind an Admin policy. Tests go through HTTP and create their own users with `UserManager`. The test factory turns the Development seed off. Risks: a refresh token could outlive deactivation, so F01.4 checks the active flag on refresh too.

Acceptance criteria:
- [x] A user logs in with email and password and gets a bearer token. A wrong password or unknown email returns 401.
- [x] Five failed logins lock the account for five minutes, and a locked account gets the same 401 (A16).
- [x] A valid refresh token returns a new bearer token. An invalid one returns 401 (D13).
- [x] `GET /auth/me` returns the caller's user ID, email and role.
- [x] Any non-health endpoint called without a valid token returns 401.
- [x] Public self-registration isn't exposed: `POST /register` returns 404.
- [x] An Admin can create an Admin or Staff user with email, password and exactly one role. A missing or unknown role returns 400. A weak password returns 400 with Identity's reasons. A duplicate email returns 409.
- [x] Staff and Client Viewers calling user management get 403.
- [x] An Admin can deactivate and reactivate a user. After deactivation, that user's existing token gets 401 on the next request, and login fails. After reactivation, login works again (D9).
- [x] Deactivating an unknown user returns 404.
- [x] In Development, an Admin is seeded from validated options. No user is seeded in tests.
- [x] The Volunteer role isn't part of this feature. It comes with the brief's F09.

Tasks:
- [x] F01.1 Identity with bearer login and refresh (D13), lockout (A16), the Admin, Staff and Client Viewer roles, `GET /auth/me`, no `/register`, and 401 for anonymous calls. Also rewrite `GetHealth_WithoutAuthorizationHeader_Returns200` so it proves `/health` stays anonymous, and add a 405 ProblemDetails test for the first method-specific endpoint (F00 review N1, A13).
- [x] F01.2 The Development Admin seed from validated options, turned off in tests.
- [x] F01.3 Admin creates an Admin or Staff user (`POST /users`), with 400, 403 and 409 cases (A20, A21).
- [x] F01.4 The user's active flag, checked on every authenticated request with the security stamp (A23, A24), on login and on refresh (D9).
- [x] F01.5 Admin deactivates and reactivates a user, with 404 for an unknown user.
- [x] F01.6 fix: create user returns 400 for an email over 256 characters or containing whitespace (A27), and 409 when two creates race on one email (review M1, S2, S4).
- [x] F01.7 fix: the active-user check skips anonymous endpoints, so `/health` never touches the database, and a concurrent deactivate or reactivate is retried once after a reload (A29) (review S1, S3).
- [x] F01.8 fix: login checks a dummy password hash for unknown, inactive and locked-out accounts, so timing doesn't reveal accounts (A28) (review S5).
- [x] F01.9 fix: tests for the lockout ending, the token lifetime boundaries, refresh while locked out, and the correlation ID on every new error status (review S6, N1, N3, N4).
- [x] F01.10 fix: refuse to deactivate the last active Admin, also when two Admins deactivate each other at once (A31, D15, review N2).
- [x] F01.11 fix: EF Core's own Error logs stop, so a handled 409 logs no Error and an unexpected database failure logs one Error, from the global handler (D16, second review S1).

Review notes kept, not tasks: an authorization policy that names its own scheme, such as F09's volunteer auth, may re-authenticate and skip the active-user check, so the check must cover that scheme too (N5, unverified). The login, refresh and activation handlers take no `CancellationToken`, because Identity's `UserManager` and `SignInManager` calls accept none (N6).

Second review notes kept, not tasks (Chandan, 04:03):
- The A29 retry logs nothing when it recovers from a concurrency conflict, so how often it happens can't be seen.
- A30's wrong-method case (405 with a token while the database is down) works but has no test.
- A29's "a second conflict throws" works (500, one Error) but has no test.
- An email containing a zero-width space (U+200B) passes A27's whitespace rule and creates a lookalike account.
- The A28 timing tests only check a lower bound of half a wrong password's time, so a dummy check with half the iterations would still pass.
- The last-Admin 422 path is reached in only about a third of race rounds, and its body and Warning log aren't asserted.

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
| 00:55 | Checkpoint: no fixed time limit any more, and the plan stays as it was. Nothing is cut. | /checkpoint, Chandan |
| 01:01 | F01.1 split: the Development Admin seed becomes F01.2 and user management becomes F01.3. Added D13 (amends D9), A16, and criteria for lockout, refresh and `GET /auth/me`. | Chandan, F01.1 approach gate |
| 01:16 | D14 replaces the planned fallback authorization policy with an authenticated route group and an endpoint audit test. | Chandan, F01.1 build |
| 01:55 | F01.3 split: create user stays in F01.3, and deactivate, reactivate and the active-flag check move to F01.4. Added A20 and A21. | Chandan, F01.3 approach gate |
| 02:10 | F01.4 split: the active flag and its checks stay in F01.4, and the deactivate and reactivate endpoints move to F01.5. Added A23 and A24. | Chandan, F01.4 approach gate |
| 02:42 | F01 review added F01.6 to F01.10 as fix tasks, and A27 to A29 (amending A22, A16 and A26). | /review, Chandan |
| 04:03 | Second F01 review added F01.11 (EF Core Error logs) and kept six notes. | /review, Chandan |
