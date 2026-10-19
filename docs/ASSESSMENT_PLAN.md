# Northwind assessment: evidence and implementation plan

Revised 19 October 2026 after a second requirements, security and environment review. Planning only: no application changes or tests have been executed for this report. Deadline: EOD 21 October 2026, per the candidate email; timezone unspecified. Work within the assessment's approximately 13–17 working hours and record actual time honestly.

## Implementation status

Tasks 1–3 now have implementation and automated/browser-fixture evidence. Container configuration is written, but Docker runtime and live SQL Server checks are deferred after storage exhaustion. Actual status and limitations are in EVIDENCE.md and README.md; this document records the intended plan, not proof that every planned check passed.

## Revision decisions

This revision supersedes the previous plan. It cannot guarantee a hiring outcome or be called perfect before implementation evidence exists. Improvements are: an explicit AI handoff threat model; the user's last-successful-login requirement; tighter limits on optional concurrency work; verified installation status; and mandatory pass/fail gates for each task.

Mandatory scope is the original assessment plus last-successful-login tracking. A short advisory AI handoff guide and pre-expiry UX are bounded polish only after all four core tasks pass. Automated bot blocking, human-verification claims and new delegation infrastructure are excluded. The user-facing alternative to a security detector is a voluntary, accessible handoff path. No AI service or paid bot product is needed.

## 1. Delivery objective and scope

Deliver Tasks 1, 2, 3 and then 4 in order. Quality means meeting acceptance criteria with understandable code and reproducible evidence, not adding architecture for its own sake. The optional SLA summary is excluded from the initial plan. Do not build bot detection, a redesign, registration, roles, refresh tokens, CI or other excluded features.

“Agent” means a seeded human support employee, not an AI agent or browser bot. Task 3 explicitly specifies agent email/password login and JWTs. Headless-browser detection cannot establish that identity and would not meet AU-1 through AU-13.

Task 1 cannot satisfy the specification without modifying the faulty existing code. Interpret the preference as preserving current architecture, contracts and unrelated behaviour while making the smallest justified change. A short correct fix backed by strong evidence is preferable to an elaborate workaround.

The PDF is the primary assessment; additionally obey the email's fork requirement and the repository README's APPROACH.md requirement. If wording differs, record the interpretation. No score or selection outcome can be guaranteed.

## 2. Inspected repository evidence

| Inspected location | Finding | Consequence |
| --- | --- | --- |
| `apps/web/src/features/tickets/hooks/useTicketList.ts` | Effect reads the full filters object but watches only search, page and page size; exhaustive-deps is suppressed. All watched changes are delayed 300 ms. | Static evidence for DEFECT-117; reproduce with a request assertion before calling it proven. Non-search filters must not be debounced. |
| `apps/web/src/features/tickets/hooks/useTicketList.test.ts` | Only settled search is covered; ticketsApi is mocked. | Add filter coverage and a regression reaching the actual request serializer/fetch boundary. |
| `apps/web/src/api/tickets.ts` | Query serialization explicitly lists each parameter. | SLA needs a typed field and serialization entry; it must not need a special effect dependency. |
| `apps/api/src/SupportDesk.Infrastructure/Queries/TicketQueries.cs` | Labels joined, reads untracked, deterministic sort tie-break already present; no filters before count. | Preserve these strengths and compose predicates before count/paging. |
| `apps/api/src/SupportDesk.Domain/Aggregates/Tickets/SlaEvaluator.cs` | No AtRisk calculation; custom method is used in the final projection. | Ordinary C# invocation cannot be assumed SQL-translatable in a filter. Establish server-side translation explicitly. |
| `Ticket.cs`, `RaiseTicketCommandHandler.cs` | New ticket gets no due date or owner. Aggregate owns mutations; handler orchestrates repositories. | Extend the existing design; share policies between create and escalate. |
| `AgentQueries.cs` | Open workloads already counted using database subqueries. | Reuse the query style, but use a narrow eligibility/workload projection for triage. |
| `AssignTicketCommandHandler.cs` | Manual assignment deliberately overrides specialization; rejects inactive agents. | Preserve and document this existing override; automatic assignment follows BR-4/5. |
| `TicketConfiguration.cs` | Existing filter indexes; dates mapped as datetime2(3). | Avoid speculative indexes. Address persistence precision for one-tick SLA boundary tests. |
| `TicketRepository.cs` | Reference generated from highest existing Id + 1. | This is unsafe even with simultaneous requests to one API process; handle or clearly expose the limitation. |
| `SupportDbSeeder.cs` | Returns immediately if tickets exist. | Adding credentials below that return would leave existing development agents unable to log in. |
| `Program.cs` | Central validation/error handling, no authentication, Development migrations/seeding. | Extend central pipeline; maintain ProblemDetails behaviour. |
| `SupportDbContext.cs`, architecture tests | Existing unit of work and inward dependency tests; migration/model consistency test. | Preserve dependency direction and commit migrations/snapshot together. |
| `docker-compose.yml` | SQL Server service already has a health check. | Extend existing Compose rather than replace it. |
| Git remotes | origin is Verdentra's original repository. | Establish the candidate fork before implementation commits/pushes. Never push to the original. |

Environment observations were refreshed during revision. Node v24.13.1 and Git 2.53.0 are available; node_modules is absent. .nvmrc requests Node 22, and package-lock.json exists. No dotnet executable was found on PATH or in usual machine/user locations. Docker Desktop 4.84.0 is registered at `%LOCALAPPDATA%/Programs/DockerDesktop`; its docker client 29.6.2 and Compose v5.3.1 run by absolute path. The Docker engine is not reachable (missing docker_engine pipe), so it is not ready for database/container tests. WSL 2.6.3.0 is installed with Ubuntu and default WSL version 2. Machine: Windows 11 Home x64, approximately 15.4 GiB usable RAM, virtualization enabled. GitHub CLI is not on PATH and is optional. No application build/runtime/performance result is claimed here. See SETUP_CHECKLIST.md for verified prerequisites and commands.

## 3. Architecture decision

Keep the existing layered monolith and feature-oriented handlers. There is already a useful command/read separation; do not add a mediator framework or split services. Microsoft describes inward dependency boundaries and testable application cores [S3]. This is a design recommendation tailored to this repository, not a claim that there is one universally best architecture.

| Layer | Responsibility |
| --- | --- |
| Domain | Ticket invariants, immutable escalation values, pure priority/SLA/assignment decisions. No EF, HTTP, JWT or configuration framework dependencies. |
| Application | Raise/escalate/login use cases, coordination, typed contracts, validation, clock and actual external seams. |
| Infrastructure | EF projections/persistence, transaction implementation, credential hashing/token implementation where appropriate, system clock. |
| Presentation | Request binding, authenticated actor extraction, status codes, centralized authorization and errors. |
| React | Typed API adapter and data hooks, forms and accessible presentation. Server remains authoritative for rules. |

Use three patterns only where they naturally fit: pure policies for shared rules; existing repositories/unit of work for mutation persistence; query composition for SQL reads. Use interfaces for a clock, persistence, password/token facilities and other replaceable boundaries, not every concrete policy.

Proposed units (names may follow repository conventions): PriorityPolicy, SlaPolicy, AssignmentPolicy, TriageDecision, EscalateTicketCommandHandler, TicketEscalation, SlaQueryExpressions, LoginCommandHandler and AuthSession. Policies accept typed input snapshots and return values/reasons; they do not fetch their own dependencies. Handlers remain short orchestration code. Do not create a single giant TicketService.

## 4. Stage 0: orientation and baseline (about 1 hour)

1. Confirm candidate GitHub identity and fork; upstream remains the original and origin becomes the fork. Use a feature branch in the fork and preserve real history.
2. Resolve runtime availability, install dependencies from the lockfile, and check pinned packages actually restore. Do not upgrade the stack to solve unrelated problems.
3. Run SQL Server, API and web using the existing instructions. Run existing build, tests, lint and type checks. Record pre-existing failures separately.
4. Use list, detail, create, status, assignment and customer workflows; inspect Swagger and SQL logs. Follow the assessment's prescribed code-reading order.
5. Start an actual time log. Record baseline commit and command results. Never invent elapsed work, rejected AI suggestions or test outcomes.

## 5. Task 1A: defect fix (about 1 hour)

### Investigation and fix

Reproduce status=InProgress on page 1 with no search interaction, observing that no request is sent. Before changing production code, add a failing test using the real ticketsApi serializer with fetch intercepted. A hook-only mock assertion is useful but insufficient as the only evidence for the transmitted URL.

Separate input search text from committed/debounced search. Debounce search alone for 300 ms. Build a stable effective request object from the entire non-search filter state and the committed search; the request effect depends on that object, not a hand-maintained subset. Keep lint dependency checking enabled [S1]. Avoid a newly allocated object on every render.

On a non-search change, issue the request immediately with the current committed search. If the user is still typing, the pending search commits once after 300 ms, using the latest non-search filters. Changing search must not cause an early request merely because it resets pagination: reset page atomically when that search commits. Reset-all clears the search timer and applies an empty effective query immediately. Pagination and sort changes are immediate. Adding slaStatus changes the typed filter object/serializer, not the fetch scheduling logic.

Guard request ordering: obsolete successes and failures must not replace a newer result. Prefer AbortSignal through getTickets to the existing request adapter plus a latest-request identity/cleanup guard for state writes. Reload uses the same request lifecycle. Cancellation is not displayed as a user error. Do not promise one development mount request under React Strict Mode, which intentionally exercises setup/cleanup [S1].

### Proof

- Red-before/green-after test for the status-only request; assert pathname and URLSearchParams, not parameter order.
- Parameterized immediate requests for priority, category, customer, assignee, unassigned-only, sort and page.
- Fake timers: rapid search keystrokes send one settled request; clear-search works; test from page >1; dropdown during pending search has the documented semantics.
- Reset, manual reload, out-of-order response/error and unmount cleanup tests.
- Task 2 adds a real SLA request test demonstrating no fetch-effect edit is needed.
- Browser network check of the original reported steps after the fix.

Comment only the reason for search-only debounce and stale-response protection. README and the focused commit explain the omitted dependencies and why later search changes picked up the previously selected status. A small diff is desirable; do not inflate it to look sophisticated.

Commit separately: `fix(web): synchronize ticket requests with filter changes`.

## 6. Task 1B: database filtering (about 1.5 hours)

Compose ApplyFilters over IQueryable before CountAsync, then sort, Skip/Take and narrow DTO projection. Preserve AsNoTracking and the existing Id tie-break [S2]. Do not run concurrent operations on the same DbContext or materialize before filtering.

- FL-1: exact enum/Id equality for all five filters.
- FL-2: unassignedOnly means AssignedAgentId IS NULL.
- FL-3: trim once; blank ignored; search title OR reference OR customer name. Use parameterized provider-translated operations and test literal wildcard characters if choosing LIKE. State that case matching follows SQL Server collation; do not lowercase indexed columns indiscriminately.
- FL-4: separate supplied predicates combine with AND. An agent Id plus unassignedOnly yields an empty intersection; do not silently choose one.
- FL-5: same filtered source for count/items and existing PagedResult semantics, including no-match/out-of-range pages.
- FL-6: SLA adds a predicate against the same pipeline.

Relational tests cover every filter, required exact High/Low priority, combinations, customer-name search, blank search, unassigned contradictions, count, paging and stable sort. SQLite can supply fast basic relational tests, but SQL Server must verify actual provider translation/collation/migrations; it is not interchangeable with SQLite [S4]. Avoid mocked DbSet or EF InMemory as proof of SQL behaviour.

Capture actual executed SQL for both count and page. Assert database predicates and paging through relational tests; inspect plans/logical reads for representative queries. Preserve useful existing indexes. Substring searches may scan; a tiny seeded dataset cannot prove production-scale speed. Do not claim every LIKE/Contains query is an index seek. Offset pagination is retained because numbered pages and totalPages are required; discuss keyset pagination at interview for a different large-scale UI [S2].

Commit separately: `feat(api): apply ticket filters before count and paging`.

## 7. Task 2: shared triage, SLA and escalation (about 7 hours)

### Decisions before code

Bind existing Sla configuration to validated options: all four positive base windows, positive multiplier/floor and threshold between 0 and 100. Snapshot settings per operation so one triage uses consistent values. Pass ordinary values to pure domain policies. Category behaviour uses flags, never names.

Priority: requested or Medium, overridden to Critical by the category flag. SLA window: max(configured floor, base times tier multiplier). Capture IClock.UtcNow once per operation. Create and escalate share both calculations and their explanation generation.

Assignment: query a narrow snapshot of active status, specialization eligibility, limit and SQL-counted open workload. Pure selection orders eligible candidates by open count then Id. No eligible candidate returns null plus a reason and is a normal 201 create result. For escalation, count the current open ticket in workload: the specification says strictly below MaxOpenTickets; an owner at exactly the limit is ineligible and must be re-evaluated. Document this literal interpretation and test it. Preserve the existing manual specialization override rather than silently changing it.

### SLA temporal model and SQL

Choose an explicit historical interpretation: configuration changes apply to newly created/escalated SLA windows, rather than silently changing existing promises. Persist DueAtUtc and an additional nullable SlaAtRiskAtUtc timestamp calculated by SlaPolicy for each window. This is timing metadata, not stored SLA status. It avoids reconstructing the window from CreatedAtUtc after escalation. The threshold is configurable and snapshotted for each new window, just as the due date is. Document this choice prominently.

Backfill seeded/existing risk timestamps from their current due date, priority, customer tier and configured rules; old rows did not retain full timing metadata, so document that reconstruction assumption. Tickets without SLA retain null values. Use datetime2(7) for SLA-relevant timestamps (including resolution and escalation time) so persistence can represent a .NET tick; inspect migration effects on existing data.

Derive status using ordered comparisons:

1. No due date => NotApplicable.
2. Resolved timestamp present => Met if <= due, otherwise Breached.
3. Unresolved and now > due => Breached.
4. Unresolved and now >= risk timestamp => AtRisk.
5. Otherwise WithinSla.

At exactly due, unresolved is AtRisk; one tick after due is Breached. At exactly risk threshold it is AtRisk. Closed tickets use the resolution timestamp maintained by the existing aggregate.

Infrastructure exposes a typed SQL-translatable expression using the comparisons above. Use it for the list projection and SLA filtering; reuse its projection in all read paths where practical. Do not place a custom C# evaluator inside Where or compile an expression before database execution: EF permits some client work in final projections, but predicates must translate [S5]. Domain evaluation and the SQL expression are different representations at a necessary boundary; parity tests against all five statuses and exact boundaries prevent divergence. Capture one now parameter for count and page. This approach avoids provider-specific duration arithmetic [S6].

Trade-off: one extra timestamp and a documented configuration snapshot policy. Rejected: storing SlaStatus (would become stale), deriving elapsed window from creation after escalation (wrong), and provider-specific date arithmetic without a demonstrated need. If reviewers require configuration changes to recalculate existing windows, this snapshot policy would need revision; the current assessment does not specify retroactivity.

### Mutation and persistence

- Extend create to load the real category/customer, compute triage, assign/apply due/risk timestamps and return typed triage reasons plus the full decision.
- Add an aggregate escalation operation enforcing legal state, one-step priority movement and trimmed reason length 5–500. Application coordinates owner selection and timing policy.
- Add TicketEscalations with all specified fields; private mutation API, required reason/actor/time, nullable before/after owner/due values as appropriate. No update/delete history endpoints. Record snapshots before changing the ticket.
- Restrict deletion of referenced tickets/agents to preserve audit links. Add `(TicketId, EscalatedAtUtc DESC, Id DESC)` for deterministic newest-first history; explain why it serves ticket history and avoid a redundant single-column index if EF's FK index is covered.
- Commit ticket update and history insert atomically in the same unit of work; a failure must roll both back. One SaveChanges already provides atomic writes; an explicit transaction is justified only when the preceding eligibility reads must share isolation with the writes [S17].
- Add 200 escalation, 400 invalid input, 404 unknown ticket and distinct 409 state messages. Unknown ticket history returns 404, not a misleading empty list; existing ticket with no history returns [].
- During Task 2 use validated escalatedBy (required, <=100); Task 3 removes trust in this client value. Final implementation derives a stable authenticated identity from claims and stores a readable actor, keeping the length bound explicit.

### Concurrent requests (conditional hardening within the time budget)

Correctness is not established by a single successful demo. Two automatic assignments can see the same final capacity slot, and two escalations can race. If implementing concurrency hardening, evaluate a short infrastructure-managed serializable transaction for workload read/selection and write, with SQL Server execution strategy around the entire retriable unit; keep tracked state isolated on retries. Bring workload-changing operations into the same consistency design, including existing status/assignment paths. Do not hold transactions during user interaction or response rendering. Protect concurrent ticket mutation with a concurrency token or equivalent serialization and translate exhausted conflicts into a clear 409 [S7]. Explicit transactions must cooperate with the existing EnableRetryOnFailure configuration [S16]. A lost connection during commit creates an uncertain outcome: do not blindly retry a create/escalation and duplicate it; verify by the operation's stable identifier or report the uncertainty. Prove concurrency behaviour with two SQL Server contexts contending for the final slot and simultaneous escalation of the same ticket.

The current reference generator also races. A database sequence initialized above existing references, retaining the unique reference constraint, is the preferred bounded hardening if time allows. Sequence gaps are acceptable; duplicates are not. Include it in a migration and test parallel creation if implemented.

Scope correction: atomic ticket/history writes and server-side state guards are mandatory; a general transaction/retry framework, broad mutation refactor and reference-sequence change are conditional hardening, not prerequisites to finishing authentication and Docker. First preserve correctness for specified sequential flows and prove atomic history writes. Timebox concurrency investigation to 30 minutes within Task 2; implement a narrow solution only if it can be verified without consuming later tasks. Otherwise explicitly document the capacity/reference races, including that a single process is not a concurrency guarantee. Do not claim concurrency safety based solely on isolation choice without a parallel SQL Server test. Reassess this decision after core task completion rather than silently growing the design.

### Frontend

- Replace list-only SlaCell with one reusable typed SlaIndicator, used by list/detail; text plus optional icon and colour for five distinguishable states.
- Add typed slaStatus filter/serialization and verify it immediately requests data through Task 1's mechanism.
- Add useTicketEscalations/useEscalateTicket or equivalent small hooks; pending state prevents duplicate submit, identical trimmed validation bounds, field errors linked accessibly.
- Escalation 409 displays the server reason without optimistically changing the ticket. Success adopts the authoritative response and refreshes history without page reload; refresh failures have retry UI.
- Show history's before/after priority, owner and deadline, reason, actor and UTC-derived local display time. Hide/disable escalation for Critical/Resolved/Closed but handle stale-state rejection.
- Extend the existing create success notice with applied priority, deadline, SLA, assigned agent or unassigned reason, and triage explanations.
- Cover loading/error/empty states, keyboard use and focus/error announcements. No any or ts-ignore, no fetching inside new presentation components.

### Tests and verification

Reuse FixedClock and TicketBuilder. Test all priorities x tiers, one-hour floor, forced Critical and requested/default priority, configuration validation, every eligibility exclusion, deterministic ties and nobody eligible. Test escalation step, due/risk from now, owner retained/replaced, exact history snapshots, immutable access, atomic rollback and every rejected state/input. Verify supplied worked examples against independent reset seed state, since creating tickets changes workloads.

Test domain and SQL status at before/at/after due and risk threshold, early/late resolution, no SLA and Closed behaviour, including persistence round trips. SQL SLA filter combines with ordinary filters, count/paging and narrow projection. Frontend tests must include short reason never calls API, 409 leaves ticket unchanged and every indicator state; also cover success refresh and triage unassigned rendering. Apply migrations to an empty SQL Server database and upgrade the baseline schema with data. Keep existing architecture/model tests green.

Suggested commits: shared policies/tests; timing/history migration and handlers; SQL SLA queries/tests; typed UI/tests. Keep each coherent and runnable.

## 8. Task 3: staff authentication (about 4 hours)

Use Agent.PasswordHash rather than a separate users subsystem: one existing agent is one account, no separate identity lifecycle is requested. Add a nullable hash migration for existing agents; unprovisioned accounts cannot log in. Do not expose the hash through any DTO, log or reference endpoint. Normalize email consistently; enforce uniqueness after checking existing data and document case behaviour.

Use vetted PasswordHasher<Agent>, its verifier and rehash result handling [S8]. No custom cryptography. Development seeding hashes an obviously local fixture password at runtime. Add an idempotent credential backfill before the seeder's existing tickets-present return; do not reset an existing password on every startup. Inactive Yuki stays unable to log in. Fixture credentials may be documented; only hashes go into database storage. Do not put passwords into migrations or logs.

Login returns token, expiry and typed user data. Unknown email, wrong password, inactive or unprovisioned agent all return the same generic 401 response and no token. Use dummy hash verification on missing accounts where practical, without claiming perfect timing indistinguishability. Login payload validation returns 400 for malformed input.

JWT decision: short-lived 30-minute HS256 token for this assessment, strong runtime-configured key (at least 256 bits), issuer/audience validation, signature and lifetime validation with a documented small clock skew. Include stable agent Id subject and bounded display actor/name; do not trust a browser-decoded token as server validation. Validate configuration at startup. Keep compatible package versions centralized [S9].

Use a fallback authenticated-user policy and correct authentication/authorization middleware ordering. Explicitly allow only login and later health anonymously; Swagger stays open as required. Document whether Swagger is enabled only in Development. Test every endpoint family, including new history/escalation and auth/me, with no/invalid/expired tokens. auth/me checks the agent still exists/is active; document that already-issued access tokens otherwise remain valid until expiry without per-request revocation checks.

Frontend decision: memory-only token storage with a small session module/provider, attached centrally by api/client.ts. Reloading requires login; document that trade-off. This avoids persistent browser token storage, but does not make XSS harmless [S10]. No refresh token is in scope. For a future production app discuss an HttpOnly-cookie/BFF design without implementing it here.

Add accessible /login with validation, pending and generic failure state. Protect all existing app routes. Preserve a local return path and reject external redirect destinations. Render current agent and logout. A central 401 event clears the session and redirects once, preserving intended location; login failures remain on the form without redirect loops. Ignore late requests from an old session after logout/new login. Logout clears sensitive in-memory user/query state; server-issued JWT revocation is a documented limitation.

Escalation body no longer supplies a trusted actor; derive it from the authenticated identity through a narrow boundary. Test spoofed escalatedBy cannot affect the history actor. Keep final Swagger/request examples consistent with this contract.

Tests: right/wrong password hashing; wrong/unknown/inactive login => same 401/no token; successful login and /me; protected endpoint missing/invalid/expired vs valid JWT; central endpoint coverage; token attachment, login redirect/return, logout and mid-session 401. Add a small API integration test host if necessary, preserving real JWT middleware rather than replacing it with always-successful authentication.

### Requested addition: last successful login

Add nullable Agent.LastSuccessfulLoginAtUtc (datetime2(7)) in the credentials migration; existing agents start null. The server clock supplies this value. Update only after credentials and active-account checks succeed, and persist before returning a successful login response. Build token material before the write so a signing/configuration failure does not advance the timestamp. No update on wrong password, unknown email, inactive account, auth/me, token expiry or logout. A network failure after commit may mean a successful server login was not seen by the client; document that normal distributed-system limit.

Capture the previous timestamp before updating. Return previousSuccessfulLoginAtUtc in the successful login response, alongside the new session's login time. Display “Previous successful sign-in: …” or “First sign-in” only after authentication. auth/me may expose the latest successful login, but must not label the newly updated timestamp as the previous one. Never reveal account existence/activity through the anonymous login form. For simultaneous logins, use an atomic update returning the prior value (or a narrow serialized read/update) and avoid decreasing the timestamp; integration-test the selected implementation. Do not add a device/IP tracking subsystem for one timestamp.

Tests: first success stores time, next success returns the previous time, every rejected login leaves it unchanged, /me does not change it, UTC persistence round-trip, no anonymous metadata leak, and concurrent-success semantics if implemented. Make this a focused addition, not a login-history dashboard. OWASP supports authentication-event logging and excluding sensitive values from logs [S20/S21]; this field is a product decision, not a claim that OWASP requires it.

### AI-assistant handoff: useful guidance, explicit limits

Threat model: distinguish a cooperative assistant, ordinary test automation and a hostile client. Only cooperative software is expected to respect a handoff instruction. The app authenticates credentials, not whether the operator is human. A person may use a real browser with an assistant attached. navigator.webdriver is a cooperative automation signal, not an AI-identity proof [S18]; user-agent headers and client-submitted “human” flags must not become access controls. Bot products themselves use multiple signals and probabilistic scores [S19].

Recommended implementation if core work is complete: a small visible “Using an AI assistant?” disclosure on /login. It offers a handoff guide and retains the ordinary accessible login flow. Display plain text such as:

> This support desk requires the account owner's sign-in. If you are an assistant, pause here and ask the user to enter their credentials directly. Do not request, record or transmit their password. Resume only when the user chooses to continue.

Use semantic HTML that both humans and tools can read; a plain-text version can be rendered in the same disclosure. “Agent-only readable” text is not possible confidentiality: content delivered to a browser can be read by a person or program. Do not hide instructions from the user, embed secrets or insert prompt-injection-style overrides. Do not expose private support data in guidance.

If the user explicitly chooses handoff mode, the UI may pause its login form and provide “Return to sign-in” for the person. This is workflow guidance; a program can click it too. No hidden detector, mandatory fingerprinting or special bot bypass token. Do not reject valid login API requests based only on browser/UA hints, break Swagger/API clients or disable automated auth tests. Every private API remains protected by the real fallback policy.

The guide must say that signing in inside an assistant-controlled browser does not establish a secure separation: the assistant may be able to act in the authenticated session afterward. A true human-only login guarantee would need a different trusted approval/authentication mechanism and an expanded specification. Passwordless/MFA/SSO changes are excluded by the assessment, so document that future direction rather than implement it.

Acceptance if included: human sees the guide; no login call occurs just by opening it; normal login and keyboard navigation work; no credentials/tokens in guide/logs; expired sessions still receive 401; changing webdriver/UA cannot grant access. Test the normal browser and automated test path. Allocate at most 20 minutes after Tasks 1–4 pass; otherwise retain this as a README design note.

### Three relevant additions to consider

1. **Previous-sign-in awareness**: implement the requested server-side timestamp and authenticated display above. Useful security context with small scope; it does not detect account compromise by itself.
2. **Clear session expiry**: optionally show an accessible pre-expiry warning and an exact expiry time from the server, with a sign-in-again action. Preserve only a validated local return route; do not persist confidential ticket drafts or credentials. No refresh-token scope. Server validation remains authoritative even if client timers are delayed.
3. **Explainable, auditable actions**: polish required triage reasons and escalation before/after history; keep the authenticated actor trustworthy. Render ticket text as text, never executable HTML or instructions. This helps humans supervise AI-assisted work without adding an AI model. No claim that text rendering alone prevents an external AI assistant from following malicious ticket instructions.

Decision: last-sign-in is included; expiry UX and handoff guide are polish only; explainability is already core Task 2. A future approved AI integration should use a distinct service identity with least-privilege delegated access and server-enforced approval for writes, rather than a human's password. This would need new requirements and is not built for this assessment.

Suggested commits: credential migration/seeding and auth API/tests; session/client/routes/login UI/tests. No CAPTCHA, browser fingerprinting or automation blocking: these do not satisfy staff authentication and obstruct legitimate testing.

## 9. Task 4: reproducible API container (about 1.5 hours)

Use a root-context multi-stage apps/api/Dockerfile: compatible .NET 10 SDK for restore/build/publish and ASP.NET 10 runtime for execution. Copy global.json, Directory.Build.props, Directory.Packages.props, applicable NuGet configuration and required project files before source. Restore the application project dependency graph, then copy only API sources and publish; test projects need not enter the runtime build. Avoid a package-cache mount with missing cached assets between restore/publish unless verified; normal Docker layer caching is sufficient [S11].

Add .dockerignore covering nested bin/obj, node_modules, .git, .env/real secret files, web and irrelevant artifacts. Exclude appsettings.Development.json from publish so its existing connection-string fixture is not baked into the runtime image; provide nonsecret SLA defaults separately and runtime environment values for sensitive configuration. Inspect the published output/image rather than just Dockerfile text. No SDK/source/credential files in the final stage.

Run as the image's app user/APP_UID, listen on 8080 and expose localhost:5080 only. Do not assume a non-root user is automatically selected just because it exists [S12]. Compose passes environment name, connection string using sqlserver service DNS, JWT settings and local fixture settings from runtime environment/.env. Every added variable is described in .env.example. Compose .env interpolation does not automatically load variables into dotnet run; document PowerShell setup explicitly.

Depend on SQL Server service_healthy, not just container startup [S13]. Development startup migrates and seeds; startup failures remain visible. /health anonymously checks database reachability, returns healthy/503 appropriately and discloses no connection secrets. If adding a container health check, verify its HTTP probe exists in the runtime image; do not assume curl is installed. Test web proxy targeting http://localhost:5080 with PowerShell's $env:VITE_API_PROXY_TARGET syntax.

Production documentation explains reviewed migration scripts/bundles as a separate controlled deployment step, least-privilege runtime database access and no demo seeding; do not implement a deployment platform [S14].

Verification in a disposable Compose project/volume, never an unexamined user's database:

1. Clean build/start with empty data => /health 200, login works, authenticated /api/tickets contains seed data.
2. Restart => no duplicate seeds/password resets; original data persists.
3. Database unavailable => /health failure status; reconnect/restart as appropriate.
4. Change one .cs file temporarily and rebuild => restore layer explicitly reports cached; restore the temporary change.
5. Record exact final runtime image and matching SDK image sizes with tags/commands; no invented size reduction.
6. `docker compose exec api whoami` returns a non-root identity; inspect user/port/environment and published image contents.
7. Browser login/list/create/escalation through the web proxy against the container API.

Commit: `build: containerize the API with runtime configuration`.

## 10. Final evidence, documentation and delivery (about 1 hour)

Target total: 17 hours including orientation and documentation; measured work may vary. Last-login work fits the Task 3 allocation by keeping the implementation small; if estimates slip, defer polish and conditional hardening first. Do not present 17 hours as a guaranteed estimate. Prioritise completed earlier tasks over partial later ones. A failing final check is unresolved, not “verified.” Defer optional report and extra polish first.

Mandatory gates: baseline runs before Task 1 changes; red/green request regression before the defect commit; filtered SQL/count/paging proof before Task 2; rule/boundary/history/UI checks before Task 3; login/central authorization/actor/last-login checks before Task 4; empty-volume/container/proxy checks before completion. Each gate records commands/results or an explicit blocker, never an assumed pass. If a prerequisite is externally blocked, continue independent work but do not declare the blocked gate complete.

Run root build/tests, frontend lint/typecheck, repository format verification and existing architecture/model consistency tests. Run selected live browser/API/SQL/Docker checks in addition to automated suites. Check keyboard access and responsive usability of touched UI without redesign. Inspect final diff for secrets, generated build output, unnecessary dependencies, dead debug logs and unrelated changes.

Maintain a compact evidence table: requirement, test/check, actual result, command or test name, and commit. Requirements groups to map explicitly: DEFECT-117; FL-1–6; BR-1–8; triage/escalation/history/persistence/SLA endpoints and Task 2 UI/tests; AU-1–13; DK-1–8 and all four Docker verification checks. This table is a useful review artifact, not a replacement for the README.

README remains practical, about 1–3 pages: completed/incomplete work; meaningful chosen/rejected design decisions; defect root cause and verification; SQL filtering evidence and extension path; schema/index rationale; authentication storage/hashing/token lifetime/production trade-offs; local credentials; exact Windows-compatible setup/run/test commands; Docker results and migration strategy; actual AI use; limitations.

APPROACH.md explains each task's reasoning in the candidate's own words and actual time spent. Include an actual effective AI workflow, an accepted suggestion, a changed/rejected suggestion and a real bug found/introduced only if they actually occurred. Do not fabricate anecdotes for the checklist. Link detailed evidence instead of bloating the README.

Commit docs and review real history. Prefer a PR within the candidate fork if appropriate; do not submit against Verdentra's repository without an explicit reason. Prepare a concise email/PR description with fork link, dev credentials, actual time breakdown and unfinished items. Sending email is a separate user-authorized action.

Interview rehearsal: explain the reactive dependency bug, SQL count/paging, SLA threshold snapshots and boundary parity, transaction/assignment trade-offs, password verification, central authorization, memory-only JWT limitations and Docker restore caching. For 10 million tickets discuss measured indexes/plans, substring search/full-text options and pagination alternatives, without implementing them now.

## 11. Research sources and what they support

These are official project/vendor/security sources, consulted 19 October 2026. Recommendations above combine their guidance with the inspected assessment and code; they are not quotations or claims of measured application performance.

- [S1: React useEffect](https://react.dev/reference/react/useEffect): reactive dependencies, cleanup, request races and Strict Mode.
- [S2: EF Core efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying): projections, indexes, bounded reads and pagination trade-offs.
- [S3: Microsoft common web architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures): inward dependencies and testable application cores.
- [S4: EF Core testing strategy](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy): SQLite differences and real-provider testing.
- [S5: EF Core client/server evaluation](https://learn.microsoft.com/en-us/ef/core/querying/client-eval): final projection versus translated predicates.
- [S6: SQL Server provider function mappings](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/functions): provider-supported translation; supports avoiding assumptions about arbitrary C# methods.
- [S7: EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency): concurrency tokens, isolation and short transaction trade-offs.
- [S8: PasswordHasher API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1?view=aspnetcore-10.0): vetted hashing/verifying facility.
- [S9: ASP.NET Core JWT bearer configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0): token validation, policies and secure token handling. Assessment-local token issuance is a scoped fixture, not a production identity-provider recommendation.
- [S10: OWASP HTML5 security](https://cheatsheetseries.owasp.org/cheatsheets/HTML5_Security_Cheat_Sheet.html): JavaScript-accessible browser storage and XSS risk.
- [S11: Docker build cache](https://docs.docker.com/build/cache/optimize/): dependency layers before frequently changing source.
- [S12: .NET non-root app user](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/8.0/app-user): available runtime image user and explicit selection.
- [S13: Compose startup order](https://docs.docker.com/compose/how-tos/startup-order/): service_healthy dependencies.
- [S14: EF migrations deployment](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying): reviewing/testing migrations and deployment options.
- [S15: Testing Library fake timers](https://testing-library.com/docs/using-fake-timers/): controlled timer tests and cleanup.
- [S16: EF Core connection resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency): explicit transaction retry units and uncertain commit outcomes.
- [S17: EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions): atomic SaveChanges and when explicit transactions are justified.
- [S18: MDN navigator.webdriver](https://developer.mozilla.org/en-US/docs/Web/API/Navigator/webdriver): cooperative WebDriver indication, not authenticated AI identity.
- [S19: Cloudflare bot scores](https://developers.cloudflare.com/bots/concepts/bot-score/): multi-signal bot scoring, not a human-identity assertion.
- [S20: OWASP authentication guidance](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html): generic failures and authentication security.
- [S21: OWASP logging guidance](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html): authentication events and excluding secrets from logs.
- [S22: Docker Desktop Windows setup](https://docs.docker.com/desktop/setup/install/windows-install/): WSL/Linux container prerequisites and per-user installation paths.
- [S23: .NET Windows setup](https://learn.microsoft.com/en-us/dotnet/core/install/windows): SDK installation versus runtime-only installation.
