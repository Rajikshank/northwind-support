# Northwind Support Desk

Support ticket management with .NET 10, React 19/TypeScript and SQL Server 2022.

Tasks 1–3 are implemented and checked with automated relational/API tests and browser UI fixtures. Task 4's Docker configuration is implemented, but its image build and runtime verification are **not finished**: the local disk filled during image extraction, and further Docker work was deferred. SQL Server runtime verification is pending for the same reason. See [verification evidence](docs/EVIDENCE.md) and [approach](APPROACH.md).

## Run locally

Prerequisites: .NET 10 SDK (`global.json`), Node 22.22.2+ or 24.15+ (locked test dependencies require these patch levels), Git, and Docker Desktop with Linux containers. The original `.nvmrc` selects Node 22. No separate SQL Server installation is needed. Use a fresh PowerShell terminal after installing tools.

```powershell
# Copy only if .env does not already exist.
Copy-Item .env.example .env
npm ci
dotnet tool restore
npm run db:up
# Wait for the SQL Server service to be healthy: docker compose ps
$env:Jwt__SigningKey = 'LocalFixture-Only-Change-Before-Production-2026'
npm run api
```

Run `npm run web` in another terminal. Web: http://localhost:5173. API: https://localhost:7043 or http://localhost:5043. Swagger is open in Development at `/swagger`; `GET /health` anonymously checks database connectivity. Use `dotnet dev-certs https --trust` for the local HTTPS profile, or point the Vite proxy at the HTTP API.

`.env` supplies Compose interpolation; it is **not** automatically exported into `dotnet run`. Set process environment values explicitly as above. Development startup applies migrations, seeds demo data, and fills missing demo password hashes and SLA risk timestamps. Existing hashes are not reset.

**Local login:** `alex.turner@northwind-support.example` / `LocalDemo-Only!2026`. Other active seeded agents share this obvious development-only password. Inactive Yuki cannot sign in. Never use fixture keys/passwords in a deployed environment.

## Implemented behaviour and decisions

**DEFECT-117:** the request effect read all filters but subscribed only to search and pagination. A status change on page 1 therefore sent no request until a watched value changed. The hook now subscribes to the complete committed query, debounces search alone for 300 ms, and aborts/discards obsolete responses. Search and its page reset commit together. Dropdowns use the last settled search immediately; a pending search commits later with the latest filters. Reset cancels pending search. The real URL regression failed before the fix (expected one request, received zero) and passes afterward. The defect and server filtering are separate commits.

**Filtering:** `TicketQueries` composes exact status/priority/category/customer/agent predicates, unassigned-only and trimmed title/reference/customer search with AND, before both count and paging. Text fields inside search combine with OR. Whitespace search is ignored. Conflicting assigned-agent/unassigned-only filters yield zero rows. Reads are untracked, narrowly projected and stably ordered by the selected field then Id. Add a filter to `ApplyFilters`, the typed request and serializer; no hook dependency change is needed. Relational tests inspect executed CASE/WHERE/count SQL; SQL Server logs/plans still need verification. Substring search can scan; no production-scale performance claim is made.

**Architecture:** retain the existing layered monolith and feature handlers. Shared SLA and assignment policies keep rules out of controllers/components; triage orchestration loads category/customer facts through a read seam and uses those policies for both create and escalate. Existing repositories/unit of work commit mutations; query composition handles reads. These are the three useful patterns here. A mediator framework, generic repository framework, event bus and microservices would add machinery without helping the assessment.

Priority defaults to Medium; category flags can force Critical. SLA windows are configurable (Critical 4h, High 8h, Medium 24h, Low 72h), with Premium multiplier 0.5 and 1h minimum. Automatic assignment uses active, eligible agents strictly below capacity, fewest open tickets, then lowest Id. Technical/General do not require specialists. No eligible owner is a normal unassigned 201 result with an explanation. Existing manual assignment remains an explicit specialization override.

Escalation raises priority once, recalculates the deadline from escalation time, retains an eligible owner or selects another, and records before/after history in the same atomic SaveChanges. Critical/Resolved/Closed return descriptive 409 responses; invalid reason is 400; unknown ticket is 404. An owner exactly at capacity is ineligible even when already holding this ticket, following the rule's literal “strictly below” wording.

**SLA timing:** status is always derived. Persisting a risk-boundary timestamp alongside the deadline records the promised window after escalation without storing a stale status. Configuration changes apply to future create/escalate operations. Existing timestamps are reconstructed from current priority/tier/settings because the original schema did not retain the window. No due date means No SLA; resolution on/before due is Met; unresolved exactly due is AtRisk, one tick later Breached. The query has one SQL expression shared by filter/projection; tests compare it with the pure evaluator. Timing fields use datetime2(7).

**Authentication:** one Agent is one account, so a PasswordHash column is sufficient. ASP.NET Identity PasswordHasher supplies salted hashes; failures are generic for unknown/wrong/inactive accounts. JWTs last 30 minutes, with signature/issuer/audience/lifetime validation and 15-second clock skew. The signing key is runtime configuration and validated at startup. A fallback policy protects all API endpoints; login/health are explicit exceptions and Swagger is open in Development. Escalation records `agent:<subject-id>` from the token, ignoring a supplied actor. No password hashes are returned.

The token stays in memory, attached centrally by `api/client.ts`. Reloading requires sign-in; this avoids persistent token storage but does not eliminate XSS risk. Protected routes preserve an internal return path; logout or any API 401 clears the session. Previous successful sign-in is shown after authentication; rejected logins do not update it. An atomic compare-and-swap updates the timestamp with bounded retries. Tokens already issued remain valid until expiry; logout is client-side.

## Database and container

Committed migrations add escalation history, SLA risk timing/precision and agent credentials/login time. `(TicketId, EscalatedAtUtc DESC, Id DESC)` supports newest-first history; restricted foreign keys preserve references. Existing ticket indexes are retained. No speculative search index is added.

```powershell
# Once Docker storage is available:
docker compose up --build
# In the web terminal:
$env:VITE_API_PROXY_TARGET = 'http://localhost:5080'
npm run web
```

Compose supplies SQL connectivity via service DNS and starts the API after SQL Server is healthy. The API binds localhost:5080 to container HTTP 8080. Runtime variables are documented in `.env.example`, including JWT and SLA overrides. The root-context Dockerfile caches restore before source copy and publishes into an ASP.NET runtime stage with an explicit non-root user. SDK, source and Development settings are omitted from publish/image. Normal runtime images retain SQL globalization support and `whoami`; trimming/AOT were rejected because EF/validation depend on reflection. Image size, cache reuse, non-root execution and clean-volume startup remain **unverified**, not estimated.

For production, run reviewed migration scripts/bundles as a controlled deployment step with appropriate privileges, disable demo seeding, and give the running API a less privileged database account. Do not use the Development environment or fixture credentials.

## Test and verify

```powershell
npm test
npm run build
npm run lint
npm run typecheck
npm run format:check
npm audit
```

The backend suite uses xUnit/Moq, the supplied FixedClock/TicketBuilder conventions, real SQLite queries and real ASP.NET middleware/controllers/JWT integration. Frontend tests cover request timing/races, validation, conflicts, five SLA states and session behaviour. [Evidence](docs/EVIDENCE.md) records actual results and [browser screenshots](docs/evidence/) are explicitly based on HTTP fixtures, not a SQL Server end-to-end run. The optional Playwright fixture smoke script is `scripts/browser-smoke.mjs`; run the web server on 127.0.0.1:5173 and provide Playwright separately to execute it. It adds no production dependency.

**AI use:** Codex helped inspect the requirements, implement and review code, write tests and documentation, and operate verification tools. The successful workflow was a real request regression that failed first, then a focused fix. The search-only debounce approach was retained; brittle per-filter effect dependencies were rejected. Review replaced a duplicated SLA projection with one query expression and replaced a persistence entity response with a DTO. An AI-generated health-check call referenced the wrong extension class; the build caught it and it was corrected. Generated work was checked with builds, test suites, migration/model checks and browser screenshots. These notes describe observed work; the candidate must still review and be able to explain every submitted change.

## Known limitations

Docker runtime and SQL Server-specific migration/query/seed checks remain pending. SQLite is not proof of SQL Server collation or provider behaviour. Concurrent ticket creation can race in the inherited max-id reference generator, and automatic assignments can compete for the last capacity slot; no multi-user load guarantee is claimed. General transaction/retry machinery was deferred to protect the required delivery scope. SLA labels are refreshed on fetch rather than by a background timer. No registration/reset, MFA, SSO, roles, refresh tokens, lockout/rate limiting, notifications, CI, caching, summary report or redesign was added, as requested. AI/browser detection is not a security boundary; a cooperative sign-in handoff guide is a future UX option rather than a claim of human-only authentication.
