# TEST_STRATEGY

| Field | Value |
|---|---|
| Document ID | QE-DOC-01 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 21 |
| Owner | QE Lead |
| Approvers | QE Lead, Tech Lead, Security Lead |
| Review cadence | Quarterly, and after any Critical escaped defect |
| Last updated | 2026-11-13 |

## 1. Purpose

This document is the root of the Quality Engineering (QE) framework. It states how the team decides what to test, at which level, to what depth, and what evidence is required before a change is trusted. Every other framework document refines one part of this strategy and may not contradict it.

## 2. Scope

In scope: all production code, database migrations, API contracts, identity configuration (Keycloak realm, clients, roles), infrastructure definitions that affect runtime behavior, and all test code.

Out of scope: exploratory prototypes that are never merged to a protected branch, and third-party products themselves (their integration points are in scope).

## 3. Definitions

Terms below are owned by this document. Other documents use them unchanged.

| Term | Definition |
|---|---|
| Confidence | The justified belief, backed by evidence, that the system behaves as required under the conditions that matter. Confidence is the goal of testing. |
| Behavior Coverage | The share of specified behaviors (acceptance criteria and documented business rules) that have at least one passing test which fails when the behavior is broken. |
| Risk Coverage | The share of identified risks, per severity level, that have at least one passing test which fails when the risk's mitigation is removed. Risks are defined in [RISK_MODEL.md](RISK_MODEL.md). |
| Code Coverage | The share of code lines or branches executed by tests. Informational only. It is never a gate and never reported as evidence of quality on its own. |
| Deterministic Test | A test that gives the same result on every run for the same code and inputs, regardless of run order, clock, locale, network or other tests. |
| Side Effect | Any observable change a behavior causes beyond its return value: persisted data, emitted events or messages, audit entries, outbound calls, notifications, changed identity state. |
| Mutation Score | Killed mutants divided by all non-equivalent mutants in the measured scope. |
| Flaky Test | A test that has produced both pass and fail for the same code revision. |
| Evidence | A reproducible, linkable artifact: a test result, execution log, review record, mutation report, scan report or signed approval. |
| Test Level | One of the levels in section 7. |

## 4. Ownership and Roles

| Role | Responsibility in this document |
|---|---|
| QE Lead | Owns the strategy, approves changes to it, arbitrates level and depth disputes |
| Tech Lead | Ensures the strategy is applied in design and code review; owns testability of the architecture |
| Security Lead | Owns the security parts (section 10) through [SECURITY_TESTING.md](SECURITY_TESTING.md) |
| Product Owner | Supplies and approves acceptance criteria; accepts residual risk within the limits in [RISK_MODEL.md](RISK_MODEL.md) |
| Engineers | Write and maintain tests for their changes; own the defect reproduction test for defects they fix |
| AI agents | Assist as defined in [AGENTS_CATALOG.md](AGENTS_CATALOG.md). They never approve. |

## 5. Testing Philosophy

The six framework principles, in precedence order. When two rules conflict, the higher principle wins.

1. Reliability over speed
2. Confidence over coverage
3. Evidence over assumptions
4. Validation over generation
5. Human accountability over AI autonomy
6. Risk coverage over test quantity

What this means in practice:

- A test exists to detect a specific failure. A test that cannot fail for a realistic defect adds cost and false confidence, and is removed under section 15.
- Fewer tests that each protect a named behavior or risk are preferred over many tests that execute code without checking outcomes.
- A claim that something works is accepted only with evidence. "It was generated", "it compiles" and "it ran" are not evidence of correctness.
- Every test artifact produced by AI is validated and then approved by a named human before it counts toward any gate.

### 5.1 Confidence over coverage

Code Coverage answers "what ran". Confidence requires "what would be caught". The framework therefore measures:

| Measure | Role |
|---|---|
| Behavior Coverage | Gate (see [QUALITY_GATES.md](QUALITY_GATES.md) G-BEH-01) |
| Risk Coverage | Gate (G-BEH-02) |
| Mutation Score | Gate (G-MUT-01, G-MUT-02) |
| Code Coverage | Diagnostic only: used to find untested areas, never to claim quality |

## 6. Risk-Based Testing

Test depth follows risk severity from [RISK_MODEL.md](RISK_MODEL.md). The mapping there (section 11, risk-to-test mapping) is binding. In summary:

| Risk severity | Minimum test obligation |
|---|---|
| Critical | 100% Risk Coverage, tests at two or more independent Test Levels, negative and abuse-case tests, highest mutation threshold |
| High | 100% Risk Coverage, at least one Test Level plus negative tests |
| Medium | Risk Coverage at or above the Medium threshold |
| Low | Covered when cheap; otherwise recorded and accepted |

A requirement without a risk assessment is treated as High until assessed.

## 7. Test Levels

| Level | Verifies | Dependencies | Typical owner |
|---|---|---|---|
| Unit | A single unit's logic and business rules | None real; collaborators replaced only at architectural boundaries | Engineer |
| Integration | A component with its real dependencies: database, migrations, message handling | Real instances of owned dependencies; no mocks of the database | Engineer |
| API / Contract | HTTP contract, status codes, validation, authorization outcomes, side effects | Running service with real persistence and a real or faithfully configured identity provider | Engineer, API Agent assists |
| End-to-End | Critical user journeys across components | Full stack | QE |
| Security | Authentication, authorization, BOLA, information leakage, abuse cases | As API level | Security Lead, Security Agent and BOLA Agent assist |

Rules:

- Test at the lowest level that can truly detect the failure. Push down for speed only when detection power is unchanged.
- End-to-End tests are reserved for Critical and High risk journeys.
- Mutation testing is not a level. It validates the tests at Unit and Integration level (section 11).

## 8. Deterministic Tests

Every test must be a Deterministic Test.

- No dependence on wall-clock time, time zone, locale, random seeds, run order or shared mutable state. Inject the clock and seeds.
- Each test creates the data it needs and does not rely on data left by another test.
- No fixed sleeps. Wait on an observable condition with a timeout.
- No calls to systems the team does not control. Use a controlled stand-in at that boundary.
- A Flaky Test is a defect in the test suite. It is fixed or removed within the time limit in [QUALITY_GATES.md](QUALITY_GATES.md) G-TST-01. Retrying until green is prohibited.

## 9. Side Effects

A behavior is not verified until its Side Effects are verified.

- For every state-changing operation, assert the persisted state by reading it back through an independent path, not the return value alone.
- Assert that prohibited side effects did not happen: a rejected request must leave data unchanged.
- Assert audit and notification side effects where the requirement names them.
- For failures midway through an operation, assert the documented outcome (rollback or compensation).

Example: `DELETE /api/Patient/{id}` for a patient with appointments must return 409 and a follow-up `GET` must still return the patient.

## 10. Security

Security testing is part of the definition of done for every endpoint, not a separate phase. The requirements are in [SECURITY_TESTING.md](SECURITY_TESTING.md). At minimum each new or changed endpoint has tests for: no token (401), wrong role (403), and, where the route carries an object identifier, another user's object (BOLA).

## 11. Mutation Testing

Mutation testing measures whether tests detect change, which is the direct evidence for principle 2.

- Scope: business logic, authorization logic and validation. Generated code, DTOs and configuration wiring are excluded.
- Thresholds depend on the risk severity of the code and are set in [QUALITY_GATES.md](QUALITY_GATES.md) G-MUT-01.
- A surviving mutant is handled in one of three ways: add or strengthen a test, mark the mutant equivalent with a written reason approved by a reviewer, or record an accepted risk.
- The Mutation Score may not regress beyond the tolerance in G-MUT-02.

## 12. CI/CD Gates

Gates are defined once, in [QUALITY_GATES.md](QUALITY_GATES.md). This strategy requires that:

- Every gate is automated where possible and blocks the pipeline when it fails.
- A gate result is Evidence and is retained for audit.
- A gate may be waived only through the waiver process in QUALITY_GATES.md section 7. Gates protecting Critical risks cannot be waived.

## 13. Traceability

Every test must be traceable in both directions.

```
Requirement (REQ-n) -> Acceptance criterion (AC-n.m) -> Risk (RSK-n) -> Test (name or ID) -> Result (CI run) -> Gate decision
```

- Each test names the acceptance criterion, risk or defect it protects, in its name, a tag or an attribute.
- A test that traces to nothing is a candidate for removal (section 15).
- A Critical or High risk that traces to no test fails G-BEH-02.

## 14. Defect Reproduction

1. A defect is reproduced by an automated test before it is fixed. The test must fail for the reported reason.
2. The fix is accepted only when that test passes and it is shown to have failed before the fix (G-TST-03).
3. The test is linked to the defect ID and stays in the suite permanently unless removed under section 15.
4. If a defect cannot be reproduced automatically, the reason is recorded and the QE Lead approves the alternative evidence.
5. Each escaped Critical or High defect triggers the incident learning flow in [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md).

## 15. Test Removal Policy

Removing, skipping, or weakening a test (loosening an assertion, widening a tolerance, deleting a case) reduces Confidence and is controlled.

| Allowed reason | Required evidence |
|---|---|
| The behavior was removed or changed by an approved requirement | Link to the requirement |
| The test duplicates another test with equal or greater detection power | Link to the other test; mutation result showing no drop |
| The test cannot fail for any realistic defect | Mutation or review evidence |
| The test is flaky and is replaced by a deterministic equivalent | Link to the replacement in the same change |

Rules:

- Every removal is recorded in the pull request with the reason and is approved by a human reviewer other than the author (G-PR-05).
- A test protecting a Critical or High risk, or reproducing a defect, additionally needs QE Lead approval.
- "It was failing" and "it was slow" are not allowed reasons.
- AI agents may propose a removal. They may not perform one without human approval.

## 16. Inputs and Outputs

| Inputs | Outputs |
|---|---|
| Approved requirements and acceptance criteria | Test suites at each Test Level |
| Risk register from RISK_MODEL.md | Traceability links |
| Architecture and API inventory | Gate evidence (results, mutation reports) |
| Defect and incident history from KNOWLEDGE_CATALOG.md | Defect reproduction tests |

## 17. Metrics

| Metric | Definition | Target |
|---|---|---|
| Behavior Coverage | Section 3 | Per G-BEH-01 |
| Risk Coverage | Section 3 | Per G-BEH-02 |
| Mutation Score | Section 3 | Per G-MUT-01 |
| Flaky Rate | Flaky Tests divided by all tests, rolling 30 days | Below 1% [A-TS-03] |
| Defect Escape Rate | Defects found after release divided by all defects found, per release | Trending down; any Critical escape triggers review |
| Reproduction Rate | Fixed defects with a linked reproduction test divided by all fixed defects | 100% for Critical and High |

## 18. Review Process and Escalation

- Changes to this document need approval from all three approvers in the header.
- Disputes about test level or depth go to the QE Lead (L2). Disputes about residual risk go through the acceptance rules in RISK_MODEL.md.
- Escalation levels L1 to L3 are defined in [AI_GOVERNANCE.md](AI_GOVERNANCE.md) section 9.

## 19. Best Practices and Anti-Patterns

Best practices are collected in [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md). The anti-patterns below are prohibited by this strategy.

| ID | Anti-pattern | Why it harms Confidence | Required response |
|---|---|---|---|
| AP-TS-01 | Coverage chasing: tests written to raise Code Coverage | Executes code without detecting failure | Reject in review; measure Mutation Score |
| AP-TS-02 | Assertion-free or tautological tests | Cannot fail | Remove under section 15 |
| AP-TS-03 | Retry until green | Hides real nondeterminism | Fix or remove the test |
| AP-TS-04 | Mocking the thing under test, or mocking the database in an Integration test | Verifies the mock | Use real dependencies at that level |
| AP-TS-05 | Happy path only | Risks live in the negative paths | Add negative and abuse-case tests |
| AP-TS-06 | Asserting the return value only for a state-changing operation | Side Effects unverified | Apply section 9 |
| AP-TS-07 | Weakening an assertion to make a test pass | Silent loss of detection | Treat as test removal |
| AP-TS-08 | Accepting AI-generated tests because they pass | Passing is not validation | Validate per AI_RELIABILITY_STANDARD.md, then human approval |
| AP-TS-09 | Shared mutable test data | Order dependence and flakiness | Per-test data |
| AP-TS-10 | Fixing a defect without a reproduction test | No proof and no regression protection | Apply section 14 |

## 20. Governance References and Related Documents

| Document | Relationship |
|---|---|
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Applies this strategy to AI-assisted work |
| [RISK_MODEL.md](RISK_MODEL.md) | Supplies severity and the risk-to-test mapping used in section 6 |
| [QUALITY_GATES.md](QUALITY_GATES.md) | Defines every gate and threshold this strategy requires |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Details section 10 |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Validation rules for AI-produced tests |
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Human oversight, four-eyes principle, escalation levels |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Agents that assist with the activities here |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Incident learning flow used in section 14 |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Skills behind test design, mutation and failure analysis |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Stores defect, incident and test-pattern knowledge |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Heuristics and decision rules |

## 21. Baseline and Assumptions

Baseline (from the approved source document): purpose, testing philosophy, confidence over coverage, risk-based testing, test levels, deterministic tests, side effects, security, mutation testing, CI/CD gates, anti-patterns, traceability, defect reproduction, test removal policy. The six principles come from the documentation brief.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-TS-01 | The five Test Levels in section 7 | The source names "test levels" but does not list them |
| A-TS-02 | Code Coverage is diagnostic only and never a gate | Follows from "confidence over coverage"; the source does not state it outright |
| A-TS-03 | Flaky Rate target below 1% | No number in the source |
| A-TS-04 | The four allowed reasons for test removal and the extra QE Lead approval | The source names the policy but gives no rules |
| A-TS-05 | Unassessed requirements default to High | Conservative default in line with principle 1 |
| A-TS-06 | Role names (QE Lead, Tech Lead, Security Lead, Product Owner) | The source names no human roles |

## Supporting Documents

Operational documents that implement this one: [ROLE_REGISTER.md](ROLE_REGISTER.md) (who holds each role), [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) (how each gate is enforced), [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) (required records), [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) (incident procedure), [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) (test data, identities and environments).
