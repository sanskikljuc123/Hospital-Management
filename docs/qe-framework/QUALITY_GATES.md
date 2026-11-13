# QUALITY GATES

| Field | Value |
|---|---|
| Document ID | QE-DOC-07 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 16 |
| Owner | QE Lead |
| Approvers | QE Lead, Tech Lead, Security Lead, Release Approver |
| Review cadence | Quarterly, and after any gate that passed a change later found defective |
| Last updated | 2026-11-13 |

## 1. Purpose

This document is the single place where gates and thresholds are defined. Other documents state requirements and refer to gate IDs here. A threshold stated anywhere else has no force.

## 2. Scope

All changes merged to a protected branch and all releases. Gates apply equally to human-written and AI-assisted changes; AI-assisted changes have additional gates.

## 3. Definitions

| Term | Definition |
|---|---|
| Quality Gate | A check with a defined pass condition, an owner and a consequence, which a change or release must pass to proceed. |
| Blocking gate | A gate whose failure stops the pipeline or the release. All gates in this document are blocking unless marked otherwise. |
| Waiver | A recorded, time-limited human decision to proceed despite a failed gate (section 7). |
| Evidence pack | The set of gate results and approvals presented for a release decision. |
| Non-waivable | A gate that can never be waived. |

Behavior Coverage, Risk Coverage, Mutation Score, Deterministic Test and Flaky Test are defined in [TEST_STRATEGY.md](TEST_STRATEGY.md). Severity is defined in [RISK_MODEL.md](RISK_MODEL.md). Trust Level and Validation are defined in [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md).

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| QE Lead | Owns the gate definitions and thresholds; approves Waivers for test, behavior and mutation gates |
| Security Lead | Owns security gates; approves Waivers for waivable security gates |
| Tech Lead | Owns pipeline enforcement; co-approves Waivers |
| Knowledge Owner | Owns knowledge gates |
| Release Approver | Makes the release decision on the Evidence pack; always a human |
| Agents | Coverage Agent (AGT-08) and PR Review Agent (AGT-09) compute and report gate inputs. Agents never pass, fail or waive a gate. |

## 5. Gate Catalog

### 5.1 PR gates

| ID | Gate | Pass condition | Waivable |
|---|---|---|---|
| G-PR-01 | Build and tests | Build succeeds; all tests pass; no test skipped or quarantined without a linked ticket | No |
| G-PR-02 | Human review | Approved by at least one qualified human who is not the author (four-eyes principle). Two approvers for changes touching Critical risk areas or authorization code. | No |
| G-PR-03 | Traceability | The change links to a requirement, risk or defect; new and changed tests name what they protect | Yes |
| G-PR-04 | AI-produced content | Every AI-produced artifact in the change is declared, has a passed Validation record, and is approved by a human | No |
| G-PR-05 | Test removal | Any removed, skipped or weakened test has a record and approval under TEST_STRATEGY.md section 15 | No |

### 5.2 Test gates

| ID | Gate | Pass condition | Waivable |
|---|---|---|---|
| G-TST-01 | Determinism | New and changed tests pass 3 consecutive runs in randomized order. No test retried to pass. A Flaky Test is fixed or removed within 5 working days and is tracked meanwhile. | Yes |
| G-TST-02 | Assertion quality | State-changing behaviors assert Side Effects; no assertion-free tests in the change | Yes |
| G-TST-03 | Defect reproduction | A defect fix includes a test shown to fail before the fix and pass after | Only by QE Lead with alternative Evidence |

### 5.3 Behavior coverage gates

| ID | Gate | Pass condition | Waivable |
|---|---|---|---|
| G-BEH-01 | Behavior Coverage | 100% of acceptance criteria for requirements carrying Critical or High risks; at least 90% for all others | Critical and High: No. Others: Yes |
| G-BEH-02 | Risk Coverage | Critical 100%; High 100%; Medium at least 80%; Low no minimum | Critical: No. High: only with a Risk Acceptance. Medium: Yes |

Code Coverage is reported for diagnosis and is not a gate.

### 5.4 Security gates

| ID | Gate | Pass condition | Waivable |
|---|---|---|---|
| G-SEC-01 | Authentication and authorization tests | Every new or changed endpoint has passing tests for: no token (401), invalid or expired token (401), each disallowed role (403), each allowed role (success) | No |
| G-SEC-02 | BOLA tests | Every new or changed endpoint that takes an object identifier has a passing test in which an authenticated caller requests an object they do not own and is refused, with no object data returned | No |
| G-SEC-03 | Open findings | No open Critical or High security finding. Medium findings have an owner and due date. | Critical: No. High: only with a Risk Acceptance including the Security Lead |
| G-SEC-04 | Leakage | No secrets, tokens or real personal data in code, test data, logs, error responses or prompts | No |

Details of each test are in [SECURITY_TESTING.md](SECURITY_TESTING.md).

### 5.5 Mutation thresholds

| ID | Gate | Pass condition | Waivable |
|---|---|---|---|
| G-MUT-01 | Mutation Score by risk tier | Code mitigating Critical risks, and all authorization logic: at least 80%. High: at least 70%. Medium: at least 60%. Other in-scope code: reported. | Critical tier: No. Others: Yes |
| G-MUT-02 | No regression | Mutation Score of changed in-scope files does not drop by more than 2 percentage points against the target branch | Yes |

Surviving mutants marked equivalent need a written reason approved by the reviewer.

### 5.6 Release gates

| ID | Gate | Pass condition | Waivable |
|---|---|---|---|
| G-REL-01 | Upstream gates | All PR, test, behavior, security and mutation gates are green on the exact release candidate revision; all Waivers are unexpired | No |
| G-REL-02 | Risk position | No unmitigated Critical risk. Every High Residual Risk is mitigated or has an unexpired Risk Acceptance. No Critical or High risk at Risk Confidence Low. | No |
| G-REL-03 | Human sign-off | The Release Approver signs the Evidence pack. The Release Approver is not the author of the majority of the release's changes. | No |
| G-REL-04 | AI reliability | No Output from a T0 agent is in the release unless fully re-verified by a human. No open escaped-Hallucination incident. | No |
| G-REL-05 | Knowledge closure | G-KNW-03 holds for all incidents closed since the last release | Yes |

### 5.7 Knowledge gates

| ID | Gate | Pass condition | Waivable |
|---|---|---|---|
| G-KNW-01 | Usable knowledge | A Knowledge Item used as Evidence or agent input is in state Active and within its freshness interval | No |
| G-KNW-02 | Admission | A new Knowledge Item has a source, owner, classification, Knowledge Confidence and review date, and is approved by the Knowledge Owner or delegate | No |
| G-KNW-03 | Incident learning | A Critical or High incident is not closed until its learning is recorded as a Knowledge Item and its reproduction test is merged | Yes |

Knowledge terms are defined in [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md).

## 6. Gate Evaluation Order

```
PR opened
  -> G-PR-01 build and tests
  -> G-TST-01..03, G-SEC-01..04 (automated parts)
  -> G-MUT-01..02
  -> G-BEH-01..02, G-PR-03, G-PR-05  (Coverage Agent and PR Review Agent report; values are computed from data, not from agent judgment)
  -> G-PR-04 (AI content Validation record present)
  -> G-PR-02 human review and approval
  -> merge

Release candidate
  -> G-REL-01 -> G-REL-02 -> G-REL-04 -> G-REL-05 -> G-REL-03 human sign-off
```

A gate result is Evidence and is stored with the revision it was computed on. A result computed on a different revision is not valid for the release candidate.

## 7. Waivers

1. Only gates marked waivable may be waived.
2. A Waiver records: gate ID, change or release, reason, risk introduced (with RSK ID), compensating control, expiry date, approvers.
3. Approvers: the gate owner and one other of QE Lead, Tech Lead or Security Lead. The requester cannot approve (four-eyes principle).
4. Maximum duration: 14 days. One renewal, with QE Lead approval.
5. An expired Waiver fails the gate again and fails G-REL-01.
6. Agents cannot request, approve or record a Waiver on their own. An agent may point out that a Waiver would be needed.
7. Waiver counts are a reported metric. A gate waived more than three times in a quarter is reviewed: either the threshold is wrong or the practice is.

## 8. Escalation Rules

| Trigger | Escalate to | Level |
|---|---|---|
| Non-waivable gate fails and the author disputes it | Gate owner | L2 |
| Gate owner unavailable for a Waiver | QE Lead | L2 |
| Release gate fails | Release Approver; release stops | L3 |
| Pressure to bypass a gate outside the Waiver process | AI Governance Owner and QE Lead | L3 |
| Gate found to have passed a defective change | QE Lead; gate reviewed | L2 |
| Gate tooling unavailable | Tech Lead. The gate is treated as failed, not skipped. | L2 |

Levels are defined in [AI_GOVERNANCE.md](AI_GOVERNANCE.md) section 9.

## 9. Inputs and Outputs

| Inputs | Outputs |
|---|---|
| CI results, mutation reports, scan reports | Gate results per revision |
| Traceability links, risk register | Waiver records |
| Validation records, Trust Levels | Evidence pack for release |
| Knowledge Item states | Release decision record |

## 10. Metrics

| Metric | Definition | Target |
|---|---|---|
| First-pass gate rate | Changes passing all gates on first attempt | Tracked |
| Waiver count | Per gate per quarter | At most 3 per gate |
| Expired Waivers open | Count | 0 |
| Gate escape rate | Defects traced to a change that passed all gates, divided by changes | Trending down |
| Time to green | Median time from PR opened to all gates passed | Tracked; never optimized by weakening a gate |
| Flaky quarantine age | Oldest quarantined test | At most 5 working days |

## 11. Review Process

- Thresholds are reviewed quarterly using the gate escape rate and the Waiver record.
- Raising a threshold needs QE Lead approval. Lowering a threshold needs all approvers in the header and a written reason.
- New gates are added here first, then referenced elsewhere.

## 12. Examples

A PR adds `PUT /api/User/{id}/link` (Admin links a patient record to a user account).

| Gate | What is checked |
|---|---|
| G-SEC-01 | Tests: no token 401; Doctor, Nurse and Patient roles 403; Admin success |
| G-SEC-02 | Not applicable as ownership, since Admin may link any user; the reviewer records why |
| G-TST-02 | After success, the link is read back; after a 403, the link is unchanged |
| G-BEH-02 | The risk "Patient linked to the wrong record" (Critical, PRV) has linked tests at two levels |
| G-MUT-01 | Authorization and linking logic at 80% or above |
| G-PR-02 | Two human approvers, because authorization code is touched |

## 13. Best Practices

- Fail closed. A gate that cannot be evaluated has failed.
- Keep a gate's pass condition objective enough that two people would reach the same result.
- Prefer fixing the change over waiving the gate.
- Show the failing Evidence to the author, not only the verdict.

## 14. Anti-Patterns

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-QG-01 | Gate theater: a gate that always passes | Produces green without Confidence | Review gates with no failures in two quarters |
| AP-QG-02 | Threshold lowering under schedule pressure | Trades reliability for speed against principle 1 | Section 11 approval rule |
| AP-QG-03 | Permanent or repeated Waivers | The gate no longer exists in practice | Section 7 limits |
| AP-QG-04 | Gaming the metric: excluding code from mutation scope to reach the threshold | Number rises, detection does not | Scope changes need QE Lead approval |
| AP-QG-05 | Agent verdict as gate result | AI autonomy over human accountability | Agents report inputs only |
| AP-QG-06 | Skipping a gate when tooling is down | Unverified change proceeds | Treat as failed |
| AP-QG-07 | Using Code Coverage as a gate | Coverage over confidence | Not a gate, by design |
| AP-QG-08 | Reusing gate results from another revision | Evidence does not match what ships | Results are per revision |
| AP-QG-09 | Self-approval through a second account or by an agent | Four-eyes bypass | Audit; L3 escalation |

## 15. Traceability, Governance References and Related Documents

Each gate result links to: revision, pipeline run, inputs used, and for human gates the approver. The release decision record links to the full Evidence pack.

| Document | Relationship |
|---|---|
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | States the requirements these gates enforce; defines coverage terms |
| [RISK_MODEL.md](RISK_MODEL.md) | Severity tiers and Risk Acceptance used by G-BEH-02, G-MUT-01, G-REL-02 |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Test detail for G-SEC gates |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Validation and Trust Levels used by G-PR-04, G-REL-04 |
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Four-eyes principle, audit, escalation levels |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Terms and states used by G-KNW gates |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Coverage Agent and PR Review Agent |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Where gate inputs come from |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Lifecycle stages 10 and 11 |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Mutation Testing skill (SKL-05) |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Decision rules for borderline gate cases |

## 16. Baseline and Assumptions

Baseline (from the approved source document): PR gates, test gates, behavior coverage, security gates, mutation thresholds, release gates, knowledge gates, escalation rules.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-QG-01 | Every numeric threshold in section 5 (90%, 80%, 70%, 60%, 2 points, 3 runs, 5 days) | The source names gate families without values |
| A-QG-02 | The individual gates within each family and their IDs | Not listed in the source |
| A-QG-03 | The Waiver process, its 14-day limit and which gates are non-waivable | The source does not mention waivers; a controlled exception path is needed so gates are not bypassed informally |
| A-QG-04 | G-PR-04 and G-REL-04 (AI gates) placed inside the PR and release families | The source has no AI gate family; AI_RELIABILITY_STANDARD needs an enforcement point |
| A-QG-05 | Two human approvers for Critical risk areas and authorization code | Not in the source |
| A-QG-06 | Role "Release Approver" | Not named in the source |

## Supporting Documents

Operational documents that implement this one: [ROLE_REGISTER.md](ROLE_REGISTER.md) (who holds each role), [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) (how each gate is enforced), [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) (required records), [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) (incident procedure), [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) (test data, identities and environments).
