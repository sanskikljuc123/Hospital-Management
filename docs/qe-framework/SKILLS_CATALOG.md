# SKILLS CATALOG

| Field | Value |
|---|---|
| Document ID | QE-DOC-10 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 14 |
| Owner | QE Lead |
| Approvers | QE Lead, AI Governance Owner |
| Review cadence | Quarterly, and whenever an agent is added or changed |
| Last updated | 2026-11-13 |

## 1. Purpose

A Skill is a defined, reusable capability that agents apply. This catalog lists the Skills, what each must be able to do, how each is checked, and which agents consume it. An agent may use only Skills listed for it here and in [AGENTS_CATALOG.md](AGENTS_CATALOG.md).

## 2. Scope

The nine Skills used by framework agents. Human competencies are out of scope, although each Skill names the human role that owns it.

## 3. Definitions

| Term | Definition |
|---|---|
| Skill | A named capability with defined inputs, outputs, procedure, quality criteria and owner, identified as SKL-nn. |
| Consumer | An agent that uses a Skill. |
| Primary Skill | The Skill central to an agent's mission. Each agent has exactly one. |
| Supporting Skill | A Skill an agent uses in service of its Primary Skill. |
| Skill Owner | The human accountable for a Skill's definition and quality criteria. |
| Skill hierarchy | The grouping of Skills into the three tiers in section 5. |

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| QE Lead | Owns the catalog; Skill Owner for SKL-01, 02, 03, 05, 07 |
| Security Lead | Skill Owner for SKL-04 and SKL-08 |
| Tech Lead | Skill Owner for SKL-06 |
| AI Governance Owner | Skill Owner for SKL-09; approves adding or removing a Skill |
| Agent Owners | Ensure their agents apply Skills as defined |

## 5. Skill Hierarchy and Consumers

| Tier | Skill | ID | Consumers (P = Primary) |
|---|---|---|---|
| Analysis | Requirement Analysis | SKL-01 | AGT-01 (P), AGT-02 (P) |
| Analysis | Risk Analysis | SKL-02 | AGT-03 (P), AGT-04, AGT-06, AGT-08 |
| Design and verification | Test Design | SKL-03 | AGT-04 (P), AGT-05 (P), AGT-07, AGT-08, AGT-09 (P) |
| Design and verification | Security Testing | SKL-04 | AGT-06 (P), AGT-07 (P), AGT-09 |
| Design and verification | Mutation Testing | SKL-05 | AGT-08 (P), AGT-04, AGT-09 |
| Evidence and assurance | Observability | SKL-06 | AGT-05, AGT-10 |
| Evidence and assurance | Failure Analysis | SKL-07 | AGT-10 (P), AGT-11 |
| Evidence and assurance | Compliance | SKL-08 | AGT-01, AGT-02, AGT-03, AGT-06, AGT-11 |
| Evidence and assurance | Prompt Engineering | SKL-09 | AGT-11 (P); human Prompt Owners |

Every Skill has at least one Consumer and every agent has exactly one Primary Skill. Open dependency findings are listed in section 11.

## 6. Skill Definitions

Each Skill produces Outputs that meet [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) section 5. Quality criteria below are additional.

### SKL-01 Requirement Analysis

| Aspect | Definition |
|---|---|
| Capability | Extract, structure and assess requirements; identify ambiguity, contradiction, gaps and untestable statements |
| Inputs | Source requirement material, K-DOM, K-REQ, K-STD |
| Outputs | Requirements with IDs, acceptance criteria in checkable form, open questions |
| Procedure | Separate facts from interpretation; state each acceptance criterion as an observable outcome; list every assumption as a question, not as a requirement |
| Quality criteria | No acceptance criterion invented without a source; every ambiguity raised; each criterion testable |
| Must not | Resolve an ambiguity by choosing an interpretation |

### SKL-02 Risk Analysis

| Aspect | Definition |
|---|---|
| Capability | Identify Risks, propose Impact, Likelihood and Severity, apply STRIDE, write Abuse Cases, propose risk-to-test mapping, all per [RISK_MODEL.md](RISK_MODEL.md) |
| Inputs | Approved requirements, architecture, API inventory, K-RSK, K-DEF |
| Outputs | Proposed register entries with reasons and Evidence; Abuse Cases |
| Procedure | Start from harm to users and data; check each trust boundary; consult incident history; state Risk Confidence honestly |
| Quality criteria | Each Risk is an event with a consequence; each rating has a stated reason; none presented as final |
| Must not | Rate without Evidence at more than Risk Confidence Low; lower a rating to fit a gate |

### SKL-03 Test Design

| Aspect | Definition |
|---|---|
| Capability | Derive tests from acceptance criteria and Risks: equivalence classes, boundaries, state transitions, negative cases, Side Effect checks; choose the Test Level per [TEST_STRATEGY.md](TEST_STRATEGY.md) |
| Inputs | Approved requirements, risk register, API inventory, K-TST, K-DEF |
| Outputs | Test designs and draft tests, each traced to a criterion, Risk or defect |
| Procedure | Design from "what must be true" and "what must never happen", not from the implementation; name the failure each test detects |
| Quality criteria | Deterministic; asserts behavior and Side Effects; fails when the behavior is broken; traced |
| Must not | Produce tests without a stated purpose; assert on implementation detail; reference endpoints or fields not in the inventory |

### SKL-04 Security Testing

| Aspect | Definition |
|---|---|
| Capability | Design tests for authentication, authorization, BOLA, information leakage, token handling and Abuse Cases per [SECURITY_TESTING.md](SECURITY_TESTING.md) |
| Inputs | API inventory with roles, identity configuration, threat models, K-SEC, K-RSK |
| Outputs | Security test cases per endpoint and role; findings with proposed Severity |
| Procedure | Enumerate every endpoint by every role and by no identity; for each object identifier test another owner's object; check response bodies and errors for leakage |
| Quality criteria | Complete role by endpoint matrix; every negative case asserts both the status and the absence of data and state change |
| Must not | Run against environments other than designated test environments; use real credentials or personal data |

### SKL-05 Mutation Testing

| Aspect | Definition |
|---|---|
| Capability | Run and interpret mutation analysis; identify surviving mutants; propose the tests that would kill them; identify likely equivalent mutants |
| Inputs | Code and tests in scope, risk tiers, thresholds from [QUALITY_GATES.md](QUALITY_GATES.md) |
| Outputs | Mutation Score by risk tier; surviving mutants with location and meaning; proposed tests |
| Procedure | Prioritize survivors in Critical and High tier code and authorization logic; explain each survivor as a missing check in plain terms |
| Quality criteria | Scores computed from tool output, never estimated; equivalence claims carry a reason |
| Must not | Change mutation scope or exclusions; declare a mutant equivalent as fact |

### SKL-06 Observability

| Aspect | Definition |
|---|---|
| Capability | Use logs, traces, metrics and health signals as Evidence; check that behaviors emit the signals needed to detect and diagnose failure |
| Inputs | Execution logs, traces, health endpoints, pipeline output |
| Outputs | Evidence extracts with references; gaps where a failure would be invisible |
| Procedure | Quote the exact signal and where it came from; separate observed fact from inference |
| Quality criteria | Every conclusion cites a specific signal; no personal data or secrets reproduced |
| Must not | Infer a cause from absence of logs without saying so |

### SKL-07 Failure Analysis

| Aspect | Definition |
|---|---|
| Capability | Classify a failure (product defect, test defect, environment, flakiness, data), propose causes ranked by Evidence, propose the reproduction test |
| Inputs | Failure output, change history, K-DEF, signals via SKL-06 |
| Outputs | Triage report: classification, Evidence, hypotheses with confidence, next diagnostic step |
| Procedure | Reproduce before explaining; change one thing at a time; keep hypotheses separate from findings |
| Quality criteria | Classification supported by Evidence; alternative causes listed; no fix proposed that weakens a test |
| Must not | Recommend retrying, skipping or loosening a test as a resolution |

### SKL-08 Compliance

| Aspect | Definition |
|---|---|
| Capability | Check artifacts against framework rules and applicable standards: traceability, data classification, auditability, required approvals |
| Inputs | The artifact, [AI_GOVERNANCE.md](AI_GOVERNANCE.md), K-STD, gate definitions |
| Outputs | Compliance findings with the rule breached and the Evidence |
| Procedure | Cite the exact rule; report absence of required Evidence as a finding |
| Quality criteria | No finding without a cited rule; no "compliant" statement without the checks listed |
| Must not | Declare regulatory compliance; that is a human judgment |

### SKL-09 Prompt Engineering

| Aspect | Definition |
|---|---|
| Capability | Write, test and evaluate Prompts so that agents cite Evidence, admit uncertainty, resist injected instructions and stay within mission; evaluate Outputs against the Prompt's contract |
| Inputs | Agent mission, K-PRM, rejected Outputs and review corrections |
| Outputs | Versioned Prompts with regression sets; evaluation results |
| Procedure | Follow Prompt governance in AI_GOVERNANCE.md section 7; add a regression case for every confirmed Hallucination |
| Quality criteria | Regression set passes, including the "unknown" and injection cases |
| Must not | Change a Prompt outside governance; tune a Prompt to raise scores without raising correctness |

## 7. Inputs, Outputs and Quality Gates

Inputs and outputs are listed per Skill above. Skills feed gates as follows:

| Skill | Gates informed |
|---|---|
| SKL-01 | G-BEH-01 (through acceptance criteria) |
| SKL-02 | G-BEH-02, G-REL-02 |
| SKL-03 | G-TST-01 to G-TST-03, G-PR-03 |
| SKL-04 | G-SEC-01 to G-SEC-04 |
| SKL-05 | G-MUT-01, G-MUT-02 |
| SKL-06, SKL-07 | G-TST-01, G-KNW-03 |
| SKL-08 | G-PR-03, G-PR-04, G-KNW-02 |
| SKL-09 | G-PR-04 (through Validation) |

## 8. Metrics

| Metric | Definition |
|---|---|
| Skill rejection rate | Outputs rejected for failing a Skill's quality criteria, per Skill |
| Skill-attributed Hallucinations | Confirmed Hallucinations per Skill |
| Reviewer corrections per Skill | Count and kind of human corrections |
| Consumers per Skill | Must be at least 1 |
| Skills without owner | Must be 0 |

These feed the per-agent Reliability Score; a Skill with a rising rejection rate is reviewed by its Skill Owner.

## 9. Best Practices

- Apply one Skill per Output where possible, so failures can be attributed.
- State what the Skill could not determine. A partial, honest result is the expected behavior.
- Design before generating: a Test Design Output lists the cases and their purpose before any test code.
- Keep Skill definitions short enough to be fully reflected in the Prompt and its regression set.
- Change a Skill definition and its Prompt together, in one reviewed change.

## 10. Anti-Patterns

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-SK-01 | Skill sprawl: adding near-duplicate Skills | Inconsistent behavior, unclear ownership | New Skills need AI Governance Owner approval |
| AP-SK-02 | Orphan Skill with no Consumer | Maintained but unused, or used informally | Remove or assign |
| AP-SK-03 | Agent using an unlisted Skill | Unmeasured and ungoverned behavior | Catalog change first |
| AP-SK-04 | Skill defined only inside a Prompt | No reviewable definition | Define here; Prompt implements |
| AP-SK-05 | Quality criteria that cannot be checked | Validation becomes opinion | Criteria must be verifiable |
| AP-SK-06 | Test Design from implementation only | Encodes current defects as expected | SKL-03 procedure |
| AP-SK-07 | Failure Analysis that ends in "retry" | Hides nondeterminism | SKL-07 must-not rule |
| AP-SK-08 | Estimated metrics | Numbers without Evidence | SKL-05 quality criteria |

## 11. Dependency Findings

Verification of the agent and skill model found the following. None is resolved by inventing a Skill; each needs a decision.

| ID | Finding | Effect | Suggested decision |
|---|---|---|---|
| DF-01 | No Skill for review or validation of artifacts. AGT-09 (PR Review) and AGT-11 (AI Validation) are assigned existing Skills as a best fit. | The central activities of two agents have no defined capability or quality criteria | Decide whether to add a "Review and Validation" Skill |
| DF-02 | No API-specific Skill. AGT-05 (API) uses Test Design as Primary. | Contract-specific practice lives only in the agent definition | Decide whether API contract testing is part of SKL-03 or separate |
| DF-03 | SKL-09 Prompt Engineering has a single agent Consumer. Its main users are human Prompt Owners. | Low agent consumption; acceptable if intended | Confirm that SKL-09 is primarily a human-applied Skill |
| DF-04 | SKL-06 Observability and SKL-08 Compliance are never Primary. | No agent is accountable for observability or compliance as a mission | Confirm these are supporting by design |
| DF-05 | No Skill for knowledge curation, although knowledge gates exist | Knowledge work is entirely human | Confirm this is intended; it is the safer position |

## 12. Review Process and Escalation

- A Skill change is reviewed by its Skill Owner and one other approver and triggers the Trust Level reset for consuming agents where the Prompt changes.
- Adding or removing a Skill needs AI Governance Owner approval and an update to AGENTS_CATALOG.md in the same change.

| Trigger | Escalate to | Level |
|---|---|---|
| Agent found using an unlisted Skill | Agent Owner | L1, then L2 if repeated |
| Skill quality criteria repeatedly failed | Skill Owner | L2 |
| Disagreement on Skill ownership or scope | QE Lead | L2 |

Levels are defined in AI_GOVERNANCE.md section 9.

## 13. Traceability, Governance References and Related Documents

Each Output records the Skill applied. Each Skill traces to its Consumers (section 5), its gates (section 7) and its Knowledge types.

| Document | Relationship |
|---|---|
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Consumers of these Skills; must match section 5 |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | How Skill outputs pass between agents |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Skill per lifecycle stage |
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Prompt governance; approval of Skill changes |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Output requirements all Skills meet |
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Rules behind SKL-03 and SKL-05 |
| [RISK_MODEL.md](RISK_MODEL.md) | Rules behind SKL-02 |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Rules behind SKL-04 |
| [QUALITY_GATES.md](QUALITY_GATES.md) | Gates informed by Skills |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Knowledge types each Skill reads |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Heuristics applied by the Skills |

## 14. Baseline and Assumptions

Baseline (from the approved source document): the nine Skills (Requirement Analysis, Risk Analysis, Test Design, Security Testing, Mutation Testing, Observability, Failure Analysis, Compliance, Prompt Engineering) and a best practices section.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-SK-01 | The three-tier Skill hierarchy | The brief asks for a skill hierarchy; the source gives a flat list |
| A-SK-02 | Skill-to-agent assignments and Primary markings | Not in the source |
| A-SK-03 | Each Skill's procedure, quality criteria and must-not rules | The source names Skills only |
| A-SK-04 | Skill Owner assignments | Not in the source |
| A-SK-05 | Exactly one Primary Skill per agent | Introduced for attribution of failures |

## Supporting Documents

Operational documents that implement this one: [ROLE_REGISTER.md](ROLE_REGISTER.md) (who holds each role), [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) (how each gate is enforced), [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) (required records), [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) (incident procedure), [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) (test data, identities and environments).
