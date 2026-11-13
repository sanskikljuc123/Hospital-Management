# AGENT INTERACTION MATRIX

| Field | Value |
|---|---|
| Document ID | QE-DOC-06 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 17 |
| Owner | QE Lead |
| Approvers | QE Lead, AI Governance Owner |
| Review cadence | Quarterly, and whenever AGENTS_CATALOG.md changes |
| Last updated | 2026-11-13 |

## 1. Purpose

This document defines how agents work together: who hands what to whom, where validation and human approval sit in each hand-off, how knowledge and incident learning move, how escalations travel, and how confidence is carried from one Output to the next. Its aim is that an error in one agent's Output is stopped at a checkpoint and does not travel downstream.

## 2. Scope

All interactions between the eleven agents in [AGENTS_CATALOG.md](AGENTS_CATALOG.md) and the humans who approve their work.

## 3. Definitions

| Term | Definition |
|---|---|
| Hand-off | The use of one agent's approved Output as input by another agent. |
| Checkpoint | Validation followed by human approval. Every Hand-off passes a Checkpoint. |
| Producer, Consumer | The agent that created an Output; the agent that uses it. |
| Collaboration | Two or more agents contributing to one human decision. |
| Validation flow | The path an Output takes from generation to approval (section 6). |
| Knowledge propagation | How knowledge reaches agents and how proposed knowledge leaves them (section 7). |
| Incident learning flow | The path from an incident or escaped defect to changed knowledge, tests, risks and Prompts (section 8). |
| Confidence propagation | The rule by which an Output's confidence is limited by its inputs (section 9). |
| Escalation flow | The path of an escalation raised by or about an agent (section 10). |

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| QE Lead | Owns this matrix; approves new Hand-offs |
| AI Governance Owner | Ensures every Hand-off has a Checkpoint |
| Agent Owners | Own their agent's side of each Hand-off |
| Human approvers | Operate the Checkpoints |
| Knowledge Owner | Owns the knowledge and incident learning flows |

## 5. Collaboration Matrix

Rows produce, columns consume. A cell names what is handed off. Every cell passes a Checkpoint. An empty cell means no direct Hand-off is allowed.

| Producer \ Consumer | 01 Req | 02 ReqQ | 03 Risk | 04 TestD | 05 API | 06 Sec | 07 BOLA | 08 Cov | 09 PR | 10 Triage |
|---|---|---|---|---|---|---|---|---|---|---|
| 01 Requirement | | Requirements | Requirements | Requirements | Requirements | | | Criteria list | | |
| 02 Requirement Quality | Findings | | Open questions | Testability verdicts | | | | | | |
| 03 Risk | | | | Risks, mapping | | Security risks, Abuse Cases | Ownership risks | Risk list | Risk context | |
| 04 Test Design | | Untestable criteria | Untestable risks | | Test designs | | Test patterns | Test-to-risk links | | |
| 05 API | | | | Contract gaps | | Endpoint list | Identifier list | | | |
| 06 Security | | | Findings as risks | Security cases | | | Role matrix | | Security checklist | |
| 07 BOLA | | | Findings as risks | | | Ownership gaps | | | BOLA checklist | |
| 08 Coverage | | | Uncovered risks | Gaps, surviving mutants | | Security gaps | BOLA gaps | | Coverage report | |
| 09 PR Review | | | | Weak-test findings | | Security gaps | | | | |
| 10 Failure Triage | | | Causes as risk input | Reproduction test proposals | | Suspected security causes | | | | |

AGT-11 AI Validation is not in the matrix as a Producer or Consumer: it validates every cell (section 6).

Rules:

- Flow is forward by lifecycle stage. Backward cells (for example 04 to 02) are feedback: they reopen the earlier stage and its human approval, they do not edit the earlier Output.
- AGT-01 and AGT-02 do not consume later-stage Outputs except as feedback through their human approver.
- Agents never call each other directly without a Checkpoint, and never negotiate an outcome between themselves.

## 6. Validation Flow

```
Producer agent
   -> Output (structure per AI_RELIABILITY_STANDARD.md section 5)
   -> AI Validation Agent + automated checks (V1..V7)
        fail -> back to Producer with reasons (counted in Validation Pass Rate)
        pass -> Validation record attached
   -> Human approver (depth by Trust Level and Severity)
        reject -> back to Producer or human rework; Hallucinations recorded
        approve -> approver becomes Accountable human
   -> Consumer agent or repository
```

Rules:

- The AI Validation Agent does not validate Outputs it produced or contributed to. Its own records are sampled by humans (at least 20%).
- A Consumer checks that its input carries an approval. An input without one is refused and reported (L2).
- If an approved upstream Output is later changed or withdrawn, every downstream Output that used it is marked for re-validation.
- Outputs touching Critical risks or security receive full human review regardless of Trust Level.

## 7. Knowledge Propagation

```
Primary sources and human authorities
   -> Knowledge Items (admitted under KNOWLEDGE_CATALOG.md, state Active)
   -> read by agents according to their Knowledge types
   -> Outputs list the items and versions used

Agent observation that knowledge is missing, stale or in conflict
   -> reported in the Output (Output Confidence lowered)
   -> human Proposer adopts and submits a proposal
   -> Knowledge Owner admission review
   -> new or corrected Knowledge Item
   -> Outputs that used the old version are listed and their owners notified
```

Rules:

- Knowledge flows to agents only from the catalog. One agent's Output is not another agent's knowledge; it is an input, with its own confidence.
- No agent writes to the catalog. Agents may draft proposals; a human submits them.
- A corrected Knowledge Item propagates as re-validation, not as silent change.

## 8. Incident Learning Flow

Triggered by a production incident, an escaped defect, an escaped Hallucination, or a gate that passed a defective change.

| Step | Action | Agent (assists) | Human accountable | Output |
|---|---|---|---|---|
| 1 | Contain and record the incident | | Incident owner | Incident record |
| 2 | Triage: classify, gather Evidence, propose cause | AGT-10 | Engineer | Triage report |
| 3 | Reproduce with an automated test that fails | AGT-04 | Engineer | Reproduction test (G-TST-03) |
| 4 | Fix; reproduction test passes | | Engineer, reviewer | Merged fix |
| 5 | Missed-detection analysis: at which stage should this have been caught, and why was it not | AGT-10, AGT-08 | QE Lead | Analysis per stage |
| 6 | Update the risk register: new or re-rated risk | AGT-03 | Risk Owner | Register change |
| 7 | Close the test gap class, not only the instance | AGT-04, AGT-06 or AGT-07 as relevant | QE engineer, Security Lead | New tests |
| 8 | Record the learning as a K-DEF Knowledge Item | AGT-10 drafts | Knowledge Owner | Active Knowledge Item (G-KNW-03) |
| 9 | If an agent contributed to the miss: add a Prompt regression case; review Trust Level | AGT-11 | Prompt Owner, AI Governance Owner | Prompt change, Trust Level decision |
| 10 | If a gate passed the defect: review the gate | | QE Lead | Gate change |

The incident is closed only when steps 3, 6 and 8 are complete for Critical and High incidents.

## 9. Confidence Propagation

Three confidence terms exist and stay distinct: Risk Confidence ([RISK_MODEL.md](RISK_MODEL.md)), Knowledge Confidence ([KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md)) and Output Confidence ([AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md)). This section governs how they limit Output Confidence.

Rule 1, minimum rule: an Output's confidence cannot exceed the lowest confidence among its inputs.

```
Output Confidence <= min( Output Confidence of each upstream Output used,
                          ceiling from each Knowledge Item used,
                          ceiling from each Risk rating used )
```

| Input | Ceiling on Output Confidence |
|---|---|
| Knowledge Item Verified | High |
| Knowledge Item Corroborated | Medium |
| Knowledge Item Stale | Low |
| Knowledge Item Unverified | Not usable as Evidence |
| Risk Confidence High | High |
| Risk Confidence Medium | Medium |
| Risk Confidence Low | Low |
| Unresolved Conflict among inputs | Low |

Rule 2: human approval does not raise confidence by itself. A human may raise an input's confidence only by recording new Evidence (for example verifying a risk rating), after which downstream Outputs may be regenerated.

Rule 3: confidence never increases through a chain of agents. Agreement between agents is not Evidence.

Rule 4: Low confidence travels with a reason. Each Output states which input set its ceiling.

Rule 5: effect of Output Confidence on use:

| Output Confidence | Use |
|---|---|
| High | Normal review per Trust Level |
| Medium | Normal review; assumptions checked explicitly by the reviewer |
| Low | Full review regardless of Trust Level; may not be the sole basis for anything touching a Critical or High risk |

Example: AGT-03 proposes a risk at Risk Confidence Low. AGT-04 designs tests from it; the tests' Output Confidence is at most Low. The Risk Owner verifies the rating with Evidence and raises it to High. AGT-04's Output is regenerated and may now be Medium or High depending on its other inputs.

## 10. Escalation Flow

```
Agent detects a trigger -> stops the affected work -> reports in its Output and to its Human approver (L1)
   L1 unresolved in time, or trigger is L2 by rule -> domain lead:
        quality -> QE Lead | security or data -> Security Lead | knowledge -> Knowledge Owner
   L2 unresolved in time, or trigger is L3 by rule -> AI Governance Owner + Release Approver
```

| Trigger | Raised by | First level |
|---|---|---|
| Conflict with an approved source | Any agent | L1 |
| Cannot proceed without assuming facts | Any agent | L1 |
| Input lacks approval | Consumer agent | L2 QE Lead |
| Suspected Hallucination | AGT-11 or reviewer | L1 |
| Suspected Critical or High vulnerability | AGT-06, AGT-07, AGT-09, AGT-10 | L2 Security Lead, immediately |
| Proposed Critical risk | AGT-03 | L2 |
| Critical or High risk uncovered at release candidate | AGT-08 | L2 |
| Test removed or weakened without record | AGT-09 | L1; L2 in Critical or High areas |
| Restricted data encountered | Any agent | L2 Security Lead |
| Escaped Hallucination; output took effect without approval | Anyone | L3 |

Rules:

- Agents escalate to humans, never to other agents.
- An agent that escalates does not continue the affected task on assumptions.
- Levels, response times and the automatic step-up rule are defined in [AI_GOVERNANCE.md](AI_GOVERNANCE.md) section 9. Agent-specific triggers are in AGENTS_CATALOG.md section 7.

## 11. Inputs and Outputs

| Inputs | Outputs |
|---|---|
| Approved agent Outputs | Hand-off records (Producer, Consumer, Output ID, approval) |
| Validation records | Re-validation lists when an upstream Output changes |
| Incident records | Incident learning records |
| Escalations | Escalation log |

## 12. Metrics

| Metric | Target |
|---|---|
| Hand-offs without a recorded approval | 0 |
| Downstream Outputs not re-validated after an upstream change | 0 |
| Confidence rule violations (Output Confidence above its ceiling) | 0 |
| Incident learning flow completed for Critical and High | 100% |
| Escalations raised by agents and confirmed valid | Tracked |
| Errors caught at a Checkpoint versus downstream | Share caught at the first Checkpoint rising |
| Repeat incidents of a class with a recorded learning | 0 |

## 13. Quality Gates and Review Process

- G-PR-04 requires the Validation record and approval that this flow produces. G-KNW-03 enforces section 8. G-TST-03 enforces step 3.
- A new Hand-off (a new cell in section 5) is approved by the QE Lead and AI Governance Owner.
- The matrix is checked against AGENTS_CATALOG.md at each change of either document.

## 14. Best Practices

- Keep chains short. Each added agent between source and decision is another place for error.
- Pass the Evidence with the Output, not only the conclusion.
- Send feedback to the stage that owns the problem; do not patch it downstream.
- Treat disagreement between agents as a signal for a human, not something to be averaged.

## 15. Anti-Patterns

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-IM-01 | Agent-to-agent chaining without a Checkpoint | Errors compound unseen | Every Hand-off has a Checkpoint |
| AP-IM-02 | Confidence laundering: Low-confidence input becomes a High-confidence Output | False confidence | Minimum rule |
| AP-IM-03 | Consensus as evidence: several agents agree, so it is true | Shared model, shared blind spots | Rule 3 |
| AP-IM-04 | Circular validation: A validates B, B validates A | No independence | Only AGT-11 validates; humans sample it |
| AP-IM-05 | Downstream patching of an upstream error | The source stays wrong and re-infects | Feedback reopens the earlier stage |
| AP-IM-06 | Using another agent's Output as knowledge | Bypasses knowledge admission | Section 7 rules |
| AP-IM-07 | Incident closed after the fix only | The class of defect recurs | Section 8 closure rule |
| AP-IM-08 | Agent continues on assumptions after escalating | The escalation has no effect | Stop rule |
| AP-IM-09 | Stale downstream Outputs after an upstream change | Decisions rest on withdrawn inputs | Re-validation list |

## 16. Traceability, Governance References and Related Documents

Each Hand-off is recorded with Producer, Consumer, Output ID and version, Validation record and approver, so any decision can be traced back through every agent and human involved.

| Document | Relationship |
|---|---|
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | The agents and their escalation triggers |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | The skills whose outputs are handed off |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Lifecycle order behind the matrix; continuous learning |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Validation checks; Output Confidence |
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Escalation levels; human oversight; Four-eyes principle |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Knowledge admission; Knowledge Confidence; Conflict resolution |
| [RISK_MODEL.md](RISK_MODEL.md) | Risk Confidence; register updates in the learning flow |
| [QUALITY_GATES.md](QUALITY_GATES.md) | G-PR-04, G-TST-03, G-KNW-03 |
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Defect reproduction |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Security escalation triggers |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Failure investigation practices |

## 17. Baseline and Assumptions

Baseline (from the approved source document): collaboration, validation flow, knowledge propagation, incident learning flow, escalation flow and confidence propagation between agents.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-IM-01 | The contents of each cell in the collaboration matrix | The source names collaboration without specifying hand-offs |
| A-IM-02 | Every Hand-off passes Validation and human approval | Derived from "human approves" in the AI strategy; the source does not say it applies between agents |
| A-IM-03 | The minimum rule and the ceiling table for confidence | The source names confidence propagation without a rule |
| A-IM-04 | The ten steps of the incident learning flow and the closure condition | The source names the flow only |
| A-IM-05 | Agents escalate only to humans | Derived from principle 5 |
| A-IM-06 | AGT-11 sits outside the matrix as the validator of every cell | Inferred from its name |

## Supporting Documents

Operational documents that implement this one: [ROLE_REGISTER.md](ROLE_REGISTER.md) (who holds each role), [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) (how each gate is enforced), [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) (required records), [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) (incident procedure), [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) (test data, identities and environments).
