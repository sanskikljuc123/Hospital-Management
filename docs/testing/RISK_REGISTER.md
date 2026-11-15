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
No row is `Covered` yet: the test projects arrive in phases 2–4. "Evidence" is where the current control is.

**Ratings assume real patient data.** The system holds only synthetic data, but it is built and tested as if
it were in production with real patients (Q-007, [BUSINESS_FLOWS.md](BUSINESS_FLOWS.md#operating-principle)),
so no rating is lowered because the data is fake.

| ID | Risk | STRIDE | I | L | Severity | Evidence (current control) | Level | Tests | Status |
|---|---|---|---|---|---|---|---|---|---|
| R-001 | A patient reads another patient's medical record | I | 4 | 3 | **Critical (12)** | `IPatientAccessPolicy` (`api/Auth/PatientAccessPolicy.cs`), scope `ClinicalStaff`, used by `MedicalRecordController`; controllers may not decide ownership themselves (architecture test) | API | `BolaTests`, `PatientAccessPolicyTests`, `ArchitectureRulesTests` | Proposed |
| R-002 | A patient reads or cancels another patient's appointment, or books one for another patient | I, T | 4 | 3 | **Critical (12)** | `IPatientAccessPolicy`, scope `Staff`, used by `AppointmentController` for list, read, book and cancel | API | `BolaTests`, `PatientAccessPolicyTests`, `ArchitectureRulesTests` | Proposed |
| R-003 | A patient reads another patient's profile | I | 4 | 3 | **Critical (12)** | `IPatientAccessPolicy`, scope `Staff`, used by `PatientController` | API | `BolaTests`, `PatientAccessPolicyTests`, `ArchitectureRulesTests` | Proposed |
| R-004 | A patient learns which record IDs exist (404 for missing vs 403 for someone else's) | I | 3 | 3 | **High (9)** | `PatientAccessPolicy.CheckAsync`: a missing resource is `NotFound` for staff and `Forbidden` for everyone else | API | `BolaTests`, `PatientAccessPolicyTests` | Proposed |
| R-005 | A non-admin calls an admin endpoint (user status, user link, audit log, delete) | E | 4 | 2 | **High (8)** | `Policies.cs:29`; `[Authorize(Policy = AdminOnly)]` on 15 actions and on `AuditLogController` | API | `AuthorizationMatrixTests` | Proposed |
| R-006 | A forged, expired, wrong-issuer or wrong-audience token is accepted | S | 4 | 2 | **High (8)** | `AuthenticationExtensions.cs:31-38` | API | `TokenValidationTests` | Proposed |
| R-007 | A deactivated account still gets data | E | 4 | 2 | **High (8)** | `UserSyncMiddleware.cs:17-24` | API | `AccountStateTests` | Proposed |
| R-008 | An admin links a user to the wrong patient, giving them that patient's records | E, I | 4 | 2 | **High (8)** | `UserService.LinkAsync`: a record already linked to another user is refused (409), a new link replaces the old one, every link is audited (`User.Linked`). Still no confirmation step against linking the wrong patient | API | `UserServiceTests` | Proposed |
| R-009 | A doctor is double-booked in the same time slot | T | 3 | 3 | **High (9)** | `AppointmentService.cs:253-264` | Unit | `AppointmentRulesTests` | Proposed |
| R-010 | A doctor creates a medical record attributed to another doctor, or attaches it to another doctor's appointment | S, R | 3 | 3 | **High (9)** | Fixed in `7417a9e`: author from the caller's linked doctor (`MedicalRecordController.cs:60`); appointment must match patient and doctor (`MedicalRecordService.cs:69`) — Q-002 | API | `RecordAuthorTests` | Accepted |
| R-011 | A change to a medical record cannot be traced to the doctor who made it, or the previous content is lost | T, R | 3 | 3 | **High (9)** | Fixed in `871484e`: every change stored as a revision with editor, in the same transaction (`MedicalRecordService.cs:114-121`); history at `GET /api/MedicalRecord/{id}/history` (`MedicalRecordController.cs:83-88`) — Q-003 | API | `RecordAuthorTests` | Accepted |
| R-012 | Secrets are committed to the repository | I | 4 | 2 | **High (8)** | `.env` is git-ignored; no automated scan yet | CI | – | Proposed |
| R-013 | A clinical or access change is not written to the audit log | R | 3 | 2 | Medium (6) | `AuditActions.cs`; `LogAsync` calls in every service that changes data (appointments, records, users, patients, doctors, services, departments) | Unit | `AppointmentRulesTests`, `CatalogTests` | Proposed |
| R-014 | An appointment is put into an invalid state (e.g. `Completed` → `Scheduled`, completed before it starts, moved into the past) | T | 3 | 2 | Medium (6) | Fixed in `8f7079e`: lifecycle in `AppointmentService.cs:92` (final states), `:132-150` (transitions), `:119` (moved → Scheduled), `:203-205` (cancel) — Q-004; rules in `docs/API_ENDPOINTS.md` | Unit | `AppointmentRulesTests` | Accepted |
| R-015 | A user with a valid token but no hospital role gets any data | I, E | 2 | 3 | Medium (6) | Fixed in `cd15a5e`: `DefaultPolicy` and `FallbackPolicy` require a hospital role (`Policies.cs:22-27`); frontend shows "No access" (`AuthGate.tsx`) — Q-001 | API | `AuthorizationMatrixTests` | Accepted |
| R-016 | Over-posting on `POST/PUT /api/Service` creates or links unrelated data (entity bound directly, with `Patients` and `DoctorServices` collections) | T, E | 2 | 2 | Medium (4) | Fixed in C-2 refactor: `ServiceController` binds `ServiceDto`, only documented fields are copied (`ServiceServiceImp.cs`) | API | `CatalogTests` | Proposed |
| R-017 | An error response or log line exposes another patient's data, a stack trace or a token | I | 3 | 2 | Medium (6) | `DbUpdateExceptionHandler.cs`; `app.UseExceptionHandler()` in `Program.cs:47` | API | – | Proposed |
| R-018 | An appointment is booked or moved into the past | T | 2 | 3 | Medium (6) | `AppointmentService.cs:48-49, 108-109` | Unit | `AppointmentRulesTests` | Proposed |
| R-019 | Deleting a doctor, patient or department with linked data corrupts or orphans records instead of returning 409 | T | 2 | 2 | Medium (4) | `DbUpdateExceptionHandler.cs`; documented 409 in `docs/API_ENDPOINTS.md` | API | `CatalogTests` | Proposed |
| R-020 | An admin can read medical records or their history (they must not: `Admin` is not in the class-level roles) | I | 3 | 1 | Low (3) | `MedicalRecordController.cs:18, 84` | API | `AuthorizationMatrixTests`, `RecordAuthorTests` | Proposed |
| R-021 | A doctor whose account is not linked to a doctor profile cannot write records (blocked at work) | D | 2 | 3 | Medium (6) | 403 by design (`MedicalRecordController.cs:60-62, 75-77`); depends on admins linking accounts — A-005 | API | `RecordAuthorTests` | Proposed |
| R-022 | A final status set by mistake cannot be corrected, or a correction is misused to rewrite history | T, R | 2 | 2 | Medium (4) | Fixed in `4af797a`: admin-only `POST /api/Appointment/{id}/correct-status` (`AppointmentController.cs:81`) with a mandatory reason; only NoShow ⇄ Completed and Cancelled → Scheduled before the start (`AppointmentService.cs:154-194`); audited as `Appointment.StatusCorrected` — Q-005 | Unit | `AppointmentRulesTests` | Accepted |
| R-023 | Script injection (XSS) in `frontend-web` steals the access token | I, E | 4 | 1 | Medium (4) | Tokens are kept in memory by keycloak-js, not in `localStorage` (`frontend-web/src/auth/keycloak.ts`); React escapes output; no CSP header yet | E2E | – | Proposed |
| R-024 | A user with no hospital role still gets a local profile and a login audit entry | R | 1 | 3 | Low (3) | `UserSyncMiddleware` runs before authorization (`Program.cs:57-58`) | API | – | Proposed |

### 2.1 Production readiness

Gaps found in the production-readiness review of 2026-11-15. They are about deployment, configuration and
operations rather than code behavior, so two more levels are used: **Config** — proven by an automated check
of the deployment configuration in CI; **Ops** — proven by a documented and rehearsed procedure (restore
drill, failover). The order in which they are fixed is in [QUALITY_GATES.md](QUALITY_GATES.md#7-production-readiness).

| ID | Risk | STRIDE | I | L | Severity | Evidence (current state) | Level | Tests | Status |
|---|---|---|---|---|---|---|---|---|---|
| R-025 | Tokens and medical data travel unencrypted: no TLS on Keycloak, the API or the frontend | I, S | 4 | 3 | **Critical (12)** | `docker-compose.yml:37` (`start-dev`), `:49` (`KC_HTTP_ENABLED`), `:83` (`RequireHttpsMetadata: false`); API listens on HTTP 8080 (`backend/api/Dockerfile`) | Config | – | Proposed |
| R-026 | Keycloak runs in development mode and imports the sample users with one shared password | S, E | 4 | 3 | **Critical (12)** | `docker-compose.yml:37`, `:52`; realm users in `keycloak/realms/HospitalRealm-realm.json` | Config | – | Proposed |
| R-027 | The password grant on the public `hospital-web` client lets anyone script password guessing outside the login page | S | 3 | 3 | **High (9)** | Fixed in `942cd37`: `directAccessGrantsEnabled: false` on `hospital-web` in the realm file; no client in the realm allows the password grant | Config | – | Accepted |
| R-028 | Staff with access to medical data sign in with a password only: no MFA, no password policy | S | 4 | 2 | **High (8)** | realm: no `passwordPolicy`, no OTP required action | Config | – | Proposed |
| R-029 | The compose file marked `Production` seeds sample data and exposes Swagger | I, E | 3 | 3 | **High (9)** | Fixed in `d7a7f4d`: off in `docker-compose.yml`; on only in `docker-compose.override.yml` for local development | Config | – | Accepted |
| R-030 | A hard-coded SQL Server `sa` password and 177 build-output files (`bin/`, `obj/`, `.vs/`) are in the public repository | I | 3 | 3 | **High (9)** | Fixed in `619ef39`: legacy projects deleted, `bin/`, `obj/`, `.vs/` ignored. The password remains in git history — rotate it where still used; gitleaks in CI (wave 2) keeps new secrets out — see R-012 | CI | – | Accepted |
| R-031 | The API and Keycloak use the same PostgreSQL superuser, so a flaw in either reaches all data | E, T | 4 | 2 | **High (8)** | `docker-compose.yml:18, 43, 80` | Config | – | Proposed |
| R-032 | Data is lost for good: no backup, no tested restore, no RPO/RTO | D | 4 | 2 | **High (8)** | only the `postgres-data` volume (`docker-compose.yml:22`) | Ops | – | Proposed |
| R-033 | The frontend image runs the Vite development server and is not part of the deployed stack | D, I | 3 | 3 | **High (9)** | `frontend-web/Dockerfile` (`CMD ["npm", "run", "dev"]`); not in root `docker-compose.yml` | Config | – | Proposed |
| R-034 | Hostnames, issuer, CORS and redirect URIs are fixed to `localhost`, so a deployment fails or is patched by hand | D | 2 | 4 | **High (8)** | `docker-compose.yml:47, 82, 87`; realm `redirectUris` — Q-006 | Config | – | Proposed |
| R-035 | No automated tests and no CI: any regression of R-001 … R-024 reaches production unseen | T, E | 4 | 3 | **Critical (12)** | no `.github/`, no test projects | CI | – | Proposed |
| R-036 | No security headers (HSTS, CSP, `X-Content-Type-Options`) and no rate limiting | I, D | 3 | 2 | Medium (6) | `Program.cs` has neither `UseHsts` nor a rate limiter — see R-023 | API | – | Proposed |
| R-037 | List endpoints return every row (no paging): slow responses, large data exposure per call | D, I | 2 | 3 | Medium (6) | `AppointmentService.cs:39`, `DoctorServiceImp.cs:24` | API | – | Proposed |
| R-038 | Migrations and seeding run at start-up; several API instances race each other | T, D | 3 | 2 | Medium (6) | `docker-compose.yml:84-85`; `DatabaseInitializer.cs` — Q-008 | Ops | – | Proposed |
| R-039 | A deactivated user keeps access on other API instances for up to 5 minutes | E | 3 | 2 | Medium (6) | `UserService.cs:14` (`SyncCacheDuration`) — Q-008 | API | – | Proposed |
| R-040 | Floating image tags and no dependency or image vulnerability scanning | T | 3 | 2 | Medium (6) | `postgres:17-alpine`, `keycloak:26.4` (`docker-compose.yml:16, 35`), `node:alpine` (`frontend-web/Dockerfile`) | CI | – | Proposed |
| R-041 | The audit trail can be altered or lost: same database and user as the app, no retention; Keycloak keeps events 30 days | R | 3 | 2 | Medium (6) | Lost: fixed in C-4 — the audit entry is written in the same transaction as the change and fails closed (`TransactionalWriteFilter`, `AuditService.cs`). Still open: same database and user as the app, no retention; realm `eventsExpiration: 2592000` | Ops | `TransactionalWriteTests` | Proposed |
| R-042 | Personal data in logs (email recipients), and email/SMS notifications are never actually sent | I | 2 | 3 | Medium (6) | `LoggingIntegrations.cs:18` logs the e-mail recipient (placeholder provider). Notifications now go through the outbox (C-4): delivered after the change, retried, payload without personal data | Unit | `OutboxTests`, `CatalogTests` | Proposed |
| R-043 | "Forgot password" is offered but no SMTP server is configured, so it cannot work | D | 2 | 3 | Medium (6) | Fixed in `942cd37`: `resetPasswordAllowed: false` in the realm file until SMTP is configured | Config | – | Accepted |
| R-044 | The patient model cannot hold realistic patients (phone is a number, no name or date of birth) and deletion is permanent, so real-life flows cannot be exercised | T | 3 | 3 | **High (9)** | `Patient.cs:8-12`; `PatientServiceImp.cs:107` — Q-007, BUSINESS_FLOWS F-02 | Unit | – | Proposed |
| R-045 | No data retention, patient data export or consent mechanisms (the legal paperwork is out of scope with synthetic data; the mechanisms are not) | I, R | 3 | 2 | Medium (6) | no document — Q-007 | Ops | – | Proposed |
| R-046 | Problems in production go unnoticed: telemetry is exported only if configured, no alerts, no central logs | R, D | 3 | 2 | Medium (6) | `HospitalManagement.ServiceDefaults/Extensions.cs:60-61` | Ops | – | Proposed |
| R-047 | No runbooks for deploy, rollback, secret rotation and restore | D | 2 | 2 | Medium (4) | no document | Ops | – | Proposed |

### 2.2 Business flows

Gaps found while writing the real-life flows in [BUSINESS_FLOWS.md](BUSINESS_FLOWS.md) on 2026-11-15. The
flow scenario that exposes each one is named in the evidence column.

| ID | Risk | STRIDE | I | L | Severity | Evidence (current state) | Level | Tests | Status |
|---|---|---|---|---|---|---|---|---|---|
| R-048 | Two bookings for the same doctor and slot at the same moment both succeed | T | 3 | 2 | Medium (6) | Check-then-insert in `AppointmentService.cs:253-264`; the index on `(DoctorId, ScheduledAt)` is not unique and cannot express overlap (`ApplicationDbContext.cs:98`) — F-03.9 | API | – | Proposed |
| R-049 | The same person is registered twice, splitting their history across two patient records | T | 3 | 3 | **High (9)** | No duplicate check in `PatientServiceImp.cs` — F-02.3 | Unit | – | Proposed |
| R-050 | Appointments nobody closes stay `Scheduled`/`Confirmed` forever | T | 2 | 4 | **High (8)** | No job or rule closes past appointments — F-07.4 | Unit | – | Proposed |
| R-051 | A local time sent without an offset is stored as if it were UTC, shifting the appointment by the time-zone difference | T | 3 | 3 | **High (9)** | `AppointmentService.cs:269` (`Unspecified` → `Utc`) — F-03.10 | API | `KnownGapTests` (known gap, skipped) | Proposed |
| R-052 | A role removed or changed in Keycloak keeps working until the access token expires | E | 3 | 2 | Medium (6) | `accessTokenLifespan: 900` in the realm file; roles read only from the token — F-01.7 | E2E | – | Proposed |
| R-053 | A medical record can be attached to an appointment that never took place (future, cancelled, no-show) | T | 3 | 2 | Medium (6) | `MedicalRecordService.cs:69` checks patient and doctor, not status — F-08.4 | API | – | Proposed |
| R-054 | Doctor and patient endpoints return database entities, exposing internal fields such as account links (`userId`) | I | 3 | 3 | **High (9)** | Fixed in C-2 refactor: doctor, patient and service endpoints return `DoctorDto`, `PatientDto`, `ServiceDto` (no account links or navigation data) — F-10.4 | API | `CatalogTests` | Proposed |
| R-055 | A deactivated doctor keeps future appointments; patients are not told | T, D | 3 | 2 | Medium (6) | Deactivation does not touch appointments (`UserService.cs`) — F-12.2 | Unit | – | Proposed |
| R-056 | Two doctors amending the same record: the later save silently overwrites the earlier change | T | 3 | 2 | Medium (6) | No version check on `PUT /api/MedicalRecord/{id}` (`MedicalRecordService.cs:114-121`); history keeps both — F-09.3 | API | – | Proposed |
| R-057 | Deleting a service silently deletes every patient registered to it (the foreign key cascaded) | T, D | 4 | 2 | **High (8)** | Fixed in C-2 refactor: foreign key `Restrict` (migration `PatientServiceRestrictDelete`), and `DELETE /api/Service/{id}` returns 409 while patients or doctors use it (`ServiceServiceImp.cs`) | API | `CatalogTests` | Proposed |

## 3. Assumptions

Things we treat as true without proof yet. Each has an owner and a date to re-check.

| ID | Assumption | Why it matters | Owner | Re-check by | Status |
|---|---|---|---|---|---|
| A-001 | Keycloak puts realm roles in `realm_access.roles`, and the API copies them to `roles` | API test tokens copy this shape; if Keycloak config changes, API tests would pass while production fails. E2E covers the real token. | Test owner | 2027-01-31 | Verified 2026-11-15 by E2E (`ClinicalJourney.feature`, `SignIn.feature`): real Keycloak tokens give each role exactly its access; kept open as a check that runs on every E2E run |
| A-002 | `frontend-web` has no Keycloak login today | E2E uses the API request context until the frontend signs in through Keycloak | Test owner | When frontend auth is added | Closed 2026-11-15: login added in `d96bba1`; browser E2E journeys are possible |
| A-003 | The legacy `AppDesktop/` and `GestionHopitalSQL/` projects are not deployed | They were out of scope and held a hard-coded password (R-030) | Test owner | – | Closed 2026-11-15: both projects deleted in `619ef39` (Q-009) |
| A-004 | Using `DateTime.UtcNow` directly in services is acceptable for now | Tests must use relative dates with margins; a `TimeProvider` would make time rules exact. The appointment lifecycle (R-014) now depends on "has it started", so this matters more. | Test owner | – | Closed 2026-11-15: `TimeProvider` in `e358209`; unit tests fix the clock |
| A-005 | Every Doctor account that writes records is linked to its doctor profile by an admin | Unlinked doctors get 403 on record writes (R-021) | Test owner | 2027-01-31 | Open |

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
| Q-006 | Where will the system run: own server, a cloud provider (Azure, AWS) or Kubernetes? Which hostnames? | R-025, R-026, R-031 – R-034, R-046 | Open |
| Q-007 | Will the system hold **real patient data**, or is it a demo / training project? Which law applies to the health data? | R-028, R-041 – R-045 | **Synthetic data only, but everything as close to real life and production as possible**: real-life flows, production-like configuration, health-data security (no leakage), reliability, and no important scenario or edge case missed. Ratings are not lowered for fake data; only the legal paperwork for real patients is out of scope (2026-11-15). See BUSINESS_FLOWS. |
| Q-008 | One API instance or several? | R-038, R-039 | Open |
| Q-009 | Should the legacy `AppDesktop/` and `GestionHopitalSQL/` projects be deleted from the repository, moved to an archive repository, or kept? | R-030, A-003 | **Delete** from the repository; git history keeps them (2026-11-15). |

## 5. Accepted risks

High risks the test owner chose not to cover yet. Each has a reason and an end date.

| ID | Reason | Accepted by | Until |
|---|---|---|---|
| – | – | – | – |
