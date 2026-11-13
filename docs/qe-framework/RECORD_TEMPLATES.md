# RECORD TEMPLATES

| Field | Value |
|---|---|
| Document ID | QE-DOC-15 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 9 |
| Owner | QE Lead |
| Approvers | QE Lead, AI Governance Owner, Security Lead |
| Review cadence | Quarterly, and whenever a document that requires a record changes |
| Last updated | 2026-11-13 |

## 1. Purpose

The framework requires records: acceptances, waivers, validations, audits, abuse cases, knowledge items and others. A record that is missing a field cannot serve as Evidence. This document gives one template for each required record, so that records are complete, comparable and auditable.

## 2. Scope

Ten templates. Six were listed as missing in the review report (Risk Acceptance, Waiver, Validation record, Audit record, Abuse Case, Knowledge Item). Four more are included because a gate cannot be passed without them: AI Contribution Declaration (G-PR-04), Test Removal record (G-PR-05), Risk register entry (G-BEH-02, G-REL-02) and Release record (G-REL-03). The Incident record is in [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md).

## 3. Definitions

| Term | Definition |
|---|---|
| Record | A completed template stored where section 5 says, with an ID. |
| Required field | A field that must be filled for the Record to be valid. All fields are required unless marked optional. |
| Immutable | Not edited after approval. A correction is a new Record that supersedes the old one. |
| Expiry | The date after which a Record no longer has effect. |

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| QE Lead | Owns the templates and their consistency with the core documents |
| Record author | Completes every Required field truthfully |
| Approver named in the template | Checks completeness before approving; an incomplete Record is not approved |
| AI Governance Owner | Samples Records monthly for completeness and for evidence of real review |

## 5. Rules for All Records

1. A Record with an empty Required field is invalid and has no effect on any gate.
2. "Not applicable" is allowed only with a reason.
3. Names are named humans. A role name alone, a team name or an agent is not accepted in a field that asks who decided.
4. Dates are ISO 8601. Times are UTC.
5. Approved Records are Immutable.
6. Records never contain Restricted data: no credentials, tokens or real personal data. Refer to data by synthetic identifier.
7. An agent may draft a Record. A human reviews every field and is the author of record. Approval fields are never filled by an agent.
8. Storage and IDs:

| Record | ID format | Stored |
|---|---|---|
| Risk register entry | RSK-n | Risk register |
| Risk Acceptance | RA-n | With the risk register; linked from the Risk |
| Waiver | WVR-n | Waiver log; linked from the pull request or release |
| Abuse Case | ABU-n | With the threat model; linked from the Risk |
| AI Contribution Declaration | In the pull request | Pull request description |
| Validation record | VAL-n | Audit store; linked from the Output |
| Audit record | AUD-n | Audit store, append-only |
| Test Removal record | In the pull request | Pull request description |
| Knowledge Item | KNW-n | Knowledge catalog |
| Release record | REL-n | Release log with the Evidence pack |

## 6. Templates

### 6.1 Risk register entry

Required by [RISK_MODEL.md](RISK_MODEL.md) section 10.

```
ID:                 RSK-
Title:              (an event with a consequence, for example "A Patient reads another patient's record")
Description:        (what can happen, to whom, and the harm)
Category:           FUN | SEC | PRV | DAT | REL | PRF | INT | CMP | OPS | AIQ
Source:             requirement ID | threat model | incident ID | review | agent proposal (Output ID)
Impact:             1-4, with reason
Likelihood:         1-4, with reason
Severity:           Critical | High | Medium | Low   (matrix lookup, then overrides)
Risk Confidence:    High | Medium | Low
Evidence for rating:(links; required for Medium and High confidence)
Effective Severity: (one level higher if Risk Confidence is Low)
Risk Owner:         (named human)
Rated by:           (named human)
Second reviewer:    (named human, different; required for Critical and High)
Mitigation:         (link to design or code) | none yet
Linked tests:       (test IDs and Test Levels)
Fail-on-removal evidence: (mutation report link or recorded manual check; required to count toward Risk Coverage)
Linked Abuse Cases: ABU-   (required for Critical and High security Threats)
State:              Identified | Assessed | Mitigated | Accepted | Closed
Acceptance:         RA-    (if any)
Last reviewed:      (date)
```

### 6.2 Risk Acceptance

Required by RISK_MODEL.md section 9. Not available for Critical risks.

```
ID:                   RA-
Risk:                 RSK-    Severity:            Risk Confidence:
Residual Risk accepted: (what remains, in plain terms)
Reason for accepting: (why mitigation before release is not possible or not proportionate)
Compensating controls:(what limits the harm meanwhile, and how each is verified)
Change carrying the risk: (pull request or release)   Author:
Acceptors:            (named humans, per the appetite table; none may be the author above)
                      High:   Product Owner + Tech Lead (+ Security Lead for SEC, PRV)
                      Medium: Tech Lead
                      Low:    Risk Owner
Date accepted:
Expiry:               (at most 30 days for High, 90 days for Medium)
Plan to remove the risk: (action, owner, date)
Renewal of:           RA-    (optional; a renewal states what changed since the last acceptance)
```

Checks before approval: the Risk is not Critical; Risk Confidence is not Low for a High risk; the expiry is within the limit; no acceptor is the author.

### 6.3 Waiver

Required by [QUALITY_GATES.md](QUALITY_GATES.md) section 7. Only for gates marked waivable.

```
ID:                   WVR-
Gate:                 G-          (confirm the gate is waivable for this case)
Applies to:           (pull request or release, with revision)
Failing result:       (link to the Evidence showing the failure)
Reason:               (why the gate cannot be met now)
Risk introduced:      RSK-        (a risk entry is required)
Compensating control: (what is done instead, and how it is verified)
Requester:            (named human)
Approvers:            (gate owner + one of QE Lead, Tech Lead, Security Lead; neither is the requester)
Date approved:
Expiry:               (at most 14 days)
Action to pass the gate: (action, owner, date)
Renewal of:           WVR-   (optional; one renewal only, with QE Lead approval)
```

### 6.4 Abuse Case

Required by RISK_MODEL.md section 7.4 and [SECURITY_TESTING.md](SECURITY_TESTING.md) section 12.

```
ID:            ABU-
Actor:         (role; authenticated or not; what they legitimately have)
Goal:          (the harm they intend)
Feature used:  (the legitimate feature or endpoint)
Family:        access to others' data | privilege gain | business rule bypass | enumeration |
               resource exhaustion | repudiation | deactivated or stale identity
Steps:         (how, in order)
Expected:      (the system's required response: status, no data, no state change, logging)
Linked risk:   RSK-
Linked tests:  (test IDs, with security case IDs such as BOLA-02)
Author:        (named human)
Reviewed by:   (Security Lead or delegate)
```

### 6.5 AI Contribution Declaration

Required by G-PR-04. Part of the pull request description.

```
AI-produced content in this change:   none | yes
If yes, for each contribution:
  Files or sections:
  Agent or tool:        AGT-   (or the approved tool name)
  Approved use case:    UC-
  Output ID:
  Validation record:    VAL-    Result: pass
  Adopted by:           (named human who ran the agent and reviewed the Output)
  What I changed after generation: (summary, or "nothing")
  What I verified myself and how:  (for example "ran each new test against a broken implementation")
Second approver:        (named human, different from "Adopted by")
```

A declaration of "none" is a statement by the author and is audited by sample.

### 6.6 Validation record

Required by [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) section 9.

```
ID:               VAL-
Output ID:                     Producer: AGT-      Prompt version:
Validated by:     AGT-11 and automated checks   (not the Producer)
Date and time:
Checks:
  V1 Structure complete:                   pass | fail   (missing items)
  V2 References exist:                     pass | fail   (each unresolved reference)
  V3 Evidence supports each Claim:         pass | fail   (Claims not supported)
  V4 No contradiction with approved sources: pass | fail (source and conflict)
  V5 Executable Outputs run; tests fail when behavior is broken: pass | fail | not applicable (reason)
  V6 No restricted data:                   pass | fail
  V7 Output Confidence consistent:         pass | fail   (declared, permitted ceiling, limiting input)
Evidence Completeness:  (Claims with valid Evidence / all Claims)
Suspected Hallucinations: (each, with why)
Result:           pass | fail   (first failed check)
Human sample:     not sampled | sampled by (named human), agrees | disagrees (detail)
```

A pass is not an approval. The human reviewer's decision is recorded in the Audit record.

### 6.7 Audit record

Required by [AI_GOVERNANCE.md](AI_GOVERNANCE.md) section 13. Generated automatically at the time of the action; append-only.

```
ID:                 AUD-
Output ID:                         Timestamp:
Agent:              AGT-           Model identifier:           Prompt version:
Skill applied:      SKL-
Input reference:    (link)
Knowledge Items:    KNW- with versions
Output:             (immutable link)
Output Confidence:  High | Medium | Low     Limiting input:
Evidence links:
Validation record:  VAL-
Human review:
  Reviewer:         (named human)
  Review depth:     full | sampled (what was sampled)
  Decision:         approved | rejected
  Material changes: (summary)
  Hallucinations confirmed: (count and description)
  Time of decision:
Second approver:    (named human; where the Four-eyes principle requires)
Effect:             (the change, risk entry, gate input or proposal this Output fed)
Escalations:        (level, trigger, outcome)
Classification:     (highest data class contained)
```

### 6.8 Test Removal record

Required by [TEST_STRATEGY.md](TEST_STRATEGY.md) section 15 and G-PR-05. Part of the pull request description. Covers removed, skipped and weakened tests.

```
Tests removed, skipped or weakened in this change:  none | listed below
For each:
  Test:                (name)
  Kind:                removed | skipped | assertion loosened | case deleted | tolerance widened
  What it protected:   (AC-, RSK-, defect ID, or "nothing traceable")
  Allowed reason:      behavior removed or changed by approved requirement |
                       duplicate with equal or greater detection |
                       cannot fail for any realistic defect |
                       flaky, replaced by deterministic equivalent
  Evidence:            (requirement link | other test + mutation result | mutation or review evidence | replacement test)
  Proposed by:         (named human; note if an agent suggested it)
Approved by:           (reviewer, not the author)
QE Lead approval:      (required if the test protected a Critical or High risk or reproduced a defect)
```

### 6.9 Knowledge Item

Required by [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) section 6.

```
ID:                 KNW-
Title:              (one line)
Type:               K-DOM | K-REQ | K-RSK | K-TST | K-DEF | K-SEC | K-PRM | K-DEC | K-STD
Content:            (short, checkable statements)
Source:             (link to primary source, or named authority)
Item Owner:         (named human)
Classification:     Public | Internal | Confidential     (Restricted material is never stored)
Knowledge Confidence: Verified | Corroborated | Unverified
Basis for confidence: (what was checked against what, by whom, when)
State:              Proposed | Under Review | Active | Stale | Deprecated | Archived | Rejected
Version:
Proposed by:        (named human; note if drafted by an agent, with Output ID)
Admitted by:        (Knowledge Owner or delegate, not the proposer)
Last reviewed:                     Review record: (what was compared with what)
Next review due:    (per the freshness interval for the type)
Supersedes / superseded by: KNW-   (optional)
Related:            RSK-, REQ-, ABU-, INC-   (optional)
```

### 6.10 Release record

Required by G-REL-03.

```
ID:                  REL-
Revision:            (exact commit)
Changes included:    (list or link)
Gate status at this revision (per gate family):
  Enforced:            (gate IDs)
  Interim control:     (gate IDs, with where each result is recorded)
  Not applied:         (gate IDs, with reason)   -- must be empty unless the Release Approver accepts each in writing
Gate results:        (links, all computed on the revision above)
Open Waivers:        WVR-  with expiry dates
Risk position:       unmitigated Critical: 0 | open High with acceptance: RA- | Low-confidence Critical or High: 0
AI contributions:    (agents, Trust Levels, Accountable humans; any T0 Output and its re-verification)
Open incidents:      INC-  (and any open escaped-Hallucination incident)
Knowledge closure:   (incidents closed since last release, each with KNW- and reproduction test)
Known limitations:   (what was not tested and why)
Decision:            approved | refused
Release Approver:    (named human; not the majority author of the changes)
Date and time:
```

## 7. Metrics

| Metric | Target |
|---|---|
| Sampled Records with all Required fields complete | 100% |
| Records with a role or team in place of a named human | 0 |
| Records edited after approval | 0 |
| Expired Risk Acceptances and Waivers still relied on | 0 |
| AI Contribution Declarations found false in audit | 0 |
| Records containing Restricted data | 0 |

## 8. Anti-Patterns, Escalation and Related Documents

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-RT-01 | Boilerplate answers copied between Records | The Record shows a form was filled, not that a decision was made | Reject on review; audit sample |
| AP-RT-02 | Record written after the decision | It may not match what happened | Records are created before the decision takes effect |
| AP-RT-03 | "N/A" without a reason | Hides an unanswered question | Rule 2 |
| AP-RT-04 | Role name in a "who" field | No person is accountable | Rule 3 |
| AP-RT-05 | Renewal with unchanged text | Acceptance by habit | Renewal states what changed |
| AP-RT-06 | Agent-completed approval fields | AI autonomy over human accountability | Rule 7; L3 |
| AP-RT-07 | Free-form records in place of the template | Fields get left out | Template required |
| AP-RT-08 | "None" declared for AI content by default | G-PR-04 bypassed | Sampled audit; L2 on a false declaration |

Escalation: an invalid Record relied on for a gate is L2 to the gate owner; a falsified Record or an agent-filled approval is L3. Levels are defined in AI_GOVERNANCE.md section 9.

| Document | Relationship |
|---|---|
| [RISK_MODEL.md](RISK_MODEL.md) | Templates 6.1, 6.2, 6.4 |
| [QUALITY_GATES.md](QUALITY_GATES.md) | Templates 6.3, 6.5, 6.8, 6.10 |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Template 6.6 |
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Template 6.7; Four-eyes principle; data rules |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Template 6.9 |
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Template 6.8 |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Template 6.4 |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Agent IDs used in the Records |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Skill IDs used in the Audit record |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Hand-offs that carry these Records |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Stage exits that produce Records |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Review practices for Records |
| [ROLE_REGISTER.md](ROLE_REGISTER.md) | Who may sign each Record |
| [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) | Where Records are checked automatically |
| [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) | Incident record |
| [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) | Synthetic data rule for Records |

## 9. Baseline and Assumptions

Baseline: every field comes from a requirement in a core document (the field lists in RISK_MODEL section 10, QUALITY_GATES section 7, AI_RELIABILITY_STANDARD sections 5 and 9, AI_GOVERNANCE section 13, KNOWLEDGE_CATALOG section 6, TEST_STRATEGY section 15).

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-RT-01 | ID formats and storage locations | Not in any source |
| A-RT-02 | The four additional templates (6.1, 6.5, 6.8, 6.10) | Not listed as missing; added because their gates need a record |
| A-RT-03 | Fields beyond the core documents' lists: "What I verified myself", "Fail-on-removal evidence", "Plan to remove the risk", "Known limitations", gate status by Enforced, Interim control and Not applied | Added so each Record carries its own evidence and states its limits |
| A-RT-04 | Records are immutable and corrections supersede | Derived from the audit rules; stated here for all Records |
