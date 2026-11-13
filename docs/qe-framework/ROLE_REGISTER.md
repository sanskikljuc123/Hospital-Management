# ROLE REGISTER

| Field | Value |
|---|---|
| Document ID | QE-DOC-13 |
| Version | 1.0-draft |
| Status | Draft. No role has a named holder yet; see section 6. |
| Owner | QE Lead |
| Approvers | QE Lead, AI Governance Owner, Tech Lead |
| Review cadence | Quarterly, and whenever a holder changes |
| Last updated | 2026-11-13 |

## 1. Purpose

Every framework document assigns decisions to roles. This register is the single place that defines those roles, records who holds each one and who deputizes, states which roles one person may not combine for a single decision, and shows who is responsible and accountable for each governed decision. A decision whose role has no named holder cannot be made under the framework.

## 2. Scope

All human roles named in the twelve core documents. AI agents hold no role here: they assist and are never Responsible or Accountable for a decision.

## 3. Definitions

| Term | Definition |
|---|---|
| Role | A named set of responsibilities and decision rights. |
| Holder | The named person currently assigned to a Role. |
| Delegate | The named person who may act for the Holder when the Holder is absent or excluded from a decision. |
| Standing role | A Role with one Holder for the whole team. |
| Per-item role | A Role assigned separately for each risk, agent, prompt, skill, knowledge item or incident. |
| Excluded | Barred from a specific decision by the Four-eyes principle or by section 7. |
| R, A, C, I | Responsible (does the work), Accountable (owns the decision; exactly one per decision), Consulted, Informed. |

Four-eyes principle and Accountable human are defined in [AI_GOVERNANCE.md](AI_GOVERNANCE.md).

## 4. Standing Roles

| Role | Core responsibilities | Decision rights | Defined in |
|---|---|---|---|
| QE Lead | Owns the test strategy, risk model integrity, gate definitions, agent and skill catalogs | Thresholds; test level disputes; Waivers for test, behavior and mutation gates; approval to remove tests protecting Critical or High risks | TEST_STRATEGY, QUALITY_GATES |
| Tech Lead | Testability of the design; pipeline enforcement; agent permissions | Technical risk ratings; acceptance of Medium risks; co-approval of Waivers | TEST_STRATEGY, RISK_MODEL |
| Security Lead | Security testing, threat models, data governance, security findings | Security risk ratings and acceptance; Waivers for waivable security gates; classification of Confidential and Restricted material | SECURITY_TESTING, AI_GOVERNANCE |
| Product Owner | Requirements and acceptance criteria; business risk | Approval of requirements; business Impact ratings; joint acceptance of High risks | RISK_MODEL, AI_QUALITY_ENGINEERING_STRATEGY |
| AI Governance Owner | Approved use cases, AI Risk Register, reliability standard, audit sampling | Use case approval; Trust Level promotion; adding or retiring agents and skills; joint authority at L3 | AI_GOVERNANCE, AI_RELIABILITY_STANDARD |
| Knowledge Owner | Knowledge catalog, admission, conflict resolution, knowledge gates | Admit, change or retire Knowledge Items; resolve Conflicts | KNOWLEDGE_CATALOG |
| Release Approver | Release decision on the Evidence pack | Approve or refuse a release; joint authority at L3 | QUALITY_GATES |

## 5. Per-Item and Activity Roles

| Role | Assigned per | Assigned by | Responsibilities | Defined in |
|---|---|---|---|---|
| Risk Owner | Risk | QE Lead; Security Lead for SEC and PRV | Accountable for one Risk from identification to closure | RISK_MODEL |
| Agent Owner | Agent | AI Governance Owner | Accountable for one agent's behavior and reliability | AGENTS_CATALOG |
| Prompt Owner | Prompt | Agent Owner | Maintains a Prompt, its regression set and history | AI_GOVERNANCE |
| Skill Owner | Skill | QE Lead | Maintains a Skill's definition and quality criteria | SKILLS_CATALOG |
| Item Owner | Knowledge Item | Knowledge Owner | Correctness and timely review of one item | KNOWLEDGE_CATALOG |
| Incident Owner | Incident | QE Lead; Security Lead for security incidents | Drives one incident to closure | INCIDENT_MANAGEMENT |
| Human reviewer | Output or pull request | Team practice | Reviews and approves or rejects; becomes the Accountable human on approval | AI_GOVERNANCE |
| Engineer | Change | Team practice | Implements changes and tests; owns reproduction tests for defects they fix | TEST_STRATEGY |
| QE engineer | Feature or change | QE Lead | Approves test designs and coverage reports; reviews agent Outputs | AI_QUALITY_ENGINEERING_STRATEGY |

## 6. Holder Register

No holder was named in any source document. The table must be completed before the framework is enforced.

| Role | Holder | Delegate | Date assigned | Assigned by |
|---|---|---|---|---|
| QE Lead | Unassigned | Unassigned | | |
| Tech Lead | Unassigned | Unassigned | | |
| Security Lead | Unassigned | Unassigned | | |
| Product Owner | Unassigned | Unassigned | | |
| AI Governance Owner | Unassigned | Unassigned | | |
| Knowledge Owner | Unassigned | Unassigned | | |
| Release Approver | Unassigned | Unassigned | | |

Agent Owners by role, as assigned in AGENTS_CATALOG.md:

| Agents | Agent Owner (role) | Named holder |
|---|---|---|
| AGT-01, 02, 03, 04, 05, 08, 09, 10 | QE Lead | Unassigned |
| AGT-06, 07 | Security Lead | Unassigned |
| AGT-11 | AI Governance Owner | Unassigned |

Rules for the register:

- Every Standing role has exactly one Holder and at least one Delegate. Holder and Delegate are different people.
- A change of Holder is a reviewed change to this file, approved by someone other than the incoming Holder.
- Per-item roles are recorded on the item (risk register, agent definition, knowledge item, incident record), not here.
- A vacant role is escalated at L2 within one business day. While it is vacant, decisions reserved to it wait; they do not default to the requester.

## 7. Incompatible Combinations

One person may hold several roles. For a single decision the following combinations are not allowed.

| Decision | These may not be the same person |
|---|---|
| Merge a change | Author and approving reviewer |
| Merge a change touching authorization code, identity configuration or a Critical risk area | Author and either of the two approvers |
| Rate a Critical or High risk | Risk Owner and second reviewer |
| Accept a Residual Risk | Author of the change carrying the risk, and any acceptor |
| Waive a gate | Requester and any approver |
| Change a Prompt | Prompt author and Prompt reviewer |
| Admit or change a Knowledge Item | Proposer and admitting Knowledge Owner or delegate |
| Promote a Trust Level | Agent Owner proposing and AI Governance Owner approving |
| Release | Majority author of the release's changes and Release Approver |
| Close a Critical or High incident | Incident Owner and the person confirming closure |
| Review an AI Output | The person who ran the agent and adopted the Output, and the second approver |

Structural limits, independent of any single decision:

- The AI Governance Owner for AGT-11 is also the approver of Trust Level promotions. For AGT-11 itself, promotion is proposed by the AI Governance Owner and approved by the QE Lead.
- The Release Approver should not also be the Tech Lead where the Tech Lead authors a large share of changes.

## 8. Minimum Staffing

- The framework needs at least three people to operate without breaching section 7: two approvers are required for authorization changes, and neither may be the author.
- With fewer than three, authorization changes and Critical risk changes cannot be merged under the framework. The gap is recorded as a risk in the AI Risk Register or the product risk register and escalated at L3. It is not solved by an agent acting as a reviewer.
- When one person holds more than three Standing roles, the QE Lead and AI Governance Owner record how the Four-eyes principle is kept for each decision in section 9.

## 9. Decision Matrix

A = Accountable, R = Responsible, C = Consulted, I = Informed. Each row has exactly one A.

| Decision | QE Lead | Tech Lead | Security Lead | Product Owner | AI Gov. Owner | Knowledge Owner | Release Approver | Engineer / QE engineer |
|---|---|---|---|---|---|---|---|---|
| Approve a requirement | C | C | I | A | | | | R |
| Rate a risk (non-security) | C | R | I | C | | | | Risk Owner: A |
| Rate a security or privacy risk | I | C | A | C | | | | R |
| Accept a High Residual Risk | C | R | R for SEC, PRV | A | | | I | |
| Accept a Medium Residual Risk | I | A | C for SEC, PRV | I | | | | |
| Approve a test design | C | | C for security | | | | | QE engineer: A |
| Merge a change | | C | C for security | | | | | Reviewer: A; author: R |
| Remove a test protecting a Critical or High risk | A | C | C for security | | | | | R |
| Change a gate threshold | A | R | R for security gates | I | I | | I | |
| Waive a waivable gate | A (test, behavior, mutation) | R | A (security gates) | | | A (knowledge gates) | I | Requester: R |
| Approve a release | C | C | C | C | C | | A | |
| Approve an AI use case | C | C | C | | A | | | |
| Promote a Trust Level | C | | | | A | | | Agent Owner: R |
| Change a Prompt | I | | | | C | | | Prompt Owner: A; reviewer: R |
| Admit a Knowledge Item | C | | C for classification | | | A | | Proposer: R |
| Resolve a knowledge Conflict | C | | | C | | A | | Item Owners: R |
| Declare and close an incident | A (quality) | R | A (security) | I | I if AI-assisted | C | I | Incident Owner: R |
| Add, change or retire an agent | R | C | C | | A | | | |

Where a column shows A for more than one role, the A belongs to the role named for that case (for example security gates to the Security Lead), so each actual decision still has one Accountable role.

## 10. Delegation Rules

1. A Delegate acts only when the Holder is absent for more than one business day, or is Excluded from the decision.
2. Delegation is recorded on the decision: who acted, for whom, and why.
3. A Delegate is subject to the same exclusions as the Holder.
4. Decisions at L3, Trust Level promotions and threshold reductions are not delegated without the Holder's written agreement.
5. Roles are never delegated to an agent.

## 11. Metrics

| Metric | Target |
|---|---|
| Standing roles without a named Holder | 0 |
| Standing roles without a named Delegate | 0 |
| Decisions made by an Excluded person | 0 |
| Decisions where the Accountable role could not be identified in audit | 0 |
| Days a Standing role stayed vacant | At most 5 working days |
| People holding more than three Standing roles | Reported |

## 12. Anti-Patterns

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-RR-01 | Role on paper: a role with no named Holder | Decisions reserved to it are made by whoever is present | Section 6 rules; L2 |
| AP-RR-02 | One person as author, reviewer and approver through several roles | Four-eyes principle bypassed without a visible breach | Section 7 |
| AP-RR-03 | Team-held role ("the team owns it") | Nobody is accountable | One Holder per role |
| AP-RR-04 | Permanent delegation | The Holder no longer exercises the role | Reassign the role |
| AP-RR-05 | Agent filling a staffing gap as reviewer | AI autonomy over human accountability | Section 8 |
| AP-RR-06 | Register updated after the decision | The record does not show who held the role at the time | Holder changes are reviewed changes with a date |
| AP-RR-07 | Accountability shared between two roles for one decision | Each assumes the other decided | One A per decision |

## 13. Escalation Rules

| Trigger | Escalate to | Level |
|---|---|---|
| Vacant Standing role | QE Lead and AI Governance Owner | L2 |
| Decision made by an Excluded person | AI Governance Owner and Release Approver | L3 |
| Too few people to satisfy section 7 | AI Governance Owner and Release Approver | L3 |
| Dispute over who is Accountable | QE Lead; AI Governance Owner for AI matters | L2 |

Levels are defined in AI_GOVERNANCE.md section 9.

## 14. Related Documents

| Document | Relationship |
|---|---|
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Four-eyes principle, human-reserved decisions, escalation levels |
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Roles in testing and test removal |
| [RISK_MODEL.md](RISK_MODEL.md) | Risk Owner, acceptance rights |
| [QUALITY_GATES.md](QUALITY_GATES.md) | Gate owners, Waiver approvers, Release Approver |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Agent Owners and human approvers |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Skill Owners |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Knowledge Owner, Item Owner |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Reviewer duties by Trust Level |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Human approver per lifecycle stage |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Humans accountable in each flow |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Security Lead duties |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Review practices for reviewers |
| [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) | Incident Owner |
| [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) | Records that name the acting role |
| [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) | Branch protection and code owners that enforce section 7 |
| [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) | Roles that own environments, data rules and test identities |

## 15. Baseline and Assumptions

Baseline: the roles and decision rights are collected from the twelve core documents, where each is already recorded as an assumption. No source document names a role or a person.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-RR-01 | The role set itself | Carried from the core documents (A-TS-06, A-GV-01, A-QG-06, A-KC-09) |
| A-RR-02 | Every Standing role has exactly one Holder and one Delegate | Not stated anywhere; needed for accountability |
| A-RR-03 | Minimum of three people | Derived from G-PR-02 requiring two approvers other than the author |
| A-RR-04 | The R, A, C, I assignments in section 9 | Derived from the decision rights in the core documents; the split of R and C is new |
| A-RR-05 | Promotion of AGT-11 is approved by the QE Lead | Needed because the AI Governance Owner is both its Agent Owner and the usual approver |
| A-RR-06 | Vacancy limit of 5 working days | Not in any source |
| A-RR-07 | Incident Owner role | Introduced by INCIDENT_MANAGEMENT.md |
