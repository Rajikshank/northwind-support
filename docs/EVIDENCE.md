# Verification evidence

Observed on 19 October 2026. The test names below link to runnable checks rather than screenshots of a green terminal. SQL Server and Docker runtime checks are explicitly pending.

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
| EF idempotent SQL Server migration script | Generated successfully: [migrations.sql](evidence/migrations.sql); not applied to SQL Server |
| Chromium browser fixture smoke | PASS: login redirect/login, filter request, escalation/history refresh, triage feedback, 390px overflow assertion, no page errors |
| Visual inspection | Login, escalation form/detail and mobile layout inspected; mobile account header wrapping corrected and recaptured |
| Docker build/start/image size/cache/user | UNVERIFIED: disk filled during extraction; further Docker work deferred by user |
| SQL Server clean migration/seed/query plan/collation | PENDING: requires available container storage |

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
| DK-1–8 | Dockerfile/ignore/Compose/health implemented and source-reviewed. Anonymous health passes in HTTP integration host. Actual container acceptance checks pending. |

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
