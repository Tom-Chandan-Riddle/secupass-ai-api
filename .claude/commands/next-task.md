---
description: Build exactly one task from FEATURES.md, with an approved approach and an audited diff.
argument-hint: [task ID, optional]
---

Task: $ARGUMENTS (if empty, take the first unchecked task of the first feature that isn't Done).

0. **Orient.** Read FEATURES.md and the last 15 lines of PROCESS-LOG.md. Run `git status`: if the tree isn't clean, stop and ask me what the uncommitted changes are. Check the environment: `dotnet --version`, `docker info`, and whether any app process from this repo is running. If the SDK or Docker differs from the latest values in PROCESS-LOG.md, append a NOTE. If an app process is running that you didn't start, ask me before stopping it (R6).

1. **Branch.** If the feature has no branch yet:
   - If another feature branch is still unmerged, stop and tell me. It has to ship first.
   - `git checkout main && git pull`, create `feature/F0X-short-name`, push with upstream.
   - Write the feature's **Approach** in FEATURES.md: design in three to five lines, where the code goes, the key types, how it's tested, and any risks. Wait for my approval.

2. **Approach gate.** Append a `START · F0X.Y` entry to PROCESS-LOG.md with `date +%H:%M`. Then tell me:
   - what changes and in which files
   - the tests that prove it, including unhappy paths
   - edge cases it must handle
   - anything unclear, as questions with your recommended answer
   Then stop and wait. Don't write code until I approve. If I approve an assumption, record it per CLAUDE.md before coding.

3. **Build.** Write the failing tests first and run them to see them fail, then the smallest implementation that passes, following CLAUDE.md. Nothing beyond this task. Where a test can't be run red first, prove it afterwards by briefly removing the code it covers (R4). Check any framework behaviour the code relies on with a test or probe, not from memory (R5).

4. **Verify.** `dotnet format`, then `dotnet build` with no warnings, then `dotnet test --no-build` all green, plus any checker the brief provides (command recorded in PROCESS-LOG.md). For a new or changed endpoint, update its metadata and the feature's `.http` file. If you ran the app by hand, stop that process and confirm none of yours remain (R6). If something unrelated fails, stop and tell me.

5. **Record.** Tick the task in FEATURES.md, set the feature to In Progress. Append a TASK entry to PROCESS-LOG.md with `date +%H:%M`. If I corrected you during this task, add an AI-FIX entry per correction (R3).

6. **Stage and audit.** `git add` the task's files, including FEATURES.md and PROCESS-LOG.md, then audit `git diff --staged` yourself against this list. Fix what you find before showing me, and report what you changed.
   - **Correctness:** matches the acceptance criteria. Nulls, boundaries, time zones, rounding.
   - **Readability:** names a newcomer understands without context, methods doing one thing, no magic values.
   - **Modern C#:** older patterns where a modern feature from CLAUDE.md would be clearer, and modern syntax used where it makes the code harder to read.
   - **Simplicity:** anything that can be removed, merged or made plainer. Abstractions with one implementation. Dead code.
   - **Extensibility:** the feature is self-contained in its folder and registration. Nothing in other features had to change.
   - **Observability:** business events, handled failures and dependency calls logged with structured properties per CLAUDE.md.
   - **Design:** single responsibility, dependencies pointing inward, no hidden coupling, easy to extend at the obvious seam.
   - **Errors and validation:** per CLAUDE.md. Nothing swallowed, nothing leaked.
   - **Performance:** N+1 queries, loading whole tables, missing `AsNoTracking` on reads, missing indexes for new lookups, sync-over-async, needless allocations in loops.
   - **Security:** injection, missing authorisation, secrets or personal data in logs.
   - **Assumptions:** any behaviour not traceable to the brief, an acceptance criterion or an approved assumption.
   - **Tests prove something:** each new test failed without the code it covers (R4).

7. **Commit gate.** Show me: changed files, a short diff summary, test results, the red-first evidence for each new test, and the audit (findings fixed, plus anything you chose not to fix and why). The staged diff is exactly what will be committed. Ask for approval in the picker and wait (R2).

8. **Commit and push.** One commit, `F0X.Y: imperative summary`. Then `git push`.

9. Tell me the next task, and how long this feature has taken so far against its estimate, using the START and TASK entries. If the feature is past 150% of its estimate, say so and recommend `/checkpoint` now.
