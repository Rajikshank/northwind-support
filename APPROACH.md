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

Tests include request regressions, triage choices and boundaries, real relational queries, actual HTTP/JWT middleware, reassignment/history snapshots, atomic failure rollback and stale updates. Live SQL Server/browser checks supplement SQLite; they do not replace SQL Server migration and precision checks. Results, reproduction commands and the authentication diagram are in the README. Generated reports stay in ignored artifacts; only three selected screenshots are retained.

The reference and last-slot races were reproduced against SQL Server before fixing them. A sequence handles allocation; a short serializable transaction covers workload reads and writes. Only confirmed deadlocks before commit retry, with tracked state cleared before rerunning the decision. A lost-commit-acknowledgement test proves a successful write is not blindly replayed. This intentionally replaces blanket provider retries; see [EF transaction/retry guidance](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency).

That choice favours correctness over high throughput for this small desk. Manual assignment remains the supplied override; production still needs controlled migrations and a fuller session lifecycle. No production completeness or selection outcome is promised.

## AI and time disclosure

Codex assisted code, tests, debugging, Docker operation and documentation. The request-regression-first workflow worked. Review changed duplicated SLA logic and an entity-shaped response, and found a singular/plural schema mismatch. A generated health-call compilation error was caught by the build; a broad browser fixture pattern was narrowed after it intercepted source modules. Final evidence uses the actual API instead of retained mock screenshots.

Implementation and verification spanned 19–20 October 2026. The submission email records actual time per task, including research, implementation, review and study. Session dates are not a measure of independent hours, and the brief's effort estimates are not claimed as time spent.
