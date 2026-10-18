# FEATURES.md

Single source of truth for the event registration and check-in tool. Every feature, change request and piece of feedback lands here first. Code follows this file, never the other way round.

Version 0.2, 15 Oct 2026. Scope: PrismScale-operated tool for corporate events. SaaS is out of scope for now.

## How this file works

- Each feature has an ID, priority, status, branch and PR. One feature gets one branch and one PR. One task gets one commit.
- `/pm-drill` is the only way new requirements, CRs and feedback enter this file. It updates the feature, adds a change log entry, and resets status where needed.
- A feature is Done only when every acceptance criterion has a passing test, or a written reason why it can't be automated.

Statuses: `Proposed` → `Ready` (drilled, criteria agreed) → `In Progress` → `In Review` → `Done`. Use `Blocked` with a reason when stuck.

## Open assumptions

Unconfirmed. Overrule any of these in the first `/pm-drill`.

| ID | Assumption | Why it matters |
|---|---|---|
| A1 | Payment is out of the MVP. | Payment brings gateways, refunds, GST invoices and failed-payment states. |
| A2 | Counters are open. Any volunteer can check in any attendee, and the counter is recorded for reporting only. | Assigned segments (A–M, VIPs) need routing rules and change the data model. |
| A3 | Rack position comes from an optional column in the import file. Blank is allowed. | Auto-assignment needs a rack layout per event, which is a separate feature. |
| A4 | Volunteers check in on their phones through a web app (PWA), not a native app. | Avoids app store reviews, installs on event morning and a second codebase. |

## Decisions

| ID | Decision | Reason |
|---|---|---|
| D1 | Check-in is stored as an append-only event log (`CheckedIn`, `KitHandedOver`, `CheckInReverted`, `DuplicateScan`), not as flags on the attendee. | Full audit trail, clean undo, safe offline sync, and the dashboard counts from the same data. |
| D2 | Every volunteer has their own identity (login or personal magic link). No shared event links. | "Who checked in whom" is meaningless with a shared link. |
| D3 | QR codes carry a signed token, not a raw attendee ID. Signing is asymmetric. | Devices can verify codes offline holding only the public key. |
| D4 | Records created on a device get a device-generated unique ID (UUIDv7). | Makes sync retries idempotent. |

## P0: needed to run one event end to end

### F01 Users and roles
Status: Proposed · Branch: — · PR: —

Roles: Admin, Staff, Client Viewer, Volunteer.

- Admin can create, edit, deactivate and reactivate users, and assign one role each.
- Staff can manage clients, events and attendees but not users.
- Client Viewer sees only their own client's events and dashboards.
- Volunteer access is scoped to the events they are assigned to (see F09).
- A deactivated user is logged out within one minute and can't log back in.
- Every endpoint enforces role and client scope on the server. A test proves a Client Viewer can't read another client's event by changing the ID in the URL.

### F02 Clients
Status: Proposed · Branch: — · PR: —

- Admin and Staff can create, edit and archive clients (name, contact person, email, phone, logo).
- Archived clients and their events are read-only and hidden from default lists.
- Client names are unique, ignoring case.

### F03 Events
Status: Proposed · Branch: — · PR: —

- Admin and Staff can create events under a client: name, venue, start and end date-time with time zone, and status (Draft, Live, Closed).
- Event settings include an auto-email toggle (default on).
- Closed events are read-only except for exports.
- End time can't be before start time.

### F04 Attendees: manual entry
Status: Proposed · Branch: — · PR: —

- Staff can add, edit and remove an attendee: name, email, phone, company, designation, kit type, rack row, rack column, custom notes.
- Email or phone is required. Email is validated for format.
- Duplicate check within the event on email, then phone. A duplicate is rejected with a link to the existing record.
- Removing a checked-in attendee is blocked. They must be reverted first.

### F05 Attendees: bulk import
Status: Proposed · Branch: — · PR: —

- Accepts CSV, XLSX and JSON up to 10,000 rows.
- Staff map file columns to fields before import, and the mapping can be saved per event.
- Validation runs on every row before anything is written. The result shows valid, duplicate and invalid counts, with a downloadable error file giving the row number and reason.
- Staff choose to import valid rows only or cancel. A partial write on failure is not allowed: the import is one transaction.
- Duplicates within the file and against existing attendees are both detected.
- Formula injection is neutralised in the error and export files (cells starting with `=`, `+`, `-`, `@`).
- An 800-row file imports in under 10 seconds.

### F06 QR tokens
Status: Proposed · Branch: — · PR: —

- Each attendee gets one signed token per event, generated on creation.
- The token encodes event ID, attendee ID and issue time, signed with the event's private key (D3).
- Staff can regenerate a token, which revokes the old one. Revoked tokens are rejected at scan.
- A tampered or wrong-event token fails verification with a clear message.

### F07 Email sending
Status: Proposed · Branch: — · PR: —

- With auto-email on, every new attendee (manual, import, self-registration) is queued an invitation email containing their QR code.
- With auto-email off, nothing is sent until Staff trigger it.
- Staff can send to one attendee, a selection, or everyone not yet sent.
- Sending runs through a background queue, never inside the web request. Bulk sends of 800 must not time out the UI.
- Sent from a domain with SPF, DKIM and DMARC configured, through a transactional email provider.
- Failures retry with exponential backoff, at most 3 times, then mark the attendee as Failed.

### F08 Email delivery tracking
Status: Proposed · Branch: — · PR: —

Last time a few attendees never got their email. This feature exists to catch that before the event.

- Each attendee shows a status: Not sent, Queued, Sent, Delivered, Bounced, Spam complaint, Failed.
- Statuses update from the provider's webhooks. Webhook calls are verified and processed idempotently.
- A "Not received" list (Bounced, Spam, Failed, or Sent with no delivery after 1 hour) can be filtered and exported for the client.
- Staff can correct an email address and resend from that list.

### F09 Volunteers and event access
Status: Proposed · Branch: — · PR: —

- Staff add volunteers to an event by name and phone or email.
- Each volunteer gets a personal login or magic link (D2). Access works only for that event, only between a set start and end time.
- Staff can revoke a volunteer instantly.
- Volunteer sessions show the volunteer's name and counter on every screen.

### F10 Counters
Status: Proposed · Branch: — · PR: —

- Staff create counters per event (name and optional location).
- Staff assign one or more volunteers to each counter. A volunteer works one counter at a time and can be moved.
- Every check-in records its counter (A2).

### F11 QR check-in
Status: Proposed · Branch: — · PR: —

- A volunteer scans with the phone camera from the PWA. No install required.
- A valid scan writes a `CheckedIn` event with attendee, volunteer, counter, device ID, device time and server time.
- The screen shows the attendee's name, kit type and rack position within one second of scanning when online.
- A second scan of a checked-in attendee shows "Already checked in at Counter 3 by Ravi, 10:42" and writes a `DuplicateScan` event, not a second check-in.
- Staff can revert a check-in with a mandatory reason, which writes `CheckInReverted`.
- Invalid, revoked and wrong-event codes show distinct error messages.

### F12 Manual check-in and walk-ins
Status: Proposed · Branch: — · PR: —

For attendees with no QR code, a lost email, or no network.

- A volunteer can search attendees by name, email or phone and check them in. The source is recorded as `Manual`.
- A walk-in not on the list can be added and checked in only after entering the name of the organiser who approved it. The approval is stored with the record.
- Check-ins first recorded on paper can be backfilled by Staff with the original time. Source is `Backfill`, and the record keeps both the original and entry times.
- Backfill is the MVP's only answer to a network outage (see F13). It must handle a batch of at least 200 paper entries, with duplicate detection against existing check-ins.
- When a scan fails for lack of network, the scanner says so clearly and tells the volunteer to log on paper. It never shows a false success.

### F13 Offline check-in and sync
Status: Deferred (P2) · Branch: — · PR: —

Removed from the MVP on 15 Oct 2026. The spec stays here so it doesn't need rewriting when it returns. D1, D3 and D4 are still implemented in P0 so this can be added later without a data migration. Until then, the no-network fallback is operational: a 4G hotspot per counter, a printed attendee list, paper logging, and backfill through F12.

- Before the event, a volunteer's device downloads the event's attendee list, the public key and the counter list. The screen shows when this snapshot was taken.
- The snapshot is limited to fields check-in needs. It is stored in IndexedDB and wiped on logout, on access revocation (at next contact), and 24 hours after the event ends.
- With no network, QR scan, manual search, check-in and kit handover all work against the local snapshot and write events to a local queue.
- Walk-ins created offline get a device-generated ID and are flagged for review after sync, since the server may find a duplicate.
- When the network returns, the queue syncs in order. Sync is idempotent: replaying the same batch creates no duplicates.
- If two devices checked in the same attendee offline, both events are kept. The earliest by device time is the check-in, and the later becomes `DuplicateScan`. Both are visible in the audit view.
- Each event stores device time, last-known clock offset, and server-received time. Large clock skew is flagged.
- A status bar always shows Online or Offline, the number of unsynced records, and the last successful sync. There is a "Sync now" button.
- Logout is blocked while records are unsynced, with a warning.
- Background sync is not relied on, because Safari on iPhone doesn't support it. Sync runs whenever the app is open and online.
- Tests cover: going offline mid-scan, two devices conflicting, a batch sent twice, a revoked token scanned offline, and a 500-record backlog.

Accepted risk: an offline device won't know about a revoked volunteer or token until it reconnects.

### F14 Helper desk screen and kit handover
Status: Proposed · Branch: — · PR: —

- A logged-in screen for Staff or Volunteers, not public, shows a live feed of check-ins for the event, newest first.
- Each row shows name, kit type, rack row and column, check-in time, counter, and kit status.
- The helper marks kit as handed over, which writes `KitHandedOver` with actor and time. This can be undone with a reason.
- Filters: kit pending, kit handed over, by counter.
- Updates arrive within 2 seconds without a page refresh.

### F15 Client live dashboard
Status: Proposed · Branch: — · PR: —

- Client Viewers see their events only.
- Live numbers: registered, emails delivered, not received, checked in, kits handed over, walk-ins.
- Check-ins over time (15-minute buckets) and by counter.
- Export of attendees with email and check-in status as CSV or XLSX.
- Numbers are computed from the event log, so the dashboard and audit view always agree.

### F16 Audit and observability
Status: Proposed · Branch: — · PR: —

- An audit view per event lists every check-in event with actor, counter, device, source, and device and server times.
- Structured JSON logs with a correlation ID on every request and background job.
- A health endpoint covers the database, queue and email provider.
- Unhandled exceptions go through one global handler, return a problem-details response, and are logged with the correlation ID.
- Error tracking and basic metrics are in place: request rate, error rate, email queue depth, and sync failures.

## P1: after the first live event on P0

### F17 Self-registration link
Status: Proposed · Branch: — · PR: —

- Each event can publish a registration link with the fixed core fields plus up to 10 configurable custom fields (text, number, dropdown, checkbox, date).
- The form has a CAPTCHA or rate limit, a consent checkbox for personal data, and the same duplicate check as F04.
- Staff can open and close registration, and set a capacity limit.

### F18 Email templates per event
Status: Proposed · Branch: — · PR: —

- Admin can edit the invitation template per event, with placeholders (name, event, venue, date, QR code).
- Preview with a sample attendee, and send a test to yourself.
- Unknown placeholders are rejected on save.

### F19 Clone event
Status: Proposed · Branch: — · PR: —

- Cloning copies settings, templates, counters and custom fields. It does not copy attendees, volunteers, check-ins or QR keys.
- The clone starts in Draft with "Copy of" in its name.

## P2: backlog, not committed

- Offline check-in and sync (F13, spec above)
- Payments for self-registration (A1)
- SMS and WhatsApp invitations
- Email verification on self-registration
- Drag-and-drop form builder
- Auto-assigned rack positions from a rack layout (A3)
- Assigned counter segments (A2)
- Badge printing
- Native mobile app (A4)
- Multi-tenant SaaS with self-serve signup and billing

## Change log

| Date | Change | Source |
|---|---|---|
| 2026-10-15 | v0.2: F13 offline sync deferred to P2. F12 strengthened as the no-network fallback (bulk backfill, no false success on failed scans). D1, D3, D4 kept so F13 can return without migration. | Scope cut by Chandan |
| 2026-10-15 | v0.1 created from the MVP scope discussion. Added check-in (F11), offline sync (F13) and delivery tracking (F08), which were missing from the first list. Payment moved to P2. | Initial scoping |
