# RISK MODEL

| Field | Value |
|---|---|
| Document ID | QE-DOC-08 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 19 |
| Owner | QE Lead |
| Approvers | QE Lead, Security Lead, Product Owner |
| Review cadence | Quarterly, and after any Critical incident |
| Last updated | 2026-11-13 |

## 1. Purpose

This document defines how risks are identified, classified, scored, owned, accepted and mapped to tests. It supplies the severity scale that every other framework document uses. No other document defines its own severity scale.

## 2. Scope

Product risks (things that can go wrong in the system) and AI process risks that affect product quality. AI process risks are registered in the AI Risk Register in [AI_GOVERNANCE.md](AI_GOVERNANCE.md) using the scale defined here. Project schedule and budget risks are out of scope.

## 3. Definitions

| Term | Definition |
|---|---|
| Risk | A possible event that would harm users, data, the organization or its compliance position, recorded with an ID (RSK-n). |
| Impact | The harm if the risk occurs, rated 1 to 4. |
| Likelihood | How probable the risk is, given the current design and controls, rated 1 to 4. |
| Severity | Critical, High, Medium or Low, derived from Impact and Likelihood by the matrix in section 6. |
| Risk Confidence | How well the Impact and Likelihood ratings are supported by Evidence: High, Medium or Low. |
| Risk Appetite | The amount of residual risk the organization accepts per Severity, and who may accept it. |
| Mitigation | A control in the design or code that reduces Impact or Likelihood. |
| Residual Risk | The risk remaining after Mitigation. |
| Risk Acceptance | A recorded, time-limited human decision to release with a Residual Risk. |
| Abuse Case | A description of how an actor misuses a legitimate feature to cause harm. |
| Threat | A potential cause of a security Risk, identified through threat modeling. |
| Risk Coverage | Defined in [TEST_STRATEGY.md](TEST_STRATEGY.md) section 3. |

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| QE Lead | Owns this model and the risk register's integrity |
| Product Owner | Owns business risks; rates Impact for business harm; accepts within section 9 limits |
| Security Lead | Owns security risks and threat models; accepts security risks within section 9 limits |
| Tech Lead | Owns technical risks; rates Likelihood; implements Mitigations |
| Risk Owner | The named human accountable for one Risk from identification to closure |
| Risk Agent | Proposes risks, ratings and mappings (see [AGENTS_CATALOG.md](AGENTS_CATALOG.md) AGT-03). Proposals are not ratings until a human approves them. |

Every Risk has exactly one Risk Owner, and the Risk Owner is always a human.

## 5. Risk Categories

| Code | Category | Examples in the reference system |
|---|---|---|
| FUN | Functional correctness | Double booking of an appointment slot |
| SEC | Security | Missing role check on an admin endpoint |
| PRV | Privacy and data protection | A Patient reads another patient's record |
| DAT | Data integrity | Deleting a doctor orphans appointments |
| REL | Reliability and availability | Database unreachable and readiness still reports healthy |
| PRF | Performance and capacity | Unbounded list endpoint |
| INT | Integration | Token audience mismatch between Keycloak and the API |
| CMP | Compliance and auditability | State change without an audit trail |
| OPS | Operational | Migration that cannot be rolled back |
| AIQ | AI-induced quality | A hallucinated requirement leads to tests for behavior that does not exist |

## 6. Severity Matrix

Impact scale:

| Rating | Impact | Guide |
|---|---|---|
| 4 | Severe | Harm to a person's safety or care, large exposure of sensitive personal data, regulatory breach, loss of data integrity that cannot be repaired |
| 3 | Major | Exposure or corruption of some users' data, a core function unavailable, privilege escalation |
| 2 | Moderate | A function degraded with a workaround, limited incorrect data that can be corrected |
| 1 | Minor | Cosmetic or inconvenient, no data or security effect |

Likelihood scale:

| Rating | Likelihood | Guide |
|---|---|---|
| 4 | Almost certain | Reachable in normal use, or trivially exploitable by any authenticated user |
| 3 | Likely | Reachable in common edge cases, or exploitable with basic skill |
| 2 | Possible | Needs unusual conditions or specific knowledge |
| 1 | Rare | Needs several independent failures or privileged access |

Severity = matrix lookup:

| Impact \ Likelihood | 1 Rare | 2 Possible | 3 Likely | 4 Almost certain |
|---|---|---|---|---|
| 4 Severe | High | Critical | Critical | Critical |
| 3 Major | Medium | High | High | Critical |
| 2 Moderate | Low | Medium | Medium | High |
| 1 Minor | Low | Low | Low | Medium |

Overrides, applied after the lookup:

- Any Risk in category PRV or SEC with Impact 4 is at least High, whatever the Likelihood.
- A Risk with Risk Confidence Low is treated as one Severity level higher until the confidence is raised (section 8).

## 7. Threat Modeling, STRIDE and Abuse Cases

### 7.1 When threat modeling is required

A threat model is created or updated when a change adds or alters: an endpoint, an authentication or authorization rule, a trust boundary, a data store holding sensitive data, or an external integration.

### 7.2 Method

1. Draw the data flow for the change: actors, processes, stores, trust boundaries.
2. For each element crossing a trust boundary, ask the six STRIDE questions.
3. Record each credible Threat as a Risk in category SEC or PRV and rate it.
4. Write at least one Abuse Case for every Critical or High Threat.
5. Map each Risk to tests (section 11). Security test requirements are in [SECURITY_TESTING.md](SECURITY_TESTING.md).

### 7.3 STRIDE

| Letter | Threat | Property violated | Example question |
|---|---|---|---|
| S | Spoofing | Authentication | Can a caller present a token from another realm or audience? |
| T | Tampering | Integrity | Can a caller change a field they should not, such as `userId` on a record? |
| R | Repudiation | Non-repudiation | Can a state change happen without an attributable record? |
| I | Information disclosure | Confidentiality | Does an error or a list response reveal other users' data? |
| D | Denial of service | Availability | Can one caller exhaust a resource? |
| E | Elevation of privilege | Authorization | Can a Patient reach a Staff-only operation? |

### 7.4 Abuse Case format

```
ID:            ABU-n
Actor:         who misuses (role, authenticated or not)
Goal:          the harm they intend
Feature used:  the legitimate feature
Steps:         how
Expected:      the system's required response
Linked risk:   RSK-n
Linked tests:  test IDs
```

Example:

```
ID:            ABU-1
Actor:         authenticated user with only the Patient role
Goal:          read another patient's record
Feature used:  GET /api/Patient/{id}
Steps:         request an id that is not linked to the caller's account
Expected:      403, no record data in the body, attempt logged
Linked risk:   RSK-1 (PRV, Impact 4, Likelihood 3, Critical)
Linked tests:  BOLA test for GET /api/Patient/{id}
```

## 8. Risk Confidence

| Level | Meaning | Effect |
|---|---|---|
| High | Ratings rest on direct Evidence: code review, a threat model, test results, incident history | Severity used as rated |
| Medium | Ratings rest on analogy or partial Evidence | Severity used as rated; a confidence-raising action is recorded |
| Low | Ratings rest on assumption, or on AI output not yet verified by a human | Treated as one Severity level higher; must be raised or re-rated before release |

A rating proposed by an agent starts at Risk Confidence Low and can only be raised by a human who records the Evidence.

## 9. Risk Appetite and Acceptance

| Severity | Appetite | Who may accept Residual Risk | Maximum acceptance period |
|---|---|---|---|
| Critical | None. Must be mitigated before release. | Not acceptable | Not applicable |
| High | Very low | Product Owner and Tech Lead jointly; Security Lead as well for SEC and PRV | 30 days |
| Medium | Low | Tech Lead | 90 days |
| Low | Moderate | Risk Owner | Reviewed at the next quarterly review |

Rules:

- A Risk Acceptance records: the Risk, the reason, the compensating controls, the acceptors, and the expiry date.
- An expired acceptance is the same as no acceptance and fails release gate G-REL-02 in [QUALITY_GATES.md](QUALITY_GATES.md).
- The person accepting may not be the author of the change that carries the Risk (four-eyes principle, [AI_GOVERNANCE.md](AI_GOVERNANCE.md) section 8).
- An AI agent can never accept a Risk.

## 10. Risk Lifecycle and Register

States: Identified, Assessed, Mitigated, Accepted, Closed. A Risk may return to Assessed at any time.

Register fields:

| Field | Content |
|---|---|
| ID | RSK-n |
| Title and description | What can happen and to whom |
| Category | Section 5 code |
| Source | Requirement, threat model, incident, review, or agent proposal |
| Impact, Likelihood, Severity | Section 6 |
| Risk Confidence | Section 8, with the Evidence link |
| Risk Owner | Named human |
| Mitigation | Link to design or code |
| Linked tests | Test IDs, for Risk Coverage |
| State | Lifecycle state |
| Acceptance | Acceptors and expiry, if any |

## 11. Risk-to-Test Mapping

This table is binding for [TEST_STRATEGY.md](TEST_STRATEGY.md) section 6. Thresholds are enforced by [QUALITY_GATES.md](QUALITY_GATES.md).

| Severity | Risk Coverage required | Test Levels | Required test kinds | Mutation threshold on mitigating code |
|---|---|---|---|---|
| Critical | 100% | Two or more independent levels | Positive, negative, Abuse Case, Side Effect checks | G-MUT-01 Critical tier |
| High | 100% | One or more | Positive, negative | G-MUT-01 High tier |
| Medium | G-BEH-02 Medium threshold | One | Positive, main negative | G-MUT-01 Medium tier |
| Low | No minimum | Any | As judged useful | None |

A test counts toward Risk Coverage only if it is linked to the Risk and has been shown to fail when the Mitigation is removed (by mutation result or by a recorded manual check).

## 12. Inputs and Outputs

| Inputs | Outputs |
|---|---|
| Approved requirements | Risk register entries |
| Architecture, API inventory, data flows | Threat models and Abuse Cases |
| Incident and defect knowledge from [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Risk-to-test mapping |
| Risk Agent proposals | Risk Acceptances |

## 13. Metrics

| Metric | Definition | Target |
|---|---|---|
| Risk Coverage (Critical, High) | Per TEST_STRATEGY.md | 100% |
| Unowned risks | Risks without a Risk Owner | 0 |
| Low-confidence Critical or High risks | Count at release | 0 |
| Expired acceptances | Count | 0 |
| Escaped-risk rate | Incidents whose cause was not in the register, divided by all incidents | Trending down |
| Risk assessment lead time | Requirement approved to Risk assessed | Tracked |

## 14. Review Process

- New and re-rated Critical and High risks are reviewed by the Risk Owner and one other qualified human.
- The register is reviewed in full each quarter and before each release.
- An incident whose cause was not in the register triggers a review of the category and of the method that missed it.

## 15. Best Practices

- Rate Impact from the victim's point of view, not the implementer's.
- Write the Risk as an event with a consequence: "A Patient reads another patient's record", not "authorization".
- Prefer splitting a broad Risk into testable ones.
- Re-rate after a Mitigation; do not assume it worked until a linked test shows it.

## 16. Anti-Patterns

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-RM-01 | Rating by convenience: lowering Severity to pass a gate | Hides real exposure | Re-rating needs the same review as the original rating |
| AP-RM-02 | Permanent acceptance | Risk never revisited | Every acceptance expires |
| AP-RM-03 | Unowned or team-owned risks | Nobody is accountable | One named human owner |
| AP-RM-04 | Treating an agent's proposed rating as assessed | Unverified AI output presented as fact | Starts at Risk Confidence Low |
| AP-RM-05 | Risk register written after the tests | Tests drive risks instead of the reverse | Risk assessment precedes test design |
| AP-RM-06 | Counting tests instead of Risk Coverage | Quantity masks gaps | Report Risk Coverage per Severity |
| AP-RM-07 | Threat model done once | Design drifts from the model | Update on the triggers in section 7.1 |
| AP-RM-08 | Mitigation assumed effective without a test | False confidence | Linked test must fail when the Mitigation is removed |

## 17. Escalation Rules

| Trigger | Escalate to | Level |
|---|---|---|
| New Critical Risk identified | Risk Owner, QE Lead, and Security Lead for SEC or PRV | L2, same business day |
| Critical Risk without Mitigation at release candidate | Release Approver | L3, release blocked |
| Disagreement on a rating | QE Lead; Security Lead decides for SEC and PRV | L2 |
| Acceptance about to expire (7 days) | Acceptors and Risk Owner | L1 |
| Agent output conflicts with the register | Risk Owner | L1 |

Levels are defined in [AI_GOVERNANCE.md](AI_GOVERNANCE.md) section 9.

## 18. Traceability, Governance References and Related Documents

Chain: Requirement, Risk, Mitigation, Test, Result, Gate decision, Acceptance if any. Each link is stored in the register.

| Document | Relationship |
|---|---|
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Consumes Severity and the mapping in section 11; owns the term Risk Coverage |
| [QUALITY_GATES.md](QUALITY_GATES.md) | Enforces thresholds and acceptance rules |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Uses this Severity scale; details tests for SEC and PRV risks |
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | AI Risk Register, four-eyes principle, escalation levels |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Reliability of agent-proposed risks |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Risk Agent, Security Agent, BOLA Agent |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | How risk output flows to test design |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Risk Analysis skill (SKL-02) |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Risk and incident knowledge |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Lifecycle stage 3 |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Risk heuristics |

## 19. Baseline and Assumptions

Baseline (from the approved source document): risk categories, severity matrix, threat modeling, STRIDE, abuse cases, risk appetite, risk confidence, ownership, risk-to-test mapping.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-RM-01 | The ten categories in section 5 | The source names "risk categories" without listing them |
| A-RM-02 | 4 by 4 Impact and Likelihood scales and the matrix cells | The source names a severity matrix without values |
| A-RM-03 | Four Severity levels named Critical, High, Medium, Low | Not stated in the source |
| A-RM-04 | Low Risk Confidence raises effective Severity by one level; agent ratings start Low | Derived from "evidence over assumptions"; the rule itself is new |
| A-RM-05 | Appetite table: acceptors and 30 and 90 day limits | The source names risk appetite without values |
| A-RM-06 | Examples are taken from the HospitalManagement reference system | The source names no system; SECURITY_TESTING mentions Keycloak, which that system uses |
| A-RM-07 | Impact 4 examples include harm to a person's care | Follows from the reference system's domain |

## Supporting Documents

Operational documents that implement this one: [ROLE_REGISTER.md](ROLE_REGISTER.md) (who holds each role), [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) (how each gate is enforced), [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) (required records), [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) (incident procedure), [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) (test data, identities and environments).
