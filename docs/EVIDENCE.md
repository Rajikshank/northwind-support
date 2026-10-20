# Verification evidence

Updated 20 October 2026. The test names below link to runnable checks. Live API/browser reports now use the final Docker API and SQL Server; fixture screenshots remain labelled separately.

## Final available checks

| Check | Observed result |
| --- | --- |
| `npm test` | PASS: 49 backend tests + 4 architecture/model tests + 27 frontend tests (80 total) |
| Frontend suite under compatible Node 24.19.0 | PASS: 27 tests |
| `npm run build` | PASS: API and production frontend; no backend warnings/errors |
| `npm run lint` / `npm run typecheck` | PASS |
| `npm run format:check` | PASS: no formatting changes required |
| `npm audit` | Zero known vulnerabilities from configured registry |
| `dotnet list package --vulnerable --include-transitive` | No vulnerable packages reported in all six projects |
| EF idempotent SQL Server migration script | Generated successfully: [migrations.sql](evidence/migrations.sql); all three migrations applied by application startup on a fresh SQL Server volume |
| Chromium browser fixture smoke | PASS: login redirect/login, filter request, escalation/history refresh, triage feedback, 390px overflow assertion, no page errors |
| Visual inspection | Login, escalation form/detail and mobile layout inspected; mobile account header wrapping corrected and recaptured |
| Docker build/start/image size/cache/user | PASS: 454,857,234 bytes; app user; no SDK/Development settings; restore CACHED. [Runtime record](evidence/docker-runtime.json) |
| SQL Server clean migration/seed/collation | PASS: 40 seeded tickets; three migrations; SQL_Latin1_General_CP1_CI_AS. Live filtering/count checks passed; execution-plan benchmarking not performed |

## Red-before / green-after evidence

- Original backend baseline: 26 unit tests and 4 architecture tests passed. Original frontend: 2 tests passed.
- `useTicketList.requests.test.ts`: original code failed the status-only request assertion, **expected one fetch, received zero**. It passes after the effect observes the full committed query. Search debounce remained covered.
- `TicketQueryTests.FiltersBeforeCountingAndPaging_WithExactPriorityAndCustomerSearch`: original code returned **Critical** for a **High** filter. It passes after predicates are composed before count and paging.
- The new SLA policy test initially could not compile because the policy did not exist, then passed after implementation. Later tests were added incrementally; not every test was written before production code, and no blanket TDD claim is made.

## Requirement coverage

| Requirement | Runnable evidence |
| --- | --- |
| DEFECT-117: dropdowns immediate; search debounced; SLA extension | `apps/web/src/features/tickets/hooks/useTicketList.requests.test.ts`, `useTicketList.test.ts`: real URL assertions for each filter, page/search timing, reset and obsolete response |
| FL-1–6: exact/combined/search/unassigned/count/paging | `TicketQueryTests`: High/Low, customer-name and reference search, blank search, contradictory assignment filters, status/customer/category exclusion and filtered count |
| BR-1–6: shared triage rules | `TriagePolicyTests`, `TriageTests`: priority/tier windows, floor, forced Critical, workload eligibility, specialists, limits, inactive, deterministic ties and no eligible candidate |
| BR-7: escalation/history/status guards | `TriageTests`, `DeskApiTests.CreateEscalateAndHistoryUseTheAuthenticatedActor`: step/from-now timing, owner retained, snapshots, required reason, Critical rejection, unknown history and normal unassigned 201 |
| Atomic history and ticket update | `DeskApiTests.FailedHistoryInsertRollsBackTheTicketChange`: injected SQLite trigger failure returns 500, original priority/deadline remain, history stays empty |
| BR-8: SQL status/filter parity | `TriagePolicyTests.SlaBoundariesAreExact` and `TicketQueryTests.SqlSlaFilterAndProjectionAgreeAtEveryBoundary`: exact threshold/due/tick boundaries, every status, filtered counts and executed SQL containing CASE/WHERE/COUNT |
| Task 2 frontend | `EscalationPanel.test.tsx`: short reason never calls API, 409 does not update ticket, all five SLA labels; browser script confirms success refresh and create feedback |
| AU-1–5: password/login/me | `AuthenticationTests`, `DeskApiTests`: salted password verification, correct/wrong/unknown/inactive login, generic messages and successful /me |
| AU-6–8: structural protection and actor | `DeskApiTests`: anonymous reads/writes rejected, valid/invalid/expired bearer tokens, protected endpoint families, spoofed body actor ignored |
| AU-9–13: session/client/routes | `session.test.tsx`: anonymous redirect, central header and 401 reset, safe same-origin return path; browser checks login and signed-in shell |
| Additional previous-login requirement | `AuthenticationTests.LoginChangesTimestampOnlyOnSuccessAndReturnsPreviousLogin`: first/next success, previous timestamp, rejected login unchanged |
| Migrations and boundaries | Existing `PersistenceModelTests` and `LayerDependencyTests` pass; SQL Server script generated |
| DK-1–8 | Dockerfile/ignore/Compose verified with actual build, fresh-volume startup, cached rebuild, runtime file/user inspection and anonymous health 200. Outage retest after timeout correction blocked by execution policy. |

SQLite tests use a relational database, not EF InMemory. HTTP integration uses the actual application composition root, controllers, validation, error middleware and JWT middleware, replacing only the database provider with SQLite. It is not proof of SQL Server-specific behaviour. Concurrent capacity selection/reference allocation and concurrent timestamp stress are not verified and are not claimed as load-safe.

## Browser evidence

The [smoke script](../scripts/browser-smoke.mjs) drives the real frontend with HTTP fixtures. It does not connect to a running SQL Server API. Screenshots contain only fixture data:

- [Login](evidence/login-desktop.png)
- [Ticket list](evidence/tickets-desktop.png)
- [Escalation form](evidence/escalation-form-desktop.png)
- [Escalation/history](evidence/escalation-desktop.png)
- [Mobile detail](evidence/escalation-mobile.png)
- [Unassigned triage result](evidence/triage-result-desktop.png)

Browser setup failures were corrected before the successful run: the first dev-server invocation passed an unintended root argument, and the first fixture glob also matched source modules. The final fixture prefix is restricted to the actual API URL. A mobile header that technically fit the screen still looked squeezed; visual review prompted wrapping rather than relying only on an overflow assertion.

## Dependency/environment observations

The initial npm lockfile contained vulnerable source-map-js 1.2.1; a targeted lockfile update resolved the advisory without upgrading the app framework. Initial .NET restores reported vulnerable transitive Pkcs 9.0.11. The central pin is now 10.0.13, based on Microsoft's published patched version, and the final package audit is clean. The integration-test host required Logging.Abstractions 10.0.13; its central version was aligned.

The shell's system Node 24.13.1 is below some locked dependencies' supported patch level. Tests also passed using an available compatible Node 24.19.0 runtime. .NET 10.0.402 was present but missing from the initial shell PATH. README and package engines now state compatible Node versions. No machine-specific runtime path is part of the application or public run instructions.

## Deferred completion checklist

After freeing storage: build/start with a disposable empty SQL volume; apply migrations and verify seeded logins/data; inspect actual filtered count/page SQL and SLA boundaries on SQL Server; check database-unavailable health; record restore-layer cache reuse after a source edit; measure image versus matching SDK size; run `whoami`; verify browser via the container proxy. Do not run volume deletion against an existing database just to produce evidence. Until these pass, Task 4 and full SQL Server runtime verification are unfinished.

## Resumption check

Later on 19 October, C: had about 8.5 GB free. Docker Desktop still failed to start: its backend reported that the temporary `Docker/run/dockerInference` socket could not be removed/accessed. A normal startup retry and termination of only the docker-desktop WSL distribution did not restore the engine. Automated removal of the zero-byte runtime socket was blocked by tool policy, so the user was given the precise manual cleanup step. No images, volumes or database files were deleted.

`scripts/verify-live.ps1` now provides repeatable live SQL-backed API checks and a credential-free JSON report. `scripts/browser-live.mjs` provides a real browser/API workflow through the Vite proxy. They were initially prepared without a running backend; both subsequently passed against SQL Server, as recorded below.

## Live SQL Server and container run — 20 October

- [Live API report](evidence/live-api.json): 40 passing checks using the actual JWT middleware, controllers and SQL Server. Includes generic failed login, inactive login, protected endpoints, previous login, exact/combined/search filters, count/paging, triage, escalation guards and authenticated history actor.
- [Live browser report](evidence/live-browser.json): 8 passing checks through Vite's API proxy, with no HTTP mocks. Covers login redirect, status request, create/triage, escalation/history refresh, 390px overflow, logout and page errors. Scripts create development test tickets.
- Live screenshots: [login](evidence/live-login.png), [filter](evidence/live-filter.png), [triage](evidence/live-triage.png), [escalation](evidence/live-escalation.png), [mobile](evidence/live-mobile.png). Desktop/mobile escalation screenshots were visually inspected.
- SQL Server reported migrations `20260926183612_InitialCreate`, `20261019132018_TicketEscalationAndSla`, `20261019132532_AgentCredentials`. A restart retained data and did not duplicate seed records.
- A source-only cache experiment reused restore and reran publish; a subsequent final-image rebuild reported restore and publish CACHED. Unused build cache was then pruned; future cold builds need to download SDK layers again.
- The first SQL image had a corrupt archive after disk exhaustion. The tested CU27 image starts successfully. This is environment recovery evidence, not an application test failure.
- A database-down health probe returned 503 after 119.88 seconds because connection retries were slow. Registration now bounds the check to five seconds. Automated tests and normal live health passed, but automatic approval review rejected the final temporary SQL shutdown with “blocked by policy”; outage timing and recovery after this change remain unverified.
- Rechecked after the timeout change: backend 49 + architecture 4 tests, frontend 27 tests, production build, lint and typecheck passed. Format verification identified mixed line endings in Program.cs; normalized to LF and reran the check.
