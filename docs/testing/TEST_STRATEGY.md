# Test Strategy

How the Hospital Management platform is tested: scope, test levels, tools, data, environments and
conventions. Principles are in [README.md](README.md); risks in [RISK_REGISTER.md](RISK_REGISTER.md).

## 1. Scope

| In scope | Out of scope (for now) |
|---|---|
| Backend API in `backend/` (.NET 8, ASP.NET Core, EF Core, PostgreSQL) | Keycloak configuration beyond the realm file |
| Authentication and authorization with Keycloak (`HospitalRealm`) | Keycloak itself, PostgreSQL itself, Aspire itself |
| Business rules in `backend/Services/` | Performance and load testing |
| A few end-to-end journeys through the full stack | Visual / layout testing of `frontend-web/` |

## 2. Test levels

Pick the **lowest level that can prove the risk**. A risk is covered at a higher level only when a lower
level cannot see it.

| Level | Project | Proves | Real dependencies | Fakes | Runs |
|---|---|---|---|---|---|
| **Unit** | `backend/tests/HospitalManagement.UnitTests` | A business rule in one service: double booking, scheduling in the past, status rules, audit entry written | none | EF Core on a real PostgreSQL container is preferred over the in-memory provider when queries matter; otherwise plain objects | every PR |
| **API** | `backend/tests/HospitalManagement.ApiTests` | The HTTP contract: status codes, the role-by-endpoint matrix, per-record access (BOLA), validation, error bodies | PostgreSQL (Testcontainers), the real API pipeline (`WebApplicationFactory<Program>`) | Keycloak is replaced by a local test token issuer (§5) | every PR |
| **E2E** | `e2e/HospitalManagement.E2E` | A whole journey works with the real identity provider and deployment wiring | PostgreSQL, Keycloak, API (docker-compose stack) | nothing | merge to `master`, nightly, PR label `e2e` |

### Where a check belongs

| Question the test answers | Level |
|---|---|
| Is a rule inside a service correct for given inputs? | Unit |
| Does role X get status Y on endpoint Z? | API |
| Can patient A see patient B's record? | API |
| Does a real Keycloak token for a real sample user work against the deployed API? | E2E |
| Does the API reject a token with the wrong issuer, audience, signature or expiry? | API |

## 3. Tools

| Purpose | Tool |
|---|---|
| Test framework | xUnit |
| Assertions | xUnit `Assert` (no extra assertion library unless the team agrees) |
| In-process API host | `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) |
| Real database | `Testcontainers.PostgreSql` (same major version as `docker-compose.yml`: PostgreSQL 17) |
| Test tokens | `System.IdentityModel.Tokens.Jwt` with an RSA key generated per test run |
| E2E scenarios | Reqnroll (Gherkin) |
| E2E driver | Playwright for .NET: browser for user journeys through `frontend-web`, API request context for API-only journeys |
| Mutation testing (optional, phase 5) | Stryker.NET on `AppointmentService` and `MedicalRecordService` |
| Secret scanning | gitleaks in CI |

## 4. Project layout and conventions

```
backend/tests/
  HospitalManagement.UnitTests/
    Appointments/…Tests.cs
    MedicalRecords/…Tests.cs
  HospitalManagement.ApiTests/
    Infrastructure/   ApiFactory, TestTokenIssuer, TestIdentities, database fixture
    Security/         authorization matrix, BOLA, token validation
    Contracts/        status codes, validation and error bodies per endpoint
e2e/
  HospitalManagement.E2E/
    Features/*.feature
    Steps/
```

Rules for every test:

1. **Name:** `Method_or_Endpoint_Condition_ExpectedResult`, e.g. `GetMedicalRecord_OtherPatientsRecord_Returns403`.
2. **Traceability:** at least one `[Trait("Risk", "R-xxx")]`. Use `[Trait("Category", "Security")]` for security tests.
   A test for a risk that is still open is skipped as a known gap (`Skip = "Known gap R-xxx"` plus
   `[Trait("KnownGap", "R-xxx")]`) and asserts the correct behavior — see [QUALITY_GATES.md](QUALITY_GATES.md#1-gates).
3. **One behavior per test.** Arrange – Act – Assert, with the assertion on the outcome the risk is about
   (status code **and** that no other patient's data is in the body).
4. **Exact assertions.** Assert the exact status code, not "not 200". Never `Assert.True(true)`.
5. **No shared mutable state** between tests. A test creates the data it needs.
6. **No conditional logic** (`if`) in a test body. Use `[Theory]` with data rows instead.

## 5. Test identities

| Identity | Used at | How |
|---|---|---|
| Anonymous | API | No `Authorization` header |
| No role | API | Valid token with no realm roles |
| Admin, Doctor, Nurse | API | Valid token with that realm role in `realm_access.roles` |
| Doctor D1, Doctor D2 | API | `Doctor` role, each linked to a different doctor profile (record author and editor, R-010, R-011) |
| Unlinked doctor | API | `Doctor` role, no doctor profile linked (R-021) |
| Patient A, Patient B | API | Valid token with `Patient` role, each linked to a different patient record |
| Unlinked patient | API | `Patient` role, no patient record linked |
| Deactivated user | API | Valid token for a user whose account status is inactive |
| Tampered tokens | API | Wrong issuer, wrong audience, expired, bad signature, `alg: none` |
| `admin.user`, `doctor.user`, `nurse.user`, `patient.user` | E2E | Real Keycloak sample users; password from `DEV_USERS_PASSWORD` in `.env` |

The API validates issuer, audience, lifetime (30 s clock skew) and signature, and reads roles from
`realm_access` (`backend/api/Auth/AuthenticationExtensions.cs:31-38`, `:56-74`). The test token issuer
must produce the same shape, and the test host must trust only the test key.

## 6. Test data

- **Synthetic only.** No real names, IDs or medical content. Use obviously fake values (`Test Patient A`),
  following the synthetic data rules in [BUSINESS_FLOWS.md §4](BUSINESS_FLOWS.md#4-synthetic-data-rules).
- **Scenarios come from the flows.** Every scenario in [BUSINESS_FLOWS.md](BUSINESS_FLOWS.md) is covered at
  the lowest level that can prove it; the test carries the scenario ID as a trait
  (`[Trait("Scenario", "F-03.4")]`) next to the risk, and E2E scenarios carry it as a Gherkin tag (`@F-03.4`).
- **Created by the test** through the API as an authorized identity (`TestData` in the API tests) or
  directly in an in-memory database (unit tests). The seed data (`Database:SeedOnStartup`) is not relied on.
- **Isolation by data:** one PostgreSQL container and one API host per test run; every test creates its own
  users, patients, doctors and appointments with unique values and asserts only on what it created. Tests
  never depend on IDs or rows created by another test.
- **Time:** the services read the clock from an injected `TimeProvider` (A-004). Unit tests fix it with
  `FakeTimeProvider` and move it explicitly ("before / after the start" is exact). API tests use dates
  relative to now with a safe margin (e.g. `+7 days`), never fixed calendar dates.

## 7. Environments

| Environment | Started by | Used for | Secrets |
|---|---|---|---|
| Unit / API test host | `dotnet test` | Unit and API tests | None; the test key is generated per run |
| Docker Compose stack | `docker compose up -d` | E2E locally and in CI | `.env` locally (git-ignored), GitHub Actions secrets in CI |
| Aspire AppHost | `dotnet run --project backend/HospitalManagement.AppHost` | Developer exploration, not used by the test suite | Aspire parameters / user secrets |

## 8. Determinism

- **No retries** — not in xUnit, not in Playwright, not in the CI job.
- **No fixed waits.** Wait for a condition: `/health` returning 200, a Playwright locator or response.
- **No order dependence.** Tests pass when run alone, in any order, and in parallel within their collection.
- **No network** outside the containers the test starts.
- A failure that cannot be reproduced is handled by the flaky-test policy in [QUALITY_GATES.md](QUALITY_GATES.md#3-flaky-tests).

## 9. End-to-end approach

- **Few journeys, high value.** 3–5 scenarios: sign in per role, book an appointment as a patient, a doctor
  records a diagnosis, a patient cannot see another patient's record.
- **Gherkin describes behavior,** not clicks: `Given a linked patient`, `When they book an appointment for next week`.
- **The frontend signs in through Keycloak** (`frontend-web/src/auth/`, since `d96bba1`; A-002 closed).
  Browser journeys start at `http://localhost:3000`, are redirected to the real Keycloak login page and
  sign in as a sample user. Journeys that the frontend does not offer yet (appointments, medical records)
  use the Playwright API request context with the real Keycloak token obtained by signing in through the
  browser first. The realm allows no password grant (R-027), so tokens cannot be requested with a password.
- **Login journeys to cover:** each role signs in and sees its name; a user without a hospital role sees
  "No access"; logout returns to the Keycloak login page.

## 10. What makes a test "done"

A test is done when it is tagged with its risk, fails when the behavior is broken (checked once by breaking
the code or by mutation), passes reliably, and the risk's row in [RISK_REGISTER.md](RISK_REGISTER.md)
names it.
