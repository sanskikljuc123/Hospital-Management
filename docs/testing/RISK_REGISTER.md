# Risk Register

The list of what could go wrong, how bad it would be, and which test proves it does not happen.
Every test traces to a row here; every Critical and High row must have a passing test before merge
([QUALITY_GATES.md](QUALITY_GATES.md)).

## 1. Risk model

**Impact** — what happens if the risk occurs:

| Score | Level | Meaning in this system |
|---|---|---|
| 4 | Critical | Patient medical data disclosed or changed by someone not allowed; account takeover; secrets exposed |
| 3 | High | Wrong clinical or scheduling data saved; a role gets access it should not have to non-medical data |
| 2 | Medium | A business rule is bypassed with limited harm; misleading error; missing audit entry |
| 1 | Low | Cosmetic or inconvenience; no data or access impact |

**Likelihood** — how likely it is to occur or be exploited:

| Score | Level | Meaning |
|---|---|---|
| 4 | Almost certain | Happens in normal use, or any user can trigger it by changing one value |
| 3 | Likely | Any authenticated user can trigger it with a crafted request |
| 2 | Possible | Needs a specific role, state or sequence |
| 1 | Unlikely | Needs several unusual conditions or an insider with admin rights |

**Severity** = Impact × Likelihood:

| Score | Severity | Required before merge |
|---|---|---|
| 12–16 | **Critical** | Covered by a passing test; no exceptions |
| 8–9 | **High** | Covered by a passing test, or explicitly accepted by the test owner with a reason and an end date |
| 4–6 | **Medium** | Covered when the related code changes |
| 1–3 | **Low** | Recorded; covered at the owner's discretion |

**Threat categories (STRIDE)** used to find risks: **S**poofing, **T**ampering, **R**epudiation,
**I**nformation disclosure, **D**enial of service, **E**levation of privilege.

**Status:** `Proposed` (an agent or a person suggested it) → `Accepted` (the test owner agreed with the
rating) → `Covered` (a passing test is named) → `Closed` (the risk no longer applies). A rating changes
only when the test owner changes it.

## 2. Risks

Ratings marked `Proposed` wait for the test owner. `Accepted` rows were decided by the test owner on
2026-11-15 when the related open question was answered; "Fixed in" names the commit that added the control.
`Covered` was set by the test owner on 2026-11-20 for every risk whose tests prove it at its level; from then
on gate G4 fails if a Covered risk loses its last active test. Risks whose tests prove only part of them
stay below `Covered` (R-008: no check against linking the wrong patient; R-021: depends on admins linking
accounts; R-024, R-039, R-041, R-042, R-043: partial or configuration-only evidence), and so do risks whose
only test is an E2E scenario, because E2E does not run on every pull request.
"Evidence" is where the current control is; "Tests" names the test classes that prove it.

**Ratings assume real patient data.** The system holds only synthetic data, but it is built and tested as if
it were in production with real patients (Q-007, [BUSINESS_FLOWS.md](BUSINESS_FLOWS.md#operating-principle)),
so no rating is lowered because the data is fake.

| ID | Risk | STRIDE | I | L | Severity | Evidence (current control) | Level | Tests | Status |
|---|---|---|---|---|---|---|---|---|---|
| R-001 | A patient reads another patient's medical record | I | 4 | 3 | **Critical (12)** | `IPatientAccessPolicy` (`api/Auth/PatientAccessPolicy.cs`), scope `ClinicalStaff`, used by `MedicalRecordController`; controllers may not decide ownership themselves (architecture test) | API | `BolaTests`, `PatientAccessPolicyTests`, `ArchitectureRulesTests` | Covered |
| R-002 | A patient reads or cancels another patient's appointment, or books one for another patient | I, T | 4 | 3 | **Critical (12)** | `IPatientAccessPolicy`, scope `Staff`, used by `AppointmentController` for list, read, book and cancel | API | `BolaTests`, `PatientAccessPolicyTests`, `ArchitectureRulesTests` | Covered |
| R-003 | A patient reads another patient's profile | I | 4 | 3 | **Critical (12)** | `IPatientAccessPolicy`, scope `Staff`, used by `PatientController` | API | `BolaTests`, `PatientAccessPolicyTests`, `ArchitectureRulesTests` | Covered |
| R-004 | A patient learns which record IDs exist (404 for missing vs 403 for someone else's) | I | 3 | 3 | **High (9)** | `PatientAccessPolicy.CheckAsync`: a missing resource is `NotFound` for staff and `Forbidden` for everyone else | API | `BolaTests`, `PatientAccessPolicyTests` | Covered |
| R-005 | A non-admin calls an admin endpoint (user status, user link, audit log, delete) | E | 4 | 2 | **High (8)** | `Policies.cs:34`; `[Authorize(Policy = AdminOnly)]` on 15 actions and on `AuditLogController` | API | `AuthorizationMatrixTests` | Covered |
| R-006 | A forged, expired, wrong-issuer or wrong-audience token is accepted | S | 4 | 2 | **High (8)** | `AuthenticationExtensions.cs:31-38` | API | `TokenValidationTests` | Covered |
| R-007 | A deactivated account still gets data | E | 4 | 2 | **High (8)** | `UserSyncMiddleware.cs:17-24` | API | `AccountStateTests` | Covered |
| R-008 | An admin links a user to the wrong patient, giving them that patient's records | E, I | 4 | 2 | **High (8)** | `UserService.LinkAsync`: a record already linked to another user is refused (409), a new link replaces the old one, every link is audited (`User.Linked`). Still no confirmation step against linking the wrong patient | API | `UserServiceTests` | Proposed |
| R-009 | A doctor is double-booked in the same time slot | T | 3 | 3 | **High (9)** | `AppointmentService.cs:253-264` | Unit | `AppointmentRulesTests` | Covered |
| R-010 | A doctor creates a medical record attributed to another doctor, or attaches it to another doctor's appointment | S, R | 3 | 3 | **High (9)** | Fixed in `7417a9e`: author from the caller's linked doctor (`MedicalRecordController.cs:60`); appointment must match patient and doctor, checked before its state (`MedicalRecordService.cs:74-80`) — Q-002 | API | `RecordAuthorTests` | Covered |
| R-011 | A change to a medical record cannot be traced to the doctor who made it, or the previous content is lost | T, R | 3 | 3 | **High (9)** | Fixed in `871484e`: every change stored as a revision with editor, in the same transaction (`MedicalRecordService.cs:130-136`); history at `GET /api/MedicalRecord/{id}/history` (`MedicalRecordController.cs:79-85`) — Q-003 | API | `RecordAuthorTests` | Covered |
| R-012 | Secrets are committed to the repository | I | 4 | 2 | **High (8)** | `.env` is git-ignored; no automated scan yet | CI | – | Proposed |
| R-013 | A clinical or access change is not written to the audit log | R | 3 | 2 | Medium (6) | `AuditActions.cs`; `LogAsync` calls in every service that changes data (appointments, records, users, patients, doctors, services, departments) | Unit | `AppointmentRulesTests`, `CatalogTests` | Covered |
| R-014 | An appointment is put into an invalid state (e.g. `Completed` → `Scheduled`, completed before it starts, moved into the past) | T | 3 | 2 | Medium (6) | Fixed in `8f7079e`: lifecycle in `AppointmentService.cs:92` (final states), `:132-150` (transitions), `:119` (moved → Scheduled), `:203-205` (cancel) — Q-004; rules in `docs/API_ENDPOINTS.md` | Unit | `AppointmentRulesTests` | Covered |
| R-015 | A user with a valid token but no hospital role gets any data | I, E | 2 | 3 | Medium (6) | Fixed in `cd15a5e`: `DefaultPolicy` and `FallbackPolicy` require a hospital role (`Policies.cs:27-32`); frontend shows "No access" (`AuthGate.tsx`) — Q-001 | API | `AuthorizationMatrixTests` | Covered |
| R-016 | Over-posting on `POST/PUT /api/Service` creates or links unrelated data (entity bound directly, with `Patients` and `DoctorServices` collections) | T, E | 2 | 2 | Medium (4) | Fixed in C-2 refactor: `ServiceController` binds `ServiceDto`, only documented fields are copied (`ServiceServiceImp.cs`) | API | `CatalogTests` | Covered |
| R-017 | An error response or log line exposes another patient's data, a stack trace or a token | I | 3 | 2 | Medium (6) | `DbUpdateExceptionHandler.cs`; `app.UseExceptionHandler()` in `Program.cs:47` | API | – | Proposed |
| R-018 | An appointment is booked or moved into the past | T | 2 | 3 | Medium (6) | `AppointmentService.cs:48-49, 108-109` | Unit | `AppointmentRulesTests` | Covered |
| R-019 | Deleting a doctor, patient or department with linked data corrupts or orphans records instead of returning 409 | T | 2 | 2 | Medium (4) | `DbUpdateExceptionHandler.cs`; documented 409 in `docs/API_ENDPOINTS.md` | API | `CatalogTests` | Covered |
| R-020 | An admin can read medical records or their history (they must not: `Admin` is not in the class-level roles) | I | 3 | 1 | Low (3) | `MedicalRecordController.cs:18, 84` | API | `AuthorizationMatrixTests`, `RecordAuthorTests` | Covered |
| R-021 | A doctor whose account is not linked to a doctor profile cannot write records (blocked at work) | D | 2 | 3 | Medium (6) | 403 by design (`MedicalRecordController.cs:60-62, 75-77`); depends on admins linking accounts — A-005 | API | `RecordAuthorTests` | Proposed |
| R-022 | A final status set by mistake cannot be corrected, or a correction is misused to rewrite history | T, R | 2 | 2 | Medium (4) | Fixed in `4af797a`: admin-only `POST /api/Appointment/{id}/correct-status` (`AppointmentController.cs:81`) with a mandatory reason; only NoShow ⇄ Completed and Cancelled → Scheduled before the start, and never Completed → NoShow while a medical record is linked (`AppointmentService.cs:170-216`); audited as `Appointment.StatusCorrected` — Q-005, F-11.6 | Unit | `AppointmentRulesTests` | Covered |
| R-023 | Script injection (XSS) in `frontend-web` steals the access token | I, E | 4 | 1 | Medium (4) | Tokens are kept in memory by keycloak-js, not in `localStorage` (`frontend-web/src/auth/keycloak.ts`); React escapes output; no CSP header yet | E2E | – | Proposed |
| R-024 | A user with no hospital role still gets a local profile and a login audit entry | R | 1 | 3 | Low (3) | `UserSyncMiddleware` runs before authorization (`Program.cs:57-58`) | API | – | Proposed |
| R-069 | A role (staff or patient) registers or changes patients outside its duty (BUSINESS_FLOWS F-02: a nurse or an administrator), e.g. a doctor or a patient, or a later change reuses the `Staff` policy for patient writes (Q-017, Q-024, Q-033, Q-034) | E, T | 3 | 4 | **Critical (12)** — rated by the test owner, 2026-11-24 | Fixed on 2026-11-26: `POST` and `PUT /api/Patient` use `Policies.PatientRegistration` (Admin, Nurse; `Policies.cs`), so a doctor, a patient and a user without a hospital role get 403 (decision Q-017); the matrix rows in `AuthorizationMatrix.cs` and SECURITY_TESTING §2 say the same; status set to Covered by the test owner on 2026-11-26 (Q-036) on the red run (8 Doctor cases failed before the change) and four deliberate breaks | API | `PatientWriteAccessTests`, `AuthorizationMatrixTests`, `CatalogTests` | Covered |

### 2.1 Production readiness

Gaps found in the production-readiness review of 2026-11-15. They are about deployment, configuration and
operations rather than code behavior, so two more levels are used: **Config** — proven by an automated check
of the deployment configuration in CI; **Ops** — proven by a documented and rehearsed procedure (restore
drill, failover). The order in which they are fixed is in [QUALITY_GATES.md](QUALITY_GATES.md#7-production-readiness).

| ID | Risk | STRIDE | I | L | Severity | Evidence (current state) | Level | Tests | Status |
|---|---|---|---|---|---|---|---|---|---|
| R-025 | Tokens and medical data travel unencrypted: no TLS on Keycloak, the API or the frontend | I, S | 4 | 3 | **Critical (12)** | `docker-compose.yml:37` (`start-dev`), `:49` (`KC_HTTP_ENABLED`), `:83` (`RequireHttpsMetadata: false`); API listens on HTTP 8080 (`backend/api/Dockerfile`) | Config | – | Accepted |
| R-026 | Keycloak runs in development mode and imports the sample users with one shared password | S, E | 4 | 3 | **Critical (12)** | `docker-compose.yml:37`, `:52`; realm users in `keycloak/realms/HospitalRealm-realm.json` | Config | – | Accepted |
| R-027 | The password grant on the public `hospital-web` client lets anyone script password guessing outside the login page | S | 3 | 3 | **High (9)** | Fixed in `942cd37`: `directAccessGrantsEnabled: false` on `hospital-web` in the realm file; no client in the realm allows the password grant | Config | – | Accepted |
| R-028 | Staff with access to medical data sign in with a password only: no MFA, no password policy | S | 4 | 2 | **High (8)** | realm: no `passwordPolicy`, no OTP required action | Config | – | Proposed |
| R-029 | The compose file marked `Production` seeds sample data and exposes Swagger | I, E | 3 | 3 | **High (9)** | Fixed in `d7a7f4d`: off in `docker-compose.yml`; on only in `docker-compose.override.yml` for local development | Config | – | Accepted |
| R-030 | A hard-coded SQL Server `sa` password and 177 build-output files (`bin/`, `obj/`, `.vs/`) are in the public repository | I | 3 | 3 | **High (9)** | Fixed in `619ef39`: legacy projects deleted, `bin/`, `obj/`, `.vs/` ignored. The password remains in git history — rotate it where still used; gitleaks in CI (wave 2) keeps new secrets out — see R-012 | CI | – | Accepted |
| R-031 | The API and Keycloak use the same PostgreSQL superuser, so a flaw in either reaches all data | E, T | 4 | 2 | **High (8)** | `docker-compose.yml:18, 43, 80` | Config | – | Accepted |
| R-032 | Data is lost for good: no backup, no tested restore, no RPO/RTO | D | 4 | 2 | **High (8)** | only the `postgres-data` volume (`docker-compose.yml:22`) | Ops | – | Accepted |
| R-033 | The frontend image runs the Vite development server and is not part of the deployed stack | D, I | 3 | 3 | **High (9)** | `frontend-web/Dockerfile` (`CMD ["npm", "run", "dev"]`); not in root `docker-compose.yml` | Config | – | Accepted |
| R-034 | Hostnames, issuer, CORS and redirect URIs are fixed to `localhost`, so a deployment fails or is patched by hand | D | 2 | 4 | **High (8)** | `docker-compose.yml:47, 82, 87`; realm `redirectUris` — Q-006 | Config | – | Accepted |
| R-035 | No automated tests and no CI: any regression of R-001 … R-024 reaches production unseen | T, E | 4 | 3 | **Critical (12)** | No longer applies (closed 2026-11-20): unit, API and E2E test projects and GitHub Actions gates G1–G7 (PR #6, #7, #8; weekly mutation run, PR #9); the ruleset *Protect master* makes them required before merge (PR #11, E2E on sign-in changes PR #19); G4 cannot lose a Covered risk's tests unnoticed (PR #12, #13). Regressions of R-001 … R-024 are now caught before merge by the tests named in their own rows | CI | `.github/workflows/ci.yml`, `e2e.yml`, `mutation.yml` | Closed |
| R-036 | No security headers (HSTS, CSP, `X-Content-Type-Options`) and no rate limiting | I, D | 3 | 2 | Medium (6) | `Program.cs` has neither `UseHsts` nor a rate limiter — see R-023 | API | – | Proposed |
| R-037 | List endpoints return every row (no paging): slow responses, large data exposure per call | D, I | 2 | 3 | Medium (6) | `AppointmentService.cs:39`, `DoctorServiceImp.cs:24` | API | – | Proposed |
| R-038 | Migrations and seeding run at start-up; several API instances race each other | T, D | 3 | 2 | Medium (6) | `docker-compose.yml:84-85`; `DatabaseInitializer.cs` — Q-008 | Ops | – | Accepted |
| R-039 | A deactivated user keeps access on other API instances for up to 5 minutes | E | 3 | 2 | Medium (6) | `UserService.cs:14` (`SyncCacheDuration`) — Q-008 | API | – | Accepted |
| R-040 | Floating image tags and no dependency or image vulnerability scanning | T | 3 | 2 | Medium (6) | `postgres:17-alpine`, `keycloak:26.4` (`docker-compose.yml:16, 35`), `node:alpine` (`frontend-web/Dockerfile`) | CI | – | Proposed |
| R-041 | The audit trail can be altered or lost: same database and user as the app, no retention; Keycloak keeps events 30 days | R | 3 | 2 | Medium (6) | Lost: fixed in C-4 — the audit entry is written in the same transaction as the change and fails closed (`TransactionalWriteFilter`, `AuditService.cs`). Still open: same database and user as the app, no retention; realm `eventsExpiration: 2592000` | Ops | `TransactionalWriteTests` | Proposed |
| R-042 | Personal data in logs (email recipients), and email/SMS notifications are never actually sent | I | 2 | 3 | Medium (6) | `LoggingIntegrations.cs:18` logs the e-mail recipient (placeholder provider). Notifications now go through the outbox (C-4): delivered after the change, retried, payload without personal data; the background loop itself is tested (start, backlog, database outage, real host) | Unit | `OutboxTests`, `CatalogTests`, `OutboxDispatcherTests`, `OutboxDispatcherHostTests` | Proposed |
| R-043 | "Forgot password" is offered but no SMTP server is configured, so it cannot work | D | 2 | 3 | Medium (6) | Fixed in `942cd37`: `resetPasswordAllowed: false` in the realm file until SMTP is configured | Config | – | Accepted |
| R-044 | The patient model cannot hold realistic patients (phone is a number, no name or date of birth) and deletion is permanent, so real-life flows cannot be exercised | T | 3 | 3 | **High (9)** | `Patient.cs:8-12`; `PatientServiceImp.cs:60-68` — Q-007, BUSINESS_FLOWS F-02; phone format decided in Q-016 (international, as text), not built yet | Unit | – | Proposed |
| R-045 | No data retention, patient data export or consent mechanisms (the legal paperwork is out of scope with synthetic data; the mechanisms are not) | I, R | 3 | 2 | Medium (6) | no document — Q-007 | Ops | – | Proposed |
| R-046 | Problems in production go unnoticed: telemetry is exported only if configured, no alerts, no central logs | R, D | 3 | 2 | Medium (6) | `HospitalManagement.ServiceDefaults/Extensions.cs:60-61` (export only if configured). Partly addressed in PR #17: failed and stuck notifications make `/health` Degraded (`OutboxHealthCheck`), and delivery counters are exported (`OutboxMetrics`). Still open: no alerts, no central logs, export not enforced in production | Ops | `OutboxMonitorTests`, `OutboxProcessorTests`, `OutboxHealthTests` | Proposed |
| R-047 | No runbooks for deploy, rollback, secret rotation and restore | D | 2 | 2 | Medium (4) | no document | Ops | – | Accepted |

### 2.2 Business flows

Gaps found while writing the real-life flows in [BUSINESS_FLOWS.md](BUSINESS_FLOWS.md) on 2026-11-15. The
flow scenario that exposes each one is named in the evidence column.

| ID | Risk | STRIDE | I | L | Severity | Evidence (current state) | Level | Tests | Status |
|---|---|---|---|---|---|---|---|---|---|
| R-048 | Two bookings for the same doctor and slot at the same moment both succeed | T | 3 | 2 | Medium (6) | Fixed in PR #15: exclusion constraint `EX_Appointments_DoctorNoOverlap` on the doctor and the time range (migration `AppointmentNoOverlap`); the violation, and the deadlock two such inserts can report, become 409 (`DbUpdateExceptionHandler.cs`). Before the fix, 8 of 10 parallel bookings of one slot were saved — F-03.9 | API | `ConcurrentBookingTests`, `AppointmentRulesTests` | Covered |
| R-049 | The same person is registered twice, splitting their history across two patient records | T | 3 | 3 | **High (9)** | No duplicate check in `PatientServiceImp.cs` — F-02.3 | Unit | – | Proposed |
| R-050 | Appointments nobody closes stay `Scheduled`/`Confirmed` forever | T | 2 | 4 | **High (8)** | No job or rule closes past appointments — F-07.4 | Unit | – | Proposed |
| R-051 | A local time sent without an offset is stored as if it were UTC, shifting the appointment by the time-zone difference | T | 3 | 3 | **High (9)** | Fixed in PR #14: a time without a time zone is refused with 400 on create and update (`AppointmentService.cs:52, 90, 299`); `Z` and offsets are stored as the same instant in UTC — F-03.10. Depends on A-007 | API | `TimeZoneTests`, `AppointmentBoundaryTests` | Covered |
| R-052 | A role removed or changed in Keycloak keeps working until the access token expires | E | 3 | 2 | Medium (6) | `accessTokenLifespan: 900` in the realm file; roles read only from the token — F-01.7 | E2E | – | Proposed |
| R-053 | A medical record can be attached to an appointment that never took place (future, cancelled, no-show) | T | 3 | 2 | Medium (6) | Fixed in PR #10: a record can be attached only to a started `Scheduled`/`Confirmed`/`Completed` appointment, 409 otherwise (`MedicalRecordService.cs:83-86`); a documented visit cannot become `NoShow` through `PUT` (`AppointmentService.cs:111-115`) or a correction (`:191-195`); a started appointment cannot be moved, so it cannot be moved back and cancelled (`:121-123`) — Q-010, F-05.7, F-07.5, F-08.4, F-11.6. Open: A-006, Q-011 | Unit, API | `MedicalRecordServiceTests`, `AppointmentRulesTests`, `RecordAuthorTests`, `RecordLinkedCorrectionTests` | Covered |
| R-058 | Deleting an appointment silently removes the link from its medical records: the record stays, but no longer shows which visit it documents, and nothing is audited about the record | T, R | 2 | 2 | Medium (4) | Foreign key `SetNull` (`ApplicationDbContext.cs:116`); admin-only `DELETE /api/Appointment/{id}` (`AppointmentService.cs:237-247`) | API | – | Proposed |
| R-054 | Doctor and patient endpoints return database entities, exposing internal fields such as account links (`userId`) | I | 3 | 3 | **High (9)** | Fixed in C-2 refactor: doctor, patient and service endpoints return `DoctorDto`, `PatientDto`, `ServiceDto` (no account links or navigation data) — F-10.4 | API | `CatalogTests` | Covered |
| R-055 | A deactivated doctor keeps future appointments; patients are not told | T, D | 3 | 2 | Medium (6) | Deactivation does not touch appointments (`UserService.cs`) — F-12.2 | Unit | – | Proposed |
| R-056 | Two doctors amending the same record: the later save silently overwrites the earlier change | T | 3 | 2 | Medium (6) | No version check on `PUT /api/MedicalRecord/{id}` (`MedicalRecordService.cs:114-121`); history keeps both — F-09.3 | API | – | Proposed |
| R-057 | Deleting a service silently deletes every patient registered to it (the foreign key cascaded) | T, D | 4 | 2 | **High (8)** | Fixed in C-2 refactor: foreign key `Restrict` (migration `PatientServiceRestrictDelete`), and `DELETE /api/Service/{id}` returns 409 while patients or doctors use it (`ServiceServiceImp.cs`) | API | `CatalogTests` | Covered |

### 2.3 Web frontend

Found while designing the UI tests on 2026-11-23 ([UI_TEST_DESIGN.md](UI_TEST_DESIGN.md) §3–4). Ratings are
proposals until the test owner accepts them.

| ID | Risk | STRIDE | I | L | Severity | Evidence (current state) | Level | Tests | Status |
|---|---|---|---|---|---|---|---|---|---|
| R-059 | Editing a doctor, patient or service in the web application fails, or silently erases fields the form does not show (the API's `PUT` replaces every field) | T | 3 | 4 | **Critical (12)** | Fixed on 2026-11-23: edit doctor sends `PUT` (`doctorSlice.ts:70`); the tables pass the whole record to Edit (`Doctors.tsx:27`); the patient form has an email field (`PatientForm.tsx:53`); the doctor and service forms send the department (`DoctorForm.tsx:43`, `ServiceForm.tsx:32`) — UI_TEST_DESIGN D-1 … D-4 | Component, E2E | `DoctorForm.test.tsx`, `PatientForm.test.tsx`, `ServiceForm.test.tsx`, `catalogueTables.test.tsx`, `doctorSlice.test.ts`, `CatalogueEditing.feature` | Proposed |
| R-060 | A date picked in the web application is saved as a different date (local midnight sent as a UTC timestamp) | T | 2 | 4 | **High (8)** | Fixed on 2026-11-23: the doctor form sends `YYYY-MM-DD` and reads back only the date part (`DoctorForm.tsx:24`, `:29`) — UI_TEST_DESIGN D-5 | Component, E2E | `DoctorForm.test.tsx`, `CatalogueEditing.feature` | Proposed |
| R-061 | The web frontend and the API disagree on a route, a method or a field (the frontend calls what the API does not offer, sends a field of the wrong type, or tests mock answers the API never gives), so a call fails or data is lost while every test of each side passes | T, D | 3 | 3 | **High (9)** | Since 2026-11-23: the API's OpenAPI description is committed as `contracts/openapi.json` and kept equal to the API by `OpenApiContractTests.cs`; every request and mocked response of the component tests is checked against it (`contract.ts`). Error responses are in the contract too (ProblemDetails, `ErrorContractTests.cs`); and so are query and path parameters (`contract.test.ts`); not described yet: 5xx — UI_TEST_DESIGN D-1 was such a mismatch | API, Component | `OpenApiContractTests.cs`, `ErrorContractTests.cs`, `doctorSlice.test.ts`, `DoctorForm.test.tsx`, `catalogueErrors.test.tsx`, `contract.test.ts` | Proposed |
| R-062 | A save or delete the API refuses looks like it worked in the web application, so users believe data was saved or removed | T, R | 2 | 4 | **High (8)** | Fixed on 2026-11-23: the API's message reaches the user (`apiError.ts`); the drawer closes only after a successful save, and a refused delete keeps its row (`Doctor.tsx`, `Patient.tsx`, `Service.tsx`) — UI_TEST_DESIGN D-6 | Component | `catalogueErrors.test.tsx` | Proposed |
| R-063 | The web application shows data of a record other than the one opened: the patient details drawer showed the service loaded for the patient opened before (I and L proposed by the implementer, unrated until the test owner rates them) | I | 2 | 3 | **Medium (6)** — proposed | Found on 2026-11-24 by ESLint (rule react-hooks/rules-of-hooks): the hooks were inside an `if` and the store's last service was written into the patient prop. Fixed: `PatientDetails.tsx` shows the store's service only when its id is the patient's | Component | `PatientDetails.test.tsx` | Proposed |
| R-064 | A list that fails to load looks empty in the web application once the error toast is gone, so users believe there are no doctors, patients or services (separate from R-062, Q-013) | T, R | 2 | 3 | **Medium (6)** — rated by the test owner, 2026-11-24 | Status `failed` is not rendered; the page keeps "Total … : 0" and an empty table (`Doctors.tsx:95`, `:117`) — UI_TEST_DESIGN D-23 | Component, E2E | `catalogueErrors.test.tsx` (toast only; the lasting empty state is not tested yet) | Proposed |
| R-065 | The web application shows a value that reads as a different value: a doctor's first name as the last name, services 1 and 2 as "12", ids instead of names (Q-014) | I | 2 | 3 | **Medium (6)** — rated by the test owner, 2026-11-24 | `DoctorDetails.tsx:15`, `Doctors.tsx:60-64`, `Patients.tsx:50-54` — UI_TEST_DESIGN D-8, D-18, D-19 | Component, E2E | – | Proposed |
| R-066 | A stored value cannot be removed through the web application: clearing a patient's email sends `""`, which the API is expected to refuse (not yet sent to a running API), although removal is allowed (Q-012) | T | 2 | 3 | **Medium (6)** — rated by the test owner, 2026-11-24 | `PatientForm.tsx:51-60`, `PatientCreationDto.cs:11` — UI_TEST_DESIGN D-20 | Component, E2E | – | Proposed |
| R-067 | The web application offers actions the signed-in role may not use (a doctor sees "Add a new Doctor"; a patient gets an empty Patients page), so users try them and are refused. The API still refuses them (R-005), so no access is gained. Also covers the reverse (an action the role may use is hidden) and the frontend's role rules drifting from SECURITY_TESTING §2 (Q-022). Expected behaviour decided in Q-015 and Q-017 … Q-021. I and L proposed by the implementer, unrated until the test owner rates them | E | 1 | 4 | **Medium (4)** — proposed | No page reads the user's roles (`PrivateRoutes.tsx:36-42`) — UI_TEST_DESIGN D-7 | Component, E2E | – | Proposed |
| R-068 | A patient sees each doctor's birthday and home address on the Doctors page, which is not intended (Q-023). I and L not proposed yet, unrated until the test owner rates them | I | – | – | unrated | `GET /api/Doctor` returns `birthday` and `address` to every role (SECURITY_TESTING §2); the Doctors table shows both (`Doctors.tsx:50-59`) | API, Component | – | Proposed |

## 3. Assumptions

Things we treat as true without proof yet. Each has an owner and a date to re-check.

| ID | Assumption | Why it matters | Owner | Re-check by | Status |
|---|---|---|---|---|---|
| A-001 | Keycloak puts realm roles in `realm_access.roles`, and the API copies them to `roles` | API test tokens copy this shape; if Keycloak config changes, API tests would pass while production fails. E2E covers the real token. | Test owner | 2027-01-31 | Verified 2026-11-15 by E2E (`ClinicalJourney.feature`, `SignIn.feature`): real Keycloak tokens give each role exactly its access; kept open as a check that runs on every E2E run |
| A-002 | `frontend-web` has no Keycloak login today | E2E uses the API request context until the frontend signs in through Keycloak | Test owner | When frontend auth is added | Closed 2026-11-15: login added in `d96bba1`; browser E2E journeys are possible |
| A-003 | The legacy `AppDesktop/` and `GestionHopitalSQL/` projects are not deployed | They were out of scope and held a hard-coded password (R-030) | Test owner | – | Closed 2026-11-15: both projects deleted in `619ef39` (Q-009) |
| A-004 | Using `DateTime.UtcNow` directly in services is acceptable for now | Tests must use relative dates with margins; a `TimeProvider` would make time rules exact. The appointment lifecycle (R-014) now depends on "has it started", so this matters more. | Test owner | – | Closed 2026-11-15: `TimeProvider` in `e358209`; unit tests fix the clock |
| A-005 | Every Doctor account that writes records is linked to its doctor profile by an admin | Unlinked doctors get 403 on record writes (R-021) | Test owner | 2027-01-31 | Open |
| A-006 | A record being written for a visit and the same visit being marked `NoShow` at the very same moment do not happen in practice | Both requests read "allowed" under Read Committed (`TransactionalWriteFilter.cs:33`, no row lock), so both could commit and leave a record on a `NoShow` visit (R-053). Proof would be a parallel-request API test; a fix would be a row lock on the appointment | Test owner | 2027-01-31 | **Disproved 2026-11-20:** in 20 of 20 parallel attempts both were saved. Fixed in PR #16: record creation and every status change lock the appointment's row first (`Services/Common/AppointmentLocking.cs`), so the second request waits and sees the first; covered by `RecordLinkedCorrectionTests.RecordAndNoShowAtTheSameMoment_NeverBothSucceed` (R-053) |
| A-007 | The API server runs in UTC (the default in the containers) | The DTOs use `DateTime`, so a time sent with an offset arrives converted to the server's local time and is converted back to UTC (R-051). In a server time zone with daylight saving, a time in the repeated autumn hour converts ambiguously. Removing the dependency means `DateTimeOffset` in the DTOs | Test owner | Wave 3 (deployment) | Open |

## 4. Open questions

Behavior the code shows but no document confirms. The test owner answers; the answer decides the expected
result in the test.

| ID | Question | Affects | Answer |
|---|---|---|---|
| Q-001 | Should a user with a valid token but **no hospital role** see doctors, departments and services? | R-015 | **No** — 403 on every endpoint, including `/api/User/me` (2026-11-15). |
| Q-002 | Should a doctor be able to create a medical record with **another doctor's** `DoctorId`? | R-010 | **No** — the author comes from the caller's linked doctor profile (2026-11-15). |
| Q-003 | Should a doctor be able to edit **any** medical record, or only records they authored? | R-011 | **Any record**, but every change must be traceable: full revision history with the editing doctor, readable by doctors and nurses (2026-11-15). |
| Q-004 | Which appointment status transitions are allowed through `PUT /api/Appointment/{id}`? | R-014 | The lifecycle in `docs/API_ENDPOINTS.md` → *Appointment lifecycle* (2026-11-15). |
| Q-005 | Who may correct an appointment that was set to a final status by mistake (e.g. `NoShow`), and does it need a reason? | R-022 | **Admin only, with a mandatory reason**, through a separate endpoint; only NoShow ⇄ Completed and Cancelled → Scheduled before the start; audited (2026-11-15). |
| Q-006 | Where will the system run: own server, a cloud provider (Azure, AWS) or Kubernetes? Which hostnames? | R-025, R-026, R-031 – R-034, R-046 | **Locally only**: on the developer's machine with docker compose; tests run in GitHub Actions, possibly Jenkins later. No server, cloud or Kubernetes deployment is planned, and no real e-mail/SMS staging (2026-11-20). The deployment risks are accepted for local use in §5 and come back if a deployment is ever planned. |
| Q-007 | Will the system hold **real patient data**, or is it a demo / training project? Which law applies to the health data? | R-028, R-041 – R-045 | **Synthetic data only, but everything as close to real life and production as possible**: real-life flows, production-like configuration, health-data security (no leakage), reliability, and no important scenario or edge case missed. Ratings are not lowered for fake data; only the legal paperwork for real patients is out of scope (2026-11-15). See BUSINESS_FLOWS. **Narrowed by Q-006 (2026-11-20):** "production-like" now covers what a local stack can show (flows, data, access rules, leakage, reliability, the real Keycloak sign-in); deployment configuration (TLS, Keycloak production mode, database users, backups) is accepted for local use in §5. |
| Q-008 | One API instance or several? | R-038, R-039 | **One instance**, because the system runs locally only (follows from Q-006, 2026-11-20). Revisit together with Q-006. |
| Q-009 | Should the legacy `AppDesktop/` and `GestionHopitalSQL/` projects be deleted from the repository, moved to an archive repository, or kept? | R-030, A-003 | **Delete** from the repository; git history keeps them (2026-11-15). |
| Q-010 | To which appointments may a medical record be attached, and what happens to a documented visit later? | R-053, R-014, R-022 | Only a **started** `Scheduled`, `Confirmed` or `Completed` appointment (409 otherwise; someone else's or a missing one stays 400). A record without an appointment stays allowed. A documented visit cannot become `NoShow` (`PUT` or correction, 409), and a started appointment cannot be moved (409). The 409 text may say that a record exists (2026-11-16). |
| Q-011 | Records created before the R-053 fix may already be linked to future, cancelled or no-show appointments. Should existing data be checked and reported before R-053 is closed? | R-053 | Open |
| Q-012 | When staff clear a patient's email in the web application and save, should the email be removed? (UI_TEST_DESIGN Q-i) | R-066 | **Yes** — clearing the field removes the email (2026-11-24). |
| Q-013 | Is "a list that fails to load looks empty" part of R-062 (refused saves and deletes) or a risk of its own? (UI_TEST_DESIGN Q-k) | R-062, R-064 | **Its own risk**, R-064, rated Medium (6); R-062 covers saves and deletes only (2026-11-24). |
| Q-014 | Do values that read as other values (D-8, D-18) belong under R-063, and where does D-19 (ids instead of names, an unnamed delete confirmation) belong? (UI_TEST_DESIGN Q-l) | R-063, R-065 | **A new risk**, R-065, rated Medium (6), for D-8, D-18 and D-19; the D-19 test stays rated Low (2026-11-24). |
| Q-015 | When the signed-in role may not use an action, how should the web application show it: hidden, disabled with a reason, or a message after the click? (UI_TEST_DESIGN Q-b) | R-067 | **Hidden**: actions the role may not use are not shown; a page the role may not open at all shows a short explanation instead of an empty table. The API keeps refusing them (2026-11-24). |
| Q-016 | What is a valid patient phone number? (UI_TEST_DESIGN Q-c) | R-044 | **International, stored as text** in E.164 form: `+`, country code, 8 to 15 digits in all (e.g. `+38761123456`) (2026-11-24). |
| Q-017 | The API lets a doctor register and edit patients (SECURITY_TESTING §2), but BUSINESS_FLOWS F-02 says a nurse or an administrator registers them. Should a doctor see Add and Edit on the Patients page? | R-067, R-003, R-069 | **No — follow F-02**: a doctor neither sees nor may use Add and Edit for patients. This changes the API's role matrix (`POST` and `PUT /api/Patient` → 403 for Doctor), not built yet (2026-11-24). API part built for R-069 (2026-11-26). |
| Q-018 | Should a user who is only a patient see the "Patients List" menu item, and should the Patients page ask the API for the list? | R-067 | **Menu item hidden; no list request.** A direct visit to `/patient` shows the explanation, with no "not allowed" message (2026-11-24). |
| Q-019 | What does a patient see on `/patient`? | R-067 | **The explanation replaces the whole page** (heading, search, Add, table): "The patient list is only available to hospital staff." (2026-11-24). |
| Q-020 | What does a user with several roles, or with no recognised role, see? | R-067 | **The union of their roles** (Admin + Patient sees the admin view); **no recognised role: no actions** and the explanation (fail closed) (2026-11-24). |
| Q-021 | Should frontend component tests with a mocked API carry `@R-003`? | R-003, R-067 | **No — only `@R-067`.** They cannot prove the API's control; `@R-003` stays on API and E2E tests (2026-11-24). |
| Q-022 | Are "an allowed action is hidden" and "the frontend's role rules drift from SECURITY_TESTING §2" risks of their own? | R-067 | **No — both are part of R-067**; its tests check that allowed actions stay visible and that the frontend follows the matrix (2026-11-24). |
| Q-023 | Is it intended that a patient sees each doctor's birthday and home address on the Doctors page? | R-068 | **No — hide them from patients.** Not part of R-067; likely an API change as well, not built yet (2026-11-24). |
| Q-024 | Which risk carries the API rule "a doctor may not register or edit patients"? The SECURITY_TESTING §2 row named R-003, which is about reading | R-003, R-069 | **A new risk, R-069**, rated Critical (12); the §2 row names R-069 (2026-11-24). |
| Q-025 | Should BUSINESS_FLOWS F-02 get a scenario "a doctor tries to register or edit a patient"? | R-069 | **Yes, a new scenario F-02.6** (2026-11-24). |
| Q-026 | May an account that holds both Doctor and Nurse register patients? | R-069 | **Yes** — one allowed role is enough, as on every other endpoint (2026-11-24). |
| Q-027 | Is it acceptable that, between the API change and the frontend change (R-067), a doctor still sees Add and Edit on the Patients page and is refused? | R-067, R-069 | **Yes** — nothing is saved, and the frontend change follows (2026-11-24). |
| Q-028 | Should a refused attempt to register or edit a patient be written to the audit log? | R-013, R-069 | **No** — the audit log records changes only, as everywhere today (2026-11-24). |
| Q-029 | Who registers a patient when no nurse or administrator is available? | R-069 | **Nobody** — the patient waits for a nurse or an administrator; no emergency path for doctors (2026-11-24). |
| Q-030 | When a patient opens `/patient` directly, the "Patients List" menu item is hidden (Q-018). Should the page title at the top be empty or name the page? | R-067 | **"Patients List Page"** — the title names the page the user landed on (2026-11-24). |
| Q-031 | Beyond showing no actions (Q-020), should a user with no recognised role get anything else on the Doctors and Services pages (no list request, an explanation, hidden menu items)? | R-067 | **No** — such a user is stopped at sign-in with "No access" (R-015) (2026-11-24). |
| Q-032 | Should BUSINESS_FLOWS gain a flow for keeping the doctor and service lists up to date, and should component-test names carry flow IDs (`@F-xx.y`)? | R-067 | **Later**, in a separate documentation change (2026-11-24). |
| Q-033 | BUSINESS_FLOWS F-02.4 ("a patient tries to register or edit patients") named R-003, which is about reading. Which risk should it name? | R-003, R-069 | **R-069** — who may write patient data (2026-11-24). |
| Q-034 | With F-02.4 under R-069 (Q-033), should R-069 cover any role, staff or patient, and keep its rating? | R-069 | **Yes** — "a role (staff or patient)"; Critical (12) stays; R-069 sits in the main risk table (2026-11-26). |
| Q-035 | Does "one allowed role is enough" (Q-026) also hold for editing a patient? | R-069 | **Yes** — an account with Doctor and Nurse may edit patients too (2026-11-26). |
| Q-036 | R-069 after the API change: does the clause "a later change reuses the `Staff` policy for patient writes" stay, and which status does R-069 get? Should F-02.1 and F-02.4 match the tests? | R-069 | **The clause stays** (I=3, L=4); **status `Covered`**, on the red run and four deliberate breaks; **F-02.1 names R-003 and R-069, F-02.4 expects "403 with an empty body, nothing saved or changed"** (2026-11-26). |

## 5. Accepted risks

Risks the test owner chose not to cover yet. Each has a reason and an end date or condition.

The system runs **locally only** (Q-006, 2026-11-20): one machine, docker compose, synthetic data, no network
users. The risks below exist only once the stack is deployed for others to reach, so they are accepted for
local use. **All of them come back the day a deployment beyond the developer's machine is planned**; wave 3 of
QUALITY_GATES §7 is then the plan. Real patient data stays excluded (Q-007).

| ID | Reason | Accepted by | Until |
|---|---|---|---|
| R-025 | No TLS: locally the traffic never leaves the machine (`localhost`) | Test owner, 2026-11-20 | Any deployment |
| R-026 | Keycloak in development mode with sample users: the local stack needs the sample users for manual testing and E2E | Test owner, 2026-11-20 | Any deployment |
| R-031 | One PostgreSQL superuser for the API and Keycloak: a single local database with synthetic data | Test owner, 2026-11-20 | Any deployment |
| R-032 | No backups or restore drill: synthetic data can be recreated from the seed | Test owner, 2026-11-20 | Any deployment, or real data |
| R-033 | The frontend image runs the Vite development server: locally the frontend runs with `npm run dev`; E2E uses the production build (`vite preview`) | Test owner, 2026-11-20 | Any deployment |
| R-034 | Hostnames, issuer, CORS and redirect URIs fixed to `localhost`: correct for local use | Test owner, 2026-11-20 | Any deployment |
| R-038 | Migrations at start-up: one instance (Q-008) cannot race itself | Test owner, 2026-11-20 | A second instance |
| R-039 | Up to 5 minutes of cached access on other instances: there is one instance (Q-008) | Test owner, 2026-11-20 | A second instance |
| R-047 | No runbooks for deploy, rollback, secret rotation and restore: nothing is deployed | Test owner, 2026-11-20 | Any deployment |

Not accepted, because they also matter locally: R-028 (MFA, password policy), R-036 (security headers, rate
limiting), R-041 (audit store), R-045 (retention, export), R-046 (alerts; the outbox is visible in `/health`),
R-052 (role changes until the token expires).
