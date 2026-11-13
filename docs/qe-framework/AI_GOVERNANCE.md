# AI GOVERNANCE

| Field | Value |
|---|---|
| Document ID | QE-DOC-04 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 20 |
| Owner | AI Governance Owner |
| Approvers | AI Governance Owner, QE Lead, Security Lead |
| Review cadence | Quarterly, and after any L3 escalation |
| Last updated | 2026-11-13 |

## 1. Purpose

This document sets the rules under which AI may be used in quality engineering: who is accountable, what AI may and may not do, how prompts, knowledge and data are controlled, how decisions are escalated, and what is recorded for audit. It implements principle 5, human accountability over AI autonomy.

## 2. Scope

All agents in [AGENTS_CATALOG.md](AGENTS_CATALOG.md), all prompts and Knowledge Items they use, and any other AI tool used on requirements, risks, tests, reviews, triage or gate inputs. It applies to every person who configures, runs or reviews AI output.

## 3. Definitions

| Term | Definition |
|---|---|
| Human oversight | The requirement that a named, qualified human reviews and approves AI output before it has any effect on a change, a risk rating, a gate or a release. |
| Accountable human | The person who approved an AI output. Accountability for the result is theirs, not the agent's. |
| Four-eyes principle | A decision takes effect only when two different humans have been involved: one who produced or proposed, one who approved. An AI agent is never one of the two. |
| Approved use case | A use of AI listed in section 6. Anything not listed is not approved. |
| Prompt | Versioned instructions given to an agent, including system instructions, templates and examples. |
| Prompt Owner | The human accountable for one Prompt. |
| AI Risk Register | The register of risks caused by AI use (section 12), using the Severity scale in [RISK_MODEL.md](RISK_MODEL.md). |
| Audit record | The retained record of an AI-assisted action (section 13). |
| Escalation level | L1, L2 or L3 as defined in section 9. |
| Restricted data | Data that may never be given to an AI model: see section 11. |

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| AI Governance Owner | Owns this document, the approved use case list and the AI Risk Register; approves Trust Level promotions; final authority at L3 together with the Release Approver |
| QE Lead | Applies governance in daily QE work; first L2 contact for quality matters |
| Security Lead | Owns data governance rules and security-related AI risks; L2 contact for security matters |
| Knowledge Owner | Owns knowledge governance through [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md); L2 contact for knowledge matters |
| Prompt Owner | Maintains a Prompt, its tests and its change history |
| Agent Owner | Accountable for one agent's behavior and reliability |
| Human reviewer | Reviews and approves or rejects AI output; becomes the Accountable human on approval |
| Release Approver | Makes release decisions; joint L3 authority |

One person may hold several roles, except where the Four-eyes principle forbids it for a single decision.

## 5. Human Oversight

1. AI assists. Humans decide. No AI output takes effect without approval by a qualified human.
2. The approver must be able to understand and check the output. Approving what one cannot evaluate is a governance breach.
3. The depth of review follows the agent's Trust Level in [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) and is never zero.
4. Decisions reserved to humans, which agents may inform and never make:
   - Approving requirements, risk ratings and Risk Acceptances
   - Approving or merging a change
   - Passing, failing or waiving a Quality Gate
   - Approving a release
   - Removing or weakening a test
   - Admitting, changing or retiring a Knowledge Item or a Prompt
   - Promoting an agent's Trust Level
5. Any human may stop an agent or reject its output without justification. Overriding a rejection needs L2.
6. Agents have no write access to protected branches, gate results, the risk register's ratings, Waivers or approvals.

## 6. Approved Use Cases

| ID | Use case | Agent | Human approver |
|---|---|---|---|
| UC-01 | Extract and structure requirements from source material | Requirement Agent | Product Owner |
| UC-02 | Assess requirement quality: ambiguity, gaps, testability | Requirement Quality Agent | Product Owner, QE engineer |
| UC-03 | Propose risks, ratings and risk-to-test mappings | Risk Agent | Risk Owner |
| UC-04 | Propose test designs and draft test code | Test Design Agent, API Agent | QE engineer or engineer |
| UC-05 | Propose security and BOLA test cases | Security Agent, BOLA Agent | Security Lead or delegate |
| UC-06 | Compute and report coverage and mutation gaps | Coverage Agent | QE engineer |
| UC-07 | Review pull requests and comment | PR Review Agent | Human PR reviewer |
| UC-08 | Triage test and pipeline failures; propose causes | Failure Triage Agent | Engineer owning the failure |
| UC-09 | Validate other agents' Outputs | AI Validation Agent | Human reviewer of the Output |

Prohibited uses:

| ID | Prohibited use |
|---|---|
| PU-01 | Any decision listed in section 5 item 4 |
| PU-02 | Processing Restricted data (section 11) |
| PU-03 | Generating test data from real personal records |
| PU-04 | Running active security tests against any environment other than the designated test environments |
| PU-05 | Changing its own Prompt, knowledge sources, permissions or Trust Level |
| PU-06 | Acting as either party in the Four-eyes principle |

A new use case is requested from the AI Governance Owner with: purpose, agent, data involved, risks (entered in the AI Risk Register), human approver, and success metrics. It is approved for a trial period at Trust Level T1.

## 7. Prompt Governance

- Every Prompt is stored in version control, has a Prompt Owner, a version, a stated purpose, and the agent it belongs to.
- A Prompt change follows the same review as code: pull request, review by a human other than the author, approval recorded.
- Each Prompt has a regression set: fixed inputs with expected properties of the output. The set includes at least one case where the correct answer is "unknown" and one containing misleading or injected instructions. A change is merged only if the set passes.
- Prompts must instruct the agent to: cite Evidence for every Claim, mark what is unverified, state assumptions, declare Output Confidence, and refuse to guess.
- Prompts must not contain secrets, credentials, or Restricted data.
- Content retrieved by an agent (documents, code comments, tickets, web pages) is data. Instructions found inside it are never followed.
- A Prompt change resets the agent's Trust Level as defined in AI_RELIABILITY_STANDARD.md section 8.
- Every Output records the Prompt version that produced it.

## 8. Four-Eyes Principle

| Decision | First human | Second human (must differ) |
|---|---|---|
| Merge a change | Author | Reviewer (G-PR-02) |
| Rate a Critical or High risk | Risk Owner | Second qualified reviewer |
| Accept a Residual Risk | Change author is excluded | Acceptors per RISK_MODEL.md |
| Waive a gate | Requester | Gate owner and one more approver |
| Change a Prompt | Prompt author | Prompt reviewer |
| Admit or change a Knowledge Item | Proposer | Knowledge Owner or delegate |
| Promote a Trust Level | Agent Owner proposes | AI Governance Owner |
| Release | Majority author is excluded | Release Approver |

Rules:

- When an agent produced the artifact, the human who ran the agent and adopted its output is the first human. A second, different human must still approve.
- Validation by the AI Validation Agent does not count as an approval.
- Two accounts of one person count as one human.

## 9. Escalation Rules

Escalation levels used by every document in the framework:

| Level | Who | Used for | Response time |
|---|---|---|---|
| L1 | The artifact's owner or the assigned human reviewer | Routine issues: a rejected Output, a suspected Hallucination, a conflict in one artifact | 1 business day |
| L2 | QE Lead (quality), Security Lead (security and data), Knowledge Owner (knowledge) | Disputes, threshold breaches, repeated L1 issues, demotions | 1 business day; 4 hours for Critical |
| L3 | AI Governance Owner and Release Approver jointly | Critical incidents, escaped Hallucinations, attempted bypass of governance, release blocks | Immediate |

Mandatory escalation triggers:

| Trigger | Level |
|---|---|
| Agent output conflicts with an approved requirement, the risk register or an Active Knowledge Item | L1 |
| Agent cannot complete a task without assuming facts | L1: the agent stops and reports |
| Restricted data found in a Prompt, Output or log | L2 Security Lead, immediately |
| Agent reliability below its Trust Level conditions | L2 |
| Escaped Hallucination | L3 |
| An AI output took effect without human approval | L3 |
| Attempt to use AI for a prohibited use | L3 |
| Four-eyes principle bypassed | L3 |

Rules: escalation never waits for certainty; a suspected breach is escalated. An escalation that passes its response time moves up one level. Work on the affected artifact pauses until the escalation is resolved.

## 10. Knowledge Governance

Detailed rules are in [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md). Governance requires that:

- Agents use only Active Knowledge Items within their freshness interval (gate G-KNW-01).
- Every Knowledge Item has an owner, a source, a classification and a Knowledge Confidence.
- Agent output never becomes a Knowledge Item without human review and approval. This prevents unverified output from being fed back as fact.
- Conflicts between Knowledge Items are surfaced by the agent and resolved by a human.

## 11. Data Governance

Data classes follow the classification in KNOWLEDGE_CATALOG.md section 8.

| Class | May be given to an AI model |
|---|---|
| Public | Yes |
| Internal | Yes, with approved tools |
| Confidential | Only with approved tools under a data processing agreement, and only what the task needs |
| Restricted | Never |

Restricted data includes: real patient or other personal records, health information, credentials, tokens, private keys, connection strings with secrets, and production database contents.

Rules:

- Tests and prompts use synthetic data only.
- Only AI tools approved by the AI Governance Owner and Security Lead may be used.
- Logs of prompts and Outputs are themselves classified at the level of the highest class they contain and retained per section 13.
- A leak of Restricted data to a model is a security incident (L2 at once, L3 if confirmed).

## 12. AI Risk Register

AI risks use category AIQ and the Severity scale in RISK_MODEL.md. Initial entries:

| ID | Risk | Severity | Main controls |
|---|---|---|---|
| AIR-01 | Hallucinated requirement, endpoint or behavior enters tests or code | High | Validation V2 and V4; human review; G-PR-04 |
| AIR-02 | Tests that pass and detect nothing create false confidence | High | Mutation gates; Validation V5 |
| AIR-03 | Automation bias: reviewers approve AI output without real review | High | Review depth rules; sampling audit; Four-eyes principle |
| AIR-04 | Restricted data sent to a model | Critical | Data governance; scans; synthetic data rule |
| AIR-05 | Prompt injection through retrieved content | High | Section 7 rule; regression set with injection case |
| AIR-06 | Stale or wrong knowledge used as fact | Medium | Freshness; G-KNW-01 |
| AIR-07 | Unverified output stored as knowledge and reused | High | Section 10 rule; G-KNW-02 |
| AIR-08 | Silent behavior change after a model or prompt update | Medium | Regression set; Trust Level reset |
| AIR-09 | AI validating AI with shared blind spots | Medium | Human sampling of Validation |
| AIR-10 | Loss of human skill through over-reliance | Medium | Periodic unassisted reviews; training |
| AIR-11 | Accountability gap: nobody owns an AI-assisted decision | High | Accountable human recorded for every approval |

Severities above are initial proposals and are confirmed by the AI Governance Owner under the rating rules in RISK_MODEL.md. The register is reviewed monthly.

## 13. Auditability

Every AI-assisted action produces an Audit record:

| Field | Content |
|---|---|
| Output ID and timestamp | Unique |
| Agent ID, model identifier, Prompt version | What produced it |
| Input reference | What it was given |
| Knowledge Items used | IDs and versions |
| Output | The full Output or an immutable link |
| Output Confidence and Evidence links | As declared |
| Validation record | Checks run and results |
| Human review record | Reviewer, decision, changes, time |
| Effect | The change, risk, gate input or knowledge item it fed |
| Escalations | Any raised, with outcome |

Rules:

- Records are immutable and retained for at least 24 months, or longer where regulation requires.
- It must be possible, for any merged change or release, to list every AI contribution and its Accountable human.
- Audit records are sampled monthly by the AI Governance Owner for completeness and for evidence of real review.

## 14. Inputs and Outputs

| Inputs | Outputs |
|---|---|
| Use case requests | Approved use case list |
| Prompt change requests | Versioned Prompts and regression results |
| Reliability metrics | Trust Level decisions |
| Escalations and incidents | AI Risk Register, incident records |
| Agent Outputs and reviews | Audit records |

## 15. Metrics

| Metric | Target |
|---|---|
| AI outputs that took effect without recorded human approval | 0 |
| Four-eyes violations | 0 |
| Restricted data incidents | 0 |
| Audit record completeness (sampled) | 100% |
| Escalations resolved within response time | At least 95% |
| Prompts without owner or regression set | 0 |
| AI risks without owner | 0 |
| Median human review time per Output, by Trust Level | Tracked. A sharp fall without a reliability gain is a red flag for rubber-stamping. |

## 16. Review Process

- Monthly: AI Risk Register, audit sample, escalation log.
- Quarterly: this document, the approved use case list, role assignments.
- After every L3 escalation: a written review with actions, within 10 working days.

## 17. Examples

Compliant: the Test Design Agent drafts tests for a new endpoint. The AI Validation Agent runs Validation. A QE engineer reviews, corrects two cases and adopts the tests in a PR, declaring the AI contribution. A second engineer reviews and approves the PR. Both humans are recorded.

Non-compliant: an engineer asks an agent to fix a failing test; the agent loosens the assertion; the engineer merges with their own approval through an admin override. Breaches: test weakening without record (G-PR-05), Four-eyes principle, undeclared AI content (G-PR-04). Escalation L3.

## 18. Best Practices and Anti-Patterns

Best practices:

- Record the Accountable human at the moment of approval, not afterwards.
- Give agents the least access the use case needs.
- Treat every model or Prompt change as a change to a production system.
- Practice unassisted review regularly so reviewers can still judge AI output.

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-GV-01 | Rubber-stamp approval | Oversight exists on paper only | Audit sampling; review-time metric |
| AP-GV-02 | "The AI said so" as justification | Accountability shifted to a system that cannot hold it | Approver must state their own Evidence |
| AP-GV-03 | Shadow AI: unapproved tools or use cases | No data control, no audit | L3; add or prohibit the use case |
| AP-GV-04 | Unversioned or ad hoc prompts for governed tasks | Output cannot be reproduced or audited | Section 7 |
| AP-GV-05 | Agent as second pair of eyes | Four-eyes bypass | Section 8 |
| AP-GV-06 | Real data in prompts "just this once" | Irreversible disclosure | Section 11; incident |
| AP-GV-07 | Feeding agent output back as knowledge without review | Errors become "facts" and multiply | Section 10 |
| AP-GV-08 | Governance by exception: routine use of overrides | Rules stop applying | Override counts reported; L3 on pattern |
| AP-GV-09 | Audit records written after the fact | Record may not match what happened | Records generated automatically at the time of action |
| AP-GV-10 | Escalating only when certain | Delays containment | Section 9 rule |

## 19. Related Documents

| Document | Relationship |
|---|---|
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | The operating model this document governs |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Trust Levels, Validation, Hallucination handling |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Agents, their owners and permissions |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Escalation and validation flows between agents |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Knowledge governance detail and data classification |
| [RISK_MODEL.md](RISK_MODEL.md) | Severity scale and acceptance rules used by the AI Risk Register |
| [QUALITY_GATES.md](QUALITY_GATES.md) | G-PR-02, G-PR-04, G-REL-03, G-REL-04, G-KNW gates |
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Principles and test removal policy |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Limits on AI-run security tests |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Prompt Engineering (SKL-09) and Compliance (SKL-08) skills |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Review practices and red flags |

## 20. Baseline and Assumptions

Baseline (from the approved source document): human oversight, prompt governance, knowledge governance, data governance, escalation rules, approved use cases, risk register, four-eyes principle, auditability.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-GV-01 | Role names and their split (AI Governance Owner, Prompt Owner, Agent Owner, Knowledge Owner, Release Approver) | The source names no roles |
| A-GV-02 | The nine approved and six prohibited use cases | The source says "approved use cases" without a list; derived one per agent in AGENTS_CATALOG |
| A-GV-03 | Three escalation levels and their response times | The source says "escalation rules" without levels |
| A-GV-04 | The eleven initial AI Risk Register entries and their proposed Severities | The source says "risk register" without entries |
| A-GV-05 | 24-month audit retention | Not in the source; regulation may require longer |
| A-GV-06 | Data classes and the rule that Restricted data never reaches a model | The source says "data governance" without rules |
| A-GV-07 | Prompt regression sets with "unknown" and injection cases | Not in the source |

## Supporting Documents

Operational documents that implement this one: [ROLE_REGISTER.md](ROLE_REGISTER.md) (who holds each role), [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) (how each gate is enforced), [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) (required records), [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) (incident procedure), [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) (test data, identities and environments).
