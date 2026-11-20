# Business Flows and Scenarios

The real-life journeys through the hospital, each with the edge cases and negative scenarios that must not
be missed. This is the source for E2E scenarios (phase 4) and for the "what could go wrong" step of every
change. Risks are in [RISK_REGISTER.md](RISK_REGISTER.md); access rules in
[SECURITY_TESTING.md](SECURITY_TESTING.md).

## Operating principle

**Production-like, with synthetic data** (decision of 2026-11-15, Q-007):

- The system is built, configured and tested **as if it held real patients**: same flows, same security,
  same reliability expectations, same data volumes and variety.
- **All data is synthetic.** No real person can ever be identified: identifiers use formats that cannot
  belong to a real person (§4).
- **Health data rules apply as if the data were real**: no leakage through responses, errors, logs or
  telemetry; least privilege; every change to clinical data is traceable. Only the legal paperwork that
  exists solely for real patients (DPIA, data protection officer, processor contracts) is out of scope.

## 1. Actors

| Actor | Role | Linked to |
|---|---|---|
| Administrator | `Admin` | – (no clinical access) |
| Doctor | `Doctor` | A doctor profile (required to write records) |
| Nurse | `Nurse` | – |
| Patient | `Patient` | A patient record (required to see own data) |
| Unprivileged user | none | – (valid Keycloak account without a hospital role) |

## 2. How to read a flow

Each flow lists its steps, where it can be done today (**API** / **UI** in `frontend-web`), and its
scenarios. Scenario IDs (`F-04.3`) are used in test names and Gherkin tags (`@F-04.3`). The **Expected**
column is the required behavior. A row marked **Gap** describes a scenario the system does not handle yet;
it points to the risk that tracks it.

## 3. Flows

### F-01 Staff and patient account onboarding

Steps: an administrator creates the Keycloak account and role → the person signs in for the first time and
the API creates a local profile → the administrator links the account to a doctor or patient record.
API ✅ · UI ❌ (accounts in the Keycloak console, linking only through Swagger)

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-01.1 | First sign-in | Local profile created, `User.Provisioned` and `User.Login` audited | R-013 |
| F-01.2 | Doctor signs in before being linked, tries to write a record | 403 "not linked to a doctor profile", nothing written | R-021 |
| F-01.3 | Patient signs in before being linked | Empty lists; 403 on any ID | R-001 – R-004 |
| F-01.4 | Link to a doctor or patient record already linked to another account | 409, existing link unchanged | R-008 |
| F-01.5 | Link the wrong patient by mistake, then correct it | Link replaced; both changes audited (`User.Linked`) | R-008 |
| F-01.6 | Account without a hospital role signs in | UI shows "No access"; every `/api/*` call 403 | R-015, R-024 |
| F-01.7 | Role removed or changed in Keycloak while the person is signed in | **Gap:** old role works until the access token expires (15 min) | R-052 |

### F-02 Patient registration

Steps: a nurse or administrator registers the patient → optionally links an account (F-01).
API ✅ · UI ✅ (patients list and form)

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-02.1 | Register a new patient | 201, patient visible to staff only | R-003 |
| F-02.2 | Required data missing or invalid | 400 with field errors, nothing saved | – |
| F-02.3 | Same person registered twice | **Gap:** no duplicate detection; two records split the history | R-049 |
| F-02.4 | Patient tries to register or edit patients | 403 | R-003 |
| F-02.5 | Delete a patient who has appointments or records | 409, nothing deleted | R-019 |

### F-03 Booking an appointment

Steps: patient (for self) or staff (for any patient) picks doctor, service, time and duration.
API ✅ · UI ❌

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-03.1 | Book a free future slot | 201, status `Scheduled`, audited, patient notified | R-009 |
| F-03.2 | Slot in the past, or exactly now | 400 | R-018 |
| F-03.3 | Doctor already busy, overlapping slot | 409 | R-009 |
| F-03.4 | Back-to-back slots (09:00–09:30 and 09:30–10:00) | Both accepted (touching is not overlapping) | R-009 |
| F-03.5 | Overlap with a **cancelled** appointment | Accepted (cancelled slots are free) | R-009 |
| F-03.6 | Duration below 5 or above 480 minutes | 400 | – |
| F-03.7 | Unknown doctor or service | 400 | – |
| F-03.8 | Patient books for another patient | 403, nothing created | R-002 |
| F-03.9 | Two or more bookings for the same or overlapping slot of one doctor at the same moment | Exactly one is saved (201); every other gets 409 and nothing is saved. Decided by the database, not by timing | R-048 |
| F-03.10 | Time sent without a time zone, or across a daylight-saving change | Stored as UTC exactly as sent; **Gap:** a local time without offset is treated as UTC | R-051 |

### F-04 Confirmation

Steps: staff confirm a scheduled appointment. API ✅ · UI ❌

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-04.1 | Confirm a scheduled appointment | 200, `Confirmed` | R-014 |
| F-04.2 | Patient tries to confirm | 403 (patients cannot `PUT`) | R-014 |
| F-04.3 | Confirm a final appointment | 409 | R-014 |

### F-05 Rescheduling

Steps: staff change time, doctor, service or duration. API ✅ · UI ❌

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-05.1 | Move a confirmed appointment to a free future slot | 200, back to `Scheduled` | R-014 |
| F-05.2 | Move into the past | 400 | R-018 |
| F-05.3 | Move onto a busy slot of the new doctor | 409 | R-009 |
| F-05.4 | Move and set a final status in one request | 409 | R-014 |
| F-05.5 | Move a final appointment | 409 | R-014 |
| F-05.6 | Patient tries to reschedule | 403 | R-014 |
| F-05.7 | Move an appointment (time, doctor, service or duration) at or after its start | 409, unchanged; close it with `Completed` / `NoShow` instead. Otherwise a documented visit could be moved back into the future and cancelled | R-014, R-053 |

### F-06 Cancellation

Steps: patient (own) or staff cancel before the start. API ✅ · UI ❌

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-06.1 | Cancel before the start | 200, `Cancelled`, audited, slot free again | R-014 |
| F-06.2 | Cancel after the start | 409 (use `Completed` / `NoShow`) | R-014 |
| F-06.3 | Cancel twice | 409 the second time | R-014 |
| F-06.4 | Patient cancels another patient's appointment, or an unknown ID | 403 in both cases; target unchanged | R-002, R-004 |

### F-07 The visit

Steps: at or after the start, the doctor marks `Completed`, or staff mark `NoShow`. API ✅ · UI ❌

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-07.1 | Doctor completes after the start | 200, `Completed` | R-014 |
| F-07.2 | Nurse or admin tries to complete | 403 | R-014 |
| F-07.3 | Complete or no-show before the start | 409 | R-014 |
| F-07.4 | Nobody closes the appointment | **Gap:** it stays `Scheduled`/`Confirmed` forever; statistics and the doctor's list are wrong | R-050 |
| F-07.5 | Staff mark `NoShow` on a started visit that already has a medical record | 409, unchanged (the visit is documented) | R-014, R-053 |

### F-08 Writing a medical record

Steps: the doctor writes diagnosis, treatment and notes, optionally for an appointment. API ✅ · UI ❌

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-08.1 | Linked doctor writes a record | 201, author = that doctor, revision 1, audited without clinical content | R-010, R-011 |
| F-08.2 | Request names another doctor as author | Ignored; author = caller | R-010 |
| F-08.3 | Attach to another doctor's or another patient's appointment | 400, nothing written | R-010 |
| F-08.4 | Attach to an own appointment that cannot have taken place (not started yet, `Cancelled`, `NoShow`) | 409, nothing written. Allowed: started and `Scheduled`, `Confirmed` or `Completed` (the doctor may write during the visit) | R-053 |
| F-08.5 | Nurse, admin or patient tries to write | 403 | R-005 |
| F-08.6 | Diagnosis missing or over the length limit | 400 | – |

### F-09 Amending a record

Steps: any linked doctor edits; every change becomes a revision. API ✅ · UI ❌

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-09.1 | Another doctor amends the record | 200, `lastEditedBy` = editor, new revision with changed fields | R-011 |
| F-09.2 | Edit that changes nothing | 200, no revision, no audit entry | R-011 |
| F-09.3 | Two doctors edit the same record; the second saves after the first | **Gap:** the second save overwrites the first without warning (no version check); the history keeps both, but the second doctor never saw the first change. Saves at the very same moment: one gets 409 (unique revision number) | R-056 |
| F-09.4 | Doctor or nurse reads the history | All revisions in order with editors | R-011 |
| F-09.5 | Patient or admin reads the history | 403 | R-020 |

### F-10 Patient views own data

Steps: the patient signs in and opens appointments and records. API ✅ · UI ❌

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-10.1 | List own appointments / records | Only own items; `patientId` in the query is ignored | R-001, R-002 |
| F-10.2 | Open another patient's item, or an ID that does not exist | 403 in both cases | R-004 |
| F-10.3 | Unlinked patient | Empty lists, 403 on IDs | R-001 |
| F-10.4 | Response content | Nothing about other patients; no internal fields (account IDs, links) | R-054 |

### F-11 Status correction

Steps: an administrator corrects a final status set by mistake, with a reason. API ✅ · UI ❌

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-11.1 | `NoShow` → `Completed` with a reason | 200, audited with old, new and reason | R-022 |
| F-11.2 | Reopen a cancelled appointment before its start, slot still free | 200, `Scheduled` | R-022 |
| F-11.3 | Reopen after the start, or the slot is taken now | 409 | R-022 |
| F-11.4 | Reason missing or shorter than 10 characters | 400 | R-022 |
| F-11.5 | Non-admin tries | 403 | R-005 |
| F-11.6 | `Completed` → `NoShow` while a medical record is linked to the appointment | 409, stays `Completed`, no audit entry, no notification. Without a linked record the correction is applied | R-022, R-053 |

### F-12 Offboarding

Steps: a staff member leaves; the administrator deactivates the account. API ✅ · UI ❌

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-12.1 | Deactivated user calls the API | 403 "Account deactivated" (other instances within 5 min) | R-007, R-039 |
| F-12.2 | Deactivated doctor still has future appointments | **Gap:** appointments stay with them; patients are not told | R-055 |
| F-12.3 | Delete a doctor with appointments or records | 409 | R-019 |

### F-13 Session

Steps: sign in, work, token refresh, sign out. API ✅ · UI ✅

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-13.1 | Work longer than the access-token lifetime | Token refreshed silently before the call | R-006 |
| F-13.2 | Refresh token expired or revoked | Back to the login page, no data shown | R-006 |
| F-13.3 | Sign out | Keycloak session ended; back button shows no data | R-006 |
| F-13.4 | Password grant attempted against the token endpoint | 400 `unauthorized_client` | R-027 |

### F-14 Audit review

Steps: the administrator reads the audit trail. API ✅ · UI ❌

| ID | Scenario | Expected | Risks |
|---|---|---|---|
| F-14.1 | Every flow above leaves the expected audit entries | Who, what, when; no clinical content | R-013, R-041 |
| F-14.2 | Non-admin reads the audit log | 403 | R-005 |

## 4. Synthetic data rules

| Rule | Why |
|---|---|
| Generated by a **deterministic** generator (fixed seed): the same seed always gives the same data | Tests and bug reports are reproducible |
| **Realistic variety and volume**: several departments, 10–20 doctors and nurses, hundreds of patients, appointments over past and future weeks in every status, records with several revisions | Flows, paging and performance behave as in production |
| **Identifiers that cannot be real**: record numbers `TEST-000123`, e-mail on the reserved `.test` domain, phone numbers from a reserved fictional range, no national ID numbers | No synthetic record can match a real person |
| Names and addresses come from a generator, never from a real list of people | Same |
| Every Keycloak sample user is linked to its profile on import | Flows start in a usable state |
| Production-like configuration loads **no** sample data; the generator runs only in development and test environments | R-029 |

## 5. Leakage checklist

Applied to every flow and every new endpoint. A "no" is a finding (SECURITY_TESTING §9).

| Channel | Check |
|---|---|
| Responses | Only fields the caller may see; no internal IDs or account links of others; lists filtered before paging |
| Errors | Same status for "missing" and "not yours"; no stack traces, SQL, connection strings or other patients' values |
| Logs | No clinical content, no tokens, no passwords, no names, e-mail addresses or phone numbers |
| Audit | Who and what, never clinical content |
| Telemetry | Traces and metrics carry IDs and routes, not personal data or query strings with personal data |
| Browser | Tokens in memory only; nothing clinical in `localStorage`, URLs or the page title |
| Configuration | No secrets in the repository; Swagger and sample data off outside development |
