# PIPELINE AND TOOLING MAPPING

| Field | Value |
|---|---|
| Document ID | QE-DOC-14 |
| Version | 1.0-draft |
| Status | Draft. Describes a target state. Section 5 records the starting point; section 5.1 records the first tests and pipeline definition, none of which is enforced yet. |
| Owner | Tech Lead |
| Approvers | Tech Lead, QE Lead, Security Lead |
| Review cadence | Quarterly, and whenever a gate or a tool changes |
| Last updated | 2026-11-13 |

## 1. Purpose

A gate that no tool or person enforces is advisory. This document maps each gate in [QUALITY_GATES.md](QUALITY_GATES.md) to the mechanism that evaluates it, the Evidence it produces, and the manual control that applies until the mechanism exists. It also records honestly what the repository has today.

## 2. Scope

The 24 gates in QUALITY_GATES.md, applied to the HospitalManagement repository: the .NET backend (`backend/`), the web frontend (`frontend-web/`), the identity configuration (`keycloak/`), and the container definitions. The legacy projects `AppDesktop/` and `GestionHopitalSQL/` are out of scope unless the Tech Lead brings them in.

## 3. Definitions

| Term | Definition |
|---|---|
| Mechanism | The tool step or human action that evaluates a gate. |
| Automated | Evaluated by the pipeline with no human judgment; blocks on failure. |
| Hybrid | The pipeline computes the inputs; a human makes the decision. |
| Manual | Evaluated by a human using a checklist, with a recorded result. |
| Interim control | The Manual check that applies while an Automated or Hybrid Mechanism is not yet in place. |
| Enforced | The Mechanism exists, runs on every change, and a failure blocks merge or release. |
| Evidence artifact | The stored output of a Mechanism, kept with the revision it was computed on. |

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| Tech Lead | Owns the pipeline, branch protection and this mapping |
| QE Lead | Confirms each Mechanism really evaluates the gate's pass condition |
| Security Lead | Owns Mechanisms for security gates and secret scanning |
| Knowledge Owner | Owns Mechanisms for knowledge gates |
| AI Governance Owner | Owns Mechanisms for G-PR-04 and G-REL-04 |

## 5. Current State of the Repository

Observed on 2026-11-13 on branch `feature/keycloak-aspire-postgres`:

| Item | Observation |
|---|---|
| Automated tests | None found. No test project in `backend/backend.sln`; no test files in the tracked sources. |
| Continuous integration | No workflow definitions in the repository (no `.github/` directory). |
| Mutation testing | Not configured. |
| Secret scanning, dependency scanning | Not configured in the repository. |
| Branch protection and required reviews | Not verified. They are hosting settings and are not visible in the repository. |
| Traceability data (requirements, risks linked to tests) | None. |
| Identity test fixtures | The realm export defines one sample user per role (`admin.user`, `doctor.user`, `nurse.user`, `patient.user`). |
| Container-based local environment | Present: `docker-compose.yml` and .NET Aspire AppHost with PostgreSQL and Keycloak. |

Consequence: no gate in QUALITY_GATES.md is Enforced today. Until the adoption phases in section 8 are carried out, every gate is either an Interim control or not applied, and that must be said plainly in any release decision.

### 5.1 State after the first implementation step

Later on 2026-11-13 a first slice of phases 0 to 2 was added to the working tree. It is not committed, and the pipeline has never run on the hosting platform.

| Item | State | Verified how |
|---|---|---|
| Test project `backend/api.Tests` | Added to `backend.sln`. Runs the real API in-process against PostgreSQL 17 in a container, with real migrations and real token validation. | 105 tests pass locally on .NET 10, three consecutive runs. The net8.0 target, which the pipeline uses, builds but was not run: the machine has no .NET 8 runtime. |
| Authentication tests | AUTHN-01 to AUTHN-10 and AUTHZ-05 on a representative endpoint | Passing |
| Role by endpoint matrix | As data, for the 10 Patient and User endpoints: 59 cells, one test each, plus refused-write read-back tests | Passing |
| Matrix completeness check | Fails when an exposed endpoint has no row. Six controllers are listed as not yet covered: Appointment, AuditLog, Department, Doctor, MedicalRecord, Service. | Passing with that list |
| BOLA tests | Patient records only, with two Patient identities each linked to their own record | Passing. Detection confirmed: with the ownership check weakened by hand, four BOLA tests failed; the change was then reverted. |
| Test identities | Two per role, no-roles, multi-role, linked and unlinked patients, deactivated: defined in the test project and issued by a test issuer | The Keycloak realm export still has one sample user per role. No test runs against Keycloak. |
| Pipeline definition | `.github/workflows/ci.yml`: build, test on net8.0, secret scan | Not run. Advisory until marked as required checks. |
| Secret scan | gitleaks with `.gitleaks.toml` | Run locally: clean. 14 initial hits in legacy projects were inspected and are false positives (assembly public key tokens, designer metadata, tracked `.vs` IDE state). A planted fake token is detected. |
| Pull request template | `.github/pull_request_template.md` with traceability, test removal and AI declaration fields | Present; nothing enforces it |
| Code owners | `.github/CODEOWNERS` with placeholders, all commented out | Inactive until role holders are named |
| Branch protection | Not changed. It is a hosting setting and needs an administrator. | Not verified |
| Production code | One line added to `backend/api/Program.cs` (`public partial class Program`) so the tests can host the API | Builds |

Gate status after this step:

| Gate | Status | What is missing for Enforced |
|---|---|---|
| G-PR-01 | Mechanism exists, not yet blocking | First pipeline run; required status check |
| G-SEC-01 | Partly covered (2 of 8 controllers), not yet blocking | Rows for the six remaining controllers; required status check |
| G-SEC-02 | Partly covered (patient records), not yet blocking | Object-level tests for appointments and medical records; the route-with-identifier completeness check; required status check |
| G-SEC-04 | Repository scan exists, not yet blocking | Required status check; the response and log leakage tests |
| G-TST-01 | Not automated. Checked by hand once: three consecutive passing runs. | Repeat-run and random-order step in the pipeline |
| G-PR-02 | Not in place | Branch protection; named code owners |
| All others | Unchanged from section 5 | |

No gate is Enforced yet. Findings from this step:

- The tested behavior matched the documented rules in every cell. No authorization defect was found in the Patient and User endpoints.
- The 8.0 in-process test server cannot run rolled forward onto the .NET 10 runtime, so the test project targets both net8.0 and net10.0.
- `GestionHopitalSQL/.vs/` (per-user IDE state) is tracked in git. It should be removed from tracking and ignored.

### 5.2 State after the matrix was extended

Still 2026-11-13, still uncommitted. The role by endpoint matrix now has a row for all 36 endpoints of the eight controllers, and the "not yet covered" list is empty, so any new endpoint without a row fails the completeness test. Object-level tests were added for appointments and medical records.

| Item | State |
|---|---|
| Tests | 281 in total. 277 pass and 4 fail, identically in three consecutive runs on .NET 10. The net8.0 target was again not run. |
| Detection check | With the appointment and medical record ownership checks weakened by hand, four further tests failed; the changes were then reverted. |
| G-SEC-01 | All controllers covered. One cell fails (finding F-02). Not yet blocking. |
| G-SEC-02 | Patient records, appointments and medical records covered. Three tests fail (finding F-01). The generic "every route with an identifier has BOLA tests" completeness check does not exist. Not yet blocking. |
| G-PR-01 | Would fail today because of the four failing tests. That is the intended behavior of the gate. |

Findings, to be entered in the risk register by a Risk Owner (ratings below are proposals at Risk Confidence Low until confirmed):

| ID | Finding | Category | Proposed Severity |
|---|---|---|---|
| F-01 | A Patient-role caller can tell which appointment and medical record identifiers exist: a missing id returns 404, another patient's id returns 403. Affects `GET /api/Appointment/{id}`, `POST /api/Appointment/{id}/cancel` and `GET /api/MedicalRecord/{id}`. | PRV | Medium (enumeration; no record content is disclosed) |
| F-02 | An authenticated identity with no roles gets 200 and an empty list from `GET /api/Appointment`, which is documented for Staff and Patient only. | SEC | Low (no data is returned) |

The four failing tests are left failing. They were not skipped or loosened, in line with TEST_STRATEGY.md section 15.

## 6. Candidate Tooling

No source document names a tool. The candidates below fit the repository's stack and are all assumptions for the Tech Lead to confirm or replace.

| Need | Candidate | Notes |
|---|---|---|
| Pipeline | GitHub Actions | The repository history shows merged pull requests from GitHub |
| Unit and integration tests (backend) | xUnit | Any mainstream .NET test framework meets the requirement |
| Real database in integration tests | Testcontainers for .NET with PostgreSQL | Meets the "no mocked database" rule |
| API-level tests | ASP.NET Core `WebApplicationFactory` against the real pipeline, with real token validation | See TEST_DATA_AND_ENVIRONMENTS.md for the identity provider |
| Mutation testing (backend) | Stryker.NET | Reports per file, which allows risk tiers |
| Frontend tests | To be decided by the Tech Lead | The frontend's current test tooling was not examined |
| Secret scanning | gitleaks, or the hosting platform's secret scanning | For G-SEC-04 |
| Dependency vulnerabilities | `dotnet list package --vulnerable`, `npm audit` | Feeds G-SEC-03 |
| Traceability | Test traits or categories carrying `REQ-`, `AC-`, `RSK-`, defect and case IDs; a register file under version control | Lightest option that is machine-readable |
| Risk register, Waivers, acceptances | Files under version control using RECORD_TEMPLATES.md | Reviewed through pull requests |

## 7. Gate Mapping

"Current" is the state in section 5. "Interim control" applies from now until the target Mechanism is Enforced.

### 7.1 PR gates

| Gate | Type | Target Mechanism | Evidence artifact | Current | Interim control |
|---|---|---|---|---|---|
| G-PR-01 Build and tests | Automated | Pipeline builds and runs all tests on every pull request; required status check; fails on any skipped test without a linked ticket | Build and test report | Not in place | Author runs build locally and pastes the result; reviewer confirms |
| G-PR-02 Human review | Manual, enforced by the host | Branch protection: one required approval, two for paths `keycloak/`, authorization code and Critical risk areas (code owners); no self-approval; no admin bypass | Review record on the pull request | Not verified | Reviewer and second approver recorded in the pull request description |
| G-PR-03 Traceability | Hybrid | Pipeline checks that the pull request references an ID and that new tests carry a trace trait; reviewer confirms the link is real | Check output; pull request link | Not in place | Pull request template field, checked by the reviewer |
| G-PR-04 AI-produced content | Hybrid | Pull request template declaration; pipeline checks that a Validation record ID is present when AI content is declared; reviewer approves | Declaration and Validation record | Not in place | Declaration in the pull request; reviewer checks the Validation record by hand |
| G-PR-05 Test removal | Hybrid | Pipeline detects deleted tests, added skip attributes and removed assertions, and requires a Test Removal record; reviewer approves | Detection output; Test Removal record | Not in place | Reviewer reads removed lines first; record in the pull request |

### 7.2 Test gates

| Gate | Type | Target Mechanism | Evidence artifact | Current | Interim control |
|---|---|---|---|---|---|
| G-TST-01 Determinism | Automated | New and changed tests run three times in random order; no retry settings allowed; flaky-test tracker with age alert | Run reports; tracker | Not in place | Author runs changed tests three times; reviewer checks for retries and sleeps |
| G-TST-02 Assertion quality | Hybrid | Static check for tests without assertions; reviewer checks Side Effect assertions | Check output; review record | Not in place | Review questions in QUALITY_ENGINEERING_BEST_PRACTICES.md section 6.1 |
| G-TST-03 Defect reproduction | Hybrid | Pipeline runs the new test against the pre-fix revision and expects failure, then against the fix and expects pass | Both run results | Not in place | Author records the failing run before the fix; reviewer confirms |

### 7.3 Behavior coverage gates

| Gate | Type | Target Mechanism | Evidence artifact | Current | Interim control |
|---|---|---|---|---|---|
| G-BEH-01 Behavior Coverage | Automated once trace data exists | Computed from acceptance criteria IDs in the requirement register against passing tests carrying those IDs | Coverage report per requirement | Not possible: no requirement register | Reviewer checks each acceptance criterion of the change against its tests |
| G-BEH-02 Risk Coverage | Automated once trace data exists | Computed from the risk register against passing tests carrying `RSK-` IDs, cross-checked with mutation results on the mitigating code | Coverage report per Severity | Not possible: no risk register | Risk Owner confirms tests for each Critical and High risk touched |

### 7.4 Security gates

| Gate | Type | Target Mechanism | Evidence artifact | Current | Interim control |
|---|---|---|---|---|---|
| G-SEC-01 Authentication and authorization tests | Automated | Role by endpoint matrix kept as data; a test generator or completeness check fails when an endpoint in the API description has an empty cell | Matrix; test report | Not in place | Matrix kept by hand for changed endpoints; Security Lead or delegate reviews |
| G-SEC-02 BOLA tests | Automated | Completeness check: every route with an identifier parameter has tests tagged BOLA-01 and BOLA-02, or a recorded exemption | Check output; test report | Not in place; also blocked by having one sample user per role | Reviewer checks by hand; second identity per role added first |
| G-SEC-03 Open findings | Hybrid | Findings tracked as risks; pipeline fails on open Critical or High; dependency scan feeds findings | Findings list; scan report | Not in place | Security Lead confirms the open findings list before merge to the release branch |
| G-SEC-04 Leakage | Automated | Secret scanning on every push; test that inspects error responses and captured logs for forbidden patterns | Scan report; test report | Not in place | Reviewer checks diffs for secrets; `.env` stays git-ignored |

### 7.5 Mutation thresholds

| Gate | Type | Target Mechanism | Evidence artifact | Current | Interim control |
|---|---|---|---|---|---|
| G-MUT-01 Mutation Score by risk tier | Automated | Mutation run with per-path thresholds; a tier map under version control assigns paths to Critical, High and Medium | Mutation report | Not in place | None equivalent. Reviewer breaks the protected behavior once by hand for Critical changes and confirms a test fails |
| G-MUT-02 No regression | Automated | Compare changed files' scores with the target branch baseline | Comparison report | Not in place | None until a baseline exists |

Changes to the tier map or to mutation exclusions need QE Lead approval (code owner on those files).

### 7.6 Release gates

| Gate | Type | Target Mechanism | Evidence artifact | Current | Interim control |
|---|---|---|---|---|---|
| G-REL-01 Upstream gates | Automated | Release job re-reads all gate results for the exact release revision and checks Waiver expiry dates | Evidence pack index | Not in place | Release checklist completed by the QE Lead |
| G-REL-02 Risk position | Hybrid | Job reads the risk register for unmitigated Critical, unaccepted High, expired acceptances and Low-confidence Critical or High | Risk position report | Not in place | Risk Owners confirm in the release checklist |
| G-REL-03 Human sign-off | Manual | Protected release environment requiring the Release Approver's approval | Signed release record | Not in place | Signed release record using RECORD_TEMPLATES.md |
| G-REL-04 AI reliability | Hybrid | Job lists AI contributions in the release from G-PR-04 declarations with each agent's Trust Level | AI contribution list | Not in place | AI Governance Owner reviews the list by hand |
| G-REL-05 Knowledge closure | Hybrid | Job lists incidents closed since the last release and checks each for a K-DEF item and a reproduction test | Closure report | Not in place | Knowledge Owner confirms in the release checklist |

### 7.7 Knowledge gates

| Gate | Type | Target Mechanism | Evidence artifact | Current | Interim control |
|---|---|---|---|---|---|
| G-KNW-01 Usable knowledge | Automated | Knowledge Items as files with metadata; a check fails on any cited item that is not Active or is past its interval | Check output | Not in place | Reviewer checks cited items by hand |
| G-KNW-02 Admission | Hybrid | Schema check on required metadata; code owner approval by the Knowledge Owner | Check output; approval | Not in place | Knowledge Owner review |
| G-KNW-03 Incident learning | Manual | Incident closure checklist | Incident record | Not in place | Same |

## 8. Adoption Phases

Order matters: later gates depend on data that earlier phases create.

| Phase | Content | Gates that become Enforced |
|---|---|---|
| 0 Foundations | Name role holders; set branch protection and required reviews; add the pull request template; turn on secret scanning | G-PR-02, G-SEC-04 (repository part) |
| 1 Tests exist | Create test projects; integration tests on a real database; pipeline builds and runs tests | G-PR-01, G-TST-01 |
| 2 Security tests | Second identity per role; Role by endpoint matrix as data; authentication, authorization and BOLA tests | G-SEC-01, G-SEC-02 |
| 3 Detection power | Mutation testing with the tier map; reproduction check for defect fixes; removal detection | G-MUT-01, G-MUT-02, G-TST-03, G-PR-05, G-TST-02 |
| 4 Traceability | Requirement and risk registers as files; trace traits on tests; coverage computation | G-PR-03, G-BEH-01, G-BEH-02, G-SEC-03 |
| 5 AI and knowledge | Declarations, Validation records, knowledge files and checks | G-PR-04, G-KNW-01, G-KNW-02, G-KNW-03 |
| 6 Release | Release job, Evidence pack, protected release approval | G-REL-01 to G-REL-05 |

Rules during adoption:

- A gate is reported as Enforced only when its Mechanism runs on every change and blocks on failure.
- An Interim control is a real check with a recorded result. "Not yet automated" does not mean "not applied".
- A release made before phase 6 states in its release record which gates were Enforced, which were under Interim control and which were not applied.
- AI agents are not used for governed work on a part of the lifecycle whose gates are not at least under Interim control.

## 9. Rules for Mechanisms

1. Fail closed: a Mechanism that cannot run counts as a failed gate.
2. Results are stored with the revision and are immutable.
3. Pipeline configuration, thresholds, tier maps and exclusions are code: reviewed, and owned through code owners by the gate owner.
4. No Mechanism has a bypass flag that an author can set alone.
5. The pipeline's own credentials are least-privilege and are never available to agents.
6. A Mechanism is tested: for each Automated gate there is a known-bad example that the Mechanism must reject.

## 10. Metrics

| Metric | Target |
|---|---|
| Gates Enforced, of 24 | Reported each month; 24 at the end of phase 6 |
| Gates under Interim control with recorded results | 100% of those not Enforced |
| Gates not applied at all | 0 after phase 0 |
| Mechanisms with a known-bad test | 100% of Automated gates |
| Pipeline bypasses | 0 |
| Median pipeline duration | Tracked; never reduced by removing a check |

## 11. Anti-Patterns

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-PT-01 | Reporting a gate as in force before its Mechanism blocks | False confidence at release | Section 8 rule; release record states the true status |
| AP-PT-02 | Advisory checks: a failing check that does not block | Failures become background noise | Required status checks |
| AP-PT-03 | Admin bypass of branch protection | Removes every PR gate at once | Disable bypass; L3 on use |
| AP-PT-04 | Mechanism that checks presence, not truth (a trace tag exists but points nowhere) | Gate passes on form | QE Lead confirms the Mechanism against the pass condition |
| AP-PT-05 | Untested Mechanism | A broken check passes everything | Known-bad examples |
| AP-PT-06 | Thresholds or exclusions edited in the same change they protect | Self-approval of a weaker gate | Code owners on configuration |
| AP-PT-07 | Interim control never replaced | Manual checks decay under load | Monthly report of gates not Enforced |
| AP-PT-08 | Tool chosen before the gate's pass condition is understood | The tool's default becomes the standard | Map from the gate, not from the tool |

## 12. Escalation Rules

| Trigger | Escalate to | Level |
|---|---|---|
| Mechanism unavailable | Tech Lead; the gate is treated as failed | L2 |
| Branch protection bypassed | AI Governance Owner and Release Approver | L3 |
| Mechanism found to pass a known-bad case | Gate owner and QE Lead | L2 |
| Release proposed with gates not applied | Release Approver | L3 |

Levels are defined in [AI_GOVERNANCE.md](AI_GOVERNANCE.md) section 9.

## 13. Related Documents

| Document | Relationship |
|---|---|
| [QUALITY_GATES.md](QUALITY_GATES.md) | The gates mapped here; pass conditions and thresholds |
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Test levels, determinism and traceability rules behind the Mechanisms |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Role by endpoint matrix and case IDs used by security Mechanisms |
| [RISK_MODEL.md](RISK_MODEL.md) | Risk register and tiers used by coverage and mutation Mechanisms |
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Audit, agent permissions, escalation levels |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Validation records checked by G-PR-04 |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Metadata checked by knowledge Mechanisms |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Coverage Agent and PR Review Agent report from these Mechanisms' data |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Lifecycle stages 10 and 11 |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Hand-off records |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Mutation Testing skill |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Review questions used as Interim controls |
| [ROLE_REGISTER.md](ROLE_REGISTER.md) | Holders who operate the Manual and Hybrid gates |
| [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) | Records produced by Hybrid and Manual gates |
| [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) | Environments the Mechanisms run in |
| [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) | Source for G-KNW-03 and G-REL-05 |

## 14. Baseline and Assumptions

Baseline: the gates and their pass conditions come from QUALITY_GATES.md. Section 5 is observation of the repository, not assumption.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-PT-01 | Every candidate tool in section 6 | No source names a tool |
| A-PT-02 | The hosting platform is GitHub and supports required checks, code owners and protected environments | Inferred from merge commits in the history; settings were not inspected |
| A-PT-03 | Registers, Waivers and Knowledge Items are kept as files under version control | Not in any source; chosen so that review and audit use the same mechanism as code |
| A-PT-04 | The order and content of the adoption phases | Not in any source |
| A-PT-05 | The Automated, Hybrid or Manual classification of each gate | Derived from each pass condition |
| A-PT-06 | Legacy projects `AppDesktop/` and `GestionHopitalSQL/` are out of scope | Not stated anywhere; they are not part of the current backend solution |
