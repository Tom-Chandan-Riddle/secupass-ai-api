# PROCESS-LOG.md

A running record of how this work is done. FEATURES.md says what is built. This file records how: what was read, assumed and decided, how AI was used, where it was wrong, and what changed as a result.

Entries are appended, never edited. Times are IST. Each entry goes into the same commit as the work it describes.

## Setup

| Item | Value |
|---|---|
| Engineer | Chandan Sushma Shankar |
| AI coding tool | Claude Code in VS Code. Model and effort: Claude Opus 5.5 (`claude-opus-5-5`), effort low |
| Base commit | `fde7dac` on `main` (local only: `origin` has no branches yet) |
| Repo state at start | Greenfield. Brief and workflow docs only, no solution or projects. .NET SDK 10.0.100 installed. No Docker or PostgreSQL on the machine. No checker or test harness provided. |

## Working agreement

How I work with AI on this repo. The log below records how closely the work followed it, and the summary at the end reports the result.

- AI drafts code, tests and docs from tasks defined in FEATURES.md.
- I approve each task's approach before code is written, and each diff before it is committed.
- Requirements, assumptions and trade-offs are my decisions. AI proposes options with a recommendation.
- Corrections to AI output are logged as AI-FIX. A correction that repeats becomes a rule in CLAUDE.md, logged as RULE.
- One model and effort level for the whole piece of work, chosen for correctness on edge cases over speed or token cost. Any change is logged with the reason.
- State lives in FEATURES.md and this file, not in the chat, so the AI's context can be cleared between features without losing anything.
- The AI's instructions are in CLAUDE.md and .claude/, so the setup can be reproduced.

## Entry types

| Type | When |
|---|---|
| START | Work begins: base commit and scope |
| READ | First pass of the requirements and repo: scope, gaps, risks |
| ASSUME | An ambiguity resolved by assumption, with the reason |
| PLAN | Features and tasks created or re-planned |
| DECISION | A design choice and the alternatives considered |
| TASK | A task completed and committed |
| AI-FIX | AI output was wrong or weak, and what I corrected |
| RULE | A repeated correction turned into a rule in CLAUDE.md |
| REVIEW | Findings from a review pass and what was done about them |
| SHIP | A feature merged to main |
| CHECKPOINT | Progress against estimates and any scope call |
| CUT | Scope dropped or deferred to protect quality |
| FREEZE | Code freeze, docs only from here |
| RETRO | What went well, what didn't, what I'd change |

Format: `HH:MM · TYPE · ID · one or two sentences`. Add the short commit SHA where there is one.

<!--
Expected depth:
09:12 · START · — · Base commit a1b2c3d. Requirements ask for an order intake API on the existing solution.
09:24 · ASSUME · A1 · Orders with zero quantity rejected with 422. An order line must request at least one unit.
10:05 · AI-FIX · F01.2 · AI caught Exception in the handler and returned 500. Replaced with a domain error mapped to 422.
-->

## Log

17:14 · START · — · Base commit fde7dac. The brief asks for an event registration and check-in API. This session builds only the API, with a 2-hour budget.
17:16 · READ · — · Read TICKET-APP-SPEC.md and TICKET-APP-ARCHITECTURE.md. The repo is greenfield, the .NET 10 SDK is present, and there is no checker, Docker or Postgres. The `origin` remote has no branches, so `git pull` failed. The main risks are scope (15 P0 features), tenant isolation touching every feature, and testing on a different database engine from production.
17:24 · DECISION · D5 · PostgreSQL with Npgsql and Testcontainers instead of the SQLite default. The brief names Postgres, and the check-in race guard has to be tested on the production engine. Docker Desktop is to be installed by Chandan.
17:24 · DECISION · D6 · Api, Domain and Infrastructure projects, with a Worker project added when the first queued job arrives. That is one project over the CLAUDE.md ceiling, because the brief asks for an API and a worker.
17:24 · DECISION · D7 · Local stand-ins replace SQS, SES, KMS and S3. Chandan first picked real AWS SDKs, then switched to stand-ins once KMS was shown to be the first dependency within the 2 hours.
17:24 · DECISION · D8 · Serilog with JSON console output instead of built-in console logging, because the brief names Serilog.
17:24 · DECISION · D9 · ASP.NET Core Identity bearer tokens, with an active-flag check on every request to meet the one-minute deactivation rule.
17:24 · DECISION · D10 · No HTTP resilience handler, because nothing calls out over HTTP while the stand-ins are in place.
17:24 · ASSUME · A1 · Payment is out of scope, as the brief proposes.
17:24 · ASSUME · A2 · Counters are open, and the counter is recorded for reporting only, as the brief proposes.
17:24 · ASSUME · A3 · Rack position is optional, as the brief proposes.
17:24 · ASSUME · A5 · List endpoints are paginated with a default page size of 50 and a maximum of 200. A larger size returns 400.
17:24 · ASSUME · A6 · Clients, events and attendees carry an `xmin` row version, and a stale update returns 409.
17:24 · ASSUME · A7 · Event times are stored as UTC plus an IANA time zone ID.
17:24 · ASSUME · A8 · Duplicate checks compare emails trimmed and case-insensitive, and phones with separators removed. Duplicates are rejected, not fixed.
17:30 · ASSUME · A9 · A cross-client read returns 404, so record IDs can't be probed.
17:30 · ASSUME · A10 · Changes to an archived client or a Closed event return 422.
17:30 · ASSUME · A11 · The client logo is deferred, because nothing in Must uses it.
17:30 · PLAN · — · F00 Foundation, F01 Staff auth and F02 Clients are Must, at 80 minutes. F03 Events, F04 Attendees, F06 QR tokens and F11 QR check-in are Stretch. Chandan accepted Must running 20 minutes over half the budget, with a checkpoint after F01.
18:32 · NOTE · — · Docker 29.8.2 is now installed and running with Linux containers, so Testcontainers Postgres is available for F00.3. The setup table predates the install.
18:32 · RULE · R1 · Chandan asked that every question needing Chandan's answer goes through the interactive picker, recommended option first and Other last. Added to CLAUDE.md as R1.
18:32 · NOTE · — · Test command is `dotnet test --solution SecuPass.slnx --no-build`. xUnit v3 4.x runs on Microsoft.Testing.Platform, which SDK 10 only supports through the `global.json` test runner setting, so the VSTest packages were dropped.
18:32 · TASK · F00.1 · Solution, Api, Domain and Infrastructure projects, shared build and package props, and `GET /health` with two passing HTTP tests. `SecuPass.UnitTests` was deferred to the first domain logic, because an empty test project fails the run with exit code 8.
22:05 · ASSUME · A12 · An invalid incoming correlation ID (empty, over 64 characters, or outside letters, digits, `-_.:`) is replaced with a generated one and logged at Warning. The request still proceeds.
22:05 · ASSUME · A13 · Bodyless error responses such as unmatched-route 404s and 405s are returned as ProblemDetails, so clients see one error shape.
22:17 · AI-FIX · F00.2 · The first Serilog setup let `AddSerilog` own the static `Log.Logger`, so parallel test hosts logged into each other's sinks, and the fix then silenced request logs until the request middleware was given the DI logger. The failing log tests caught both before review.
22:17 · TASK · F00.2 · Correlation ID middleware (A12), Serilog JSON request logging, a global exception handler returning 500 ProblemDetails with `correlationId`, ProblemDetails for bodyless errors (A13), and OpenAPI with Scalar in Development only. 17 tests pass.
22:20 · DECISION · D11 · Local development and Testcontainers both use `postgres:17-alpine`, with a `compose.yaml` for the local database, so one command gives a database matching the test engine.
22:28 · NOTE · — · The app process from F00.2's manual check was left running and locked a DLL, which failed the first F00.3 build. It was stopped by PID, and later manual runs confirm no process is left behind.
22:28 · TASK · F00.3 · Npgsql `SecuPassDbContext` with validated `DatabaseOptions`, an empty `InitialCreate` migration applied at startup in Development, a shared Testcontainers Postgres fixture, and `/health/ready` returning 200 or 503. 22 tests pass.
23:43 · REVIEW · F00 · Two agents (reviewer, edge-case-hunter) found 1 Must fix, 2 Should fix and 6 Notes. The Must fix is a flaky startup options test, reproduced in 2 of 6 runs under parallel load. All were accepted: M1, S1, S2 and N2 to N4 become F00.4 to F00.6, N1 and the 405 test move to F01.1, N5 becomes a README note, and N6 joins F00.4. Nothing was rejected. The AI recommended against N3 and N4, and Chandan chose them anyway.
23:43 · ASSUME · A14 · ProblemDetails is written whatever the `Accept` header says, amending A13 so every error has one shape.
23:43 · DECISION · D12 · EF Core logs at Warning, so SQL commands aren't logged at Information. This overrides the CLAUDE.md default of logging every dependency call, by Chandan's choice at review.
23:48 · AI-FIX · review · The `docs: review F00` commit (1cd2c84) was committed and pushed without showing Chandan the diff. It added A14, D12, a scope change to F01.1 and three fix tasks. CLAUDE.md read a command as approval to commit, which conflicts with its own gate rule for assumptions and scope.
23:48 · RULE · R2 · Nothing is committed, pushed or merged until Chandan approves the staged diff, in every command. Added to CLAUDE.md and DEFAULTS.md, and as an explicit gate step in review, pm-drill, checkpoint, ship-feature and wrap-up.
00:09 · TASK · F00.4 · Replaced the flaky host-start options test with `IStartupValidator` tests for a missing, empty and whitespace connection string plus a valid one. Added tests for a repeated `X-Correlation-ID` and for OpenAPI returning 404 in Staging. 28 tests pass, including 6 suite runs under parallel load.
00:14 · ASSUME · A15 · Malformed requests return ProblemDetails with the exception's status and standard title but no `detail`. The framework message is logged at Warning only.
00:20 · NOTE · — · A Visual Studio Installer update (started 00:09, not part of this work) replaced .NET SDK 10.0.100 with 10.0.402 and the runtimes with 10.0.13. Builds failed until it finished at 00:20. All 33 tests pass on 10.0.402, and its test runner no longer shows app console output.
00:26 · AI-FIX · F00.5 · Review item S2 was wrong. The edge-case-hunter claimed a started-response exception lost its Error log, but a probe showed `ExceptionHandlerMiddleware` already logs it at Error with the exception and correlation ID. The AI had reproduced M1 but passed S2 on unchecked. The change is dropped and a test pins the framework behaviour. Both review agents and `/review` step 3 now require checking framework-behaviour claims before they are reported.
00:27 · TASK · F00.5 · `BadHttpRequestExceptionHandler` returns the exception's 4xx as ProblemDetails with no detail and logs at Warning (A15). `JsonProblemDetailsFallbackWriter` writes ProblemDetails when the Accept header excludes JSON (A14). A test pins the framework's single Error log for exceptions after the response started (S2, no code change). 33 tests pass, and the 4 new S1 and A14 tests fail without the new code.
00:35 · TASK · F00.6 · The Development migration log now lists only migrations applied by this host, from a before and after diff, and is skipped when none were. Chandan accepted that two hosts migrating at once can over-report. EF Core logs at Warning (D12). 36 tests pass, and the two new behaviour tests failed before the change.
00:44 · RETRO · F00 · F00 took 82 minutes of task time against 25, mostly tooling: `.slnx`, the test runner, the Serilog static logger, and an SDK swap partway through. Repeated problems were a commit without diff approval, records written after the gate, an unverified framework claim, three tests that proved nothing, two leftover app processes, environment drift found mid-task, and a flake visible only under load. Chandan approved all eleven proposed changes.
00:44 · RULE · R3 · Corrections in plain chat are handled as `/feedback`.
00:44 · RULE · R4 · Every new test is seen failing without its code, and the commit gate shows the evidence.
00:44 · RULE · R5 · Framework behaviour is checked with a test or probe before it shapes code, findings or decisions.
00:44 · RULE · R6 · App processes started for manual runs are stopped and confirmed gone. Others are stopped only with Chandan's approval.
00:44 · DECISION · — · `/next-task` now records before the commit gate, checks the SDK, Docker and running processes when orienting, writes START entries, and flags features past 150% of estimate. `/ship-feature` runs the suite three times, once in parallel. The DEFAULTS row for R1 was added.
00:47 · DECISION · — · `global.json` pins SDK 10.0.100 with `rollForward: latestFeature`, so any 10.0.x SDK from 10.0.100 up builds the repo and a .NET 8 or 11 SDK is never picked by accident. Applied from the F00 retro (C3).
00:49 · SHIP · F00 · All 6 tasks and 7 criteria done. 36 tests pass on SDK 10.0.402, run twice in sequence and twice side by side. Task time was 82 minutes against an estimate of 25.
00:55 · CHECKPOINT · — · F00 shipped. Its tasks took 82 minutes against 25, and active time is about 224 minutes against the 120-minute budget, about 173 minutes behind plan. Left: F01 (30 min) and F02 (25 min) in Must, and F03, F04, F06 and F11 (105 min) in Stretch. The AI recommended cutting Stretch. Chandan lifted the time limit and kept the full plan. `/cost` not given.
01:00 · START · F01.1 · Branch feature/F01-staff-auth cut from `65f0194`. Identity bearer login, the three staff roles, 401 for anonymous calls, no `/register`, and the Development Admin seed. A probe showed `MapIdentityApi` has no options and always maps `/register`, so D9 needs a decision before any code.
01:01 · DECISION · D13 · Login and refresh are our own `/auth/login` and `/auth/refresh` endpoints over Identity's bearer scheme, not `MapIdentityApi`, which always maps `/register` and eight other endpoints. Amends D9.
01:01 · ASSUME · A16 · Identity's default password rules, lockout after five failed logins for five minutes with the same 401, and 1-hour access and 14-day refresh tokens.
01:01 · PLAN · F01 · F01.1 was about 45 minutes of work, so the Development Admin seed moves to F01.2 and user management to F01.3. `GET /auth/me` is added so the 401 rule and the role claims have a real endpoint to test against.
01:16 · NOTE · F01.1 · With the first migration that creates tables, test hosts migrating the shared database in parallel failed with 42P07, so EF Core with Npgsql 10.0.3 doesn't serialise concurrent migrations. The F00 comment that said it did is corrected, and the test fixture now migrates the shared database once before any host starts.
01:16 · DECISION · D14 · Endpoints are closed by an authenticated root route group plus a test that every endpoint declares `[Authorize]` or `[AllowAnonymous]`. The planned fallback policy turned 10 F00 tests' anonymous 404s and 405s into 401s.
01:20 · TASK · F01.1 · Bearer login and refresh over Identity (D13) with lockout (A16), `GET /auth/me`, the three roles seeded by migration, no `/register`, and endpoints closed by the authenticated route group (D14). 67 tests pass. 15 mutations of the new code each turned their tests red, and the other new tests failed before the endpoints existed.
01:24 · START · F01.2 · Base commit `196c474`. The Development Admin seed from validated options, turned off in tests.
01:26 · ASSUME · A17 · The Development Admin's credentials are committed in `appsettings.Development.json`, so a fresh clone runs without user-secrets setup.
01:26 · ASSUME · A18 · An existing user with the configured email is left unchanged by the seed, so API changes survive restarts.
01:26 · ASSUME · A19 · A seed that can't create the Admin fails startup with Identity's reasons, so a broken dev setup shows at once.
01:32 · TASK · F01.2 · In Development the Admin from `appsettings.Development.json` is seeded after migrations, in one transaction, and an existing user is left unchanged (A17, A18). A seed that can't create the Admin stops startup (A19), and the test factory turns the seed off. 81 tests pass. 9 new tests failed against a do-nothing seeder, and 7 mutations turned the rest red.
01:53 · START · F01.3 · Base commit `6747122`. Admin user management (create, deactivate, reactivate) and the active-flag check on every authenticated request.
01:55 · PLAN · F01 · F01.3 was about 45 minutes of work, so create user stays in F01.3 and deactivate, reactivate and the active-flag check move to F01.4.
01:55 · ASSUME · A20 · Creating a user accepts only the exact role names Admin and Staff. ClientViewer returns 400 until F02.2 adds the client scope.
01:55 · ASSUME · A21 · A created user returns 201 with its ID, email and role and no Location header, because no endpoint gets a user by ID yet.
02:01 · ASSUME · A22 · Any well-formed email can be a user's email. Identity's user-name character list is cleared, because it rejected valid addresses with characters such as an apostrophe.
02:05 · TASK · F01.3 · `POST /users` lets an Admin create an Admin or Staff user in one transaction, with 400 for bad input, unsupported roles and weak passwords, 403 for other roles, and 409 for a taken email (A20 to A22). 101 tests pass. All 20 new tests failed before the endpoint existed, and 9 mutations turned the specific ones red.
02:09 · START · F01.4 · Base commit `a210636`. Deactivate and reactivate, and the active-flag check on every authenticated request, on login and on refresh.
02:10 · PLAN · F01 · F01.4 was about 40 minutes of work, so the active flag and its checks stay in F01.4 and the deactivate and reactivate endpoints move to F01.5.
02:10 · ASSUME · A23 · Every authenticated request checks the user exists, is active and still has the token's security stamp, so old tokens never come back after reactivation.
02:10 · ASSUME · A24 · A token that no longer counts is treated as no token: protected endpoints return 401 and anonymous ones still work.
02:16 · TASK · F01.4 · Staff users have an active flag that defaults to true for existing rows. A middleware drops the token of a missing, inactive or re-stamped user so the request runs as anonymous (A23, A24), and login and refresh refuse inactive users. 110 tests pass. 7 of the 9 new tests failed first, and 9 mutations proved all of them.

## Summary

_Written by /wrap-up from the entries above. Counts and facts only._
