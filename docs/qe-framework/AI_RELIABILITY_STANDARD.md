# AI RELIABILITY STANDARD

| Field | Value |
|---|---|
| Document ID | QE-DOC-05 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 18 |
| Owner | AI Governance Owner |
| Approvers | AI Governance Owner, QE Lead |
| Review cadence | Quarterly, and after any Critical hallucination incident |
| Last updated | 2026-11-13 |

## 1. Purpose

Trustworthiness over intelligence. This standard defines how the reliability of AI agents and their outputs is measured, what each output must contain to be usable, and how much human review each agent's work receives. An output that is clever but unverifiable has no value in this framework.

## 2. Scope

Every output of every agent in [AGENTS_CATALOG.md](AGENTS_CATALOG.md) that feeds a requirement, risk, test, review, triage or gate decision. It also applies to any other AI tool used for an approved use case in [AI_GOVERNANCE.md](AI_GOVERNANCE.md).

## 3. Definitions

| Term | Definition |
|---|---|
| Output | One artifact produced by an agent in one run: a requirement analysis, a risk proposal, a test design, a review comment set, a triage report. |
| Claim | A statement in an Output that can be true or false about the system, the requirements or the world. |
| Evidence | As defined in [TEST_STRATEGY.md](TEST_STRATEGY.md): a reproducible, linkable artifact. For a Claim: a requirement ID, a code reference, a Knowledge Item ID, an execution result. |
| Evidence Completeness (EC) | Claims with valid linked Evidence divided by all Claims in the Output. |
| Validation | An independent check of an Output against its Evidence and against the rules of this standard, performed by the AI Validation Agent and by automated checks. |
| Validation Pass Rate (VPR) | Outputs that pass Validation on first submission divided by Outputs validated. |
| Review | The human assessment of an Output. Review follows Validation and never replaces it; Validation never replaces Review. |
| Review Acceptance Rate (RAR) | Outputs approved by the human reviewer without material change divided by Outputs reviewed. |
| Execution Success Rate (ESR) | For executable Outputs (tests, scripts, queries): those that compile, run, and behave as the Output claims, divided by executable Outputs. |
| Hallucination | A Claim, reference or artifact that is presented as fact and is not supported by, or contradicts, the available Evidence. Includes invented endpoints, fields, requirements, IDs, citations and results. |
| Hallucination Rate (HR) | Outputs containing one or more confirmed Hallucinations divided by Outputs validated or reviewed. |
| Reliability Score (RS) | The composite score in section 6, per agent, over a rolling window. |
| Trust Level | T0 to T3, derived from the Reliability Score, which sets the depth of human Review. |
| Output Confidence | High, Medium or Low, declared by the agent for each Output according to section 7. |

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| AI Governance Owner | Owns this standard, approves Trust Level changes |
| QE Lead | Owns the measurement process and reports the metrics |
| Human reviewer | Reviews Outputs, confirms or rejects suspected Hallucinations, records the result |
| AI Validation Agent (AGT-11) | Performs Validation; reports, never approves |
| Agent Owner | The human named in AGENTS_CATALOG.md who is accountable for one agent's reliability |

## 5. Output Requirements

Every Output must contain:

1. Agent ID, prompt version, and the IDs of Knowledge Items used.
2. The input it was given (or a link to it).
3. Each Claim linked to its Evidence, or marked explicitly as `UNVERIFIED`.
4. A list of assumptions made.
5. A declared Output Confidence with the reason.
6. What the agent could not determine. "Unknown" is a valid and preferred answer over a guess.

An Output missing any of items 1 to 5 fails Validation without further checks.

## 6. Reliability Score

Computed per agent over the last 30 days or the last 50 Outputs, whichever covers more Outputs.

```
RS = 100 x ( 0.25 x EC  +  0.20 x VPR  +  0.15 x RAR  +  0.20 x ESR  +  0.20 x HF )

HF (hallucination factor) = max( 0, 1 - HR / 0.05 )
```

- All components are fractions between 0 and 1.
- For agents with no executable Outputs, the ESR weight is redistributed proportionally to the other components.
- Fewer than 20 Outputs in the window: the score is reported as provisional and the agent stays at its current Trust Level, or T1 if new.

| Component | Weight | Why it is included |
|---|---|---|
| Evidence Completeness | 0.25 | Evidence over assumptions |
| Validation Pass Rate | 0.20 | Validation over generation |
| Review Acceptance Rate | 0.15 | Human judgment of usefulness |
| Execution Success Rate | 0.20 | The Output does what it claims |
| Hallucination factor | 0.20 | Fabrication destroys trust fastest |

### 6.1 Hard rules that override the score

- An Output with a confirmed Hallucination is rejected in full, however good the rest is. It is not partially used.
- An Output with Evidence Completeness below 0.80 is rejected.
- One confirmed Hallucination that reached a merged change or a gate decision is a Critical incident (section 12).

## 7. Output Confidence

| Level | Conditions (all must hold) |
|---|---|
| High | EC is 1.0; every Knowledge Item used is Active and fresh with Knowledge Confidence Verified; no assumptions affect the conclusion; all upstream inputs were High |
| Medium | EC is at least 0.90; assumptions are listed and do not affect Critical or High risk items; upstream inputs at least Medium |
| Low | Anything else that still meets the minimum in 6.1 |

Output Confidence can never be higher than the lowest confidence of its inputs. The propagation rule is detailed in [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) section 9.

## 8. Trust Levels

| Level | Name | Entry condition | Human Review depth | Allowed use |
|---|---|---|---|---|
| T0 | Restricted | RS below 60, or HR above 5%, or new and unmeasured after suspension | Full: reviewer re-derives the Output independently | Advisory only; Outputs may not enter a change until fully re-verified |
| T1 | Supervised | RS 60 to 74; default for a new agent | Full line-by-line Review of every Output | Drafts for human completion |
| T2 | Assisted | RS 75 to 89 and HR at or below 2% | Full Review of every Claim touching Critical or High risks; sampled Review of the rest (at least 30%) | Outputs usable after approval |
| T3 | Trusted | RS 90 or above and HR at or below 1%, held for 60 consecutive days | Full Review of every Claim touching Critical or High risks; sampled Review of the rest (at least 10%) | Outputs usable after approval |

Rules:

- No Trust Level removes human approval. T3 changes the depth of Review, not the need for it.
- Promotion needs AI Governance Owner approval and is recorded. Demotion is automatic when the entry condition is no longer met.
- A change to an agent's prompt, model or knowledge sources resets the agent to at most T1 until 20 new Outputs are measured.
- Trust is per agent and per task type. It does not transfer between agents.

## 9. Validation

Validation checks, in order. The first failure rejects the Output.

| # | Check | Method |
|---|---|---|
| V1 | Output structure complete (section 5) | Automated |
| V2 | Every referenced ID, endpoint, field, file and Knowledge Item exists | Automated lookup against the repository, API inventory and Knowledge Catalog |
| V3 | Each Claim's Evidence actually supports the Claim | AI Validation Agent, sampled by human |
| V4 | No contradiction with approved requirements, the risk register or Active Knowledge Items | AI Validation Agent |
| V5 | Executable Outputs compile and run; tests fail when the protected behavior is broken | Automated execution and mutation |
| V6 | No restricted data in the Output (see AI_GOVERNANCE.md data governance) | Automated scan |
| V7 | Output Confidence is consistent with section 7 | Automated |

The AI Validation Agent may not validate its own Output or an Output it contributed to. Its own Outputs are sampled by a human at a rate of at least 20%.

## 10. Inputs and Outputs

| Inputs | Outputs |
|---|---|
| Agent Outputs | Validation records |
| Human Review records | Reliability Score per agent |
| Execution and mutation results | Trust Level per agent |
| Hallucination reports | Hallucination incident records |

## 11. Metrics

| Metric | Target |
|---|---|
| Reliability Score per agent | At or above 75 for any agent whose Outputs enter changes |
| Evidence Completeness | At or above 0.95 average; 0.80 minimum per Output |
| Validation Pass Rate | Tracked; falling trend triggers Agent Owner review |
| Review Acceptance Rate | Tracked |
| Execution Success Rate | At or above 0.95 |
| Hallucination Rate | At or below 2% for T2, 1% for T3 |
| Escaped Hallucinations | 0 (Hallucinations found after merge or after a gate decision) |
| Review sample compliance | Actual sampled Review rate at or above the Trust Level minimum |

## 12. Escalation Rules

| Trigger | Action | Level |
|---|---|---|
| Suspected Hallucination in an Output | Reviewer confirms or rejects, records it, rejects the Output | L1 |
| HR above the limit for the agent's Trust Level | Automatic demotion; Agent Owner investigates | L2 |
| HR above 5% or RS below 60 | Agent to T0; Outputs since the last good measurement are re-reviewed | L2, AI Governance Owner informed |
| Escaped Hallucination | Critical incident: agent suspended, affected changes identified and re-verified, incident learning flow started | L3 |
| Validation and human Review disagree repeatedly | AI Validation Agent's own reliability reviewed | L2 |

Levels are defined in [AI_GOVERNANCE.md](AI_GOVERNANCE.md) section 9.

## 13. Quality Gates

This standard is enforced by gates in [QUALITY_GATES.md](QUALITY_GATES.md): G-PR-04 (AI-produced content has a Validation record and human approval) and G-REL-04 (no Output from a T0 agent in a release without full re-verification).

## 14. Review Process

- Reliability Scores are computed weekly and reviewed monthly by the QE Lead and AI Governance Owner.
- The weights and thresholds in this standard are reviewed quarterly against the escaped-hallucination record.
- Every human Review records: reviewer, Output ID, decision, material changes made, Hallucinations found.

## 15. Examples

Good Claim: "`DELETE /api/Patient/{id}` is limited to the Admin role (Evidence: API inventory, Patients table, row DELETE)."

Hallucination: "`PATCH /api/Patient/{id}/archive` soft-deletes the record." No such endpoint is in the API inventory. V2 fails; the Output is rejected and the Hallucination is counted.

Correct handling of the unknown: "The requirement does not say whether a Nurse may delete an appointment. `UNVERIFIED`. Question raised to the Product Owner." This lowers Evidence Completeness honestly and is not a Hallucination.

## 16. Best Practices and Anti-Patterns

Best practices:

- Reward "unknown". An agent that says it does not know is behaving reliably.
- Verify references mechanically before a human spends time on the content.
- Keep the reviewer independent of the prompt author where possible.

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-AR-01 | Fluency as evidence: trusting an Output because it reads well | Fluent text and correct text are unrelated | Check Evidence links |
| AP-AR-02 | Self-validation: an agent validating its own Output | No independence | Section 9 rule |
| AP-AR-03 | AI validating AI with no human sample | Shared blind spots compound | Human sampling minimums |
| AP-AR-04 | Partial salvage of an Output containing a Hallucination | The rest is no longer trustworthy | Reject in full |
| AP-AR-05 | Trust carried over after a prompt or model change | The measured system no longer exists | Reset to T1 |
| AP-AR-06 | Rubber-stamp Review: approving without reading because the agent is T3 | Review depth is reduced, never removed | Audit sample compliance |
| AP-AR-07 | Score gaming: breaking work into trivial Outputs to raise the score | Score stops reflecting risk | Weight Review by risk; Agent Owner review |
| AP-AR-08 | Passing tests counted as correct tests | A test can pass and verify nothing | V5 requires failure under mutation |
| AP-AR-09 | Hiding uncertainty: omitting assumptions to reach High confidence | Confidence becomes meaningless | V7 and reviewer check |

## 17. Traceability, Governance References and Related Documents

Each Output is traceable to: agent, prompt version, Knowledge Items, input, Validation record, Review record, and the change or decision it fed. Records are kept according to the audit rules in AI_GOVERNANCE.md.

| Document | Relationship |
|---|---|
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Human oversight, audit, data rules, escalation levels |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Places Validation and Review in the lifecycle |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Agents measured by this standard; AI Validation Agent |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Confidence propagation and validation flow |
| [QUALITY_GATES.md](QUALITY_GATES.md) | G-PR-04 and G-REL-04 |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Knowledge Confidence and freshness used in section 7 |
| [RISK_MODEL.md](RISK_MODEL.md) | Severity used to set Review depth; AIQ risk category |
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Definition of Evidence; rules for AI-produced tests |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Prompt Engineering skill (SKL-09) |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Security Outputs always receive full Review |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | AI best practices and red flags |

## 18. Baseline and Assumptions

Baseline (from the approved source document): trustworthiness over intelligence, reliability score, evidence completeness, validation, review, execution success, hallucination rate, trust levels.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-AR-01 | The Reliability Score formula and weights | The source names the components, not how they combine |
| A-AR-02 | Rolling window of 30 days or 50 Outputs; minimum 20 Outputs | Not in the source |
| A-AR-03 | Four Trust Levels T0 to T3 with the stated thresholds | The source says "trust levels" without number or criteria |
| A-AR-04 | Hallucination Rate limits of 5%, 2% and 1% | Not in the source |
| A-AR-05 | Evidence Completeness minimum of 0.80 per Output | Not in the source |
| A-AR-06 | Output Confidence and its three levels | Introduced to support "confidence propagation" named in AGENT_INTERACTION_MATRIX |
| A-AR-07 | Review sampling rates of 30%, 10% and 20% | Not in the source |
| A-AR-08 | Reset to T1 on prompt, model or knowledge-source change | Not in the source; follows from principle 1 |

## Supporting Documents

Operational documents that implement this one: [ROLE_REGISTER.md](ROLE_REGISTER.md) (who holds each role), [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) (how each gate is enforced), [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) (required records), [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) (incident procedure), [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) (test data, identities and environments).
