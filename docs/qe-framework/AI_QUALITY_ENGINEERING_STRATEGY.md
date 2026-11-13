# AI QUALITY ENGINEERING STRATEGY

| Field | Value |
|---|---|
| Document ID | QE-DOC-02 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 18 |
| Owner | QE Lead |
| Approvers | QE Lead, AI Governance Owner, Tech Lead |
| Review cadence | Quarterly |
| Last updated | 2026-11-13 |

## 1. Purpose

AI assists testing. Humans approve. This document describes how AI agents take part in quality engineering: the lifecycle they work in, which agent and skill serves each stage, how their output is validated and reviewed, how knowledge is used, and how the system learns. It applies [TEST_STRATEGY.md](TEST_STRATEGY.md) to AI-assisted work and operates inside the rules of [AI_GOVERNANCE.md](AI_GOVERNANCE.md).

## 2. Scope

All AI-assisted quality engineering activity from requirement intake to post-release learning. Work done without AI follows TEST_STRATEGY.md directly and passes the same gates.

## 3. Definitions

This document introduces these terms. All other terms are used as defined in their owning documents (section 16).

| Term | Definition |
|---|---|
| AI-assisted artifact | Any requirement analysis, risk proposal, test, review comment or triage report that an agent produced in whole or in part. |
| Lifecycle stage | One of the twelve stages in section 6. |
| Stage exit | The condition, including the human approval, that must hold before the next stage may rely on a stage's result. |
| Review workflow | The fixed sequence Generation, Validation, Human review, Approval (section 8). |
| Continuous learning | The controlled process by which results, failures and incidents change knowledge, prompts and agents (section 10). |

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| QE Lead | Owns this strategy and the lifecycle; decides where AI assistance is used within the approved use cases |
| AI Governance Owner | Ensures the lifecycle respects governance; owns Trust Levels |
| Tech Lead | Integrates agents with the repository and pipeline under least privilege |
| Product Owner | Approves requirement-stage results |
| Engineers and QE engineers | Run agents, review and approve their output, and are accountable for what they approve |
| Agent Owners | Maintain individual agents |

## 5. Strategy Principles

The six framework principles apply in order. For AI assistance they mean:

| Principle | Consequence for AI assistance |
|---|---|
| Reliability over speed | An agent is used where it raises Confidence, not only where it saves time. Time saved is never a reason to skip Validation or review. |
| Confidence over coverage | Agents are measured by defects and gaps found, and by Risk Coverage, not by volume of tests produced. |
| Evidence over assumptions | Every Claim in an AI-assisted artifact links to Evidence or is marked unverified. |
| Validation over generation | Generation is the cheap step. Each generated artifact passes Validation before a human spends review time on it. |
| Human accountability over AI autonomy | Every stage exit has a named human approver. Agents propose; they never decide. |
| Risk coverage over test quantity | Agents receive the risk register as input and are directed at uncovered Critical and High risks first. |

## 6. Lifecycle

| # | Stage | Agent (assists) | Primary skill | Human approver (stage exit) | Main output |
|---|---|---|---|---|---|
| 1 | Requirement intake | Requirement Agent (AGT-01) | Requirement Analysis | Product Owner | Structured requirements with acceptance criteria |
| 2 | Requirement quality | Requirement Quality Agent (AGT-02) | Requirement Analysis | Product Owner and QE engineer | Quality findings; open questions resolved or recorded |
| 3 | Risk analysis | Risk Agent (AGT-03) | Risk Analysis | Risk Owner | Proposed risks, ratings, risk-to-test mapping |
| 4 | Test design | Test Design Agent (AGT-04) | Test Design | QE engineer | Test designs traced to criteria and risks |
| 5 | Specialized test design | API Agent (AGT-05), Security Agent (AGT-06), BOLA Agent (AGT-07) | Test Design, Security Testing | QE engineer; Security Lead or delegate for security | API, security and BOLA test cases |
| 6 | Validation | AI Validation Agent (AGT-11) | Prompt Engineering, Failure Analysis | None: Validation is a check, not an approval | Validation record per Output |
| 7 | Human review and approval | None | None | The reviewer named for the artifact | Approved or rejected artifact |
| 8 | Implementation and execution | None (engineers implement; agents may draft under UC-04) | Test Design | Engineer | Tests in the repository; execution results |
| 9 | Coverage evaluation | Coverage Agent (AGT-08) | Mutation Testing | QE engineer | Behavior Coverage, Risk Coverage and Mutation Score report with gaps |
| 10 | PR review and PR gates | PR Review Agent (AGT-09) | Test Design, Security Testing, Mutation Testing | Human PR reviewer | Review comments; gate results |
| 11 | Release gates | None | None | Release Approver | Release decision on the Evidence pack |
| 12 | Failure triage and continuous learning | Failure Triage Agent (AGT-10) | Failure Analysis, Observability | Engineer owning the failure; Knowledge Owner for knowledge changes | Triage reports; approved knowledge updates |

Rules:

- Stages 6 and 7 follow every agent-producing stage (1 to 5, 9, 10, 12). They are listed once for readability.
- A stage may not consume an upstream result that has not passed its stage exit.
- Stage 3 precedes stage 4. Tests are designed from risks, not risks from tests.
- If an upstream artifact changes after approval, downstream artifacts are re-validated.
- Gate definitions for stages 10 and 11 are in [QUALITY_GATES.md](QUALITY_GATES.md).

## 7. Agents and Skills

Agents are defined in [AGENTS_CATALOG.md](AGENTS_CATALOG.md). Skills are defined in [SKILLS_CATALOG.md](SKILLS_CATALOG.md). Their interactions are in [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md). This strategy requires:

- Each agent has a single, narrow mission. A new need is met by a new or extended agent through governance, not by widening a prompt informally.
- Each agent uses only skills listed for it in the catalog.
- Each agent's permissions are the minimum for its mission and never include approval or merge rights.
- A new agent starts at Trust Level T1.

## 8. Validation and Review Workflow

```
Generation -> Validation -> Human review -> Approval -> Use
                |                |
                v                v
           rejected:         rejected:
           back to agent     back to agent or human rework;
           with reasons      Hallucinations recorded
```

| Step | Performed by | Rule |
|---|---|---|
| Generation | Agent | Output meets the structure in [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) section 5 |
| Validation | AI Validation Agent and automated checks | Checks V1 to V7. A failed Output does not reach human review. |
| Human review | Qualified human | Depth by Trust Level and risk Severity. Security Outputs and anything touching Critical risks always receive full review. |
| Approval | The reviewing human | The approver becomes the Accountable human. For merges and other four-eyes decisions a second human is required. |
| Use | Next stage | Only approved artifacts are used |

After three consecutive rejections of an agent's Output for the same task, the task is completed by a human and the Agent Owner is informed.

## 9. Knowledge Usage

- Agents draw on Knowledge Items in [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md). Only Active, fresh items may be used (G-KNW-01).
- Each Output lists the Knowledge Items it used, so a later correction to an item can be traced to affected Outputs.
- When knowledge is missing, stale or in conflict, the agent says so and lowers its Output Confidence. It does not fill the gap from general model knowledge and present it as project fact.
- Source code, the API inventory and approved requirements take precedence over Knowledge Items when they disagree. The disagreement is reported to the Knowledge Owner.

## 10. Continuous Learning

Learning is deliberate and reviewed. Agents do not change themselves.

| Trigger | Learning action | Approver |
|---|---|---|
| Output rejected in Validation or review | Reason recorded; patterns reviewed monthly by the Agent Owner | Agent Owner |
| Confirmed Hallucination | Counted in Hallucination Rate; cause analyzed; Prompt regression case added | Prompt Owner |
| Escaped defect | Reproduction test; cause recorded as a Knowledge Item; risk register updated; missed-detection analysis across stages | QE Lead, Knowledge Owner |
| Incident | Incident learning flow in AGENT_INTERACTION_MATRIX.md section 8 | Knowledge Owner |
| Gate passed a defective change | Gate reviewed | QE Lead |
| Repeated reviewer correction of the same kind | Prompt or knowledge change proposed | Prompt Owner or Knowledge Owner |

Rules:

- Every learning action that changes a Prompt or a Knowledge Item goes through its governance (review, four-eyes, versioning).
- Agent output is never added to knowledge directly.
- Human review corrections are the primary learning signal and are recorded in a form that can be analyzed.

## 11. Inputs and Outputs

| Inputs | Outputs |
|---|---|
| Source requirements, architecture, API inventory, code | Approved AI-assisted artifacts per stage |
| Risk register | Validation and review records |
| Active Knowledge Items | Gate inputs and Evidence |
| Prompts under version control | Learning actions and knowledge updates |

## 12. Metrics

| Metric | Definition | Target |
|---|---|---|
| Reliability Score per agent | AI_RELIABILITY_STANDARD.md | At or above 75 |
| Hallucination Rate per agent | AI_RELIABILITY_STANDARD.md | Per Trust Level |
| Risk Coverage for Critical and High | TEST_STRATEGY.md | 100% |
| AI-found gap yield | Gaps or defects found by agents and confirmed by humans, per period | Tracked; the measure of AI value |
| Defect Escape Rate | TEST_STRATEGY.md | Not worse than before AI assistance; trending down |
| Stage exit compliance | Stage results used downstream with a recorded approval | 100% |
| Learning closure | Escaped defects with completed learning actions | 100% for Critical and High |
| Rework after approval | Approved AI-assisted artifacts later corrected | Tracked; rising trend signals weak review |

Volume of tests generated is deliberately not a metric.

## 13. Quality Gates and Escalation

Gates: G-PR-04 and G-REL-04 enforce this strategy; all other gates apply unchanged to AI-assisted changes.

Escalation uses levels L1 to L3 from AI_GOVERNANCE.md section 9. Strategy-specific triggers:

| Trigger | Level |
|---|---|
| A stage used an unapproved upstream result | L2 |
| An agent was used outside the approved use cases | L3 |
| Defect Escape Rate worsens for two consecutive releases | L2: review of where AI assistance is used |
| Three consecutive rejections for one task | L1 Agent Owner |

## 14. Review Process

- Monthly: QE Lead reviews agent metrics and learning actions.
- Quarterly: this strategy, the lifecycle and the agent-to-stage assignment are reviewed against the metrics.
- A stage's AI assistance is withdrawn if it does not raise Confidence over two quarters.

## 15. Examples, Best Practices and Anti-Patterns

Example, new requirement "A Patient can view only their own appointments":

1. Requirement Agent structures it; Requirement Quality Agent flags that "own" is undefined for an unlinked account. Product Owner decides: unlinked accounts see an empty list.
2. Risk Agent proposes "Patient sees another patient's appointments" (PRV). Risk Owner rates it Critical with recorded Evidence.
3. Test Design Agent and BOLA Agent propose tests: own data returned, other patient's data refused, unlinked account gets an empty list. Validation passes; a QE engineer reviews and approves.
4. Engineer implements. Coverage Agent reports Risk Coverage 100% for the risk at two Test Levels and Mutation Score for the ownership check.
5. PR Review Agent comments; two humans approve because authorization code is touched; gates pass.

Best practices:

- Give agents the risk register and acceptance criteria, not only the code. Tests derived from code alone confirm what the code does, not what it should do.
- Ask agents for what is missing as well as for what to add.
- Keep each agent's task small enough for a human to review fully.

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-AQ-01 | Generate first, think later: producing tests before risks are assessed | Volume without direction | Stage 3 before stage 4 |
| AP-AQ-02 | Tests derived only from the implementation | Tests encode existing defects as expected behavior | Derive from requirements and risks |
| AP-AQ-03 | Skipping Validation because a human will review anyway | Wastes review attention and lets reference errors through | Validation is mandatory |
| AP-AQ-04 | Measuring AI value by output volume | Rewards quantity over Risk Coverage | Use AI-found gap yield |
| AP-AQ-05 | One general agent for everything | Unmeasurable, unreviewable | Narrow missions |
| AP-AQ-06 | Agent chain without human checkpoints | Errors compound across stages | Stage exits |
| AP-AQ-07 | Unreviewed self-improvement | Errors become permanent | Section 10 rules |
| AP-AQ-08 | Filling knowledge gaps from general model knowledge | Plausible invention presented as project fact | Report the gap; lower Output Confidence |
| AP-AQ-09 | Reviewer asks the same agent to confirm its own output | No independence | Independent Validation and human review |

## 16. Traceability, Governance References and Related Documents

Every AI-assisted artifact carries: stage, agent, Prompt version, Knowledge Items, Validation record, reviewer, approval, and the downstream artifacts that used it.

| Document | Owns the terms | Relationship |
|---|---|---|
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Confidence, Behavior Coverage, Risk Coverage, Evidence | Parent strategy |
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Human oversight, Four-eyes principle, escalation levels | Governing rules |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Reliability Score, Trust Level, Hallucination, Output Confidence, Validation | Measurement of agents |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Agent | Agents per stage |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Skill | Skills per agent |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Flows | Hand-offs between stages |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Knowledge Item, Knowledge Confidence, freshness | Knowledge usage |
| [RISK_MODEL.md](RISK_MODEL.md) | Risk, Severity, Risk Confidence | Stage 3 |
| [QUALITY_GATES.md](QUALITY_GATES.md) | Quality Gate, Waiver | Stages 10 and 11 |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Security test requirements | Stage 5 |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Heuristics | Day-to-day guidance |

## 17. Document Precedence

When framework documents conflict, the order is: the six principles, then AI_GOVERNANCE.md, TEST_STRATEGY.md, RISK_MODEL.md, AI_RELIABILITY_STANDARD.md, QUALITY_GATES.md, then all others. A found conflict is reported to the QE Lead and fixed in the lower document.

## 18. Baseline and Assumptions

Baseline (from the approved source document): AI assists testing, human approves; lifecycle, agents, skills, validation, governance, knowledge usage, review workflow, continuous learning.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-AQ-01 | The twelve lifecycle stages and their order | The source says "lifecycle" without stages; derived from the agent list in AGENTS_CATALOG |
| A-AQ-02 | Agent-to-stage and human-approver assignments | Not in the source |
| A-AQ-03 | Three consecutive rejections hand the task to a human | Not in the source |
| A-AQ-04 | Document precedence order in section 17 | No source document states precedence |
| A-AQ-05 | "AI-found gap yield" as the value metric | Not in the source; chosen to avoid a volume metric |
| A-AQ-06 | Withdrawal of AI assistance after two quarters without benefit | Not in the source |

## Supporting Documents

Operational documents that implement this one: [ROLE_REGISTER.md](ROLE_REGISTER.md) (who holds each role), [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) (how each gate is enforced), [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) (required records), [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) (incident procedure), [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) (test data, identities and environments).
