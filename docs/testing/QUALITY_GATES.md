# Quality Gates

What must be true before code is merged or released, and what happens when it is not. Gates are run by
GitHub Actions and decided by people. **No gate calls an AI agent.**

## 1. Gates

| # | Gate | Runs on | Passes when | Evidence |
|---|---|---|---|---|
| G1 | Build | every PR | `dotnet build backend/backend.sln` has 0 errors; `npm run build` in `frontend-web` succeeds; no package with a known high or critical vulnerability: NuGet audit at restore for every .NET project, including transitive packages (`Directory.Build.props`), and `npm audit --omit=dev` for what ships to the browser. Build-tool (dev) vulnerabilities are reported as a warning, not gated. A vulnerable package is fixed by pinning a patched version, never by silencing the warning | CI log |
| G2 | Unit tests | every PR | all pass; none skipped without a ticket (G5) | test results (TRX) |
| G3 | API tests | every PR | all pass against real PostgreSQL; none skipped without a ticket | test results (TRX) |
| G4 | Risk coverage | every PR | (a) every risk with status `Covered` in [RISK_REGISTER.md](RISK_REGISTER.md) is named by at least one test that is not skipped (`[Trait("Risk", "R-xxx")]`) **at the risk's level** — a Unit risk by a unit test, an API risk by an API test, an E2E risk by an E2E scenario. An E2E scenario (not run on every PR) cannot keep a Unit or API risk Covered; (b) every `Risk` trait names a risk that exists; (c) every path, `file:line` and code name cited in `backticks` in the agent and skill files, the testing documents, ARCHITECTURE and API_ENDPOINTS still exists (code names: agent and skill files only). The script also **lists** — without failing — every `Critical`/`High` risk of level API, Unit or CI that is not `Covered` yet | script output |
| G5 | No silent skips | every PR | every `Skip = "…"` names either an open risk (`Known gap R-054`) or a GitHub issue (`#123`) | script output |
| G6 | Secret scan | every PR | gitleaks finds nothing in the files, nor in any commit of the pull request (a secret added and deleted again inside one PR stays in the history once merged) | gitleaks report |
| G7 | E2E | every PR that touches sign-in (`backend/api/Auth/`, `backend/api/Program.cs`, `keycloak/`, `frontend-web/src/auth/`, the compose files, `e2e/`, the E2E workflow), PR label `e2e`, merge to `master`, nightly | all scenarios pass, no retries | Reqnroll report, Playwright trace on failure |
| G8 | Review | every PR | a code owner approved; the PR template's test section is filled in | GitHub review |

**Enforcement on GitHub.** The ruleset *Protect master* (repository settings → Rulesets) makes G1–G6
binding, not advisory: changes reach `master` only through a pull request, the four CI checks
(`Backend build and tests (G1, G2, G3)`, `Frontend build (G1)`, `Risk coverage and skips (G4, G5)`,
`Secret scan (G6)`) and the E2E check `End-to-end journeys (G7)` must pass, and force pushes and deleting
`master` are blocked. The bypass list is empty. Renaming a CI job changes its check name, so the ruleset must
be updated in the same pull request, or every later pull request waits for a check that never runs.

**G7 on pull requests.** API tests sign tokens with a test key, so only the E2E journeys exercise the real
Keycloak sign-in. The job `Does this change touch sign-in?` (plain `git diff`) decides whether they run: a pull
request that touches one of the G7 paths runs them and cannot merge until they pass; any other pull request
reports the E2E check as *Skipped*, which GitHub counts as passed. If that detection step fails, the journeys
run anyway, because a skipped required check would pass. A change elsewhere that could still affect sign-in can
ask for the journeys with the `e2e` label.

**Accepted exception — single maintainer (since 2026-11-20).** The repository has one code owner, and
GitHub does not let authors approve their own pull requests, so the ruleset requires **0 approvals** and
G8 is met by the code owner reviewing and merging the pull request themselves. Changes written by an AI
implementer still need that person's review and merge (README → *The implementer*). The exception ends
when a second maintainer joins: then the ruleset requires 1 approval and review from code owners, and
`/backend/api/Auth/` and `/keycloak/` get a second reviewer in CODEOWNERS.

G4 and G5 are small scripts in `scripts/`. They read the register and the test source; they do not judge
test quality — that is G8.

**G4 (c) — citations still resolve** (`scripts/check_doc_references.py`, in the same CI job). Agents and people
act on what the documents cite; when code moves, a citation can quietly point at nothing (the agents once named
helpers that had been removed). The check fails when a cited path or file is gone, a cited line is past the end of
the file, or a code name in an agent or skill file no longer appears in the code. It proves that a citation
resolves, **not** that the cited line still says the same thing: that stays with the reviewer and qe-reviewer's
check V3. Cite pull requests, not branch names: a branch is gone after merge. Format examples, routes such as
`/health`, git-ignored build output and `.env`, and the two legacy folders deleted on purpose are not checked.

**G4 is a ratchet.** A risk enters the gate when the test owner sets it to `Covered` after its tests pass;
from then on it can never lose its tests unnoticed. Risks of level *Config* and *Ops* join the gate in wave 3
(QUALITY_GATES §7), when their checks exist. The list of uncovered Critical/High risks is printed on every
run so the gap stays visible without keeping CI permanently red.

**Known gaps.** A test for a risk that is still open is written now, for the *correct* behavior, and skipped
until the fix lands:

```csharp
[Fact(Skip = "Known gap R-054")]
[Trait("Risk", "R-054")]
[Trait("KnownGap", "R-054")]
public async Task GetDoctor_DoesNotExposeAccountLink() { ... }
```

The fix removes the `Skip` and the `KnownGap` trait in the same pull request, and the test must pass. A
known-gap test never asserts the current, wrong behavior. It does not count for G4 (a).

**What does not gate:** coverage percentage, number of tests, test duration (tracked, not gated).

**Mutation score (weekly, not per pull request):** `.github/workflows/mutation.yml` fails when the Stryker score
of the core services drops below 90 % (TEST_STRATEGY §11). A failure there means tests stopped noticing broken
rules; it is handled like a failure on `master` (§4).

**Run the gates locally:** `scripts/verify.sh` (Git Bash, Linux, macOS) or `scripts/verify.ps1` (PowerShell, runs
the same script) runs G1–G6 in the CI order and stops at the first failure, so green locally means green in CI.
It needs the .NET 8 and 10 SDKs, Node 20, Docker and Python 3; `--no-frontend` skips the frontend part of G1.

**Dependencies:** `.github/dependabot.yml` proposes NuGet, npm and GitHub Actions updates weekly. Actions are
pinned to a commit SHA with the version as a comment, so a moved tag cannot change what runs. Each update is an
ordinary pull request through all the gates and a person's merge.

## 2. Pull request checklist

The PR template asks the author:

- Which risks does this change touch? (IDs)
- Which tests prove them? (names)
- Did any test get removed, skipped or loosened? Why?
- Does the change touch `[Authorize]`, `Policies.cs`, ownership checks or token validation? If yes, which
  security tests changed?
- Does the change set a risk to `Covered`? If yes, the **red evidence** for it: a CI run (link) or pasted output
  where its test failed on the old or deliberately broken code, then passed with the change. Without it the risk
  stays below `Covered` — a test that has never been seen failing has not proven anything (TEST_STRATEGY §10).

`qe-reviewer` can draft answers to these questions; the author confirms them.

**Register changes are shown, not hidden in the diff.** On every pull request the job *Risk coverage and skips*
writes a table of each Impact, Likelihood, Severity, Level and Status change in the register, and each added or
removed risk, to its job summary (`scripts/register_changes.py`). It never fails; it makes sure the person who
merges sees every rating decision before it lands.

## 3. Flaky tests

A test is **flaky** when it fails and passes on the same code.

**Found before it blocks someone:** `.github/workflows/flaky.yml` runs the unit and API tests three times on the
same commit every night (and on demand) and lists every test whose outcome changed (`scripts/flaky_report.py`).
It only detects: nothing is re-run to green, and it is not a required check. A test it lists starts at step 1.

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

**Current decision (Q-006, 2026-11-20): the system runs locally only**, with tests in GitHub Actions (possibly
Jenkins later). No deployment is planned, so this gate is not reached: the deployment risks are accepted for
local use in RISK_REGISTER §5 until a deployment is planned, and real patient data stays excluded (Q-007).

### Proposed order

Waves are ordered by "public exposure today" first, then by what each wave unblocks. Waves 1, 2 and 4 need
no further decision (Q-007 is answered: synthetic data). Waves 3 and 5 are **not planned** while the system runs
locally only (Q-006, Q-008); they stay here as the plan for the day a deployment is decided.

| Wave | Goal | Risks | Needs |
|---|---|---|---|
| **1 — Clean the repository and the dev defaults** ✅ done 2026-11-15 | Nothing secret or dev-only is public or on by default | R-030 (legacy projects deleted, build output ignored; rotate the old password wherever it is used), R-027 (password grant off on `hospital-web`, no replacement test client), R-029 (sample data and Swagger only in `docker-compose.override.yml`), R-043 (password reset off until SMTP exists) | Q-009 answered: delete |
| **2 — Phase 2: tests and CI** | Every change is checked automatically | R-035: API test project (role matrix, BOLA, tokens, deactivation, record author/editor, known gaps), unit tests for the appointment rules (R-009, R-014, R-018, R-022), `TimeProvider` in the services (A-004), GitHub Actions with gates G1–G6, CODEOWNERS and the PR template; R-012 (gitleaks); R-040 (pinned image tags in CI). Remaining unit tests: phase 3; E2E: phase 4 | – |
| **3 — Production deployment** ⏸ not planned (local only, Q-006) | A deployable, encrypted, least-privilege stack | R-025 (TLS everywhere, HSTS), R-026 (Keycloak `start` with a production realm, no sample users), R-033 (static frontend image behind a web server), R-034 (hostnames, issuer, CORS and redirect URIs from configuration), R-031 (separate database users, least privilege), R-032 (backups and a restore drill), R-036 (security headers, rate limiting), R-038 (migrations as a release step), R-039 (shared cache or short expiry) | Q-006, Q-008 |
| **4a — Real-life flows: data and API** | Every flow in [BUSINESS_FLOWS.md](BUSINESS_FLOWS.md) works end to end on realistic synthetic data, without leakage | R-054 (DTOs instead of entities), R-051 (time zones), R-049 (duplicate patients), R-044 (patient model and the deterministic data generator, BUSINESS_FLOWS §4), R-048 (concurrent booking), R-056 (record version check), R-053 (records only for completed appointments), R-050 (closing past appointments), R-055 (offboarded doctors' appointments), R-042 (no personal data in logs) | – |
| **4b — Real-life flows: frontend** | Each actor can do their flows in `frontend-web`, not only through Swagger | Screens for the flows marked "UI ❌" in BUSINESS_FLOWS (appointments, records and history, patient's own data, user linking, status correction, audit) | 4a |
| **4c — Health-data security** | Security as if the data were real | R-028 (MFA and password policy for staff), R-041 (append-only audit store, retention), R-045 (retention and export mechanisms), R-052 (shorter token lifetime or role re-check) | – |
| **5 — Operate** ⏸ not planned (local only, Q-006), except R-037, which is code | Problems are seen and handled | R-046 (telemetry export, alerts, central logs including Keycloak events), R-047 (runbooks for deploy, rollback, secret rotation, restore), R-037 (paging) | Wave 3 |

Wave 1 is small enough to do before phase 2. Phase 2 (wave 2) is the foundation for the others: from then on
every fix in waves 3–5 lands with the test or configuration check that proves it.
