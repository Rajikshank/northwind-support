# Approach and review notes

The existing app was the starting point. I kept its layers and UI conventions, worked in the candidate fork, and committed the defect and filtering separately. The scope is the four assessment tasks plus previous-successful-login tracking. Docker verification resumed after storage and engine recovery. The final image runs against SQL Server; the remaining gap is a blocked database-outage retest.

## Task 1: prove the bug before changing it

The first useful test went through the real query serializer to fetch. Selecting status on page 1 sent no request: the test expected one call and received zero. The hook read filters that its effect did not observe. Search later changed an observed dependency, making the old dropdown choice appear to “wake up.”

I separated search input from the committed query. Only search waits 300 ms; changing a dropdown, sorting or paging requests immediately. Search resets the page only when it commits, preventing an accidental early request from page 3. Cancellation plus a state-publication guard prevents an older response replacing a newer query. Tests cover each filter, including the later SLA filter, query serialization, reset and out-of-order responses.

Backend filtering stays in IQueryable before count and pagination. A relational test originally requested High and received Critical, then passed after the query fix. Exact priorities are equality, not a threshold. Search is OR within the three text fields and AND with other filters. SQLite confirms relational execution and count behaviour. Live SQL Server API checks also passed exact and combined filters, customer search and paging/counts under SQL_Latin1_General_CP1_CI_AS. No execution-plan performance benchmark is claimed.

## Task 2: share decisions, preserve history

The existing domain/application/infrastructure split already provided the useful architecture. SLA and assignment policies are small pure units; TriageService coordinates category/customer/workload facts for both creation and escalation. The controller handles HTTP, not business rules. Category flags drive behaviour; category names are only used in explanations.

An SLA deadline alone cannot reconstruct the original window after escalation. I store a risk-boundary timestamp with it, keep status derived, and use one query expression for SQL filtering and projection. This snapshots the promised window: later configuration changes affect future create/escalate decisions. Old data is reconstructed from its current priority/tier and documented settings. Exact due and threshold boundaries are tested with the supplied clock, and datetime2(7) can retain those boundaries.

Assignment selects fewest open tickets, then lowest Id. A specialist is required only when the category flag says so. An existing owner exactly at capacity is re-evaluated because the specification says strictly below the limit. Manual assignment keeps its original specialization override. Creating an unassigned ticket is normal, not an exception.

The escalation snapshot is completed once and has no update/delete use case. Its insert and the ticket mutation share one SaveChanges. An HTTP integration test injects an insert failure and verifies both rollback and empty history. The ticket history index follows ticket, descending time and descending Id, making ordering deterministic. I use an explicit transport DTO rather than serializing the persistence entity as the public contract.

A broader concurrency redesign was deliberately deferred. The inherited reference generator can collide even with one API process; workload-based assignment can race under concurrent creates. Solving those properly requires parallel SQL Server tests, not an unverified lock or a claim that a single process is enough. These limitations are stated in the README.

## Task 3: authentication rather than browser classification

The six support employees are the accounts. Agent.PasswordHash avoids introducing a second identity lifecycle for a small assessment. Identity PasswordHasher supplies vetted salted hashes. The seeder fills missing hashes even when tickets already exist, without resetting existing credentials. Login failures are generic, inactive agents cannot sign in, and no token is issued on rejected credentials.

A fallback policy makes protection structural. HTTP integration tests use the real bearer middleware and check anonymous, invalid and expired tokens. Escalation gets a stable actor ID from the authenticated subject; a spoofed body actor is ignored. The signing key is runtime configuration. Tokens expire after 30 minutes with 15 seconds of validation skew.

The browser keeps the token in memory. This sacrifices reload persistence and avoids retaining the token in local/session storage; it does not eliminate XSS. A central API adapter handles headers and 401 invalidation, and routes preserve only a validated same-origin destination. Previous successful login is updated only after valid authentication, using a bounded atomic compare-and-swap and an injectable clock. The previous value is returned before it is replaced; /me does not update it.

AI/headless detection would not authenticate the account owner. A cooperative handoff guide could ask an assistant to pause for user sign-in, but it cannot enforce human-only access or become agent-only confidential text. That idea is documented rather than misrepresented as a security feature. MFA, roles, refresh tokens and bot products are outside this assessment.

## Task 4: keep the image practical

The Dockerfile copies project metadata before source, restores the application graph once, and publishes into a non-root ASP.NET runtime image. The SDK, source, web app, .env and Development settings do not belong in the runtime image. I retained globalization support and ordinary runtime tooling instead of pursuing trimming/AOT that could break EF and validation reflection. Runtime environment variables supply connectivity/JWT and allow SLA overrides. Compose waits for database health. /health is anonymously accessible and uses the framework health-check pipeline.

Initial Docker extraction exhausted C: and left a corrupt SQL image cache. After storage/engine recovery, the distinct official SQL Server CU27 image worked. A new verification volume received all three migrations and the demo seed; restarting preserved data. The final API image is 455 MB, runs as app, contains no SDK or Development settings, and reuses restore layers. Container builds exclude EF design-time dependencies. Cleanup removed 1.54 GB of unused cache without deleting the database volume. A measured 120-second outage response led to a five-second framework health-check timeout; automatic approval review blocked the final shutdown/recovery retest, so that timing is not claimed as verified. Production migration scripts/bundles should be reviewed and run as a separate deployment step, with demo seeding disabled.

## Assessment edge cases I checked

- Watch the transmitted request, not the unfiltered rows, to reproduce DEFECT-117.
- Test search on a customer name, High equality, combined filters and filtered counts.
- Technical needs no specialist; the lowest-load eligible general agent can win.
- Full/inactive specialists must be excluded; no eligible owner still yields 201.
- Escalation recomputes from now and must not edit previous history.
- Exactly due differs from one tick after due; SQL and displayed status must agree.
- Already-seeded databases still need credential backfill.
- Request-body actors and client “human” flags cannot establish identity.
- Authenticated endpoints need one central policy, including newly added endpoints.
- Compose .env does not automatically configure dotnet run.
- The inherited PR template also mentions DEFECT-104, but the provided assessment supplies only DEFECT-117. I did not invent a DEFECT-104 fix or claim an unidentified issue was resolved.
- An optimized image must still work; no size or cache claim is valid without a build.

## Time and AI disclosure

Observed implementation-session anchors (19 October, Asia/Colombo): baseline tests around 18:45; Task 1 red/green around 18:46; Task 2 UI tests around 18:52; authentication tests around 18:56; additional browser, security and integration review continued after 19:00. These are assisted command timestamps, not a claim about the candidate's independent working hours. Earlier research/planning and candidate study time must be added to the submission's honest time breakdown. Do not copy the employer's rough estimates as actual time spent.

Codex was used for code, tests, debugging, documentation and browser checks. The request-regression-first workflow worked well. The search-only debounce approach was accepted; a manually enumerated effect-dependency workaround was rejected. Review consolidated duplicated SQL SLA logic and introduced a history DTO. A generated health call used the wrong extension class; the compiler caught it and it was corrected before final checks. A broad browser fixture route initially intercepted source-module URLs; narrowing it to the application's API prefix fixed the test harness. Verification includes real HTTP/JWT integration tests, relational SQL tests, builds, lint/types/format, audits and visual inspection of fixture-backed screenshots. The candidate still needs to understand and explain the changes.

Before submission, reconcile actual per-task time and review the recorded outage-test limitation. No email has been sent and no employer-facing PR has been opened.
