# Verification evidence

Verified locally on 20 October 2026. These are observed results, not a claim of production readiness. No code was published during this review.

## Required behaviour

| Assessment area | Evidence and result |
| --- | --- |
| DEFECT-117 | Request regression failed on original code: expected one fetch, received zero. After repair, dropdowns request immediately, search debounces, reset cancels pending search and obsolete responses are ignored. |
| FL-1–6 | Exact/combined filters, trimmed customer/reference search, unassigned, count/paging and SQL-side SLA filter pass relational and live SQL Server checks. Original exact-High regression returned Critical before its fix. |
| BR-1–6 | All priority/tier windows, floor, forced Critical, active/specialist/capacity/tie rules and normal unassigned creation pass. |
| BR-7 | One-step escalation, from-now deadline, kept/replaced/null owner, snapshots, newest-first history, token actor and invalid/Critical/Resolved/Closed guards pass. Injected history failure rolls back ticket changes. |
| BR-8 | Fixed-clock tests cover risk threshold, exactly due, one tick late and resolved timing. Filter and projection agree for every SLA state. SQL Server reports datetime2(7) for all five ticket timestamps. |
| Task 2 UI | Validation prevents invalid calls, 409 preserves the ticket, five SLA labels are distinguishable; actual browser create/escalate/history/mobile checks pass. |
| AU-1–13 | Salted hashing, generic wrong/unknown/inactive failure, valid/invalid/expired tokens, central protection, actor spoof rejection, redirect/header/401/logout and previous successful login pass. |
| DK-1–8 | Actual multi-stage build, root context/ignore rules, runtime configuration, app user, port 8080→5080, SQL dependency, migrations/seed, anonymous health and Vite proxy verified. |

## Tests and checks

- **55 backend tests + 5 architecture/model tests + 27 frontend tests = 87 passing.** No skips. Commands: `dotnet test`; `npm run test --workspace apps/web`.
- `npm run build`: passes, zero backend warnings/errors. `npm run lint`, `npm run typecheck` and `npm run format:check` pass.
- Stale-write regressions initially failed because no conflict was raised. After the concurrency guard, an old escalation cannot overwrite another escalation/resolution or append a misleading history row. The required table-name regression also failed before the mapping/migration fix.
- [Live API report](evidence/live-api.json): **40 checks** against Docker API/SQL Server. Repeat: `pwsh -File scripts/verify-live.ps1`.
- [Live browser report](evidence/live-browser.json): **8 checks**, actual API through Vite, no mocks. Repeat with Vite and Playwright available: `node scripts/browser-live.mjs`. Screenshots were visually inspected: [login](evidence/live-login.png), [filter](evidence/live-filter.png), [triage](evidence/live-triage.png), [history](evidence/live-escalation.png), [390px mobile](evidence/live-mobile.png).
- The earlier dependency audit reported zero npm advisories and no vulnerable NuGet packages in all six projects. Dependency versions were unchanged during this review.

## SQL Server and Docker

[Runtime record](evidence/docker-runtime.json) contains the measured results. The API image is **454,865,964 bytes (455 MB)** versus the previously measured matching SDK **1,303,849,917 bytes**: approximately 65% smaller. `/app` is 82 MB. Runtime `whoami` is `app`; SDK, .env and Development settings are absent.

The original run started from an empty volume. This review additionally applied all **four migrations** to a separate empty SQL Server database, seeded **40 tickets**, and passed the same 40 API checks there. Upgrading the existing database preserved **46 tickets and 4 history rows**. The forward migration renames the history table to `TicketEscalations`. The reviewed [idempotent script](evidence/migrations.sql) includes every migration. Collation: SQL_Latin1_General_CP1_CI_AS.

Changing API source reused the Docker restore layer and reran publish. An isolated temporary API pointed at an unreachable database returned **503 in 5.43 seconds**; the normal API remained healthy at 200. This safely completes the health-failure check that previously required an interrupted database. Temporary containers/database were removed. Cleanup reclaimed **1.828 GB** of unused build cache; only the required API/SQL images and existing database volume remain.

## Limits

The live scripts create tickets and change workloads; repeated runs can legitimately leave tickets unassigned. Use isolated development data when demonstrating original seed capacity. SQLite is not proof of SQL Server load behaviour; no execution-plan/load benchmark or parallel SQL Server stress claim is made. The inherited reference-allocation and cross-ticket capacity races remain documented. The optional SLA report is excluded. The candidate still needs to review the implementation and provide an honest time breakdown before submission.
