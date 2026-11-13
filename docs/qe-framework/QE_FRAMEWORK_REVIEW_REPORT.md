# QE FRAMEWORK REVIEW REPORT

| Field | Value |
|---|---|
| Subject | Expansion of the twelve documents in `QE_Framework_Docs_Backup` |
| Date | 2026-11-13 |
| Output location | `docs/qe-framework/` (the backup folder is unchanged) |
| Status of the document set | Draft. 109 assumptions await ratification: 77 in the twelve core documents, 32 in the five supporting documents added afterwards (section 11). |

## 1. Finding That Shapes Everything Else

Each of the twelve source files contained a title and one sentence listing topics (137 to 251 bytes each, 2,135 bytes in total). They held no thresholds, definitions, role names, agent details or procedures.

Consequences:

- The topic lists and the six principles in the brief were treated as the approved decisions. Every topic named in a source file is present in its expanded document, and none was dropped or renamed.
- Everything more specific than a topic name had to be supplied. Each such item is recorded as an ASSUMPTION with an ID in the final section of its document ("Baseline and Assumptions"), with the reason it was needed. There are 77.
- The expanded documents are therefore a consistent proposal, not a record of decisions already made. They become approved architecture only when the named owners ratify the assumptions.

## 2. Architecture Map

### 2.1 Core principles

In precedence order: reliability over speed; confidence over coverage; evidence over assumptions; validation over generation; human accountability over AI autonomy; risk coverage over test quantity. Two source statements restate them: "AI assists testing. Human approves." and "Trustworthiness over intelligence."

### 2.2 Models found in the sources and how they were made concrete

| Model | From the source | Made concrete as |
|---|---|---|
| Agent hierarchy | 11 agent names | 4 tiers: Analysis (AGT-01 to 03), Design (04 to 07), Assessment (08 to 10), Assurance (11) |
| Skill hierarchy | 9 skill names | 3 tiers: Analysis (SKL-01, 02), Design and verification (03 to 05), Evidence and assurance (06 to 09) |
| Governance model | 9 governance topics | Human-reserved decisions, 9 approved and 6 prohibited use cases, four-eyes table, escalation levels L1 to L3, audit record |
| Risk model | 9 risk topics | 10 categories, 4 by 4 matrix, 4 Severity levels, Risk Confidence, appetite and acceptance limits, risk-to-test mapping |
| Reliability model | 7 reliability topics | Reliability Score formula, Validation checks V1 to V7, Trust Levels T0 to T3, Output Confidence |
| Quality gate model | 8 gate families | 24 gates in 7 families with IDs, waivable or not, and a Waiver process |
| Knowledge model | 8 knowledge topics | 9 types, 7 lifecycle states, 4 classes, 3 confidence levels, freshness intervals, conflict order |

### 2.3 Term ownership

Each term is defined in one document and used unchanged elsewhere.

| Owning document | Terms |
|---|---|
| TEST_STRATEGY | Confidence, Behavior Coverage, Risk Coverage, Code Coverage, Deterministic Test, Side Effect, Mutation Score, Flaky Test, Evidence, Test Level |
| RISK_MODEL | Risk, Impact, Likelihood, Severity, Risk Confidence, Risk Appetite, Mitigation, Residual Risk, Risk Acceptance, Abuse Case, Threat |
| AI_RELIABILITY_STANDARD | Output, Claim, Evidence Completeness, Validation, Review, Hallucination, Hallucination Rate, Reliability Score, Trust Level, Output Confidence |
| AI_GOVERNANCE | Human oversight, Accountable human, Four-eyes principle, Approved use case, Prompt, AI Risk Register, Audit record, Escalation level |
| QUALITY_GATES | Quality Gate, Waiver, Evidence pack |
| KNOWLEDGE_CATALOG | Knowledge Item, Knowledge Confidence, Freshness, Stale, Classification, Conflict |
| AGENTS_CATALOG / SKILLS_CATALOG | Agent, Mission, Agent Owner / Skill, Consumer, Primary Skill |
| AGENT_INTERACTION_MATRIX | Hand-off, Checkpoint, Confidence propagation |

### 2.4 Document dependency graph

An arrow means "depends on definitions or rules in".

```
Six principles
   |
TEST_STRATEGY  (root)
   |-> RISK_MODEL ------------------> SECURITY_TESTING
   |        |                               |
   |        v                               v
   |-> QUALITY_GATES <-- AI_RELIABILITY_STANDARD, SECURITY_TESTING, KNOWLEDGE_CATALOG
   |
AI_QUALITY_ENGINEERING_STRATEGY --> AI_GOVERNANCE --> AI_RELIABILITY_STANDARD
   |                                     |
   |-> AGENTS_CATALOG --> SKILLS_CATALOG  |-> KNOWLEDGE_CATALOG
   |        |
   |        v
   |-> AGENT_INTERACTION_MATRIX (uses agents, reliability, knowledge, risk)
   |
QUALITY_ENGINEERING_BEST_PRACTICES (reads all; defines no rules)
```

Precedence when documents conflict (ASSUMPTION A-AQ-04): principles, AI_GOVERNANCE, TEST_STRATEGY, RISK_MODEL, AI_RELIABILITY_STANDARD, QUALITY_GATES, then the rest.

Circular dependencies: AI_GOVERNANCE and AI_RELIABILITY_STANDARD refer to each other (governance uses Trust Levels; the standard uses escalation levels). KNOWLEDGE_CATALOG and AI_GOVERNANCE refer to each other for classification and data rules. Both are resolved by single ownership of each term, so neither defines the other's concept.

### 2.5 Duplicates and contradictions detected in the sources

| # | Issue in the sources | Resolution applied |
|---|---|---|
| 1 | "Severity matrix" in RISK_MODEL and "severity model" in SECURITY_TESTING: two severity scales implied | One scale, owned by RISK_MODEL. SECURITY_TESTING gives rating guidance only. |
| 2 | "Security gates" in both QUALITY_GATES and SECURITY_TESTING | Defined once in QUALITY_GATES. SECURITY_TESTING lists the tests behind them. |
| 3 | Escalation named in four documents (AI_GOVERNANCE, QUALITY_GATES, AGENTS_CATALOG, AGENT_INTERACTION_MATRIX) with no shared model | Levels L1 to L3 defined in AI_GOVERNANCE section 9. Others list only their own triggers. |
| 4 | "Confidence" used with four meanings: confidence over coverage, risk confidence, knowledge confidence, confidence propagation | Four distinct terms: Confidence, Risk Confidence, Knowledge Confidence, Output Confidence. The matrix links them by a ceiling rule. |
| 5 | Anti-patterns in both TEST_STRATEGY and BEST_PRACTICES | Defined in the owning document; BEST_PRACTICES holds an index plus practice-level ones. |
| 6 | Abuse cases in both RISK_MODEL and SECURITY_TESTING | Format owned by RISK_MODEL; families and tests in SECURITY_TESTING. |
| 7 | "Risk register" in AI_GOVERNANCE alongside the product risk register implied by RISK_MODEL | Named "AI Risk Register"; uses the RISK_MODEL scale and category AIQ. |
| 8 | "Validation" (strategy) and "validation, review" (reliability standard) not distinguished | Validation is the independent check; Review is the human assessment. Neither replaces the other. |
| 9 | "Behavior coverage" is a gate family but defined nowhere; "Coverage Agent" does not say which coverage | Behavior Coverage and Risk Coverage defined in TEST_STRATEGY; Code Coverage declared diagnostic only. |
| 10 | "Best practices" in both SKILLS_CATALOG and BEST_PRACTICES | SKILLS_CATALOG keeps skill-application practices only. |
| 11 | Mutation testing in three places (strategy, gates, skill) | Rules in TEST_STRATEGY, thresholds in QUALITY_GATES, capability in SKILLS_CATALOG. |
| 12 | Traceability in both TEST_STRATEGY and BEST_PRACTICES | Rule in TEST_STRATEGY; habits in BEST_PRACTICES. |
| 13 | Security Agent and BOLA Agent overlap on authorization | Split by role-level versus object-level authorization (finding AF-03, needs confirmation). |
| 14 | No source names any human role, although "human approves" and "ownership" are required throughout | A minimal role set was introduced (ASSUMPTION in every document). |
| 15 | Knowledge gates exist but no agent or skill covers knowledge | Kept human-only (findings AF-01, DF-05). |
| 16 | Compliance and Observability are skills with no document describing what they apply | Listed in the Missing Documents Report. |

No source statement had to be overridden. Every resolution narrows or assigns ownership; none reverses a stated decision.

## 3. Per-Document Review

Item 7 of the requested format (the final improved document) is the file of the same name in this folder.

### 3.1 TEST_STRATEGY.md

| Item | Content |
|---|---|
| Current assessment | One sentence naming 14 topics. A sound outline; no content. |
| Gaps found | No definitions of confidence or coverage; test levels not listed; no rules for removal; no metrics, roles or references. |
| Improvements applied | Defined the coverage terms; made Code Coverage diagnostic only; rules for determinism, Side Effects, reproduction and removal; ten anti-patterns. |
| New sections added | Scope, Definitions, Ownership and Roles, Inputs and Outputs, Metrics, Review and Escalation, Related Documents, Baseline and Assumptions. All 14 source topics kept as sections. |
| Cross references added | All eleven other documents. |
| Consistency review | Owns the terms Risk Coverage, Behavior Coverage, Confidence and Evidence. Thresholds are deferred to QUALITY_GATES, severity to RISK_MODEL. |

### 3.2 AI_QUALITY_ENGINEERING_STRATEGY.md

| Item | Content |
|---|---|
| Current assessment | Two governing statements and eight topics. Clear intent; no lifecycle. |
| Gaps found | Lifecycle stages undefined; no approver per stage; "continuous learning" without controls. |
| Improvements applied | Twelve-stage lifecycle with agent, skill, human approver and output per stage; review workflow; controlled learning triggers; document precedence. |
| New sections added | Strategy Principles (applied to AI), Metrics, Gates and Escalation, Examples, Anti-Patterns, Document Precedence, Baseline and Assumptions. |
| Cross references added | All eleven other documents, with term ownership per document. |
| Consistency review | Agent and skill assignments match AGENTS_CATALOG and SKILLS_CATALOG. Volume of generated tests is deliberately not a metric. |

### 3.3 AGENTS_CATALOG.md

| Item | Content |
|---|---|
| Current assessment | Eleven agent names and five attribute headings. |
| Gaps found | No mission, owner, skills, knowledge, limits, escalation or metrics for any agent; no hierarchy; overlaps between agent pairs. |
| Improvements applied | Full definition for each agent; common rules; four-tier hierarchy; dependency verification table. |
| New sections added | Agent Hierarchy, Rules Common to All Agents, Dependency Verification, Example, Anti-Patterns, Baseline and Assumptions. |
| Cross references added | All eleven other documents. |
| Consistency review | Skill assignments match SKILLS_CATALOG section 5 exactly; knowledge assignments match KNOWLEDGE_CATALOG section 5. |

### 3.4 AGENT_INTERACTION_MATRIX.md

| Item | Content |
|---|---|
| Current assessment | One sentence naming six flows. |
| Gaps found | No matrix; no rule for confidence propagation; no checkpoints between agents. |
| Improvements applied | Producer by consumer matrix; validation flow; knowledge propagation; ten-step incident learning flow; confidence ceiling rule; escalation flow. |
| New sections added | Definitions, Metrics, Best Practices, Anti-Patterns, Baseline and Assumptions. |
| Cross references added | All eleven other documents. |
| Consistency review | Uses the three confidence terms without merging them. Escalation levels are those of AI_GOVERNANCE. |

### 3.5 SKILLS_CATALOG.md

| Item | Content |
|---|---|
| Current assessment | Nine skill names plus "best practices". |
| Gaps found | No definition, owner or consumer for any skill; no skill for review, validation or API testing. |
| Improvements applied | Definition, procedure, quality criteria and limits per skill; consumer table; gates informed per skill; dependency findings DF-01 to DF-05. |
| New sections added | Skill Hierarchy and Consumers, Dependency Findings, Metrics, Anti-Patterns, Baseline and Assumptions. |
| Cross references added | All eleven other documents. |
| Consistency review | No skill was added. Gaps are reported for decision instead. |

### 3.6 AI_GOVERNANCE.md

| Item | Content |
|---|---|
| Current assessment | Nine governance topics. |
| Gaps found | No roles; no list of use cases; no escalation levels; no audit fields; no data classes. |
| Improvements applied | Human-reserved decisions; approved and prohibited use cases; prompt governance with regression sets; four-eyes table; escalation levels; data rules; AI Risk Register; audit record. |
| New sections added | Definitions, Ownership and Roles, Metrics, Examples, Anti-Patterns, Baseline and Assumptions. All nine source topics kept as sections. |
| Cross references added | All eleven other documents. |
| Consistency review | Owns escalation levels and the Four-eyes principle for the whole set. |

### 3.7 QUALITY_GATES.md

| Item | Content |
|---|---|
| Current assessment | Eight gate family names. |
| Gaps found | No gates, thresholds, owners or exception path. |
| Improvements applied | 24 gates with pass conditions; evaluation order; Waiver process; fail-closed rule. |
| New sections added | Definitions, Gate Evaluation Order, Waivers, Metrics, Examples, Anti-Patterns, Baseline and Assumptions. |
| Cross references added | All eleven other documents. |
| Consistency review | Single home for every threshold. A script check found no gate ID used elsewhere that is not defined here. |

### 3.8 RISK_MODEL.md

| Item | Content |
|---|---|
| Current assessment | Nine risk topics. |
| Gaps found | No categories, scales, appetite values or mapping. |
| Improvements applied | Categories; Impact and Likelihood scales; matrix; STRIDE method; Abuse Case format; Risk Confidence rule; appetite and acceptance limits; register fields; mapping table. |
| New sections added | Definitions, Risk Lifecycle and Register, Metrics, Anti-Patterns, Escalation Rules, Baseline and Assumptions. |
| Cross references added | All eleven other documents. |
| Consistency review | Sole owner of Severity. Agent-proposed ratings start at Risk Confidence Low. |

### 3.9 AI_RELIABILITY_STANDARD.md

| Item | Content |
|---|---|
| Current assessment | One principle and seven measurement topics. |
| Gaps found | No formula, levels, thresholds or definition of hallucination. |
| Improvements applied | Output requirements; Reliability Score; hard rejection rules; Validation checks; Trust Levels; escalation on hallucination. |
| New sections added | Definitions, Output Confidence, Examples, Anti-Patterns, Baseline and Assumptions. |
| Cross references added | All eleven other documents. |
| Consistency review | No Trust Level removes human approval. See reliability flags R-01 to R-04. |

### 3.10 SECURITY_TESTING.md

| Item | Content |
|---|---|
| Current assessment | Ten security topics, including JWT/Keycloak. |
| Gaps found | No test cases; OWASP list unspecified; severity model would duplicate RISK_MODEL. |
| Improvements applied | Case tables for authentication, authorization, BOLA and leakage; Keycloak checks; OWASP API Top 10 mapping; Abuse Case families. |
| New sections added | Definitions, Security Testing Principles, Review and Escalation, Anti-Patterns, Baseline and Assumptions. |
| Cross references added | All eleven other documents. |
| Consistency review | Uses the RISK_MODEL scale and QUALITY_GATES thresholds without restating them. Examples use the HospitalManagement API. |

### 3.11 KNOWLEDGE_CATALOG.md

| Item | Content |
|---|---|
| Current assessment | Eight knowledge topics. |
| Gaps found | No types, states, intervals or conflict rules; no owner role. |
| Improvements applied | Taxonomy; metadata; lifecycle; classification; confidence; freshness; retention; conflict order; governance. |
| New sections added | Definitions, Knowledge Item Metadata, Metrics, Example, Anti-Patterns, Baseline and Assumptions. |
| Cross references added | All eleven other documents. |
| Consistency review | Agent output cannot become knowledge without human verification. |

### 3.12 QUALITY_ENGINEERING_BEST_PRACTICES.md

| Item | Content |
|---|---|
| Current assessment | Eight practice topics. |
| Gaps found | No content; risk of duplicating rules and anti-patterns from other documents. |
| Improvements applied | Heuristics; review questions; AI practices; investigation method; anti-pattern index; red flags; decision rules; worked example. |
| New sections added | Definitions, Anti-Pattern Index, Examples, Baseline and Assumptions. |
| Cross references added | All eleven other documents. |
| Consistency review | Defines no gates or thresholds. Rules elsewhere take precedence. |

## 4. Skills and Agents Verification

| Check | Result |
|---|---|
| Every agent uses one or more skills | Pass, 11 of 11 |
| Every skill has one or more consumers | Pass, 9 of 9 |
| Every agent has Mission, Ownership, Outputs, Escalation Rules, Success Metrics | Pass, 11 of 11 |
| Agent IDs and skill IDs used across the set are all defined | Pass (script check) |

All assignments are assumptions (A-AG-04, A-SK-02). Missing dependencies:

| ID | Missing dependency |
|---|---|
| DF-01 / AF-02 | No skill for review or validation. PR Review Agent and AI Validation Agent borrow the nearest skills. |
| DF-02 | No API testing skill for the API Agent. |
| DF-03 | Prompt Engineering has one agent consumer; it is mainly a human skill. |
| DF-04 | Observability and Compliance are never a primary skill. |
| DF-05 / AF-01 | No agent or skill for knowledge curation or release. Both are human-only, which fits principle 5. |
| AF-03 | Security Agent and BOLA Agent boundary needs confirmation. |
| AF-04 | Requirement Agent and Requirement Quality Agent share skill and knowledge; kept separate for independence. |

## 5. Reliability Review

Question applied: does this increase confidence and reliability? Items below do not clearly do so, or could work against it.

| ID | Where | Concern | Suggested improvement |
|---|---|---|---|
| R-01 | AI_RELIABILITY_STANDARD, Reliability Score | A composite can hide one weak component behind strong ones. | Add a floor per component in addition to the two hard rules. |
| R-02 | AI_RELIABILITY_STANDARD, Review Acceptance Rate | Lenient reviewers raise the score. The measure rewards rubber-stamping, which BEST_PRACTICES lists as a red flag. | Calibrate with seeded-defect audits; consider removing it from the score and keeping it as a monitored signal. |
| R-03 | AI_RELIABILITY_STANDARD, Hallucination Rate | The rate counts only detected hallucinations. Weak review produces a low rate and a higher Trust Level. | Periodically inject known-false claims and measure the detection rate; report it beside the Hallucination Rate. |
| R-04 | AI_RELIABILITY_STANDARD, Trust Levels T2 and T3 | Sampled review of non-critical claims reduces human checking. This is a trade of review effort against principle 5. | Ratify explicitly, or require full review at all levels until escaped-hallucination data exists. |
| R-05 | AI Validation Agent | One agent validates every output. A blind spot there affects everything. | Keep the 20% human sample; consider a second independent validation method for Critical items. |
| R-06 | QUALITY_GATES, Waivers | A waiver path can be used to weaken gates. It was added because an informal bypass is worse. | Ratify the non-waivable list first; report waiver counts to the Release Approver. |
| R-07 | Risk Coverage definition | "Fails when the mitigation is removed" is costly to show by hand and may be asserted without being done. | Require the mutation report or a recorded run as the evidence; audit a sample. |
| R-08 | KNOWLEDGE_CATALOG, freshness | Interval-based review invites date changes without checks. | Require the review record to name what was compared with what. |
| R-09 | Metrics marked "Tracked" | A metric with no target drives no decision. | Set a target or a trigger for each after one quarter of data. |
| R-10 | Role concentration | The QE Lead owns eight documents and eight agents. In a small team the Four-eyes principle becomes hard to honour. | Name delegates; record who may not approve what. |
| R-11 | Enforcement | No document says which tool enforces which gate. A gate that is not wired to the pipeline is advisory. | Produce the pipeline mapping listed in section 8. |
| R-12 | All thresholds | Every number is an assumption with no baseline data. A wrong threshold gives false confidence or constant waivers. | Measure the current state, then ratify or adjust. |

## 6. Anti-Pattern Review

The set now defines 109 anti-patterns. Remaining weaknesses:

### 6.1 Missing anti-patterns

| Area | Not yet covered |
|---|---|
| Test data | Tests that depend on seeded development data or shared accounts |
| Environments | Differences between test and production configuration that hide defects, especially identity settings |
| Performance | No anti-patterns, because no performance document exists |
| Requirements | Acceptance criteria written after the tests |
| Metrics reporting | Reporting averages that hide a failing Critical item |
| Roles | One person holding author, reviewer and approver roles through delegation |
| Model changes | A vendor model update applied without notice to the team |

### 6.2 Weak anti-pattern definitions

All 109 entries state the harm and the required response. None states how the anti-pattern is detected or who checks for it. Without a detection signal an anti-pattern is found only by chance. Suggested: add a "detected by" column (a gate, a metric, a review question or an audit) to each table.

### 6.3 Areas vulnerable to false confidence

- Green pipelines when gates are not yet wired to tools (R-11).
- Risk Coverage reported from links alone, without evidence that tests fail (R-07).
- Reliability Scores computed on small samples or lenient reviews (R-02, R-03).
- A complete-looking Role by endpoint matrix built from the code instead of the requirement.
- Mutation Score kept high by narrowing scope (AP-QG-04 covers this; detection is not defined).

### 6.4 Areas vulnerable to AI hallucination

- Requirement extraction (AGT-01): invented acceptance criteria are plausible and hard to spot without the source beside them.
- Risk proposals (AGT-03): ratings with confident reasons and no evidence.
- API and security cases (AGT-05 to 07): endpoints, fields and roles that do not exist. Check V2 covers references; it does not cover invented behavior of real endpoints.
- Failure triage (AGT-10): a fluent cause that was never reproduced.
- Validation records (AGT-11): a validator can itself assert that evidence supports a claim when it does not.
- Knowledge proposals drafted by agents.

### 6.5 Areas vulnerable to governance failure

- Human review under time pressure: the control most relied on and the easiest to hollow out.
- Four-eyes in a small team (R-10).
- Waivers and Risk Acceptances renewed by habit.
- Use of AI tools outside the catalog.
- Audit records that exist and are never sampled.
- Assumptions in these documents being treated as approved without ratification.

## 7. Architecture Consistency Report

| Check | Method | Result |
|---|---|---|
| Every document links to all eleven others | Script | Pass, 132 of 132 |
| Every gate ID used is defined in QUALITY_GATES | Script | Pass |
| Every agent and skill ID used is defined | Script | Pass |
| One Severity scale | Read | Pass: RISK_MODEL only |
| One set of escalation levels | Read | Pass: AI_GOVERNANCE section 9 |
| Thresholds stated once | Read | Pass: QUALITY_GATES and AI_RELIABILITY_STANDARD; other documents refer by ID |
| "Risk Coverage" used without synonyms | Script search for variants | Pass |
| Confidence terms kept distinct | Read | Pass: four named terms |
| Agent to skill tables agree in both catalogs | Read | Pass |
| Agent to knowledge tables agree | Read | Pass |
| Section numbers cited across documents exist | Read | Pass |
| Six principles not weakened | Read | Pass, with R-04 and R-06 flagged for explicit ratification |
| Source topics all present | Read against each source sentence | Pass: every topic named in a source file has a section |

Not verified: that the documents match how the team actually works, and that the thresholds are achievable on the current codebase. Neither can be checked from the documents.

## 8. Missing Documents Report

| Priority | Document | Why it is needed |
|---|---|---|
| High, now written | Role register (RACI with named people and delegates) | Every document relies on roles that no source names. See ROLE_REGISTER.md; holders are still unassigned. |
| High, now written | Pipeline and tooling mapping | Says which tool computes and enforces each gate. See PIPELINE_AND_TOOLING_MAPPING.md. |
| High, now written | Templates: Risk Acceptance, Waiver, Validation record, Audit record, Abuse Case, Knowledge Item | The documents require these records and give only field lists. See RECORD_TEMPLATES.md. |
| High, now written | Incident management procedure | The incident learning flow starts from an incident record that nothing defines. See INCIDENT_MANAGEMENT.md. |
| High, now written | Test data and test environment standard | Required by the synthetic-data and designated-environment rules. See TEST_DATA_AND_ENVIRONMENTS.md. |
| Medium | Glossary | Terms are consistent and spread over twelve documents |
| Medium | Prompt catalog | Prompt governance refers to versioned prompts and regression sets that have no home |
| Medium | Compliance and regulatory mapping | The Compliance skill has nothing to check against; health data regulation probably applies (ASSUMPTION) |
| Medium | Observability standard | The Observability skill has no stated requirements |
| Medium | Release process | Release gates exist without the process around them |
| Medium | Defect management procedure | Severity of defects, states and reproduction records |
| Medium | Metrics and reporting specification | Sources, formulas, cadence and audience for the metrics |
| Low | Non-functional testing (performance, resilience, accessibility) | Risk categories PRF and REL have no test guidance |
| Low | AI tool and model inventory | Needed for the data governance and model-change rules |
| Low | Framework change log and onboarding guide | Maintainability |

## 9. Recommendations

In order:

1. Ratify or correct the 77 assumptions. Start with roles (A-TS-06, A-GV-01), the Severity matrix (A-RM-02), gate thresholds (A-QG-01) and Trust Levels (A-AR-03), because the rest depend on them.
2. Decide the open dependency findings DF-01, DF-02 and AF-03.
3. Name people for each role and their delegates.
4. Measure the current state (Mutation Score, Behavior Coverage, Risk Coverage, flaky rate) before fixing thresholds.
5. Map each gate to a pipeline step. Mark gates that cannot yet be automated and state the manual control.
6. Decide R-02 and R-04 explicitly, since both touch principle 5.
7. Pilot on one feature of the reference system from requirement to release and record where the documents could not be followed.
8. Write the High priority missing documents.
9. Add a "detected by" column to the anti-pattern tables.
10. After one quarter, replace every "Tracked" metric with a target or a trigger.

## 10. Documentation Readiness Score

**57 out of 100** after the supporting documents in section 11 (52 before them). Section 12 raises it to 59 after the first tests were written.

| Dimension | Maximum | Before | Now | Basis |
|---|---|---|---|---|
| Structural completeness | 20 | 18 | 19 | Seventeen documents carry the required sections; ten supporting documents are still missing |
| Internal consistency | 20 | 18 | 18 | All checks in section 7 pass, including for the new documents; two flagged tensions with principle 5 are unresolved |
| Traceability to approved decisions | 20 | 6 | 6 | Sources supplied topic lists only; 109 assumptions are unratified |
| Operability | 20 | 8 | 12 | Templates, incident procedure, gate mapping and data standard now exist. No role has a named holder and no gate is enforced. |
| Evidence from use | 20 | 2 | 2 | Never applied to a real change; the repository has no automated tests and no pipeline |

The set is ready for review and ratification. It is not ready to be enforced. A score above 90 would need ratified assumptions, named owners, gates wired to the pipeline, and at least one release cycle of audit evidence showing the controls work as written.

## 11. Addendum: Supporting Documents

The five High priority items from section 8 were written after the initial review.

| Document | ID | Content | Assumptions | Anti-patterns |
|---|---|---|---|---|
| ROLE_REGISTER.md | QE-DOC-13 | Role definitions, holder register (all unassigned), incompatible combinations, minimum staffing, decision matrix, delegation | 7 | 7 |
| PIPELINE_AND_TOOLING_MAPPING.md | QE-DOC-14 | Current state of the repository, candidate tools, mechanism and interim control for each of the 24 gates, adoption phases | 6 | 8 |
| RECORD_TEMPLATES.md | QE-DOC-15 | Ten templates: risk entry, Risk Acceptance, Waiver, Abuse Case, AI Contribution Declaration, Validation record, Audit record, Test Removal record, Knowledge Item, Release record | 4 | 8 |
| INCIDENT_MANAGEMENT.md | QE-DOC-16 | Incident definition and types, classification, response targets, twelve-step procedure, closure conditions, incident record | 8 | 11 |
| TEST_DATA_AND_ENVIRONMENTS.md | QE-DOC-17 | Environment classes, data rules, test identity requirements, identity provider in tests, isolation, parity, secrets | 7 | 13 |

Checks run on the full set of seventeen: each new document links to all twelve core documents and to the other four; each core document has a Supporting Documents pointer to the five; all 24 gates are mapped; no undefined gate, agent, skill or case ID is used. The set now holds 109 assumptions and 156 anti-patterns.

Findings from writing them, which change the picture in sections 5 and 9:

| ID | Finding | Effect |
|---|---|---|
| S-01 | The repository has no automated tests and no pipeline definition. Branch protection could not be checked from the repository. | No gate is enforced today. Reliability flag R-11 is confirmed and is the largest gap between the documents and reality. |
| S-02 | The development realm defines one sample user per role. | Object-level authorization (BOLA) tests cannot be written as SECURITY_TESTING requires until a second identity per role exists. G-SEC-02 is blocked on this. |
| S-03 | No requirement register or risk register exists. | Behavior Coverage and Risk Coverage cannot be computed. G-BEH-01 and G-BEH-02 can only be checked by hand for now. |
| S-04 | Two approvers other than the author are required for authorization changes. | The framework needs at least three people. With fewer, such changes cannot be merged under it. |
| S-05 | The AI Governance Owner is both the owner of the AI Validation Agent and the approver of Trust Level promotions. | For that agent, promotion approval was assigned to the QE Lead (A-RR-05). Needs confirmation. |
| S-06 | Whether data incidents carry regulatory notification duties is not addressed anywhere. | Depends on the compliance mapping, which is still missing (A-IN-08). |

Still missing from section 8: glossary, prompt catalog, compliance and regulatory mapping, observability standard, release process, defect management procedure, metrics and reporting specification, non-functional testing, AI tool and model inventory, framework change log and onboarding guide.

Revised first steps, replacing recommendations 3 to 5 in section 9: name the role holders in ROLE_REGISTER.md; carry out adoption phases 0 to 2 in PIPELINE_AND_TOOLING_MAPPING.md (branch protection, a first test project running in a pipeline, a second test identity per role with the first authorization and BOLA tests); only then set thresholds from measured values.

## 12. Addendum: First Implementation Step

A first slice of adoption phases 0 to 2 now exists in the working tree (uncommitted). Details and per-gate status are in PIPELINE_AND_TOOLING_MAPPING.md section 5.1.

| Item | Result |
|---|---|
| Test project `backend/api.Tests` | 105 API-level security tests against a real PostgreSQL and real token validation; passing locally on .NET 10 in three consecutive runs |
| Coverage of the role by endpoint matrix | Patient and User endpoints (2 of 8 controllers); the other six are listed in the test code as not yet covered, and a completeness test fails if an unlisted endpoint appears |
| Object-level authorization | Patient records, with two identities per role. The tests were shown to fail when the ownership check was weakened by hand. |
| Pipeline | Workflow file with build, test and secret scan; never run on the hosting platform |
| Supporting files | Pull request template; code owners template (inactive); secret-scan configuration with three inspected exceptions |

Effect on earlier findings:

| Finding | Status |
|---|---|
| S-01 No tests and no pipeline | Partly addressed: tests exist and pass locally; the pipeline is defined and has not run; no gate is enforced |
| S-02 One sample user per role | Addressed for automated tests through a test issuer. The Keycloak realm is unchanged and no test runs against Keycloak. |
| S-03 No requirement or risk register | Unchanged |
| R-11 Enforcement | Still open: branch protection and required checks are hosting settings and were not changed |

Not verified: the net8.0 test run (no .NET 8 runtime on the machine used), the workflow on the hosting platform, and branch protection.

Readiness score: 59 out of 100. Evidence from use rises from 2 to 4, because part of SECURITY_TESTING.md has now been applied to real endpoints and found workable. The other dimensions are unchanged; nothing is enforced and no assumption has been ratified.

## 13. Addendum: Matrix Extended to All Controllers

The role by endpoint matrix now covers all 36 endpoints, and object-level tests cover patient records, appointments and medical records. Details are in PIPELINE_AND_TOOLING_MAPPING.md section 5.2.

Result: 281 tests, 277 passing and 4 failing. The failures are two findings in the API, not test defects:

| ID | Finding | Proposed Severity |
|---|---|---|
| F-01 | A Patient can learn which appointment and medical record identifiers exist (404 for missing, 403 for another patient's) | Medium |
| F-02 | An identity with no roles receives 200 and an empty list from the appointment list, which is documented for Staff and Patient only | Low |

What this shows about the framework: the matrix-as-data approach and the BOLA case list in SECURITY_TESTING.md found real deviations on first use, and the rule that expected outcomes come from the documentation, not from the code, is what exposed them. Both findings need a Risk Owner, who does not exist yet (ROLE_REGISTER.md).

Readiness score: 61 out of 100. Evidence from use rises from 4 to 6. Nothing is enforced, the pipeline has not run on the hosting platform, and no assumption has been ratified.

## 14. Addendum: Findings Fixed

Commit 4c67229 (pushed to the fork branch `feature/keycloak-aspire-postgres`) contains the framework documents, the test project and the pipeline files, with the four tests failing as described in section 13.

Both findings were then fixed in the API:

| ID | Change |
|---|---|
| F-01 | `GET /api/Appointment/{id}`, `POST /api/Appointment/{id}/cancel` and `GET /api/MedicalRecord/{id}` return 403 to a non-staff caller for a missing id as well as for another patient's. Staff still receive 404 for a missing id; tests were added for that. |
| F-02 | `AppointmentController` requires one of the roles Admin, Doctor, Nurse or Patient. |

Result: 283 tests, all passing in three consecutive runs on .NET 10. The net8.0 run, the pipeline on the hosting platform and branch protection remain unverified. The readiness score stays at 61: a found-and-fixed defect is already counted as evidence from use, and nothing is enforced yet.
