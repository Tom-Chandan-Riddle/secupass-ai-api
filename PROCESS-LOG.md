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

## Summary

_Written by /wrap-up from the entries above. Counts and facts only._
