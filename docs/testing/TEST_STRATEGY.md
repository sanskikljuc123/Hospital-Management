# Test Strategy

How the Hospital Management platform is tested: scope, test levels, tools, data, environments and
conventions. Principles are in [README.md](README.md); risks in [RISK_REGISTER.md](RISK_REGISTER.md).

## 1. Scope

| In scope | Out of scope (for now) |
|---|---|
| Backend API in `backend/` (.NET 10, ASP.NET Core, EF Core, PostgreSQL) | Keycloak configuration beyond the realm file |
| Authentication and authorization with Keycloak (`HospitalRealm`) | Keycloak itself, PostgreSQL itself, Aspire itself |
| Business rules in `backend/Services/` | Performance and load testing |
| A few end-to-end journeys through the full stack | Visual / layout testing of `frontend-web/` |

## 2. Test levels

Pick the **lowest level that can prove the risk**. A risk is covered at a higher level only when a lower
level cannot see it.

| Level | Project | Proves | Real dependencies | Fakes | Runs |
|---|---|---|---|---|---|
| **Unit** | `backend/tests/HospitalManagement.UnitTests` | A business rule in one service: double booking, scheduling in the past, status rules, audit entry written | none | EF Core on a real PostgreSQL container is preferred over the in-memory provider when queries matter; otherwise plain objects | all on every branch push that changes the backend, and on every PR |
| **API** | `backend/tests/HospitalManagement.ApiTests` | The HTTP contract: status codes, the role-by-endpoint matrix, per-record access (BOLA), validation, error bodies | PostgreSQL (Testcontainers), the real API pipeline (`WebApplicationFactory<Program>`) | Keycloak is replaced by a local test token issuer (§5) | all on every branch push that changes the backend, and on every PR |
| **Component** | `frontend-web/src/**/*.test.ts(x)` (Vitest) | A frontend form, table or Redux slice: the payload a form sends (every field the API's `PUT` replaces), the date it sends, the HTTP method and path a slice calls, form validation | none (jsdom) | the API is mocked at the network boundary by MSW, and an unexpected request fails the test; Keycloak is mocked (`src/test/setup.ts`) | all on every branch push that changes `frontend-web/`, and on every PR (G2) |
| **E2E** | `e2e/HospitalManagement.E2E` | A whole journey works with the real identity provider and deployment wiring | PostgreSQL, Keycloak, API (docker-compose stack) | nothing | every PR that changes what the stack runs (QUALITY_GATES G7), merge to `master`, nightly, PR label `e2e` |

`.github/workflows/smoke.yml` gives early feedback on pushes to branches other than `master`, before a pull
request exists; it is not a required merge gate. It runs **all** unit and API tests when backend code or its
build configuration changes (since 2026-11-23; before that only the five `[Trait("Suite", "Smoke")]` tests). The
tests themselves take about 25 s: restore, build and the PostgreSQL container are most of the job, so the subset
saved about 10 s and skipped 160 tests. The `Suite=Smoke` trait stays for a quick local run
(`dotnet test --filter Suite=Smoke`). Risk/skip validation and documentation-reference checks run on every branch
push; the frontend production build and the component tests run only when a push changes `frontend-web/`.

The component level was added on 2026-11-23 (UI_TEST_DESIGN Q-a): edit bugs in the frontend had passed every
gate because nothing below E2E tested the frontend. The 12 tests run in about 30 s on a developer machine;
antd and the icon packs are pre-bundled once (`vitest.config.ts`), which took the run from 88 s to 30 s.

### Where a check belongs

| Question the test answers | Level |
|---|---|
| Is a rule inside a service correct for given inputs? | Unit |
| Does role X get status Y on endpoint Z? | API |
| Can patient A see patient B's record? | API |
| Does a frontend form send every field, in the right format, to the right endpoint? | Component |
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
| Frontend component tests | Vitest in jsdom, React Testing Library and user-event, MSW for the API, jest-dom matchers |
| E2E scenarios | Reqnroll (Gherkin) |
| E2E driver | Playwright for .NET: browser for user journeys through `frontend-web`, API request context for API-only journeys |
| Mutation testing (phase 5) | Stryker.NET on `AppointmentService`, `MedicalRecordService`, `OutboxProcessor`, `UserService` (§11) |
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
2. **Traceability:** at least one `[Trait("Risk", "R-xxx")]`; in a frontend component test, a tag such as
   `@R-059` in the title of the test or of its `describe` block. Use `[Trait("Category", "Security")]` for security tests.
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
  call the deployed API with the real Keycloak token the frontend sends, captured from the browser's own
  API request. The realm allows no password grant (R-027), so tokens cannot be requested with a password.

**Implemented (phase 4)** in `e2e/HospitalManagement.E2E` — Reqnroll features with risk and scenario tags
(`@R-001`, `@F-03.1`), Playwright for .NET (Chromium):

| Feature | Scenarios |
|---|---|
| `SignIn.feature` | each role signs in and sees its name; no password-reset link; an account without a hospital role sees "No access" and its token gets 403; sign-out ends the Keycloak session |
| `CatalogueEditing.feature` | in the web application the administrator renames a doctor, renames a patient and changes a service's description, and every field the form does not change stays as stored (R-059); a birthday picked in the UTC+1 and UTC−5 time zones is stored and shown as that day (R-060). Browser time zone set per scenario with an `@timezone:` tag |
| `ClinicalJourney.feature` | a patient books and sees only own appointments; cannot book for another patient; the doctor's diagnosis reaches the patient with the linked doctor as author, another patient's and a missing record give 403; an amendment shows in the history for clinical staff and is refused to the patient |

How they run:

- **Against the production build** of the frontend (`vite build` + `vite preview` on port 3000), not the dev
  server — faster, and what users get.
- **Sequentially** (`xunit.runner.json`): all scenarios share one stack.
- **Fresh synthetic data per scenario:** the administrator creates a new patient and a new doctor profile
  and links `patient.user` and `doctor.user` to them; a temporary role-less Keycloak account is created and
  deleted by the scenario that needs it.
- **No fixed waits, no retries:** Playwright waits on URLs, elements and requests; a failed scenario saves a
  Playwright trace (`TestResults/traces`).
- **Configuration from the environment:** `E2E_USERS_PASSWORD` (or `DEV_USERS_PASSWORD`),
  `E2E_KEYCLOAK_ADMIN_PASSWORD` (or `KEYCLOAK_ADMIN_PASSWORD`), optional `E2E_FRONTEND_URL`, `E2E_API_URL`,
  `E2E_KEYCLOAK_URL`, `E2E_HEADED=1`.
- **CI:** `.github/workflows/e2e.yml` (gate G7) on every pull request that changes what the stack runs, on merges
  to `master`, nightly, on demand and on pull requests
  labelled `e2e`, with fresh random passwords per run.

## 10. What makes a test "done"

A test is done when it is tagged with its risk, fails when the behavior is broken (checked once by breaking
the code or by mutation), passes reliably, and the risk's row in [RISK_REGISTER.md](RISK_REGISTER.md)
names it. The check that it fails is not taken on trust: the pull request that sets a risk to `Covered` links
the run where the test failed (red evidence, QUALITY_GATES §2).

## 11. Mutation testing

Stryker.NET changes the code in small ways (`<=` to `<`, a removed call, a flipped condition) and checks that
some unit test fails. A change that no test notices is a test that is missing.

| | |
|---|---|
| Scope | `AppointmentService`, `MedicalRecordService`, `OutboxProcessor`, `UserService` — the business, access-identity and reliability rules |
| Config | `backend/tests/HospitalManagement.UnitTests/stryker-config.json`; tool pinned in `dotnet-tools.json` |
| Run locally | `dotnet tool restore`, then `dotnet tool run dotnet-stryker --concurrency 2` in that folder (about 25 minutes; report in `StrykerOutput/`) |
| CI | `.github/workflows/mutation.yml`: the four services in full nightly (03:00 UTC) and on demand; HTML report as an artifact. On every pull request that changes service code: only the changed files, any service (G9, below) |
| Threshold | The run fails below **90 %** (`break`) |
| SDK | Pinned to 10.0.x in `global.json`. The GitHub runner also has a newer SDK preinstalled; built with it, the VSTest that Stryker 4.16 ships aborts test discovery (`NullReferenceException` in `GetTestHostPath`, 0 tests found), while `dotnet test` itself still works. The same pin keeps local and CI builds on one SDK |

### Mutation testing of changed code (G9)

On a pull request that changes `backend/Services/`, Stryker mutates only the files changed since the base commit
(`--since`), in any service, not only the four above. `scripts/mutation_summary.py` then fails the job when a
mutant on a line the pull request changed went undetected, and lists every undetected mutant in the job summary,
changed lines first. The rule is about the change, not the file: a file's score includes old survivors (some
reviewed as equivalent, see below), so a score threshold would block changes for code they never touched.

Checked on 2026-11-23 in a clone of this repository: a new, untested rule in `DepartmentService` (description
at most 500 characters) failed the gate on its two comparison mutants (`>` to `>=` and `<`); with a unit test for
500 and 501 characters the gate passed. Stryker mutated only that file (530 other mutants filtered out); about
2½ minutes locally. The same run showed that `DepartmentService` as a whole scores 54.5 %: its existing code has
untested branches, which the nightly scope does not cover.

Locally, `--since` needs the full base SHA, a normal clone (Stryker's git library does not follow `git worktree`)
and, on Windows, `core.autocrlf=false` in that clone; put `--output` outside the repository.

**Not mutated, on purpose:** error-message strings (the text is not a business rule) and logging calls (they do
not change behavior).

**History.** First run 67.19 %: survivors showed missing tests for start-time boundaries (`<=` vs `<`), list
filters and ordering, notifications, unknown references, deletes, audit details, session and cache handling,
role-less users and error truncation. 25 tests were added (`AppointmentBoundaryTests` and additions to the
service tests). Second full run: **95.32 %**.

**Accepted survivors** (reviewed; a test cannot or should not kill them):

| Location | Mutation | Why it is accepted |
|---|---|---|
| `AppointmentService` slot query, `ScheduledAt > windowStart` → `>=` | Boundary of the database pre-filter | Equivalent: an appointment starting exactly at the window start with the maximum duration ends exactly when the new one starts, which the in-memory overlap check already treats as free |
| `AppointmentService.ToUtc`, unspecified kind → `ToUniversalTime()` | Treats a time without offset as local instead of UTC | That is the open decision D-13 / risk R-051 (known-gap test in the API tests); the expected behavior is not decided yet |
| `OutboxProcessor` and `UserService.LinkAsync` transaction handling | Begin/commit removed or forced | Equivalent on the in-memory database (it has no transactions); the real transactional behavior is covered by the API tests on PostgreSQL (`OutboxTests`, `TransactionalWriteTests`, linking in every API test) |
| `OutboxProcessor.Truncate`, `<=` → `<` | Cut at exactly 500 characters | Equivalent: cutting a 500-character string to 500 characters returns the same string |
| `UserService` concurrent first sign-in (`DbUpdateException` branch) | Not covered | Needs two requests racing on the unique index of a real database; not reachable on the in-memory database |
