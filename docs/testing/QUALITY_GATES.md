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
