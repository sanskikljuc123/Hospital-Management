# SECURITY TESTING

| Field | Value |
|---|---|
| Document ID | QE-DOC-09 |
| Version | 1.0-draft |
| Status | Draft, pending ratification of the assumptions in section 21 |
| Owner | Security Lead |
| Approvers | Security Lead, QE Lead, Tech Lead |
| Review cadence | Quarterly, and after any security incident or identity configuration change |
| Last updated | 2026-11-13 |

## 1. Purpose

This document defines the security tests every endpoint and every change must have: authentication, authorization, broken object level authorization (BOLA), information leakage, token handling with JWT and Keycloak, OWASP alignment and Abuse Cases. It details section 10 of [TEST_STRATEGY.md](TEST_STRATEGY.md) and the tests behind the security gates in [QUALITY_GATES.md](QUALITY_GATES.md).

## 2. Scope

In scope: all HTTP endpoints, the identity configuration (realm, clients, roles, audiences, token settings), authorization logic, error handling, logging of security events, and test data.

Out of scope for this document: infrastructure hardening, network security and penetration testing by third parties. They are recorded as gaps in the review report.

Environment rule: active security tests run only in designated test environments with synthetic data and test identities.

## 3. Definitions

| Term | Definition |
|---|---|
| Authentication | Establishing who the caller is, here by a signed access token issued by the identity provider. |
| Authorization | Deciding whether an authenticated caller may perform an operation. |
| Role-level authorization | Authorization by the caller's role for an operation (function level). |
| Object-level authorization | Authorization by the caller's relationship to the specific object requested. |
| BOLA | Broken object level authorization: a caller can act on an object they are not entitled to by supplying its identifier. |
| Ownership rule | The documented rule that says which callers may access which objects. |
| Information leakage | Any response, error, header, log or timing difference that reveals data or internals to a caller not entitled to them. |
| Negative security test | A test that expects refusal and asserts three things: the refusal status, the absence of protected data in the response, and no state change. |
| Security finding | A confirmed security weakness, recorded as a Risk in category SEC or PRV. |
| Role by endpoint matrix | The table of every endpoint against every role and no identity, with the expected outcome for each. |

Severity, Abuse Case and Threat are defined in [RISK_MODEL.md](RISK_MODEL.md).

## 4. Ownership and Roles

| Role | Responsibility |
|---|---|
| Security Lead | Owns this document, security gates, threat models and security findings; approves all security test designs |
| Tech Lead | Owns the authorization design and identity configuration |
| Engineers | Write security tests for their endpoints as part of the change |
| QE Lead | Ensures security tests meet the test quality rules |
| Security Agent (AGT-06) | Proposes role-level cases and the Role by endpoint matrix |
| BOLA Agent (AGT-07) | Proposes object-level cases |

All Outputs of AGT-06 and AGT-07 receive full human review at every Trust Level.

## 5. Security Testing Principles

1. Deny by default is tested, not assumed: every endpoint is tested with no identity and with every role.
2. A refusal is verified completely: status, no data, no state change.
3. Authorization is tested at the API level against the running service with real token validation. Unit tests of authorization logic are additional, not a substitute.
4. Each documented rule has a test; each endpoint without a documented rule is a finding.
5. Security tests are deterministic and run on every change.

## 6. Authentication Tests

Required for every protected endpoint (a representative endpoint per authentication configuration is sufficient for token-validation cases marked with *):

| ID | Case | Expected |
|---|---|---|
| AUTHN-01 | No Authorization header | 401 |
| AUTHN-02 | Malformed header or token | 401 |
| AUTHN-03 * | Expired token | 401 |
| AUTHN-04 * | Token with invalid signature | 401 |
| AUTHN-05 * | Token with algorithm "none" or a changed algorithm | 401 |
| AUTHN-06 * | Token from a different issuer or realm | 401 |
| AUTHN-07 * | Token with wrong or missing audience | 401 |
| AUTHN-08 * | Token not yet valid | 401 |
| AUTHN-09 | Valid token for a deactivated account | Refused as documented (403 in the reference system) |
| AUTHN-10 | Anonymous endpoints (health, liveness) | Reachable without a token and return no sensitive data |

No 401 response may reveal why validation failed beyond a generic message.

## 7. Authorization Tests

### 7.1 Role by endpoint matrix

Every endpoint has a row; every role and "no identity" has a column; every cell has a test.

Example from the reference system:

| Endpoint | No identity | Patient | Nurse | Doctor | Admin |
|---|---|---|---|---|---|
| `GET /api/Patient` | 401 | 403 | 200 | 200 | 200 |
| `GET /api/Patient/{id}` | 401 | 200 own, 403 other | 200 | 200 | 200 |
| `POST /api/Patient` | 401 | 403 | 2xx | 2xx | 2xx |
| `DELETE /api/Patient/{id}` | 401 | 403 | 403 | 403 | 2xx |
| `POST /api/Doctor` | 401 | 403 | 403 | 403 | 2xx |

### 7.2 Required cases

| ID | Case | Expected |
|---|---|---|
| AUTHZ-01 | Each allowed role | Success |
| AUTHZ-02 | Each disallowed role | 403, no data, no state change |
| AUTHZ-03 | Token with no roles | 403 |
| AUTHZ-04 | Caller with several roles | The documented combination rule holds |
| AUTHZ-05 | Role claim altered in the token | 401 (signature invalid) |
| AUTHZ-06 | Method switching: a disallowed method on an allowed path | Refused |
| AUTHZ-07 | Mass assignment: the body includes fields the caller may not set (for example `userId`, role, ownership fields) | Ignored or rejected; state unchanged for those fields |
| AUTHZ-08 | Privileged sub-operations (for example linking a user to a patient record) | Only the documented role succeeds |

## 8. BOLA Tests

Required for every endpoint that takes an object identifier in the path, query or body.

| ID | Case | Expected |
|---|---|---|
| BOLA-01 | Caller requests their own object | Success |
| BOLA-02 | Caller requests another owner's object by identifier | 403 (or the documented refusal), no object data, no state change |
| BOLA-03 | Caller with no linked object requests any object | Refused as documented; list endpoints return empty lists |
| BOLA-04 | List endpoints | Return only objects the caller is entitled to |
| BOLA-05 | Indirect reference: object reached through a parent or child (for example an appointment or record belonging to another patient) | Refused |
| BOLA-06 | Write operations (update, delete) on another owner's object | Refused; object unchanged when read back |
| BOLA-07 | Identifier in the body differs from the identifier in the path | Refused or path wins as documented; never acts on the other object |
| BOLA-08 | Non-existent identifier compared with another owner's identifier | Responses do not let the caller tell which identifiers exist, unless the Ownership rule documents otherwise |

Rules:

- Tests use at least two distinct identities of the same role, each with their own objects.
- An endpoint with an object identifier and no documented Ownership rule is a finding and fails G-SEC-02.
- Where a role may access all objects by design (for example Staff), the test asserts that and the reviewer records why BOLA-02 does not apply.

## 9. Information Leakage Tests

| ID | Case | Expected |
|---|---|---|
| LEAK-01 | Error responses (400, 401, 403, 404, 409, 500) | No stack traces, SQL, internal paths, framework versions or other users' data |
| LEAK-02 | Refusal responses | No part of the protected object in the body |
| LEAK-03 | Response bodies | Only documented fields; no internal identifiers, credentials, tokens or fields the caller's role should not see |
| LEAK-04 | Validation messages | Do not confirm the existence of other users' data |
| LEAK-05 | Logs produced during tests | No tokens, passwords, secrets or full personal records |
| LEAK-06 | Headers | No unnecessary server or version disclosure |
| LEAK-07 | API documentation and health endpoints | Expose no secrets or data; exposure in production is a recorded decision |
| LEAK-08 | Repository, test data and Prompts | No secrets or real personal data (G-SEC-04) |

## 10. JWT and Keycloak

The identity provider is Keycloak. The API accepts signed access tokens for its realm and audience and takes roles from realm roles.

Token validation requirements (each has a test in section 6): signature against the realm's published keys, issuer, audience, expiry and not-before, and an allowed algorithm list that excludes "none".

Configuration checks, reviewed on every change to the realm export and tested where possible:

| ID | Check |
|---|---|
| KC-01 | The API audience is present in tokens intended for it and the API rejects tokens without it |
| KC-02 | Roles are exactly the documented set; no default role grants staff or admin access |
| KC-03 | New users receive no privileged role by default |
| KC-04 | Access token lifetime is short and stated; refresh behavior is documented |
| KC-05 | Public clients do not use flows that expose credentials; redirect URIs and web origins are exact, without wildcards, outside local development |
| KC-06 | No real credentials or secrets in the realm export; development-only users and passwords are marked and excluded from non-development environments |
| KC-07 | Signing key rotation does not break validation (tested by validating against the published key set, not a pinned key) |
| KC-08 | A role removed in Keycloak takes effect at the API within one token lifetime |
| KC-09 | A change to the realm export is treated as authorization code: two human approvers (G-PR-02) and a threat model update |

Tests obtain tokens from a test instance of the identity provider or from a test issuer configured identically to it. Disabling token validation in tests is prohibited at API and Security level.

## 11. OWASP Alignment

Tests are mapped to the OWASP API Security Top 10 (2023).

| OWASP item | Covered by |
|---|---|
| API1 Broken Object Level Authorization | Section 8 |
| API2 Broken Authentication | Sections 6 and 10 |
| API3 Broken Object Property Level Authorization | AUTHZ-07, LEAK-03 |
| API4 Unrestricted Resource Consumption | Abuse Cases (section 12); bounded list and payload tests |
| API5 Broken Function Level Authorization | Section 7 |
| API6 Unrestricted Access to Sensitive Business Flows | Abuse Cases |
| API7 Server Side Request Forgery | Applies only where the API fetches caller-supplied URLs; recorded per endpoint |
| API8 Security Misconfiguration | LEAK-01, LEAK-06, LEAK-07, section 10 checks |
| API9 Improper Inventory Management | Every running endpoint appears in the API inventory and the Role by endpoint matrix |
| API10 Unsafe Consumption of APIs | Integration tests on responses from external services, where any exist |

Injection and input validation are tested for every endpoint that accepts input: malformed, oversized and hostile values are rejected with 400 and no Side Effect.

## 12. Abuse Cases

Each Critical or High Threat has at least one Abuse Case in the format of RISK_MODEL.md section 7.4, with linked tests. Abuse Case families to consider for every feature:

| Family | Question |
|---|---|
| Access to others' data | Can a caller see or change what belongs to someone else? |
| Privilege gain | Can a caller give themselves a role, a link or an ownership they should not have? |
| Business rule bypass | Can a rule be avoided by order of operations, repetition or concurrency (for example double booking)? |
| Enumeration | Can a caller learn which identifiers or users exist? |
| Resource exhaustion | Can a caller make the system do unbounded work? |
| Repudiation | Can a sensitive change be made without an attributable record? |
| Deactivated or stale identity | Does a deactivated account or an old token still work? |

## 13. Severity Model

Security findings use the Severity scale of RISK_MODEL.md. There is no separate security scale. Guidance for rating:

| Severity | Typical security finding |
|---|---|
| Critical | Unauthenticated access to protected data or functions; any caller reading or changing other users' sensitive data; privilege escalation to Admin; secrets exposed |
| High | Role-level bypass limited to a function; BOLA on less sensitive objects; token validation gap needing specific conditions |
| Medium | Leakage of internals without direct data exposure; enumeration; missing security logging |
| Low | Hardening gaps with no direct exploit path |

Rules: PRV and SEC risks with Impact 4 are at least High (RISK_MODEL.md section 6). A finding stays at Risk Confidence Low, and is therefore treated one level higher, until the Security Lead confirms the rating.

## 14. Security Gates

Defined in QUALITY_GATES.md and summarized here without restating thresholds.

| Gate | Tests in this document |
|---|---|
| G-SEC-01 Authentication and authorization tests | Sections 6 and 7 |
| G-SEC-02 BOLA tests | Section 8 |
| G-SEC-03 Open findings | Sections 13 and 17 |
| G-SEC-04 Leakage | Section 9, LEAK-05 and LEAK-08 |
| G-MUT-01 Critical tier | Authorization logic |
| G-PR-02 Two approvers | Authorization code and identity configuration |

## 15. Inputs and Outputs

| Inputs | Outputs |
|---|---|
| API inventory with roles and Ownership rules | Role by endpoint matrix |
| Identity configuration | Security test suites |
| Threat models and Abuse Cases | Security findings as Risks |
| K-SEC and K-RSK Knowledge Items | Gate Evidence |

## 16. Metrics

| Metric | Target |
|---|---|
| Endpoints with a complete Role by endpoint matrix and tests | 100% |
| Identifier-bearing endpoints with BOLA tests | 100% |
| Endpoints without a documented authorization or Ownership rule | 0 |
| Open Critical and High findings | 0 |
| Mean time to remediate, by Severity | Critical within 2 working days; High within 10 |
| Mutation Score of authorization logic | Per G-MUT-01 Critical tier |
| Security defects escaped to release | 0 |
| Endpoints running but absent from the inventory | 0 |

## 17. Review Process and Escalation Rules

- Security test designs are reviewed and approved by the Security Lead or delegate.
- The Role by endpoint matrix is reviewed on every endpoint or role change and in full each quarter.
- Threat models are updated on the triggers in RISK_MODEL.md section 7.1.

| Trigger | Escalate to | Level |
|---|---|---|
| Suspected Critical or High vulnerability | Security Lead, immediately; handled outside the public review thread | L2 |
| Confirmed Critical vulnerability in a released version | Security Lead, AI Governance Owner if AI-assisted, Release Approver | L3 |
| Endpoint with no authorization rule | Security Lead | L2 |
| Real credentials or personal data found in code, tests, logs or Prompts | Security Lead; treated as an incident | L2 |
| Pressure to release with an open Critical or High finding | Security Lead and Release Approver | L3 |

Levels are defined in [AI_GOVERNANCE.md](AI_GOVERNANCE.md) section 9.

## 18. Best Practices

- Build the Role by endpoint matrix first and generate the tests from it, so that a new endpoint or role shows up as an empty cell.
- Keep two test identities per role so object-level tests are always possible.
- Assert on the body of a refusal, not only the status.
- Read state back after every refused write.
- Treat identity configuration as code: reviewed, versioned, tested.

## 19. Anti-Patterns

| ID | Anti-pattern | Why it is harmful | Required response |
|---|---|---|---|
| AP-ST-01 | Authentication disabled or mocked in API tests | The real protection is never exercised | Section 10 rule |
| AP-ST-02 | Testing only with an Admin token | Every authorization defect is invisible | Role by endpoint matrix |
| AP-ST-03 | Checking the status code only on refusal | Data may leak in the body; state may have changed | Negative security test definition |
| AP-ST-04 | Role checks tested, ownership not | BOLA is the most common API weakness | Section 8 |
| AP-ST-05 | One test user per role | Object-level tests impossible | Two identities per role |
| AP-ST-06 | Security testing as a pre-release phase | Findings arrive too late and under pressure | Tests are part of every change |
| AP-ST-07 | Inferring the Ownership rule from the code | The test confirms the defect | Rule comes from the requirement; gaps are findings |
| AP-ST-08 | Accepting AI-proposed security cases as complete | A plausible list is not a complete matrix | Full human review; matrix completeness check |
| AP-ST-09 | Real tokens, passwords or personal data in tests | Disclosure | G-SEC-04 |
| AP-ST-10 | Downgrading a finding to pass a gate | Hides exposure | Re-rating needs Security Lead review |
| AP-ST-11 | Wildcard redirect URIs or origins carried from development | Token theft paths | KC-05 |

## 20. Traceability, Governance References and Related Documents

Chain: Threat or requirement, Risk (SEC or PRV), Abuse Case, matrix cell or test ID, result, gate. Each security test names the case ID from this document (for example BOLA-02) and the Risk it protects.

| Document | Relationship |
|---|---|
| [RISK_MODEL.md](RISK_MODEL.md) | Severity, STRIDE, Abuse Case format, acceptance rules |
| [QUALITY_GATES.md](QUALITY_GATES.md) | Security gates and thresholds |
| [TEST_STRATEGY.md](TEST_STRATEGY.md) | Parent strategy; determinism and Side Effect rules |
| [AGENTS_CATALOG.md](AGENTS_CATALOG.md) | Security Agent and BOLA Agent |
| [SKILLS_CATALOG.md](SKILLS_CATALOG.md) | Security Testing skill (SKL-04) |
| [AI_GOVERNANCE.md](AI_GOVERNANCE.md) | Data governance; limits on AI-run security tests; escalation levels |
| [AI_RELIABILITY_STANDARD.md](AI_RELIABILITY_STANDARD.md) | Full review of security Outputs |
| [AGENT_INTERACTION_MATRIX.md](AGENT_INTERACTION_MATRIX.md) | Security hand-offs and escalation flow |
| [KNOWLEDGE_CATALOG.md](KNOWLEDGE_CATALOG.md) | K-SEC knowledge; classification of security material |
| [AI_QUALITY_ENGINEERING_STRATEGY.md](AI_QUALITY_ENGINEERING_STRATEGY.md) | Lifecycle stage 5 |
| [QUALITY_ENGINEERING_BEST_PRACTICES.md](QUALITY_ENGINEERING_BEST_PRACTICES.md) | Security heuristics and red flags |

## 21. Baseline and Assumptions

Baseline (from the approved source document): authentication, authorization, BOLA, information leakage, JWT/Keycloak, OWASP, abuse cases, severity model, security gates, metrics.

| ID | ASSUMPTION | Why it was needed |
|---|---|---|
| A-ST-01 | The individual test cases and their IDs in sections 6 to 9 | The source names the topics only |
| A-ST-02 | "OWASP" means the OWASP API Security Top 10 (2023) | The source does not say which OWASP list; the system under test is an API |
| A-ST-03 | The severity model is the RISK_MODEL scale, with guidance, and not a separate scale | The source lists a "severity model"; a second scale would contradict RISK_MODEL |
| A-ST-04 | Examples (roles Admin, Doctor, Nurse, Patient; endpoints; realm audience) come from the HospitalManagement reference system | The source names Keycloak but no system |
| A-ST-05 | The Keycloak configuration checks KC-01 to KC-09 | Not in the source |
| A-ST-06 | Remediation times of 2 and 10 working days | Not in the source |
| A-ST-07 | Infrastructure hardening and third-party penetration testing are out of scope | The source does not mention them |

## Supporting Documents

Operational documents that implement this one: [ROLE_REGISTER.md](ROLE_REGISTER.md) (who holds each role), [PIPELINE_AND_TOOLING_MAPPING.md](PIPELINE_AND_TOOLING_MAPPING.md) (how each gate is enforced), [RECORD_TEMPLATES.md](RECORD_TEMPLATES.md) (required records), [INCIDENT_MANAGEMENT.md](INCIDENT_MANAGEMENT.md) (incident procedure), [TEST_DATA_AND_ENVIRONMENTS.md](TEST_DATA_AND_ENVIRONMENTS.md) (test data, identities and environments).
