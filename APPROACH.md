# Implementation approach

The assessment calls for extending a working application, so I kept its layers, routes and UI conventions. The work covers the four required tasks plus previous-successful-login tracking. The optional report and explicitly excluded authentication/deployment features were left out.

## Key choices

- Fix the reactive dependency at its source. A regression watched the actual request: changing status originally sent zero requests. Search input and committed query are separate so only typing is debounced.
- Compose database filters before both count and paging. Keep SLA CASE logic in one projection reused by filtering, and compare its behaviour with the pure evaluator at exact time boundaries.
- Share pure SLA/assignment policies through one triage service. Category flags define specialist/critical behaviour; configuration defines timing. Snapshot the risk boundary rather than persist a time-dependent status.
- Save escalation and history atomically. A forward rename migration corrects the required table name without rewriting applied history. An application-managed concurrency token rejects stale writes; relational tests cover races with another escalation or resolution. This follows [EF Core optimistic concurrency guidance](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).
- Add credentials to Agent because accounts have the same lifecycle as the supplied agents. Use Identity PasswordHasher and centrally enforced, short-lived JWTs. Memory-only storage is simple and intentionally requires sign-in after refresh. Browser bot detection is not authentication.
- Keep the final Docker stage conventional and non-root. Exclude design-time dependencies and secrets, retain globalization, and use runtime configuration. Avoid trimming/AOT without a compatibility need and evidence.

## Verification and trade-offs

Tests include request regressions, triage choices and boundaries, real relational queries, actual HTTP/JWT middleware, reassignment/history snapshots, atomic failure rollback and stale updates. Live SQL Server/browser checks supplement SQLite; they do not replace SQL Server migration and precision checks. Results and reproduction commands live in one [evidence file](docs/EVIDENCE.md).

The inherited ticket-reference allocator and last-slot assignment race remain documented limitations rather than an untested concurrency framework. Production would need database-backed allocation, stronger capacity coordination, controlled migrations and a fuller session lifecycle. No claim of production completeness or a guaranteed assessment score is made.

## AI and time disclosure

Codex assisted code, tests, debugging, Docker operation and documentation. The request-regression-first workflow worked. Review changed duplicated SLA logic and an entity-shaped response, and found a singular/plural schema mismatch. A generated health-call compilation error was caught by the build; a broad browser fixture pattern was narrowed after it intercepted source modules. Final evidence uses the actual API instead of retained mock screenshots.

Implementation and verification spanned 19–20 October 2026. These assisted sessions do not establish the candidate's independent hours. Before submission, add an honest per-task breakdown including research, review and study; do not substitute the employer's estimates. No email submission is sent automatically. Current work remains local pending candidate approval.
