---
argument-hint: [where the requirements are, scope limits]
description: Run once at the start of a piece of work. Records the start, reads the brief and repo, then plans.
---

Notes from me: $ARGUMENTS

If the notes name the requirements files or limit the scope, those override what you'd otherwise infer.

Be fast and thorough, in that order of priority for steps 1–3.

1. **Record the base.**
   - `git checkout main && git pull`.
   - Record the base commit (short SHA of `main`) in the Setup table of PROCESS-LOG.md and the header of FEATURES.md, with the model and effort level you are running as. Fill only the Setup table. Never pre-fill the Log or Summary sections with anything that hasn't happened yet.

2. **Inventory the repo.** Don't change anything yet.
   - Find the brief (README, docs/, *.md) and read all of it.
   - List solution, projects, target frameworks, packages, and test projects.
   - Find any checker, test harness or script the brief provides (look for `*.py`, `*.sh`, `Makefile`, `docker-compose*`, `scripts/`, and instructions in the brief). Read how to run it. Include it in the baseline run below, and record the exact command in PROCESS-LOG.md so later steps can run it.
   - Check the toolchain against the repo: any `global.json` SDK pin, and every `TargetFramework` in the projects, against `dotnet --list-sdks` and `dotnet --list-runtimes`. If an SDK or runtime is missing, stop and tell me exactly which one to install. Don't change `global.json` or target frameworks to work around it.
   - Run `dotnet build` and `dotnet test`. Record the baseline: does it build, how many tests, how many pass.
   - Decide the repo state: existing code or greenfield. For existing code, note the conventions in use (API style, data access, test framework, naming).

3. **Report to me** in this shape, nothing longer:
   - Brief in five lines: what's asked, required versus stretch, any explicit acceptance or evaluation criteria.
   - Repo state and build/test baseline.
   - Numbered list of ambiguities, each with your recommended answer.
   - Top three delivery risks.
   - **Fit check.** Use the "Override when" column of DEFAULTS.md. A short table of the CLAUDE.md defaults that don't fit this brief or repo, each with what you propose instead and why. Cover at least: project structure and architecture style, API style (controllers or minimal APIs), database and its fit for the queries the brief needs (especially money and concurrency), EF Core or Dapper, whether caching is needed, test framework and test approach, libraries already in use, and anything the brief explicitly asks for or forbids. If everything fits, say so in one line.

4. **Wait for my answers.** Then, in two approvals:

   **4a. Solution outline.** Write the Solution outline section of FEATURES.md and show it to me:
   - the domain model: main entities, their key rules and relationships
   - how a request flows: endpoint, validation, handler, domain, persistence, response
   - where cross-cutting concerns live: errors, logging, correlation ID, validation
   - project and folder structure, after any fit-check overrides
   - the main variation points the brief signals, if any
   Keep it to about 15 lines. Wait for my approval or changes.

   **4b. Feature plan.**
   - F00 Foundation is always first. Estimate 25 minutes at most, only what the features will use.
     - New repo: solution and projects, `.gitignore` (`dotnet new gitignore`) before the first build so `bin/` and `obj/` are never committed, Directory.Build.props, .editorconfig, global exception handler, ProblemDetails, correlation ID middleware, request and structured logging, `/health` and `/health/ready`, OpenAPI with Scalar, unit and integration test projects with one passing HTTP test against `/health`.
     - Existing repo: characterisation tests around the code the features will change, plus only the missing cross-cutting pieces I approve.
   - Then fill FEATURES.md: brief summary, assumptions, features in build order with priority (Must/Stretch), an estimate in minutes, testable acceptance criteria including unhappy paths, and tasks of about 20 minutes each. Ask me how much time I have for this piece of work, and size the Must features to fit in about half of it, leaving the rest for review, hardening and docs. Hardening (validation, error handling, logging, health) is either inside each feature's criteria or its own feature.
   - Record each approved override from the fit check as a decision in FEATURES.md, and update the relevant section of CLAUDE.md so later tasks follow it.
   - Append to PROCESS-LOG.md: START, READ, one DECISION per approved override, one ASSUME per assumption, one PLAN.
   - Show me the feature list for approval. Wait.

5. After I approve, commit on main: `chore: plan and AI workflow setup`, including CLAUDE.md, DEFAULTS.md, .claude/, FEATURES.md and PROCESS-LOG.md. Push. If the brief came with its own CLAUDE.md or README, merge ours into theirs rather than overwriting.

Write no production code in this command.
