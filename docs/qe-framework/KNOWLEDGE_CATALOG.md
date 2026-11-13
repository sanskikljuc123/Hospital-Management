# KNOWLEDGE CATALOG

| Field | Value |
|---|---|
| Document ID | QE-DOC-11 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 20 |
| Owner | Knowledge Owner |
| Approvers | Knowledge Owner, QE Lead, AI Governance Owner |
| Review cadence | Quarterly |
| Last updated | 2026-11-13 |

## 1. Purpose

Agents and people reason from what they are given. This catalog defines what counts as knowledge in the framework, how it is classified, how trustworthy and current it must be to be used, how long it is kept, and how disagreements between sources are settled. Its aim is that no agent or reviewer relies on knowledge that is wrong, stale or of unknown origin.

## 2. Scope

All knowledge supplied to agents or cited as Evidence: domain facts, requirements, risks, test patterns, defect and incident learnings, security knowledge, prompts, decisions and standards. Source code and the pipeline's own results are primary sources, not Knowledge Items; they are referenced, not copied.

## 3. Definitions

| Term | Definition |
|---|---|
| Knowledge Item | One reviewed, versioned unit of knowledge with an ID (KNW-n) and the metadata in section 6. |
| Knowledge type | One of the taxonomy types in section 5. |
| Source | Where the knowledge comes from: a person with authority, an approved document, code, an incident record, an external standard. |
| Knowledge Confidence | How well the item's content is supported: Verified, Corroborated or Unverified (section 9). |
| Freshness | Whether the item has been reviewed within its freshness interval (section 10). |
| Stale | Past its freshness interval without review. |
| Classification | The sensitivity class of the item (section 8). |
| Conflict | Two sources or items that cannot both be true. |
| Retention | How long an item is kept after it stops being Active (section 11). |

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| Knowledge Owner | Owns the catalog, admits and retires items, resolves Conflicts, owns knowledge gates G-KNW-01 to G-KNW-03 |
| Item Owner | The named human accountable for one Knowledge Item's correctness and review |
| Subject authority | The role with the final say for a Knowledge type (section 5) |
| Proposer | Any person who proposes an item or a change. Agents may draft a proposal; a human must adopt and submit it. |
| Security Lead | Approves classification of Confidential and Restricted material |

## 5. Knowledge Taxonomy

| Code | Type | Content | Subject authority | Used by |
|---|---|---|---|---|
| K-DOM | Domain | Business rules, domain vocabulary, roles and their rights | Product Owner | AGT-01, 02, 03, 04 |
| K-REQ | Requirements | Approved requirements and acceptance criteria | Product Owner | AGT-01, 02, 03, 04, 05, 08 |
| K-RSK | Risk | Risk patterns, threat models, Abuse Cases, the risk register | QE Lead; Security Lead for security | AGT-03, 04, 06, 07, 08 |
| K-TST | Test | Test patterns, heuristics, fixtures conventions, known hard-to-test areas | QE Lead | AGT-04, 05, 07, 08, 09 |
| K-DEF | Defect and incident | Root causes, reproduction tests, missed-detection analyses | QE Lead | AGT-03, 04, 09, 10 |
| K-SEC | Security | Authentication and authorization model, token rules, known weaknesses, OWASP mappings | Security Lead | AGT-06, 07, 09 |
| K-PRM | Prompt | Prompts, their regression sets and change history | AI Governance Owner | AGT-11, Prompt Owners |
| K-DEC | Decision | Architecture and quality decisions with their reasons | Tech Lead | All agents |
| K-STD | Standard | External standards and regulations and how they apply | Security Lead, QE Lead | AGT-01, 02, 03, 06, 11 |

Agent IDs refer to [AGENTS_CATALOG.md](AGENTS_CATALOG.md).

## 6. Knowledge Item Metadata

| Field | Required | Content |
|---|---|---|
| ID | Yes | KNW-n |
| Title | Yes | One line |
| Type | Yes | Section 5 code |
| Content | Yes | The knowledge, stated as checkable facts or rules |
| Source | Yes | Link or named authority |
| Item Owner | Yes | Named human |
| Classification | Yes | Section 8 |
| Knowledge Confidence | Yes | Section 9, with the supporting Evidence |
| State | Yes | Section 7 |
| Version and change history | Yes | Who changed what, when, approved by whom |
| Last reviewed, next review due | Yes | Dates |
| Supersedes, superseded by | If any | Item IDs |
| Related items, risks, requirements | If any | IDs |

An item missing a required field fails G-KNW-02 and cannot become Active.

## 7. Lifecycle

```
Proposed -> Under Review -> Active -> Stale -> (reviewed) -> Active
                 |             |         |
                 v             v         v
              Rejected     Deprecated -> Archived
```

| State | Meaning | Usable by agents and as Evidence |
|---|---|---|
| Proposed | Submitted by a human; not yet reviewed | No |
| Under Review | Being checked by the Item Owner and Knowledge Owner or delegate | No |
| Active | Approved and within its freshness interval | Yes |
| Stale | Freshness interval passed without review | No as sole support; see section 10 |
| Deprecated | Known to be replaced or wrong; kept for reference | No |
| Archived | Retained for audit only | No |
| Rejected | Not admitted; reason recorded | No |

Admission (Proposed to Active) requires: all required metadata, a check of the content against its Source, approval by the Knowledge Owner or delegate who is not the Proposer (Four-eyes principle).

## 8. Classification

| Class | Meaning | Examples | May be given to an AI model |
|---|---|---|---|
| Public | No harm if disclosed | Published standards | Yes |
| Internal | For the organization | Test patterns, most decisions | Yes, with approved tools |
| Confidential | Harm if disclosed | Threat models, unpatched weakness details | Only with approved tools and only what the task needs |
| Restricted | Serious harm or legal breach if disclosed | Real personal or health records, credentials, keys | Never. Not stored in the catalog. |

The rules for each class are set in [AI_GOVERNANCE.md](AI_GOVERNANCE.md) section 11. An item takes the highest class of anything it contains.

## 9. Knowledge Confidence

| Level | Meaning | Use |
|---|---|---|
| Verified | Checked directly against the primary Source by a human, with the check recorded | May support Output Confidence High |
| Corroborated | Agreed by two independent sources, or stated by the Subject authority, without a direct check | May support Output Confidence Medium at most |
| Unverified | Single non-authoritative source, or derived from AI output | May not be used as Evidence. Usable only as a lead to investigate. |

Rules:

- A new item starts at Unverified unless the admission review records the basis for a higher level.
- Knowledge Confidence is about the item. It is distinct from Risk Confidence ([RISK_MODEL.md](RISK_MODEL.md)) and Output Confidence ([AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md)).
- Anything originating from an agent is Unverified until a human verifies it against a primary Source.

## 10. Freshness

| Type | Freshness interval | Also reviewed when |
|---|---|---|
| K-DOM | 180 days | A business rule changes |
| K-REQ | 90 days | The requirement changes |
| K-RSK | 90 days | A related incident occurs or the design changes |
| K-TST | 180 days | The test stack changes |
| K-DEF | 365 days | A similar defect recurs |
| K-SEC | 90 days | The identity configuration or a security dependency changes |
| K-PRM | 90 days | The model or agent changes |
| K-DEC | 365 days | The decision is revisited |
| K-STD | 180 days | The standard is revised |

Rules:

- An item past its interval becomes Stale automatically.
- A Stale item may not be the sole support for any Claim, and may not be used at all for Critical or High risk work.
- An Output that used a Stale item is limited to Output Confidence Low.
- Review means the Item Owner checks the content against the Source again and records the result. Changing the date without a check is a governance breach.

## 11. Retention

| State | Retention |
|---|---|
| Active, Stale | Kept while in use |
| Deprecated | 12 months, then Archived |
| Archived | At least 24 months, matching the audit retention in AI_GOVERNANCE.md; longer where regulation requires |
| Rejected | 12 months with the rejection reason |
| K-DEF items | Never deleted; at most Archived |

Prior versions of every item are kept so that any past Output can be traced to the exact content it used.

## 12. Conflict Resolution

When sources disagree, agents and people apply this order and always report the Conflict.

| Priority | Rule |
|---|---|
| 1 | Primary sources win over Knowledge Items: running code and the pipeline's results for "what the system does"; approved requirements for "what the system should do" |
| 2 | Subject authority for the Knowledge type wins over other sources |
| 3 | Higher Knowledge Confidence wins |
| 4 | More recently reviewed wins |
| 5 | Still unresolved: Knowledge Owner decides, consulting the Subject authority |

Rules:

- An agent never resolves a Conflict silently. It reports both items, applies the order provisionally, and sets Output Confidence to Low.
- A difference between "what the system does" and "what it should do" is not a knowledge Conflict. It is a potential defect and is reported as one.
- The losing item is corrected or Deprecated in the same resolution. A resolved Conflict leaves no contradictory Active items.
- Conflicts touching Critical or High risks are resolved before dependent work continues.

## 13. Governance

- All changes to Active items are versioned, reviewed and approved under the Four-eyes principle.
- Agents have read access to Active items appropriate to their mission and classification. They have no write access.
- Every Output lists the item IDs and versions it used.
- When an item is corrected or Deprecated, Outputs that used the earlier version in the last 90 days are listed and their owners notified.
- Gates: G-KNW-01 (usable knowledge), G-KNW-02 (admission), G-KNW-03 (incident learning) in [QUALITY_GATES.md](QUALITY_GATES.md).

## 14. Inputs and Outputs

| Inputs | Outputs |
|---|---|
| Proposals from people (optionally drafted by agents) | Active Knowledge Items |
| Incident and defect records | K-DEF items and missed-detection analyses |
| Requirement, design and standard changes | Updated or Deprecated items |
| Conflict reports | Resolution records |

## 15. Metrics

| Metric | Target |
|---|---|
| Active items past freshness interval (not yet marked Stale) | 0 |
| Stale items older than 30 days | 0 for K-SEC, K-RSK, K-REQ |
| Items without Item Owner | 0 |
| Unverified items cited as Evidence | 0 |
| Open Conflicts older than 10 working days | 0; Critical and High related: 2 working days |
| Incident learning lead time (incident closed to K-DEF item Active) | Within 10 working days |
| Share of Active items at Verified | Rising; tracked per type |
| Outputs rejected due to knowledge problems | Tracked per item, to find weak knowledge |

## 16. Review Process and Escalation

- Item Owners review their items by the due date.
- The Knowledge Owner reviews the catalog's metrics monthly and the taxonomy quarterly.

| Trigger | Escalate to | Level |
|---|---|---|
| Conflict found | Item Owners | L1 |
| Conflict unresolved in time, or touches Critical or High risk | Knowledge Owner | L2 |
| Restricted data found in an item | Security Lead | L2, immediately |
| Item found wrong after it supported a merged change | Knowledge Owner and QE Lead; affected Outputs re-verified | L2 |
| Review dates changed without a check | AI Governance Owner | L3 |

Levels are defined in AI_GOVERNANCE.md section 9.

## 17. Example

```
ID:              KNW-12
Title:           A Patient-role user may read only the patient record linked to their account
Type:            K-DOM
Content:         A user with only the Patient role can read one patient record: the one an Admin
                 linked to their account. Unlinked accounts receive empty lists and 403 on
                 individual records.
Source:          docs/API_ENDPOINTS.md, Conventions, "Own data"; confirmed by Product Owner
Item Owner:      (named human)
Classification:  Internal
Confidence:      Verified (checked against the API inventory and the authorization tests)
State:           Active
Next review:     180 days after last review
Related:         RSK-1, ABU-1
```

## 18. Best Practices and Anti-Patterns

Best practices:

- Write items as short, checkable statements. Long narrative cannot be verified.
- Link to the primary Source rather than copying it.
- Record why, not only what, for K-DEC items.
- Deprecate promptly. A wrong Active item is worse than a missing one.

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-KC-01 | Knowledge dumping: bulk-loading documents without review | Agents treat unreviewed text as fact | Admission per item |
| AP-KC-02 | AI output stored as knowledge without verification | Errors are recycled with growing authority | Starts Unverified; human verification |
| AP-KC-03 | Date-bumping: renewing freshness without checking | False currency | Review requires a recorded check |
| AP-KC-04 | Silent conflict resolution | The wrong source may win unseen | Always report |
| AP-KC-05 | Orphan items with no owner | Nobody maintains them | G-KNW-02 |
| AP-KC-06 | Copying primary sources into items | Copies drift from the truth | Reference instead |
| AP-KC-07 | Treating "what the code does" as "what is required" | Defects are recorded as rules | Section 12 rule |
| AP-KC-08 | Keeping contradictory Active items | Agent output depends on which item is retrieved | Resolve and Deprecate |
| AP-KC-09 | Sensitive data in items for convenience | Disclosure to models and logs | Classification rules |

## 19. Traceability, Governance References and Related Documents

Each item traces to its Source, owner, approvals and versions, and forward to the Outputs that used it.

| Document | Relationship |
|---|---|
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Knowledge and data governance rules, Four-eyes principle, escalation levels |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Uses Knowledge Confidence and Freshness to set Output Confidence |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Knowledge usage and continuous learning |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Which agent reads which Knowledge type |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Knowledge propagation and incident learning flows |
| [QUALITY_GATES.md](QUALITY_GATES.md) | Knowledge gates |
| [RISK_MODEL.md](RISK_MODEL.md) | K-RSK content; Severity used in conflict urgency |
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Defect reproduction feeding K-DEF |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | K-SEC content |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Skills and the Knowledge types they need |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Source for many K-TST items |

## 20. Baseline and Assumptions

Baseline (from the approved source document): knowledge taxonomy, lifecycle, retention, classification, confidence, freshness, conflict resolution, governance.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-KC-01 | The nine Knowledge types | The source says "taxonomy" without types |
| A-KC-02 | The lifecycle states and admission rule | The source says "lifecycle" without states |
| A-KC-03 | Four classification classes | The source says "classification" without classes |
| A-KC-04 | Three Knowledge Confidence levels and their permitted uses | The source says "confidence" without levels |
| A-KC-05 | Freshness intervals per type | No values in the source |
| A-KC-06 | Retention periods | No values in the source |
| A-KC-07 | The five-step conflict order | The source says "conflict resolution" without rules |
| A-KC-08 | Agent-to-type usage in section 5 | Derived from agent missions; not in the source |
| A-KC-09 | Role "Knowledge Owner" | Not named in the source |

## Supporting Documents

Operational documents that implement this one: [ROLE_REGISTER.md](ROLE_REGISTER.md) (who holds each role), [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) (how each gate is enforced), [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) (required records), [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) (incident procedure), [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) (test data, identities and environments).
