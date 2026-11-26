# Quality Gates

What must be true before code is merged or released, and what happens when it is not. Gates are run by
GitHub Actions and decided by people. **No gate calls an AI agent.**

## 1. Gates

| # | Gate | Runs on | Passes when | Evidence |
|---|---|---|---|---|
| G1 | Build | every PR | `dotnet build backend/backend.sln` has 0 errors; `npm run build` in `frontend-web` succeeds and `npm run lint` (ESLint, `frontend-web/eslint.config.js`: TypeScript rules and the React hook rules) reports no error; no package with a known high or critical vulnerability: NuGet audit at restore for every .NET project, including transitive packages (`Directory.Build.props`), and `npm audit --omit=dev` for what ships to the browser. Build-tool (dev) vulnerabilities are reported as a warning, not gated. A vulnerable package is fixed by pinning a patched version, never by silencing the warning | CI log |
| G2 | Unit and component tests | every PR | all pass; none skipped without a ticket (G5). Backend unit tests in the backend job; frontend component tests (`npm test`, Vitest) in the `Frontend build (G1)` job, which keeps its name because the ruleset requires that check by name | test results (TRX), Vitest output |
| G3 | API tests | every PR | all pass against real PostgreSQL; none skipped without a ticket. Includes the API contract check: `contracts/openapi.json` equals what the API offers (TEST_STRATEGY §2) | test results (TRX) |
| G4 | Risk coverage | every PR | (a) every risk with status `Covered` in [RISK_REGISTER.md](RISK_REGISTER.md) is named by at least one test that is not skipped (`[Trait("Risk", "R-xxx")]`) **at the risk's level** — a Unit risk by a unit test, an API risk by an API test, a Component risk by a frontend component test (tag `@R-xxx` in its title or `describe`), an E2E risk by an E2E scenario. An E2E scenario (not run on every PR) cannot keep a Unit or API risk Covered; (b) every `Risk` trait names a risk that exists; (c) every path, `file:line` and code name cited in `backticks` in the agent and skill files, the testing documents, ARCHITECTURE and API_ENDPOINTS still exists (code names: agent and skill files only); (d) every required check named in §1 is a job in `.github/workflows/`, and every job with a gate number in its name is named in this document (`scripts/check_gate_names.py`); (e) every ID is defined once in the section that owns it and every reference to it resolves (`scripts/check_ids.py`). The script also **lists** — without failing — every `Critical`/`High` risk of level API, Unit or CI that is not `Covered` yet | script output |
| G5 | No silent skips | every PR | every `Skip = "…"` names either an open risk (`Known gap R-054`) or a GitHub issue (`#123`) | script output |
| G6 | Secret scan | every PR | gitleaks finds nothing in the files, nor in any commit of the pull request (a secret added and deleted again inside one PR stays in the history once merged) | gitleaks report |
| G7 | E2E | every PR that changes something the E2E stack runs: anything except docs and Markdown, `.claude/`, tests of other levels (`backend/tests/`, frontend component tests), gate scripts and tool manifests (`scripts/`, `dotnet-tools.json`), the other workflows, the Aspire AppHost and repository housekeeping; PR label `e2e`, merge to `master`, nightly | all scenarios pass, no retries | Reqnroll report, Playwright trace on failure |
| G8 | Review | every PR | a code owner approved; the PR template's test section is filled in | GitHub review |
| G9 | Mutation of changed code | every PR; it mutates only what the PR changed in `backend/Services/` (Stryker.NET, unit tests), `backend/api/Auth/PatientAccessPolicy.cs` (Stryker.NET, unit tests) and `frontend-web/src/` outside the tests (StrykerJS, component tests) | no mutant on a line the PR changed goes undetected by the tests; a truly equivalent one is accepted in the code with `// Stryker disable once <Mutator> : <reason>`, reviewed in the diff. The file's overall score does not count, so old survivors never block an unrelated change (TEST_STRATEGY §11) | job summary, Stryker report artifact |
| G10 | Static analysis | every PR, merge to `master`, weekly | CodeQL (`security-and-quality` queries, C# and TypeScript, `.github/workflows/codeql.yml`) reports no new alert of severity high or above; a false positive is dismissed in the Security tab by a person, with a reason | code scanning alerts |

**Enforcement on GitHub.** The ruleset *Protect master* (repository settings → Rulesets) makes G1–G7 and G9
binding, not advisory: changes reach `master` only through a pull request, the four CI checks
(`Backend build and tests (G1, G2, G3)`, `Frontend build (G1)`, `Risk coverage and skips (G4, G5)`,
`Secret scan (G6)`), the E2E check `End-to-end journeys (G7)` and the mutation check
`Mutation testing of changed code` (G9) must pass, and force pushes and deleting
`master` are blocked. The bypass list is empty. Renaming a CI job changes its check name, so the ruleset must
be updated in the same pull request, or every later pull request waits for a check that never runs;
`scripts/check_gate_names.py` (in the G4 job) fails when a check named here is not a job in `.github/workflows/`.

**Not in the ruleset yet** (added 2026-11-24; the test owner changes the ruleset, an implementer never does):

- **Branches must be up to date before merging** (*Require branches to be up to date*): off on purpose for now,
  see *Accepted exception — branches not kept up to date* below.
- **G10:** the checks `CodeQL (G10) csharp` and `CodeQL (G10) javascript-typescript`, or a code scanning rule for
  alerts of severity high and above. Until then an alert shows on the pull request but does not block the merge.

**G9 in the ruleset** (since 2026-11-23). A pull request cannot merge while a mutant on one of its changed lines
goes undetected. Since 2026-11-24 the check runs on every pull request and covers three areas, each only when the
pull request changes it: services, the patient access policy and the web frontend. A pull request that changes
none of them passes at once; if the change detection itself fails, the check fails rather than pass unchecked. Before the check became required it had run on GitHub: on #52 Stryker
found the four changed files, none of them service code, and passed; on #53 and #54 it was skipped.

**G7 on pull requests.** API tests sign tokens with a test key, so only the E2E journeys exercise the real
Keycloak sign-in, and they are the only tests that run the API, the database, Keycloak and the production
frontend together. Since 2026-11-23 they run on **every pull request that changes something that stack runs**
(before: only sign-in paths, then also the frontend). The job `Does this change reach the E2E stack?` (plain
`git diff`) lists what may be **skipped**, not what must run: docs, tests of other levels, gate scripts, the other
workflows, the AppHost and housekeeping. Any other path, including one added later, runs the journeys, and the
pull request cannot merge until they pass. A pull request that changes only skippable files reports the E2E
check as *Skipped*, which GitHub counts as passed. If the detection step fails, the journeys run anyway. A label added
to the pull request starts its own run, which never cancels the push's run: a run cancelled before it had a
job left the required check waiting as *Expected* on Dependabot's #58. The
job took 115–148 s on GitHub after the speed-up (*E2E job duration* below) and runs in parallel with CI, so it adds little
waiting. Applied to the ten pull requests before this change, the rule runs the journeys for the three that
changed the stack (#44, #50, #51) and skips the seven that changed only docs, tests, tooling or the AppHost.

**Accepted exception — single maintainer (since 2026-11-20).** The repository has one code owner, and
GitHub does not let authors approve their own pull requests, so the ruleset requires **0 approvals** and
G8 is met by the code owner reviewing and merging the pull request themselves. Changes written by an AI
implementer still need that person's review and merge (README → *The implementer*). The exception ends
when a second maintainer joins: then the ruleset requires 1 approval and review from code owners, and
`/backend/api/Auth/` and `/keycloak/` get a second reviewer in CODEOWNERS.

**Accepted exception — branches not kept up to date (since 2026-11-24, decided by the test owner).** The ruleset
does not require a pull request to be up to date with `master` before it merges. With one maintainer, keeping every
open pull request up to date after each merge (a new CI round each time, E2E included) costs more than it finds.
**What is accepted:** two pull requests that each pass every gate can still break `master` together, because nobody
tests their combination before the second one merges. Dependabot opens several pull requests at once, so this
happens even with one maintainer. **What still catches it:** CI, E2E and mutation run again on every merge commit on
`master` (each in a concurrency group of its own, so none is cancelled), and a failure there is handled as in §4.
**To lower the risk without the setting:** merge pull requests that change the same files one after another, and
update the second one before merging it. **The exception ends** when a second maintainer joins, or when a failure on
`master` comes from two pull requests that were green on their own; then *Require branches to be up to date* is
switched on again.

G4 and G5 are small scripts in `scripts/`. They read the register and the test source; they do not judge
test quality — that is G8.

**G4 (c) — citations still resolve** (`scripts/check_doc_references.py`, in the same CI job). Agents and people
act on what the documents cite; when code moves, a citation can quietly point at nothing (the agents once named
helpers that had been removed). The check fails when a cited path or file is gone, a cited line is past the end of
the file, or a code name in an agent or skill file no longer appears in the code. It proves that a citation
resolves, **not** that the cited line still says the same thing: that stays with the reviewer and qe-reviewer's
check V3. Cite pull requests, not branch names: a branch is gone after merge. Format examples, routes such as
`/health`, git-ignored build output and `.env`, and the two legacy folders deleted on purpose are not checked.

**G4 (e) — every ID is defined once and every reference resolves** (`scripts/check_ids.py`, in the same CI job;
its own tests are in `scripts/tests/`). The legend in [README.md → Identifiers](README.md#identifiers) says which
section owns each kind of ID. The check fails when an ID such as R-069, A-006, Q-017, Q-a, D-2, UI-HP-05, F-02,
F-02.6 or G9 is named in a testing document, ARCHITECTURE, API_ENDPOINTS, the root Readme, CLAUDE.md, an agent or
skill file or a test (including `[Trait("Scenario", "F-02.6")]` and `@F-02.6`) but has no row (a heading for a
flow) in its owning section, or when an ID has two rows there. It also fails when a file or folder it reads is
missing, so a rename cannot take a file out of the check. A row elsewhere, such as an accepted risk in
RISK_REGISTER §5, is a reference, not a definition. A D-number is an ARCHITECTURE decision inside ARCHITECTURE.md
or when the word right before it is ARCHITECTURE (`ARCHITECTURE D-9`); a bare D-number anywhere else is a web
defect (Q-037). In the tests it reads every line, frontend test helpers in `frontend-web/src/test/` included, so
test data must not look like an ID (BUSINESS_FLOWS §4, Q-039). Placeholders such as `R-xxx` are not IDs. It
checks that an ID *exists*, not that its state is the same everywhere: a state is written in its own row only.
Known limits: in a range ("R-001 to R-004") only the two ends are checked, in "ARCHITECTURE D-8, D-9" only the
first number is a decision (write the word before each), a scenario row is not checked against the flow heading above it, and the UI test categories are a fixed list in the script. Production code comments and
the pull request template are not read. Risk R-070.

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

**Branch smoke (early feedback, not a merge gate):** `.github/workflows/smoke.yml` runs the risk/skip,
documentation-reference, check-name and ID checks (G4, G5) on every push to branches other than `master`. All unit and API tests run
when backend code or its build configuration changes; the frontend build runs when `frontend-web/` changes.
It does not replace the checks required on every PR.

**E2E job duration.** The journeys themselves take about 20 s; the job's time is preparation. The stack starts in
the background while the frontend, the E2E project and Chromium are prepared; the Chromium build is cached by
Playwright version, and system packages are installed only if the browser reports a missing library (until
2026-11-23 `--with-deps` ran apt-get on every run, 40 s to 6 min).

**What does not gate:** coverage percentage, number of tests, test duration (tracked, not gated).

**Mutation score of whole files (nightly; per pull request only the changed lines count, G9):**
`.github/workflows/mutation.yml` fails when the Stryker score of the core services or of the patient access
policy drops below 90 % (TEST_STRATEGY §11). A failure there means tests stopped noticing broken
rules; it is handled like a failure on `master` (§4).

**Run the gates locally:** `scripts/verify.sh` (Git Bash, Linux, macOS) or `scripts/verify.ps1` (PowerShell, runs
the same script) runs every step of `ci.yml` (G1–G6: builds including the E2E project, audits, lint, unit,
component and API tests, gate scripts, gitleaks) in the CI order and stops at the first failure, so green locally
means `ci.yml` green. G7, G9 and G10 run on the pull request. It needs the .NET 10 SDK, Node 24 (as CI), Docker
and Python 3; `--no-frontend` skips the frontend part.

**Dependencies:** `.github/dependabot.yml` proposes NuGet, npm, GitHub Actions and container image updates weekly,
and only versions published at least 14 days earlier (cooldown, since 2026-11-24): a hijacked or broken release is
usually found and pulled within days. Security updates for a known vulnerability are not delayed. Actions are
pinned to a commit SHA and images to a digest, with the version as a comment or tag, so a moved tag cannot change
what runs. Each update is an
ordinary pull request through all the gates and a person's merge. Dependabot proposes no major versions of NuGet or
npm packages: a major upgrade is a deliberate change made and tested by a person, and for .NET the
`Microsoft.EntityFrameworkCore.*`, `Microsoft.Extensions.*` and `Microsoft.AspNetCore.*` majors follow the .NET
runtime, so they move only with it. A known vulnerability in a skipped major is still caught by the G1 audit.

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

- Every gate in §1 passes on the pull request (the required ones block the merge), and G8: a code owner merged it.
- Every new or changed line in the areas of G9 has a test that fails when the line breaks.
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
