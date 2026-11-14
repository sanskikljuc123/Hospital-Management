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

All ratings below are `Proposed` and wait for the test owner. "Evidence" is where the current control is.

| ID | Risk | STRIDE | I | L | Severity | Evidence (current control) | Level | Tests | Status |
|---|---|---|---|---|---|---|---|---|---|
| R-001 | A patient reads another patient's medical record | I | 4 | 3 | **Critical (12)** | `MedicalRecordController.cs:49-51` | API | – | Proposed |
| R-002 | A patient reads or cancels another patient's appointment, or books one for another patient | I, T | 4 | 3 | **Critical (12)** | `AppointmentController.cs:32, 46-47, 55, 74-75` | API | – | Proposed |
| R-003 | A patient reads another patient's profile | I | 4 | 3 | **Critical (12)** | `PatientController.cs:37-39` | API | – | Proposed |
| R-004 | A patient learns which record IDs exist (404 for missing vs 403 for someone else's) | I | 3 | 3 | **High (9)** | `AppointmentController.cs:46, 74`; `MedicalRecordController.cs:49` | API | – | Proposed |
| R-005 | A non-admin calls an admin endpoint (user status, user link, audit log, delete) | E | 4 | 2 | **High (8)** | `Policies.cs:22`; `[Authorize(Policy = AdminOnly)]` on 15 actions and on `AuditLogController` | API | – | Proposed |
| R-006 | A forged, expired, wrong-issuer or wrong-audience token is accepted | S | 4 | 2 | **High (8)** | `AuthenticationExtensions.cs:31-38` | API | – | Proposed |
| R-007 | A deactivated account still gets data | E | 4 | 2 | **High (8)** | `UserSyncMiddleware.cs:17-24` | API | – | Proposed |
| R-008 | An admin links a user to the wrong patient, giving them that patient's records | E, I | 4 | 2 | **High (8)** | `UserController.cs:52-54`; no confirmation or audit check known | API | – | Proposed |
| R-009 | A doctor is double-booked in the same time slot | T | 3 | 3 | **High (9)** | `AppointmentService.cs:157-164` | Unit | – | Proposed |
| R-010 | A doctor creates a medical record attributed to another doctor (`DoctorId` comes from the request body) | S, R | 3 | 3 | **High (9)** | `MedicalRecordService.cs:36, 45` — see Q-002 | API | – | Proposed |
| R-011 | Any doctor can change any medical record, not only their own | T | 3 | 3 | **High (9)** | `MedicalRecordController.cs:65-67`; no author check in `MedicalRecordService.cs:62-81` — see Q-003 | API | – | Proposed |
| R-012 | Secrets are committed to the repository | I | 4 | 2 | **High (8)** | `.env` is git-ignored; no automated scan yet | CI | – | Proposed |
| R-013 | A clinical or access change is not written to the audit log | R | 3 | 2 | Medium (6) | `AuditActions.cs`; `LogAsync` calls in `AppointmentService` and `MedicalRecordService` | Unit | – | Proposed |
| R-014 | An appointment status is changed to an invalid state (e.g. `Completed` → `Scheduled`) through `PUT` | T | 3 | 2 | Medium (6) | `AppointmentService.cs:84-95` checks the slot but not the transition; only cancel checks state (`:106-107`) | Unit | – | Proposed |
| R-015 | A user with a valid token but no hospital role reads doctors, departments and services | I | 2 | 3 | Medium (6) | Class-level `[Authorize]` only on `DoctorController`, `DepartmentController`, `ServiceController` — see Q-001 | API | – | Proposed |
| R-016 | Over-posting on `POST/PUT /api/Service` creates or links unrelated data (entity bound directly, with `Patients` and `DoctorServices` collections) | T, E | 2 | 2 | Medium (4) | `ServiceController.cs:44, 56`; `Service.cs:9-16` | API | – | Proposed |
| R-017 | An error response or log line exposes another patient's data, a stack trace or a token | I | 3 | 2 | Medium (6) | `DbUpdateExceptionHandler.cs`; `app.UseExceptionHandler()` in `Program.cs:47` | API | – | Proposed |
| R-018 | An appointment is booked in the past | T | 2 | 3 | Medium (6) | `AppointmentService.cs:48-49` | Unit | – | Proposed |
| R-019 | Deleting a doctor, patient or department with linked data corrupts or orphans records instead of returning 409 | T | 2 | 2 | Medium (4) | `DbUpdateExceptionHandler.cs`; documented 409 in `docs/API_ENDPOINTS.md` | API | – | Proposed |
| R-020 | An admin can read medical records (they must not: `Admin` is not in the class-level roles) | I | 3 | 1 | Low (3) | `MedicalRecordController.cs:18` | API | – | Proposed |

## 3. Assumptions

Things we treat as true without proof yet. Each has an owner and a date to re-check.

| ID | Assumption | Why it matters | Owner | Re-check by | Status |
|---|---|---|---|---|---|
| A-001 | Keycloak puts realm roles in `realm_access.roles`, and the API copies them to `roles` | API test tokens copy this shape; if Keycloak config changes, API tests would pass while production fails. E2E covers the real token. | Test owner | 2027-01-31 | Open |
| A-002 | `frontend-web` has no Keycloak login today | E2E uses the API request context until the frontend signs in through Keycloak | Test owner | When frontend auth is added | Open |
| A-003 | The legacy `AppDesktop/` and `GestionHopitalSQL/` projects are not deployed | They are out of scope; if deployed, their SQL Server access is untested | Test owner | 2027-01-31 | Open |
| A-004 | Using `DateTime.UtcNow` directly in services is acceptable for now | Tests must use relative dates with margins; a `TimeProvider` would make time rules exact | Test owner | Phase 3 | Open |

## 4. Open questions

Behavior the code shows but no document confirms. The test owner answers; the answer decides the expected
result in the test.

| ID | Question | Affects |
|---|---|---|
| Q-001 | Should a user with a valid token but **no hospital role** see doctors, departments and services? `docs/API_ENDPOINTS.md` says "Any" (any authenticated user). | R-015 |
| Q-002 | Should a doctor be able to create a medical record with **another doctor's** `DoctorId`, or should the API take the doctor from the token? | R-010 |
| Q-003 | Should a doctor be able to edit **any** medical record, or only records they authored? | R-011 |
| Q-004 | Which appointment status transitions are allowed through `PUT /api/Appointment/{id}`? | R-014 |

## 5. Accepted risks

High risks the test owner chose not to cover yet. Each has a reason and an end date.

| ID | Reason | Accepted by | Until |
|---|---|---|---|
| – | – | – | – |
