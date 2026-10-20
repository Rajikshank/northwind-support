# Northwind Support Desk

.NET 10 API, React 19/TypeScript and SQL Server 2022. The four assessment tasks are implemented locally: filter repair, automatic triage/escalation/SLA, agent authentication, and a Docker API. Verification details are in [EVIDENCE.md](docs/EVIDENCE.md); design and AI-use notes are in [APPROACH.md](APPROACH.md).

## Run locally

Install Git, Docker Desktop with Linux containers, .NET 10 SDK and Node 22.22.2+ or 24.15+ (the locked test dependencies require these patch levels). The repository's `.nvmrc` selects Node 22.

```powershell
# Copy only if .env does not already exist.
Copy-Item .env.example .env
npm ci
docker compose up --build
```

In another PowerShell terminal:

```powershell
$env:VITE_API_PROXY_TARGET = 'http://localhost:5080'
npm run web
```

Open http://localhost:5173. API: http://localhost:5080; Development Swagger: http://localhost:5080/swagger. Anonymous `GET /health` checks database reachability. Development startup applies migrations and seeds 40 demo tickets, six agents, six customers and five categories. Subsequent starts retain data and fill missing fixture hashes/SLA boundaries without resetting existing passwords.

**Development login:** `alex.turner@northwind-support.example` / `LocalDemo-Only!2026`. Other active seeded agents share this obvious local fixture password; inactive Yuki cannot sign in. Fixture passwords and keys must never be used in production.

To run the API outside Docker, start only SQL with `docker compose up -d sqlserver`, then:

```powershell
dotnet tool restore
$env:Jwt__SigningKey = 'LocalFixture-Only-Change-Before-Production-2026'
npm run api
```

The local launch profile exposes HTTP 5043 / HTTPS 7043. Point `VITE_API_PROXY_TARGET` at `http://localhost:5043` for HTTP development. Compose `.env` values are not automatically exported into `dotnet run`; set overrides in the process environment.

## Decisions and behaviour

**Defect and filtering.** DEFECT-117 came from an effect that read all filters but subscribed only to search and pagination. A status change therefore sent no request. The hook now observes the complete committed query; dropdowns request immediately, search alone waits 300 ms, and obsolete responses are cancelled/discarded. The request regression failed on the original code (one fetch expected, zero received). The defect fix and backend filtering have separate commits.

`TicketQueries.ApplyFilters` adds exact matches, unassigned-only and trimmed search before SQL count/paging. Search is OR across title/reference/customer name, AND with other filters. Blank search is ignored. Reads are untracked, projected and ordered with an Id tie-break. The SLA expression is shared by filter and projection; relational tests inspect CASE/WHERE/count execution, and live SQL Server checks verify results. A further filter needs a typed field, serializer and predicate; no effect dependency change. Substring search can scan; no large-dataset performance claim is made.

**Architecture.** Keep the supplied layered monolith. Shared SLA/assignment policies keep business decisions out of HTTP/UI code. Application handlers orchestrate these policies for both create and escalate. Repositories and the unit of work commit mutations atomically; separate query projections serve reads. These three choices fit the existing design without a mediator framework, generic repository framework or event bus.

Priority defaults to Medium; category flags can force Critical. Configurable windows are Critical 4h, High 8h, Medium 24h and Low 72h; Premium multiplies by 0.5 with a 1h floor. Active eligible agents must be strictly below capacity; choose fewest open tickets, then lowest Id. Billing/Outage/Security require specialists through flags, not name checks. No eligible owner is a normal unassigned **201** with a reason. Existing manual assignment remains an explicit override of automatic specialization rules.

Escalation raises one level, recomputes from escalation time, keeps an eligible owner or reassigns, and appends before/after history. Critical/Resolved/Closed return descriptive **409**, invalid reason **400**, unknown ticket **404**. At-capacity owners are ineligible, following the rule's literal “strictly below” wording. A concurrency token rejects stale ticket writes with 409; failed writes roll back their history too.

**SLA and database.** Status is derived using an injectable clock. The persisted deadline and risk boundary snapshot the promised window, including after escalation; configuration changes apply to future triage. Historical missing boundaries are reconstructed from current priority/tier/settings. No deadline means No SLA; resolution on/before due is Met; unresolved exactly due is At risk and one tick later Breached. Timestamps use datetime2(7).

Migrations add `TicketEscalations`, credential/login fields, risk timing and a ticket concurrency token. The history index `(TicketId, EscalatedAtUtc DESC, Id DESC)` supports newest-first reads; restricted foreign keys preserve references. Existing ticket indexes remain. A forward migration renames the earlier singular history table without discarding data.

**Authentication.** One Agent is one account, so credentials live on Agent. Identity PasswordHasher supplies salted hashes. Unknown/wrong/inactive logins have the same 401 message. JWTs last 30 minutes with signature, issuer, audience and lifetime validation, plus 15 seconds of clock skew. Runtime configuration supplies the signing key. A fallback policy protects every API endpoint; login/health are explicit exceptions, Swagger is open in Development. Escalation takes `agent:<subject-id>` from the token.

The central client keeps the token in memory and attaches it to requests. Refresh requires sign-in; this avoids persistent token storage but does not eliminate XSS. Protected routes preserve a validated internal destination. Logout/401 clears the session. Previous successful login is shown after sign-in; failed attempts leave it unchanged, and a bounded compare-and-swap handles overlapping timestamp updates.

## Docker and verification

The root-context Dockerfile restores project metadata before copying source, then publishes into a non-root ASP.NET runtime. EF design-time tooling, SDK, sources, `.env` and Development settings are excluded. Runtime variables supply connectivity, JWT and SLA overrides; see `.env.example`. SQL Server uses the tested CU27 Ubuntu 22.04 tag and service DNS; API startup waits for SQL health. SQL globalization support is retained; trimming/AOT would risk EF/validation reflection.

The measured API image is approximately **455 MB**, about **65% smaller** than the measured 1.304 GB SDK image. `/app` is 82 MB and `whoami` returns `app`. Fresh-volume migration/seed and source-edit restore caching are checked separately from normal API/browser tests; current figures and results are recorded in the evidence file.

```powershell
npm test
npm run build
npm run lint
npm run typecheck
npm run format:check
npm audit
```

Backend tests use xUnit/Moq, FixedClock/TicketBuilder, relational SQLite and actual HTTP/JWT middleware. Frontend tests use Vitest/RTL. Repeat live checks with `pwsh -File scripts/verify-live.ps1`; with Vite running and Playwright available, run `node scripts/browser-live.mjs`. These scripts create development tickets and change workloads. Screenshots and concise JSON results are in `docs/evidence/`.

In production, apply reviewed migration scripts/bundles as a separate deployment step, disable demo seeding, use a less privileged database account and replace all fixture credentials. A deployment should not give every API process schema-changing privileges.

## Known limitations and AI use

Inherited max-id reference allocation can race during simultaneous creates; assignment can also compete for the last capacity slot. The ticket concurrency guard does not solve those cross-ticket races. No load/SQL execution-plan benchmark is claimed. SLA labels refresh on fetch. Issued JWTs remain valid until expiry after logout or later account deactivation; /me checks activity, other endpoints validate the token. Registration/reset, MFA, roles, refresh tokens and rate limiting remain outside the requested scope. “Agent” means a support employee; headless-browser detection cannot establish identity and was not added. The optional SLA report is not implemented.

Codex assisted implementation, tests, debugging and documentation. A real request regression was written before its fix. Search-only debounce was retained; per-filter dependency wiring was rejected. Review consolidated SLA query logic, replaced an entity response with a DTO, corrected history-table naming and added stale-write tests. An AI-generated health extension call initially failed compilation and was corrected. Builds, relational/API tests, browser runs and visual inspection checked generated work. The candidate must understand the changes and record their own honest time breakdown before submission.
