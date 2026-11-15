# Testing — Hospital Management

This folder describes how the Hospital Management platform is tested. The test suite is a **regular,
deterministic test project** (xUnit, Reqnroll, Playwright) that runs in GitHub Actions. AI agents
**assist the people** who design, write and review tests. Nothing in the pipeline depends on AI: if every
agent were removed tomorrow, every test and every quality gate would still run the same way.

## Testing philosophy

| Principle | What it means here |
|---|---|
| **Confidence over coverage** | There is no coverage-percentage gate. The gate is: every Critical and High risk has a passing test that would fail if the risk happened. |
| **Risk over quantity** | Every test traces to a risk ID (`[Trait("Risk", "R-001")]`). A test with no risk behind it is questioned in review. |
| **Reliability over speed** | No automatic retries, no `Thread.Sleep` / fixed waits, isolated test data. A flaky test is quarantined with an owner and a ticket — never silently skipped. |
| **Evidence over assumptions** | A claim cites code (`file:line`), a test, or a test run. Anything unproven is recorded as an assumption with an owner and a review date. |

**Operating principle: production-like, with synthetic data.** Everything is built, configured and tested
as close to real life and production as possible — real-life flows, health-data security with no leakage,
reliability, and every important scenario and edge case — while all data is fake. Details in
[BUSINESS_FLOWS.md](BUSINESS_FLOWS.md#operating-principle).

## Documents

| Document | Answers |
|---|---|
| [BUSINESS_FLOWS.md](BUSINESS_FLOWS.md) | The real-life flows of each actor, their edge cases, the synthetic data rules and the leakage checklist. |
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | What we test at which level, with which tools, data and environments, and how tests are written. |
| [RISK_REGISTER.md](RISK_REGISTER.md) | How risks are rated, the current risks, the open assumptions, and which test covers which risk. |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Who may call which endpoint, per-record access rules, token and leakage checks. |
| [QUALITY_GATES.md](QUALITY_GATES.md) | What must pass before merge and release, the flaky-test and skip policy, and how failures and findings are handled. |

These documents are the **single source of truth**. Agents and skills point to them; they do not copy them.

## How the work flows

```
 requirement / change / endpoint
            │
            ▼
   ┌─────────────────┐   criteria, gaps, risks,
   │  qe-analyst     │── abuse cases, security gaps ──┐
   └─────────────────┘                                │
                                                      ▼
                                          a person accepts or edits
                                          risks in RISK_REGISTER.md
                                                      │
                                                      ▼
   ┌─────────────────┐   test cases at the right level,
   │ qe-test-designer│── Gherkin, draft code as text ─┐
   └─────────────────┘                                │
                                                      ▼
                                          a person writes / accepts
                                          the test code and commits it
                                                      │
                                                      ▼
   ┌─────────────────┐   weakened or missing tests, missing risk tags,
   │  qe-reviewer    │── auth changes without tests, failure triage, ─▶ a person decides
   └─────────────────┘   evidence check of the other agents' output
                                                      │
                                                      ▼
                                       CI quality gates (no AI involved)
```

The reviewer also validates the analyst's and the test designer's output, so no agent checks its own work.

## AI rules

1. **Agents are read-only.** They read code and documents. They never edit files, commit, push, approve a
   pull request, change a risk rating or run in CI.
2. **People decide.** A person accepts every risk rating, every test and every merge. Code owners approve
   pull requests ([.github/CODEOWNERS](../../.github/CODEOWNERS)).
3. **Agent output is a proposal.** Every statement is labelled *Fact* (with a citation), *Assumption* or
   *Question*. Uncited statements are not used.
4. **Agents never weaken a test.** They never propose retries, skips, longer timeouts, broader assertions
   or deleting a failing test as a fix.
5. **No secrets or real patient data** are given to an agent. Test data is synthetic.

## Agents and skills

| Item | File | Purpose |
|---|---|---|
| Agent `qe-analyst` | [.claude/agents/qe-analyst.md](../../.claude/agents/qe-analyst.md) | Requirements → acceptance criteria, gaps, risks, abuse cases, security gaps |
| Agent `qe-test-designer` | [.claude/agents/qe-test-designer.md](../../.claude/agents/qe-test-designer.md) | Criteria and risks → test cases at the right level, Gherkin, draft code |
| Agent `qe-reviewer` | [.claude/agents/qe-reviewer.md](../../.claude/agents/qe-reviewer.md) | Change review, failure triage, validation of other agents' output |
| Skill `risk-model` | [.claude/skills/risk-model/SKILL.md](../../.claude/skills/risk-model/SKILL.md) | How an agent applies the risk model |
| Skill `evidence-rules` | [.claude/skills/evidence-rules/SKILL.md](../../.claude/skills/evidence-rules/SKILL.md) | How every agent cites evidence and formats its output |

Examples of use in Claude Code:

```
Use qe-analyst on POST /api/Appointment.
Use qe-test-designer for R-001 and R-004.
Use qe-reviewer on the current diff.
Use qe-reviewer to triage this failing test: <paste output>.
```

## Roles

| Role | Who | Responsibility |
|---|---|---|
| Test owner | Repository owner | Accepts risk ratings and assumptions, owns this folder, decides on quarantine and accepted risks. |
| Author | Whoever changes code | Writes or updates the tests for the risks their change touches. |
| Code owner | Listed in CODEOWNERS | Approves pull requests. Cannot be an agent. |

## Test projects

```
backend/tests/HospitalManagement.UnitTests/   business rules of every service, access policy,    phase 2 ✅, phase 3 ✅
                                               outbox delivery, architecture rules (fixed clock)
backend/tests/HospitalManagement.ApiTests/    authorization matrix, tokens, BOLA, record author, phase 2 ✅ — real PostgreSQL
                                               catalogue, transactions, outbox
e2e/HospitalManagement.E2E/                    sign-in and clinical journeys — real Keycloak,      phase 4 ✅
                                               API, PostgreSQL and the production frontend build
```

Run the E2E journeys against a running stack (`docker compose up -d --build`, then
`npm run build` and `npx vite preview --port 3000` in `frontend-web`), with the passwords from `.env`:

```bash
set -a; . ./.env; set +a; dotnet test e2e/HospitalManagement.E2E
```

Install Chromium once with `pwsh e2e/HospitalManagement.E2E/bin/Debug/net8.0/playwright.ps1 install chromium`.

Run them (Docker must be running for the API tests):

```bash
dotnet test backend/tests/HospitalManagement.UnitTests
dotnet test backend/tests/HospitalManagement.ApiTests -f net10.0
python3 scripts/check_test_gates.py
```

`-f net10.0` is for machines with only the .NET 10 runtime; CI also runs `net8.0`, the runtime the API ships
on. CI: [.github/workflows/ci.yml](../../.github/workflows/ci.yml) (G1–G6) and
[.github/workflows/e2e.yml](../../.github/workflows/e2e.yml) (G7). See [TEST_STRATEGY.md](TEST_STRATEGY.md)
for what belongs where.
