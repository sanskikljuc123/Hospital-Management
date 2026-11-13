# INCIDENT MANAGEMENT

| Field | Value |
|---|---|
| Document ID | QE-DOC-16 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 17 |
| Owner | QE Lead |
| Approvers | QE Lead, Security Lead, AI Governance Owner, Tech Lead |
| Review cadence | Quarterly, and after every Critical incident |
| Last updated | 2026-11-13 |

## 1. Purpose

Several framework documents start from "an incident" without defining one. This procedure defines what an incident is, how it is classified, who drives it, how fast it must be handled, and when it may be closed. It is the entry point to the incident learning flow in [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) section 8, so that an incident ends in changed tests, risks and knowledge and not only in a fix.

## 2. Scope

Quality incidents, security incidents, AI incidents and process incidents as defined in section 5, in any environment. Infrastructure outages with no link to a defect or a control failure are handled by operations and are in scope only for the learning they yield.

## 3. Definitions

| Term | Definition |
|---|---|
| Incident | An event in which the system, or a control of this framework, did not behave as required and harm occurred or could have occurred. Identified as INC-n. |
| Escaped defect | A defect found after the change that introduced it passed all gates and was merged or released. |
| Escaped Hallucination | A Hallucination found after it reached a merged change or a gate decision ([AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md)). |
| Control failure | A gate, review, validation or governance rule that did not work as written, whether or not a defect resulted. |
| Near miss | A Control failure or defect caught by chance, outside the control meant to catch it. Recorded as an Incident. |
| Incident Owner | The named human who drives one Incident to closure. |
| Containment | Action that stops further harm. It is not the fix. |
| Missed-detection analysis | The analysis of which lifecycle stage and control should have caught the problem and why it did not. |
| Closure | The recorded decision that all closure conditions in section 9 are met. |

Severity is the scale in [RISK_MODEL.md](RISK_MODEL.md). There is no separate incident severity scale.

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| QE Lead | Owns this procedure; assigns the Incident Owner for quality and process incidents; accountable for the Missed-detection analysis |
| Security Lead | Assigns the Incident Owner and leads for security incidents; decides on disclosure handling |
| AI Governance Owner | Leads the AI-specific actions for AI incidents; decides on agent suspension and Trust Level |
| Incident Owner | Keeps the Incident record, coordinates steps, reports status, proposes closure |
| Engineer | Reproduces, fixes, writes the reproduction test |
| Risk Owner | Updates the risk register |
| Knowledge Owner | Admits the learning as a K-DEF Knowledge Item |
| Closure confirmer | A human other than the Incident Owner who confirms closure of Critical and High incidents |
| Agents | AGT-10 assists triage; AGT-04, 06, 07, 08, 03 assist as listed in the learning flow; AGT-11 validates their Outputs. None decides, closes or rates. |

## 5. Incident Types

| Type | Covers | Lead |
|---|---|---|
| Quality | Escaped defect; data corruption; wrong behavior in a released version | QE Lead |
| Security | Unauthorized access or disclosure; authentication or authorization defect in any environment; secrets or real personal data in code, tests, logs or prompts | Security Lead |
| AI | Escaped Hallucination; AI output that took effect without human approval; Restricted data sent to a model; agent used outside approved use cases | AI Governance Owner |
| Process | Control failure or Near miss: gate bypassed, Four-eyes principle breached, expired Waiver relied on, test removed without record | QE Lead |

An Incident may have more than one type. It then has one Incident Owner and each lead is accountable for their part.

## 6. Classification

Severity is assigned from actual or credible potential harm, using the Impact scale in RISK_MODEL.md section 6.

| Severity | Guide for incidents |
|---|---|
| Critical | Harm to a person's safety or care; exposure or corruption of sensitive personal data; unauthenticated or cross-user access confirmed; secrets exposed; Restricted data sent to a model; any Escaped Hallucination |
| High | A core function wrong or unavailable; privilege escalation limited in reach; a non-waivable gate bypassed; an AI output took effect without approval and no harm is yet found |
| Medium | A function degraded with a workaround; a Control failure caught before harm; a Near miss in a Critical or High risk area |
| Low | Minor deviation with no data or security effect; a Near miss elsewhere |

Rules:

- When Severity is uncertain, the higher level is used until Evidence supports the lower one.
- Severity may be lowered only by the lead for the type, with a recorded reason.
- Potential harm counts. A cross-user access defect found in a test environment is rated for what it would do in production.

## 7. Response Targets

| Severity | Acknowledge and assign Incident Owner | Containment | Fix with reproduction test | Learning complete (closure) |
|---|---|---|---|---|
| Critical | 1 hour | 4 hours | 2 working days | 10 working days |
| High | 4 hours | 1 business day | 10 working days | 15 working days |
| Medium | 1 business day | 3 working days | Next planned release | 30 days |
| Low | 3 working days | As planned | As planned | Reviewed in batch each quarter |

A missed target is escalated one level (section 12).

## 8. Procedure

Steps 2 to 10 are the incident learning flow in AGENT_INTERACTION_MATRIX.md section 8, with the detail needed to run it.

| # | Step | Action | Accountable | Output |
|---|---|---|---|---|
| 0 | Detect and report | Anyone who sees a possible Incident reports it at once. Reporting does not wait for certainty. Security incidents are reported privately to the Security Lead, not in public channels or pull request threads. | Reporter | Report |
| 1 | Record, classify, assign, contain | Open the Incident record; assign type, Severity and Incident Owner; contain. Containment options: roll back, disable the feature, revoke tokens or credentials, suspend the agent, block the release. | Lead for the type | Incident record; containment note |
| 2 | Triage | Preserve Evidence; reproduce; classify the failure; propose causes ranked by Evidence | Engineer (AGT-10 assists) | Triage report |
| 3 | Reproduce with a failing automated test | The test fails for the reported reason before any fix | Engineer (AGT-04 assists) | Reproduction test |
| 4 | Fix | Fix the cause; the reproduction test passes; normal gates apply. No gate is skipped because the change is urgent. | Engineer; reviewer | Merged fix |
| 5 | Missed-detection analysis | For each lifecycle stage and each relevant gate: should it have caught this, and why did it not | QE Lead (AGT-10, AGT-08 assist) | Analysis table |
| 6 | Update the risk register | Add or re-rate the Risk; link the new tests | Risk Owner (AGT-03 assists) | Register change |
| 7 | Close the gap class | Find and fix other instances of the same weakness; add tests for the class | QE engineer; Security Lead for security | New tests |
| 8 | Record the learning | K-DEF Knowledge Item admitted | Knowledge Owner (AGT-10 drafts) | Active Knowledge Item |
| 9 | Agent follow-up | If an agent contributed to the miss: Prompt regression case added; Trust Level reviewed; other Outputs of that agent since its last good measurement re-reviewed | Prompt Owner; AI Governance Owner | Prompt change; Trust Level decision |
| 10 | Control follow-up | If a gate or review passed the defect: the gate, its Mechanism or the review practice is changed | QE Lead | Gate or practice change |
| 11 | Close | Check the closure conditions; a second human confirms for Critical and High | Incident Owner; Closure confirmer | Closed Incident record |

Missed-detection analysis table (step 5), one row per stage of the lifecycle in [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md):

```
Stage | Could it have caught this? | Did it? | Why not | Agent involved | Human approver | Action
```

## 9. Closure Conditions

| Condition | Critical | High | Medium | Low |
|---|---|---|---|---|
| Containment verified | Required | Required | Required | Required |
| Reproduction test merged (G-TST-03) | Required | Required | Required | Where feasible |
| Fix merged and verified | Required | Required | Required or risk accepted | Required or risk accepted |
| Missed-detection analysis | Required | Required | Short form | Not required |
| Risk register updated | Required | Required | Required | Where relevant |
| Gap class addressed | Required | Required | Where found | Not required |
| K-DEF Knowledge Item Active (G-KNW-03) | Required | Required | Where a new learning exists | Not required |
| Agent follow-up, where an agent was involved | Required | Required | Required | Required |
| Control follow-up, where a control failed | Required | Required | Required | Recorded |
| Closure confirmed by a second human | Required | Required | Not required | Not required |

"Risk accepted" in place of a fix follows the acceptance rules in RISK_MODEL.md and is never available for Critical.

## 10. Incident Record

```
ID:                  INC-
Title:               (what happened, in one line)
Type:                Quality | Security | AI | Process   (one or more)
Severity:            Critical | High | Medium | Low      Reason:
Reported by:                                 Reported at:
Detected how:        (control that caught it, or by chance, or by a user)
Environment and revision:
Description:         (what was observed, what was expected; no Restricted data)
Harm:                actual:                 potential:
Incident Owner:      (named human)           Lead: (role and name)
Containment:         (action, by whom, at what time, how verified)
Timeline:            (key events with times)
Triage:              classification:         cause (confirmed):        Evidence:
Introduced by:       (change, with date)     Passed gates: (which, with results)
AI involvement:      none | agent AGT- , Output ID, Trust Level at the time, Accountable human
Reproduction test:   (test ID, link to the failing run before the fix)
Fix:                 (pull request)
Missed-detection analysis: (table or link)
Risk register:       RSK-  (new or re-rated)
Gap class actions:   (what else was checked and changed)
Knowledge Item:      KNW-
Agent follow-up:     (Prompt regression case, Trust Level decision, re-review result)
Control follow-up:   (gate or practice change)
Targets met:         acknowledge | containment | fix | closure   (each yes or no, with reason if no)
Closure:             proposed by (Incident Owner), confirmed by (second human), date
```

## 11. Metrics

| Metric | Target |
|---|---|
| Incidents closed with all closure conditions met | 100% for Critical and High |
| Response targets met, by Severity | At least 95% |
| Repeat incidents of a class that has a recorded learning | 0 |
| Incidents detected by a framework control versus by chance or by users | Share detected by controls rising |
| Escaped defects per release, by Severity | Trending down; Critical 0 |
| Escaped Hallucinations | 0 |
| Incidents whose cause was absent from the risk register | Trending down |
| Near misses reported | Tracked. A count of zero over a quarter is a red flag for under-reporting. |
| Time from detection to report | Tracked |

## 12. Escalation Rules

| Trigger | Escalate to | Level |
|---|---|---|
| Any Critical incident | Lead for the type, AI Governance Owner and Release Approver | L3, immediately |
| High incident | Lead for the type | L2 |
| Response target missed | One level above the current level | |
| Pressure to release with an open Critical or High incident | Release Approver and lead for the type | L3 |
| Severity disputed | Lead for the type; QE Lead for mixed types | L2 |
| Incident suspected and not reported | QE Lead | L2 |
| Evidence altered or lost | AI Governance Owner | L3 |

Levels are defined in [AI_GOVERNANCE.md](AI_GOVERNANCE.md) section 9. Effect on release, through [QUALITY_GATES.md](QUALITY_GATES.md): an open escaped-Hallucination incident fails G-REL-04; any other open Critical or High incident is recorded as an unmitigated Risk and fails G-REL-02. Open incidents are also listed in the Release record.

## 13. Review Process

- Open incidents are reviewed weekly by the QE Lead and the leads for the types involved.
- Every Critical incident has a written review within 10 working days, shared with all role holders.
- Each quarter the QE Lead reviews incidents as a set: recurring causes, stages that miss most, controls that failed.
- The review looks for causes in the system of work. It does not assign personal blame; it does name the Accountable human for each decision, because that is the record.

## 14. Example

A QE engineer notices in a test environment that `GET /api/Patient/{id}` returns a record to a Patient-role user who is not linked to it.

| Step | What happens |
|---|---|
| 0, 1 | Reported privately to the Security Lead. Type Security, Severity Critical (cross-user access to sensitive personal data, rated for production). Incident Owner assigned within the hour. Containment: the affected build is blocked from release. |
| 2, 3 | Cause: the ownership check was skipped for one role combination. A test for BOLA-02 with a second Patient identity fails as expected. |
| 4 | Fix merged with two approvers, since authorization code is touched. |
| 5 | Missed-detection: test design had no second identity per role, so no object-level test was possible; the PR review looked at the diff only; no mutation testing on the ownership check. An AI-drafted test suite had been approved with Admin-only cases. |
| 6, 7 | RSK-1 confirmed Critical with Risk Confidence High. All identifier-bearing endpoints checked for the same gap; BOLA tests added. |
| 8 | K-DEF item: "Object-level tests need two identities of the same role." |
| 9 | Prompt regression case added for AGT-04 and AGT-07: a suite with only Admin cases must be reported as incomplete. Trust Level reviewed. |
| 10 | G-SEC-02 completeness check prioritized in the adoption plan. |
| 11 | Closed after confirmation by the Security Lead's delegate. |

## 15. Best Practices and Anti-Patterns

Best practices:

- Report early. A false alarm costs minutes; a late report costs the containment window.
- Contain first, diagnose second, and keep the Evidence while doing both.
- Ask "why did our checks not catch this" for every incident, including small ones.
- Fix the class, not the instance.
- Write the record as events happen.

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-IN-01 | Fix and forget: closing when the fix is merged | The class of defect and the failed control remain | Closure conditions |
| AP-IN-02 | Hotfix around the gates | Urgent changes are the riskiest ones | Step 4 rule |
| AP-IN-03 | Severity set by embarrassment or schedule | Understated incidents get understated responses | Section 6 rules |
| AP-IN-04 | Blame-seeking review | People stop reporting | Section 13 |
| AP-IN-05 | Near misses not recorded | The cheapest learning is thrown away | Near misses are Incidents |
| AP-IN-06 | Security incident discussed in public threads | Discloses the weakness before the fix | Private reporting |
| AP-IN-07 | Accepting the first plausible cause | The real cause recurs | Confirm by reproduction |
| AP-IN-08 | "The AI did it" as root cause | Hides the human approval and the control that failed | Record the Accountable human and the missed control |
| AP-IN-09 | Agent-written triage accepted as the conclusion | A fluent hypothesis is not a finding | Human confirms cause with Evidence |
| AP-IN-10 | Rated for the environment where it was found | A test-environment finding is treated as harmless | Rate potential harm in production |
| AP-IN-11 | Self-confirmed closure | No independent check that learning happened | Closure confirmer |

## 16. Related Documents

| Document | Relationship |
|---|---|
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Incident learning flow that this procedure runs |
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Defect reproduction rules |
| [RISK_MODEL.md](RISK_MODEL.md) | Severity scale; register updates; acceptance rules |
| [QUALITY_GATES.md](QUALITY_GATES.md) | G-TST-03, G-KNW-03, G-REL-02, G-REL-04, G-REL-05 |
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Escalation levels; AI incident triggers; audit |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Escaped Hallucination handling; Trust Level changes |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | K-DEF items and admission |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Security findings and remediation targets |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Failure Triage Agent and other assisting agents |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Failure Analysis and Observability skills |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Lifecycle stages used in the Missed-detection analysis; continuous learning |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Failure investigation method |
| [ROLE_REGISTER.md](ROLE_REGISTER.md) | Incident Owner and leads |
| [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) | Related records (risk entry, Knowledge Item) |
| [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) | Mechanisms changed in control follow-up |
| [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) | Data incidents; reproduction environments |

## 17. Baseline and Assumptions

Baseline: the ten learning steps, the closure rule for Critical and High, gates G-TST-03 and G-KNW-03, the Severity scale, and the handling of escaped Hallucinations come from the core documents. Security remediation times of 2 and 10 working days match SECURITY_TESTING.md.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-IN-01 | The definition of Incident, including Control failures and Near misses | No source defines an incident |
| A-IN-02 | Four incident types and their leads | Not in any source |
| A-IN-03 | Severity guidance for incidents, including that any Escaped Hallucination is Critical | The latter is from AI_RELIABILITY_STANDARD; the rest is new guidance on the existing scale |
| A-IN-04 | Response targets in section 7 other than the security fix times | Not in any source |
| A-IN-05 | Closure conditions for Medium and Low | The core documents state them only for Critical and High |
| A-IN-06 | Closure confirmer for Critical and High | Derived from the Four-eyes principle |
| A-IN-07 | Incident record fields | Not in any source |
| A-IN-08 | Whether regulatory notification duties apply to data incidents is not addressed | Depends on jurisdiction and on whether real personal data is processed; needs the compliance mapping listed as missing |
