# Quality Gates

What must be true before code is merged or released, and what happens when it is not. Gates are run by
GitHub Actions and decided by people. **No gate calls an AI agent.**

## 1. Gates

| # | Gate | Runs on | Passes when | Evidence |
|---|---|---|---|---|
| G1 | Build | every PR | `dotnet build backend/backend.sln` has 0 errors; `npm run build` in `frontend-web` succeeds | CI log |
| G2 | Unit tests | every PR | all pass; none skipped without a ticket (G5) | test results (TRX) |
| G3 | API tests | every PR | all pass against real PostgreSQL; none skipped without a ticket | test results (TRX) |
| G4 | Risk coverage | every PR | every `Critical` and `High` risk in [RISK_REGISTER.md](RISK_REGISTER.md) is either named by at least one test through `[Trait("Risk", "R-xxx")]` or listed under *Accepted risks* with an unexpired date | script output |
| G5 | No silent skips | every PR | every `Skip = "…"` contains a ticket reference (`#123`) | script output |
| G6 | Secret scan | every PR | gitleaks finds nothing | gitleaks report |
| G7 | E2E | merge to `master`, nightly, PR label `e2e` | all scenarios pass, no retries | Reqnroll report, Playwright trace on failure |
| G8 | Review | every PR | a code owner approved; the PR template's test section is filled in | GitHub review |

G4 and G5 are small scripts in `scripts/` (phase 2). They read the register and the test source; they
do not judge test quality — that is G8.

**What does not gate:** coverage percentage, number of tests, test duration (tracked, not gated).

## 2. Pull request checklist

The PR template asks the author:

- Which risks does this change touch? (IDs)
- Which tests prove them? (names)
- Did any test get removed, skipped or loosened? Why?
- Does the change touch `[Authorize]`, `Policies.cs`, ownership checks or token validation? If yes, which
  security tests changed?

`qe-reviewer` can draft answers to these questions; the author confirms them.

## 3. Flaky tests

A test is **flaky** when it fails and passes on the same code.

1. **Do not re-run to green.** Record the failure with the flaky-test template (§6).
2. Reproduce: run the test alone and in its collection, 20 times. Use `qe-reviewer` to triage.
3. If the cause is not fixed within 2 working days, the test owner may **quarantine** it:
   `[Trait("Quarantine", "#123")]` plus `Skip = "Quarantined #123"`. The ticket names an owner.
4. A quarantined test covering a Critical risk blocks the merge (G4 still requires a passing test).
5. Quarantine is reviewed weekly; after 14 days the test is fixed or the risk is re-assessed.

Never acceptable as a fix: retries, longer timeouts without a measured cause, broader assertions,
deleting the test.

## 4. Failures on `master`

1. The person who merged last looks first; they may ask `qe-reviewer` to triage the failure output.
2. Classify: **product defect**, **test defect**, **environment**, or **unknown**.
3. Product defect → fix forward or revert; the failing test stays as it is.
4. Test defect → fix the test in a PR that explains why the old assertion was wrong.
5. Environment → fix the environment; the test is not changed.
6. Severity follows the affected risk. A Critical-risk failure on `master` stops merges until resolved.

## 5. Definition of done for a change

- G1–G6 and G8 pass; G7 passes on `master`.
- Risks touched by the change are in the register with a status; new Critical/High risks are `Covered`.
- Open questions the change answers are closed in the register.

## 6. Templates

### Finding

```markdown
**Finding:** <one sentence>
**Found by:** <person | qe-analyst | qe-reviewer | test name>
**Evidence:** <file:line, request/response, test output>
**Expected:** <what the docs or the matrix say>
**Actual:** <what happens>
**Proposed risk:** <existing R-xxx or new> — rating: unrated (set by test owner)
**Next step:** <test to add / question to answer>
```

### Flaky test

```markdown
**Test:** <fully qualified name>
**First seen:** <date, CI run link>
**Failure output:** <message and stack, trimmed>
**Reproduced:** <x of 20 runs alone / in collection>
**Suspected cause:** <ranked, with evidence>
**Owner / ticket:** <name, #123>
**Quarantined:** <yes/no, date>
```

### Incident (failure on `master` or in a deployed environment)

```markdown
**What failed:** <gate, test or behavior>
**Since:** <commit, date>
**Classification:** product defect | test defect | environment | unknown
**Affected risk:** <R-xxx and severity>
**Evidence:** <logs, traces, test output>
**Resolution:** <fix PR / revert / environment change>
**Follow-up:** <missing test, register update>
```

## 7. Production readiness

### Release gate

The system may hold real patient data only when **every Critical and High risk** in
[RISK_REGISTER.md §2.1](RISK_REGISTER.md#21-production-readiness) is `Covered` or `Closed` — proven by a test,
a CI check of the deployment configuration (level *Config*) or a rehearsed procedure (level *Ops*). Accepting
such a risk instead needs the test owner's written reason and an end date in RISK_REGISTER §5. Medium risks
in §2.1 follow the normal rule: covered when the related code or configuration changes.

### Proposed order

Waves are ordered by "public exposure today" first, then by what each wave unblocks. Waves 1, 2 and 4 need
no further decision (Q-007 is answered: synthetic data, production-like everything); wave 3 needs Q-006 and
Q-008.

| Wave | Goal | Risks | Needs |
|---|---|---|---|
| **1 — Clean the repository and the dev defaults** ✅ done 2026-11-15 | Nothing secret or dev-only is public or on by default | R-030 (legacy projects deleted, build output ignored; rotate the old password wherever it is used), R-027 (password grant off on `hospital-web`, no replacement test client), R-029 (sample data and Swagger only in `docker-compose.override.yml`), R-043 (password reset off until SMTP exists) | Q-009 answered: delete |
| **2 — Phase 2: tests and CI** | Every change is checked automatically | R-035 (API and unit test projects, GitHub Actions with gates G1–G6), R-012 (gitleaks), R-040 (pinned image tags, dependency and image scanning) | – |
| **3 — Production deployment** | A deployable, encrypted, least-privilege stack | R-025 (TLS everywhere, HSTS), R-026 (Keycloak `start` with a production realm, no sample users), R-033 (static frontend image behind a web server), R-034 (hostnames, issuer, CORS and redirect URIs from configuration), R-031 (separate database users, least privilege), R-032 (backups and a restore drill), R-036 (security headers, rate limiting), R-038 (migrations as a release step), R-039 (shared cache or short expiry) | Q-006, Q-008 |
| **4a — Real-life flows: data and API** | Every flow in [BUSINESS_FLOWS.md](BUSINESS_FLOWS.md) works end to end on realistic synthetic data, without leakage | R-054 (DTOs instead of entities), R-051 (time zones), R-049 (duplicate patients), R-044 (patient model and the deterministic data generator, BUSINESS_FLOWS §4), R-048 (concurrent booking), R-056 (record version check), R-053 (records only for completed appointments), R-050 (closing past appointments), R-055 (offboarded doctors' appointments), R-042 (no personal data in logs) | – |
| **4b — Real-life flows: frontend** | Each actor can do their flows in `frontend-web`, not only through Swagger | Screens for the flows marked "UI ❌" in BUSINESS_FLOWS (appointments, records and history, patient's own data, user linking, status correction, audit) | 4a |
| **4c — Health-data security** | Security as if the data were real | R-028 (MFA and password policy for staff), R-041 (append-only audit store, retention), R-045 (retention and export mechanisms), R-052 (shorter token lifetime or role re-check) | – |
| **5 — Operate** | Problems are seen and handled | R-046 (telemetry export, alerts, central logs including Keycloak events), R-047 (runbooks for deploy, rollback, secret rotation, restore), R-037 (paging) | Wave 3 |

Wave 1 is small enough to do before phase 2. Phase 2 (wave 2) is the foundation for the others: from then on
every fix in waves 3–5 lands with the test or configuration check that proves it.
