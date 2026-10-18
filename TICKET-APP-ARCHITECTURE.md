# ARCHITECTURE.md

How the event registration and check-in tool is built, and why. Read this alongside FEATURES.md, which says what we build. If the two disagree, FEATURES.md wins and this file gets fixed.

Version 0.1, 15 Oct 2026.

## Load profile

Design numbers, not guesses. Revisit after every live event.

| Measure | Typical | Design target |
|---|---|---|
| Attendees per event | 800 | 10,000 |
| Concurrent live events | 1 | 5 |
| Check-ins at peak | ~15 per minute | 8,000 in one hour |
| Invitation emails per event | 800 | 10,000 |
| Self-registration burst | none yet | 2,000 in 10 minutes |

At these numbers the system runs at single-digit requests per second. Scale is not the risk. The risks are email deliverability, check-in races, and one client seeing another client's data. Every design choice below is aimed at those three.

## System overview

```
Browsers / volunteer PWA
        │
CloudFront + WAF ──── S3 (React SPA + PWA)
        │
API service (ECS Fargate) ──── SignalR to dashboards and helper screens
   │            │
   │         SQS (jobs) ─── DLQ
   │            │
   │         Worker (ECS Fargate) ──── SES
   │            │                       │
RDS PostgreSQL ◄┘          SES events → SNS → SQS → Worker
```

One codebase, two deployables: the API and the worker. Both read and write the same PostgreSQL database.

## Components

**Frontend.** React with TypeScript. One SPA for admin, staff and client viewers. The volunteer scanner is a PWA route in the same app, using the phone camera through the browser. Served from S3 through CloudFront.

**API service.** ASP.NET Core on .NET 10, deployed to ECS Fargate behind an Application Load Balancer. Organised as a modular monolith with these modules:

- Identity: users, roles, volunteer access, magic links
- Clients and Events: clients, events, counters, settings, templates
- Attendees: manual entry, imports, QR tokens
- Messaging: outbox, email dispatch, delivery status
- CheckIn: check-in events, kit handover, helper feed, dashboard projections

Modules talk through in-process interfaces, never by reaching into each other's tables. That keeps a later split possible without promising one.

**Worker.** The same codebase in worker mode. It consumes SQS for email sending, delivery events and file imports. Nothing slow runs inside a web request.

**Database.** RDS PostgreSQL, single instance with automated backups and point-in-time recovery. Multi-AZ only when a client contract requires it.

**Email.** Amazon SES from a dedicated sending subdomain, with SPF, DKIM and DMARC. A configuration set publishes delivery, bounce and complaint events to SNS, which feeds an SQS queue the worker reads.

**Secrets and keys.** Secrets Manager for connection strings and provider keys. KMS asymmetric keys sign QR tokens.

**Infrastructure.** AWS CDK in C#, in the same repository. Two environments: staging and production. No manual console changes in production.

## Key flows

### Bulk invitation email

1. Saving an attendee also writes an outbox row, in the same transaction.
2. A dispatcher polls the outbox and publishes messages to SQS in batches, marking rows as dispatched.
3. The worker consumes SQS, throttled below the account's SES send rate.
4. For each message it renders the template, generates the QR image, and sends through SES. The QR goes in as an inline image attachment, and the email also links to a hosted pass page as a fallback.
5. Each send carries an idempotency key of attendee, template and attempt. A redelivered message that was already sent is acknowledged and skipped.
6. Failures retry with exponential backoff up to three times, then land in the dead-letter queue. An alarm fires on any DLQ message.
7. SES events arrive through SNS and SQS. The worker updates the attendee's delivery status. Events are processed idempotently and may arrive out of order, so a later status never gets overwritten by an earlier one.

### QR check-in

1. The volunteer scans. The PWA sends the token to the API.
2. The API verifies the signature with the event's public key, then checks the token is current and belongs to this event.
3. In one transaction it inserts a `CheckedIn` event and runs `UPDATE attendees SET checked_in_at = @now WHERE id = @id AND checked_in_at IS NULL`.
4. One row updated means success. Zero rows means someone already checked this attendee in, so the transaction instead records a `DuplicateScan` event and returns who, where and when.
5. On commit, SignalR pushes the check-in to the helper screen and client dashboard for that event.

Two volunteers scanning the same badge at the same moment cannot both succeed. The database decides, not the application.

### File import

1. The browser uploads the file directly to S3 with a presigned URL.
2. The API creates an import job and queues it.
3. The worker parses and validates every row, then stores a preview: valid, duplicate and invalid counts, plus an error file.
4. Staff confirm. The worker writes all valid rows in one transaction, along with their outbox rows.

## Data model

Core tables. Every table except users carries `client_id` and, where relevant, `event_id`.

| Table | Purpose and notable columns |
|---|---|
| `users` | Staff, admins and client viewers. Role, client scope, active flag. |
| `clients` | Name (unique, case-insensitive), contact, archived flag. |
| `events` | Client, name, venue, start and end with time zone, status, settings (JSONB), KMS key reference. |
| `counters` | Event, name, location. |
| `volunteers` | Event, name, contact, counter, access window, revoked flag. |
| `attendees` | Event, core fields, custom fields (JSONB), kit type, rack row and column, token version, delivery status, `checked_in_at`, `kit_handed_at`. Unique on `(event_id, lower(email))`. |
| `checkin_events` | Append-only. ID (UUIDv7), event, attendee, type, actor, counter, device, source, occurred time, recorded time, reason, approver. |
| `email_messages` | Attendee, template, idempotency key, status, provider message ID, attempts. |
| `email_events` | Raw SES events for audit and replay. |
| `outbox` | Pending messages, dispatched time. |
| `import_jobs` | File location, mapping, status, counts, error file location. |

`checkin_events` is the source of truth for check-in history. `checked_in_at` and `kit_handed_at` on attendees are projections, kept for fast dashboard queries and as the concurrency guard. If they ever disagree with the log, the log wins and a repair job rebuilds them.

## Tenant isolation

- Every query is scoped by EF Core global query filters on `client_id`, set from the authenticated user's claims.
- Volunteers are scoped to one event and an access window.
- An integration test for every endpoint proves that a user from one client cannot read or change another client's data by editing an ID.
- PostgreSQL row-level security is a later hardening step, not an MVP requirement.

## Traffic and scaling

- **Scheduled scaling.** Event start times are known, so the API scales out an hour before doors open and back in after the event.
- **Registration bursts.** WAF rate-based rules and a CAPTCHA on the public form. Each registration is one insert plus one outbox row, which PostgreSQL handles comfortably.
- **Connection pooling** in the API, with a pool size sized to the RDS instance's connection limit across all tasks.
- **SignalR.** The MVP runs a fixed two API tasks with sticky sessions on the load balancer. A Redis backplane is added only if measurement shows a need.
- **Load test** before the first live event: 8,000 check-ins in an hour across 10 simulated counters, plus a 10,000-email send.

## Resilience

- Retries with exponential backoff and jitter on every outbound call (SES, KMS, S3), using Polly through the .NET resilience libraries. Timeouts on all of them.
- A circuit breaker on SES, so an outage queues mail instead of piling up failing calls.
- SQS visibility timeouts longer than the worst-case job time. Dead-letter queues on every queue.
- A failed scan never shows success. If the API can't be reached, the scanner says so and directs the volunteer to the paper fallback (F12).

## Security

- HTTPS everywhere. HSTS on CloudFront.
- Passwords handled by ASP.NET Core Identity. Magic links are single-use, short-lived and bound to one volunteer.
- QR tokens are signed with KMS asymmetric keys, one per event. The private key never leaves KMS.
- Attendee personal data is encrypted at rest (RDS and S3 encryption). Exports are logged in the audit trail.
- CSV and Excel exports neutralise formula injection.
- Uploads are size-limited and type-checked before parsing.

## Observability

- Structured JSON logs through Serilog, shipped to CloudWatch. Every request and job carries a correlation ID, which also goes into SQS message attributes so a single email can be traced from save to delivery.
- OpenTelemetry traces and metrics into CloudWatch.
- Health endpoints: liveness for the process, readiness covering the database and queue.
- Alarms: any DLQ message, SES bounce rate above 2%, complaint rate above 0.05%, API 5xx rate, p95 check-in latency above one second during a live event.

## Decision records

### ADR-001 Modular monolith over microservices
Status: Accepted.
At single-digit requests per second, microservices add deployments, network failure modes and cost with no benefit. Module boundaries inside one codebase give most of the structure. Revisit only if one module's load or release cadence diverges sharply from the rest.

### ADR-002 ECS Fargate over Lambda
Status: Accepted.
SignalR needs long-lived connections, which suit containers. .NET cold starts on Lambda would hurt the scanner's response time. Fargate keeps one deployment model for API and worker. App Runner was simpler but gives less control over scaling schedules and networking.

### ADR-003 Transactional outbox for email
Status: Accepted.
Without it, an attendee can be saved while the queue publish fails, or an email can go out for a save that rolled back. The outbox makes the save and the intent to send one atomic action. The cost is a dispatcher process and a little latency.

### ADR-004 Conditional update as the check-in concurrency guard
Status: Accepted.
An application-level "check, then insert" has a race window when two counters scan the same badge. A conditional update on `checked_in_at IS NULL` lets the database pick one winner. The event log keeps full history, and the column is a projection.

### ADR-005 KMS asymmetric signing for QR tokens
Status: Accepted.
Symmetric signing would put a shared secret on any device that verifies codes, which blocks safe offline check-in later (F13). Asymmetric keys let devices hold only the public key. KMS keeps the private key non-exportable.

### ADR-006 PostgreSQL with JSONB for custom fields
Status: Accepted.
The data is relational: clients own events, events own attendees, check-ins reference all three. Custom registration fields vary by event, and JSONB stores them without schema changes while staying queryable.

### ADR-007 SignalR without a backplane for the MVP
Status: Accepted, revisit after first live event.
A fixed two tasks with sticky sessions covers MVP load. A Redis backplane adds a managed dependency and cost. Add it when there are more than two API tasks.

## Open questions

- Which AWS region? Mumbai (ap-south-1) is the default for Indian clients and data residency, unless a client requires otherwise.
- ASP.NET Core Identity or Amazon Cognito for staff login? This file assumes Identity for simplicity and testability.
- Will clients ever need SSO into their dashboard? If so, it changes the identity choice.

## Change log

| Date | Change |
|---|---|
| 2026-10-15 | v0.1 created. |
