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

| ID | Risk | STRIDE | I | L | Severity | Evidence (current control) | Level | Tests | Status |
|---|---|---|---|---|---|---|---|---|---|
| R-001 | A patient reads another patient's medical record | I | 4 | 3 | **Critical (12)** | `MedicalRecordController.cs:49-51` | API | – | Proposed |
| R-002 | A patient reads or cancels another patient's appointment, or books one for another patient | I, T | 4 | 3 | **Critical (12)** | `AppointmentController.cs:32, 46-47, 55, 74-75` | API | – | Proposed |
| R-003 | A patient reads another patient's profile | I | 4 | 3 | **Critical (12)** | `PatientController.cs:37-39` | API | – | Proposed |
| R-004 | A patient learns which record IDs exist (404 for missing vs 403 for someone else's) | I | 3 | 3 | **High (9)** | `AppointmentController.cs:46, 74`; `MedicalRecordController.cs:49` | API | – | Proposed |
| R-005 | A non-admin calls an admin endpoint (user status, user link, audit log, delete) | E | 4 | 2 | **High (8)** | `Policies.cs:29`; `[Authorize(Policy = AdminOnly)]` on 15 actions and on `AuditLogController` | API | – | Proposed |
| R-006 | A forged, expired, wrong-issuer or wrong-audience token is accepted | S | 4 | 2 | **High (8)** | `AuthenticationExtensions.cs:31-38` | API | – | Proposed |
| R-007 | A deactivated account still gets data | E | 4 | 2 | **High (8)** | `UserSyncMiddleware.cs:17-24` | API | – | Proposed |
| R-008 | An admin links a user to the wrong patient, giving them that patient's records | E, I | 4 | 2 | **High (8)** | `UserController.cs:52-54`; no confirmation or audit check known | API | – | Proposed |
| R-009 | A doctor is double-booked in the same time slot | T | 3 | 3 | **High (9)** | `AppointmentService.cs:253-264` | Unit | – | Proposed |
| R-010 | A doctor creates a medical record attributed to another doctor, or attaches it to another doctor's appointment | S, R | 3 | 3 | **High (9)** | Fixed in `7417a9e`: author from the caller's linked doctor (`MedicalRecordController.cs:60`); appointment must match patient and doctor (`MedicalRecordService.cs:69`) — Q-002 | API | – | Accepted |
| R-011 | A change to a medical record cannot be traced to the doctor who made it, or the previous content is lost | T, R | 3 | 3 | **High (9)** | Fixed in `871484e`: every change stored as a revision with editor, in the same transaction (`MedicalRecordService.cs:114-121`); history at `GET /api/MedicalRecord/{id}/history` (`MedicalRecordController.cs:83-88`) — Q-003 | API | – | Accepted |
| R-012 | Secrets are committed to the repository | I | 4 | 2 | **High (8)** | `.env` is git-ignored; no automated scan yet | CI | – | Proposed |
| R-013 | A clinical or access change is not written to the audit log | R | 3 | 2 | Medium (6) | `AuditActions.cs`; `LogAsync` calls in `AppointmentService` and `MedicalRecordService` | Unit | – | Proposed |
| R-014 | An appointment is put into an invalid state (e.g. `Completed` → `Scheduled`, completed before it starts, moved into the past) | T | 3 | 2 | Medium (6) | Fixed in `8f7079e`: lifecycle in `AppointmentService.cs:92` (final states), `:132-150` (transitions), `:119` (moved → Scheduled), `:203-205` (cancel) — Q-004; rules in `docs/API_ENDPOINTS.md` | Unit | – | Accepted |
| R-015 | A user with a valid token but no hospital role gets any data | I, E | 2 | 3 | Medium (6) | Fixed in `cd15a5e`: `DefaultPolicy` and `FallbackPolicy` require a hospital role (`Policies.cs:22-27`); frontend shows "No access" (`AuthGate.tsx`) — Q-001 | API | – | Accepted |
| R-016 | Over-posting on `POST/PUT /api/Service` creates or links unrelated data (entity bound directly, with `Patients` and `DoctorServices` collections) | T, E | 2 | 2 | Medium (4) | `ServiceController.cs:44, 56`; `Service.cs:9-16` | API | – | Proposed |
| R-017 | An error response or log line exposes another patient's data, a stack trace or a token | I | 3 | 2 | Medium (6) | `DbUpdateExceptionHandler.cs`; `app.UseExceptionHandler()` in `Program.cs:47` | API | – | Proposed |
| R-018 | An appointment is booked or moved into the past | T | 2 | 3 | Medium (6) | `AppointmentService.cs:48-49, 108-109` | Unit | – | Proposed |
| R-019 | Deleting a doctor, patient or department with linked data corrupts or orphans records instead of returning 409 | T | 2 | 2 | Medium (4) | `DbUpdateExceptionHandler.cs`; documented 409 in `docs/API_ENDPOINTS.md` | API | – | Proposed |
| R-020 | An admin can read medical records or their history (they must not: `Admin` is not in the class-level roles) | I | 3 | 1 | Low (3) | `MedicalRecordController.cs:18, 84` | API | – | Proposed |
| R-021 | A doctor whose account is not linked to a doctor profile cannot write records (blocked at work) | D | 2 | 3 | Medium (6) | 403 by design (`MedicalRecordController.cs:60-62, 75-77`); depends on admins linking accounts — A-005 | API | – | Proposed |
| R-022 | A final status set by mistake cannot be corrected, or a correction is misused to rewrite history | T, R | 2 | 2 | Medium (4) | Fixed in `4af797a`: admin-only `POST /api/Appointment/{id}/correct-status` (`AppointmentController.cs:81`) with a mandatory reason; only NoShow ⇄ Completed and Cancelled → Scheduled before the start (`AppointmentService.cs:154-194`); audited as `Appointment.StatusCorrected` — Q-005 | Unit | – | Accepted |
| R-023 | Script injection (XSS) in `frontend-web` steals the access token | I, E | 4 | 1 | Medium (4) | Tokens are kept in memory by keycloak-js, not in `localStorage` (`frontend-web/src/auth/keycloak.ts`); React escapes output; no CSP header yet | E2E | – | Proposed |
| R-024 | A user with no hospital role still gets a local profile and a login audit entry | R | 1 | 3 | Low (3) | `UserSyncMiddleware` runs before authorization (`Program.cs:57-58`) | API | – | Proposed |

## 3. Assumptions

Things we treat as true without proof yet. Each has an owner and a date to re-check.

| ID | Assumption | Why it matters | Owner | Re-check by | Status |
|---|---|---|---|---|---|
| A-001 | Keycloak puts realm roles in `realm_access.roles`, and the API copies them to `roles` | API test tokens copy this shape; if Keycloak config changes, API tests would pass while production fails. E2E covers the real token. | Test owner | 2027-01-31 | Open |
| A-002 | `frontend-web` has no Keycloak login today | E2E uses the API request context until the frontend signs in through Keycloak | Test owner | When frontend auth is added | Closed 2026-11-15: login added in `d96bba1`; browser E2E journeys are possible |
| A-003 | The legacy `AppDesktop/` and `GestionHopitalSQL/` projects are not deployed | They are out of scope; if deployed, their SQL Server access is untested | Test owner | 2027-01-31 | Open |
| A-004 | Using `DateTime.UtcNow` directly in services is acceptable for now | Tests must use relative dates with margins; a `TimeProvider` would make time rules exact. The appointment lifecycle (R-014) now depends on "has it started", so this matters more. | Test owner | Phase 3 | Open |
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

## 5. Accepted risks

High risks the test owner chose not to cover yet. Each has a reason and an end date.

| ID | Reason | Accepted by | Until |
|---|---|---|---|
| – | – | – | – |
