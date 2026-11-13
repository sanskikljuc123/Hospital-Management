# TEST DATA AND ENVIRONMENTS

| Field | Value |
|---|---|
| Document ID | QE-DOC-17 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 16 |
| Owner | Tech Lead |
| Approvers | Tech Lead, Security Lead, QE Lead |
| Review cadence | Quarterly, and whenever an environment or the identity configuration changes |
| Last updated | 2026-11-13 |

## 1. Purpose

The core documents require synthetic data, deterministic tests, real dependencies at integration level, real token validation at API level, and active security tests only in designated test environments. This standard says what those requirements mean in practice: which environments exist, what data and identities each may hold, how tests get the state they need, and how a test environment is kept close enough to production for its results to count as Evidence.

## 2. Scope

All environments in which the system runs and all data used in tests, examples, documentation, Prompts and Knowledge Items. The identity provider's configuration and test identities are included.

## 3. Definitions

| Term | Definition |
|---|---|
| Environment | A running instance of the system with its dependencies, identified by a class in section 5. |
| Designated test environment | An Environment in which active security tests and destructive tests are permitted. |
| Synthetic data | Data invented for testing that describes no real person and is not derived from real records. |
| Real data | Data about real people or from production, in whole or in part, including anonymized or masked copies unless the Security Lead has approved the method in writing. |
| Test identity | An account that exists only for testing, with a credential that exists only in test environments. |
| Fixture | Code that creates the state a test needs and removes or isolates it afterwards. |
| Seed data | Data loaded when an environment is created, for local use and demonstrations. |
| Environment parity | The degree to which a test environment matches production in the things that affect behavior. |
| Ephemeral | Created for a run and destroyed after it. |

Deterministic Test and Side Effect are defined in [TEST_STRATEGY.md](TEST_STRATEGY.md). Restricted data and Classification are defined in [AI_GOVERNANCE.md](AI_GOVERNANCE.md) and [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md).

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| Tech Lead | Owns environments, their configuration and parity |
| Security Lead | Owns data rules, test identities and credentials; approves any exception involving data |
| QE Lead | Owns fixture and isolation rules |
| Engineers | Write Fixtures; keep tests independent of Seed data |
| Agents | May propose Synthetic data and Fixtures. They never receive Real data or credentials. Active security tests proposed by AGT-06 and AGT-07 run only in Designated test environments, started by a human. |

## 5. Environment Classes

| Class | Purpose | Lifetime | Data | Identities | Active security tests | Designated |
|---|---|---|---|---|---|---|
| Local | A developer's machine, via docker compose or the Aspire AppHost | Developer's choice | Synthetic only | Test identities from the development realm export | Allowed against the developer's own instance | Yes |
| CI | Pipeline runs | Ephemeral, per run | Synthetic, created by Fixtures | Test identities created for the run | Allowed | Yes |
| Shared test | Integration of several changes; manual exploration | Long-lived, rebuilt regularly | Synthetic only | Test identities | Allowed with notice to other users | Yes |
| Staging | Final check before release with production-like configuration | Long-lived | Synthetic only | Test identities; production-like identity configuration | Only tests approved by the Security Lead | No, unless approved per test run |
| Production | Real use | Permanent | Real | Real | Never under this framework | No |

Today the repository provides the Local class only (docker compose and the Aspire AppHost, with PostgreSQL and Keycloak). No CI, shared test or staging environment was found. The other classes are the target.

Rules:

- A test that changes or destroys data runs only in a Designated test environment.
- No test under this framework runs against Production. Checks of a release in Production are operational checks, read-only, and are outside this standard.
- Environments are created from definitions under version control. A hand-changed environment is rebuilt, not trusted.

## 6. Test Data Rules

1. Tests, examples, documentation, Prompts and Knowledge Items use Synthetic data only.
2. Real data is never copied into any non-production Environment. Masked or anonymized copies count as Real data unless the Security Lead has approved the method in writing; re-identification risk in health-related data is high.
3. Synthetic data must not look like a real person by accident: use clearly artificial names, reserved example domains for email addresses, and reserved or invalid ranges for phone and identity numbers.
4. Each test creates the data it needs through a Fixture and does not depend on Seed data or on data left by another test.
5. Seed data is for local use and demonstrations. A test that passes only because Seed data exists fails G-TST-01 in [QUALITY_GATES.md](QUALITY_GATES.md) when the order or environment changes, and is treated as non-deterministic.
6. Data is created through the same paths production uses where practical (the API or the domain services), so that tests do not rely on states the system could never produce. Direct database setup is allowed where the public path would be too slow, and must respect all constraints.
7. Identifiers are generated per test. Fixed identifiers shared between tests are prohibited.
8. Time-dependent data uses an injected clock, not the wall clock.
9. Failure output, logs and screenshots kept as Evidence are checked for accidental secrets before storage.
10. Synthetic data proposed by an agent is reviewed like any Output. An agent is never given Real data as a pattern to imitate.

## 7. Test Identities

Requirements:

| ID | Requirement | Why |
|---|---|---|
| TI-01 | At least two Test identities per role, each with its own objects | Object-level authorization (BOLA) tests need a second owner of the same role |
| TI-02 | One Test identity with no roles | AUTHZ-03 |
| TI-03 | One deactivated Test identity | AUTHN-09 |
| TI-04 | For roles tied to a record (Patient): one identity linked to a record and one not linked | BOLA-03 |
| TI-05 | One identity holding several roles | AUTHZ-04 |
| TI-06 | Test identities exist only in non-production realms and their credentials are used nowhere else | Containment |
| TI-07 | Credentials come from environment configuration or a secret store, never from source files | G-SEC-04 |
| TI-08 | Tokens are obtained at run time from the identity provider or a faithfully configured test issuer; tokens are never stored in the repository | G-SEC-04 |

Case IDs refer to [SECURITY_TESTING.md](SECURITY_TESTING.md).

Current state against these requirements: the development realm export defines one sample user per role (`admin.user`, `doctor.user`, `nurse.user`, `patient.user`), with the password supplied from the local `.env` file, which is git-ignored. TI-07 is met for local use. TI-01 to TI-05 are not met, so BOLA tests cannot yet be written as required. Adding the missing identities is a prerequisite for gate G-SEC-02.

## 8. Identity Provider in Tests

| Test Level | Identity provider | Token validation |
|---|---|---|
| Unit | None | Not involved |
| Integration | None, or a test issuer where authorization is in scope | Real validation logic if authorization is in scope |
| API and Security | A Keycloak test instance loaded from the versioned realm export, or a test issuer that signs with a published key set and uses the same issuer, audience, algorithm and role-claim settings | Always real. Validation is never disabled, and authentication handlers are never replaced by a stub that accepts any caller. |
| End-to-End | A Keycloak test instance | Real |

Rules:

- If a test issuer is used in place of Keycloak, a smaller set of tests runs against a real Keycloak instance on every change to the realm export or to the API's authentication settings, to show the two agree.
- The realm export used in tests is the versioned one. A divergent test-only realm is a parity defect.
- Development-only settings in the realm (sample users, local redirect URIs and origins) are marked as such and are excluded from any non-development environment (SECURITY_TESTING.md KC-05, KC-06).

## 9. Isolation and Determinism

| Rule | Detail |
|---|---|
| Database per run | Integration and API tests use a real PostgreSQL instance created for the run, with the real migrations applied |
| Isolation between tests | Each test runs against its own data: a fresh database, a transaction rolled back, or uniquely keyed data. The method is chosen so that Side Effect assertions still read committed state where the behavior commits. |
| No shared mutable state | No test reads what another wrote |
| Order independence | Suites pass in random order |
| Parallel safety | Tests that cannot run in parallel are marked, with a reason |
| External systems | Systems the team does not control are replaced by a controlled stand-in at that boundary; the stand-in's behavior is checked against the real contract periodically |
| Cleanup | Ephemeral environments are destroyed after the run; long-lived test environments are rebuilt on a schedule |
| Migrations | Tests run against a schema produced by the migrations, not by a separate script |

## 10. Environment Parity

What must match production for a test result to count as Evidence:

| Aspect | Requirement |
|---|---|
| Database engine and major version | Same |
| Schema | Produced by the same migrations |
| Token validation settings (issuer, audience, algorithms, clock skew) | Same logic; values differ only by environment address |
| Roles and role mapping | Same set |
| Authorization policies | Same code and configuration |
| Error handling mode | API and Security tests run with production error handling, so leakage tests see what a caller would see |
| Feature flags | Same defaults, or the difference is listed |
| API documentation and diagnostics endpoints | Exposure matches production, or the difference is listed |

Known and accepted differences are listed in a parity record kept with the environment definition. An unlisted difference found later is a Process incident under [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md).

## 11. Secrets and Configuration

- No secret is committed. Example configuration files hold names only, never values.
- Local secrets live in git-ignored files or the developer secret store. Pipeline secrets live in the pipeline's secret store with least privilege.
- Secrets differ between environments. No production credential is valid in any other environment.
- Secret scanning runs on every push (G-SEC-04).
- A secret found in the repository, a log or a Prompt is treated as compromised: it is rotated, and a Security incident is opened.
- Agents and their tooling have no access to any secret store.

## 12. Inputs, Outputs and Metrics

| Inputs | Outputs |
|---|---|
| Environment definitions and realm export under version control | Reproducible environments |
| Requirements for identities from SECURITY_TESTING.md | Test identities and their objects |
| Fixtures | Isolated, deterministic test state |
| Parity requirements | Parity record |

| Metric | Target |
|---|---|
| Real data found outside Production | 0 |
| Secrets found in repository, logs or Prompts | 0 |
| Tests depending on Seed data or on other tests | 0 |
| Roles with fewer than two Test identities | 0 |
| API and Security tests running with token validation disabled | 0 |
| Unlisted parity differences found | 0 |
| Environments not reproducible from version control | 0 |
| Flaky Tests attributed to data or environment | Trending to 0 |
| Age of the oldest long-lived test environment since rebuild | Within the rebuild schedule |

## 13. Quality Gates, Review Process and Escalation

Gates this standard supports: G-TST-01 (determinism), G-SEC-01 and G-SEC-02 (identities), G-SEC-04 (secrets and data), G-PR-01 (tests run in CI).

- Changes to environment definitions and to the realm export are reviewed by the Tech Lead or delegate; realm changes need two approvers (G-PR-02).
- The parity record is reviewed before each release.

| Trigger | Escalate to | Level |
|---|---|---|
| Real data found in a non-production Environment, test, Prompt or Knowledge Item | Security Lead; Security incident opened | L2, immediately; L3 if confirmed |
| Secret found in repository, log or Prompt | Security Lead | L2, immediately |
| Test found running against Production | Security Lead and Release Approver | L3 |
| Request to use Real or masked data for testing | Security Lead. The default answer is no. | L2 |
| Token validation disabled in API or Security tests | Security Lead and QE Lead | L2 |
| Parity difference affecting a past release decision | QE Lead and Release Approver | L3 |

Levels are defined in AI_GOVERNANCE.md section 9.

## 14. Best Practices and Anti-Patterns

Best practices:

- Build Fixtures as small, named builders: "a patient linked to this user", "a doctor with appointments".
- Make the one value that matters to the test visible in the test; let the Fixture default the rest.
- Create environments from scratch often, so that reproducibility is exercised.
- Keep the Test identity set as data next to the Role by endpoint matrix; a new role then shows up as missing identities.

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-TD-01 | Production copy "for realism" | Disclosure of real personal data; often unlawful | Rule 2; Security incident |
| AP-TD-02 | Masked data trusted as synthetic | Re-identification | Security Lead approval of the method, in writing |
| AP-TD-03 | Tests that rely on Seed data | Pass locally, fail elsewhere, hide order dependence | Rule 4 |
| AP-TD-04 | One Test identity per role | Object-level authorization cannot be tested | TI-01 |
| AP-TD-05 | Authentication stubbed in API tests | The real protection is never exercised | Section 8 |
| AP-TD-06 | Shared long-lived test database | State leaks between runs; failures cannot be reproduced | Database per run |
| AP-TD-07 | Hand-configured environment | Results cannot be reproduced or trusted | Rebuild from definitions |
| AP-TD-08 | Test-only realm that differs from the versioned export | Tests pass against a configuration that never ships | Section 8 rule |
| AP-TD-09 | Development error pages on during leakage tests | Leakage tests see a different response than callers do | Parity table |
| AP-TD-10 | Shared credentials across environments | One leak opens everything | Section 11 |
| AP-TD-11 | Real records pasted into a prompt as an example | Restricted data sent to a model | AI_GOVERNANCE.md data rules; AI incident |
| AP-TD-12 | Realistic-looking synthetic identities | May match a real person | Rule 3 |
| AP-TD-13 | Database state set up in ways the system could never produce | Tests verify impossible scenarios and miss real ones | Rule 6 |

## 15. Related Documents

| Document | Relationship |
|---|---|
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Test Levels, determinism and Side Effect rules |
| [SECURITY_TESTING.md](SECURITY_TESTING.md) | Case IDs that set the identity requirements; Keycloak checks |
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Data governance; prohibited uses PU-02 to PU-04; escalation levels |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | Classification |
| [QUALITY_GATES.md](QUALITY_GATES.md) | G-TST-01, G-SEC-01, G-SEC-02, G-SEC-04, G-PR-01, G-PR-02 |
| [RISK_MODEL.md](RISK_MODEL.md) | PRV and SEC risks from data handling |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Limits on Security Agent and BOLA Agent |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Validation check V6 (no restricted data) |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Lifecycle stage 8 |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Escalation flow for restricted data |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Test Design and Security Testing skills |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Test-writing heuristics |
| [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) | Tools that create CI environments |
| [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) | Data and secret incidents |
| [ROLE_REGISTER.md](ROLE_REGISTER.md) | Holders of the roles named here |
| [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) | Rule that records hold no Restricted data |

## 16. Baseline and Assumptions

Baseline: synthetic data only, no Real data to models, designated test environments for active security tests, deterministic and independent tests, real dependencies at Integration level, real token validation at API level, and no secrets in code, logs or prompts all come from the core documents. The current-state statements in sections 5 and 7 are observations of the repository.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-TD-01 | The five environment classes | The core documents say "designated test environments" without listing any |
| A-TD-02 | Masked or anonymized data is treated as Real data by default | Not in any source; conservative position for health-related data |
| A-TD-03 | The Test identity requirements TI-01 to TI-08 | Derived from the security case list; the counts are new |
| A-TD-04 | A test issuer may replace Keycloak at API level if a real-Keycloak subset also runs | Not in any source; SECURITY_TESTING allows "a test issuer configured identically" |
| A-TD-05 | The parity table | Not in any source |
| A-TD-06 | Staging is not a Designated test environment by default | Not in any source |
| A-TD-07 | The system will process real personal and health-related data in Production | Inferred from the domain; no source states it |
