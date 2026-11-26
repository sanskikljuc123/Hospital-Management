# Security Testing

What the API must allow and refuse, and how we prove it. Risks referenced here are in
[RISK_REGISTER.md](RISK_REGISTER.md); identities and tooling in [TEST_STRATEGY.md](TEST_STRATEGY.md#5-test-identities).
Security tests are API-level tests tagged `[Trait("Category", "Security")]`.

## 1. Expected responses

| Situation | Status | Body must not contain |
|---|---|---|
| No token, or a token that fails validation | **401** | anything from the database |
| Valid token, role not allowed for the endpoint | **403** | anything from the database |
| Valid token, account deactivated | **403** (`title: "Account deactivated"`) | anything from the database |
| Patient asks for an ID that is not theirs — whether it exists or not | **403** | anything from the database |
| Staff asks for an ID that does not exist | **404** | stack trace, SQL, connection details |
| Allowed | **2xx** | another patient's data (for patient callers) |

## 2. Role-by-endpoint matrix

Legend: **Yes** — allowed. **Own** — allowed only for the patient record linked to the caller; 403 for any
other ID, existing or not. **Linked** — allowed only for a Doctor account linked to a doctor profile; 403
otherwise. **403** — refused. Every endpoint returns **401** for anonymous callers, and **403** for
deactivated accounts and for tokens without a hospital role (R-015: `DefaultPolicy` and `FallbackPolicy` in
`backend/api/Auth/Policies.cs`). Source: the `[Authorize]` attributes in `backend/api/Controllers/` and
`backend/api/Auth/Policies.cs`.

| Endpoint | Admin | Doctor | Nurse | Patient | No role | Risks |
|---|---|---|---|---|---|---|
| `GET /api/Appointment` | Yes | Yes | Yes | Own (filtered list) | 403 | R-002 |
| `GET /api/Appointment/{id}` | Yes | Yes | Yes | Own | 403 | R-002, R-004 |
| `POST /api/Appointment` | Yes | Yes | Yes | Own (`patientId` must be theirs) | 403 | R-002 |
| `PUT /api/Appointment/{id}` | Yes | Yes | Yes | 403 | 403 | R-014 |
| `PUT /api/Appointment/{id}` with `status: Completed` | 403 | Yes (once started) | 403 | 403 | 403 | R-014 |
| `POST /api/Appointment/{id}/cancel` | Yes | Yes | Yes | Own | 403 | R-002, R-004 |
| `POST /api/Appointment/{id}/correct-status` | Yes | 403 | 403 | 403 | 403 | R-005, R-022 |
| `DELETE /api/Appointment/{id}` | Yes | 403 | 403 | 403 | 403 | R-005 |
| `GET /api/AuditLog` | Yes | 403 | 403 | 403 | 403 | R-005 |
| `GET /api/Department`, `GET /api/Department/{id}` | Yes | Yes | Yes | Yes | 403 | R-015 |
| `POST/PUT/DELETE /api/Department…` | Yes | 403 | 403 | 403 | 403 | R-005 |
| `GET /api/Doctor`, `GET /api/Doctor/{id}` | Yes | Yes | Yes | Yes | 403 | R-015 |
| `POST/PUT/DELETE /api/Doctor…` | Yes | 403 | 403 | 403 | 403 | R-005 |
| `GET /api/MedicalRecord` | 403 | Yes | Yes | Own (filtered list) | 403 | R-001, R-020 |
| `GET /api/MedicalRecord/{id}` | 403 | Yes | Yes | Own | 403 | R-001, R-004, R-020 |
| `POST /api/MedicalRecord` | 403 | Linked | 403 | 403 | 403 | R-010, R-021 |
| `PUT /api/MedicalRecord/{id}` | 403 | Linked | 403 | 403 | 403 | R-011, R-021 |
| `GET /api/MedicalRecord/{id}/history` | 403 | Yes | Yes | 403 | 403 | R-011, R-020 |
| `GET /api/Patient` | Yes | Yes | Yes | 403 | 403 | R-003 |
| `GET /api/Patient/{id}` | Yes | Yes | Yes | Own | 403 | R-003 |
| `POST /api/Patient`, `PUT /api/Patient/{id}` | Yes | 403 | Yes | 403 | 403 | R-069 |
| `DELETE /api/Patient/{id}` | Yes | 403 | 403 | 403 | 403 | R-005 |
| `GET /api/Service`, `GET /api/Service/{id}` | Yes | Yes | Yes | Yes | 403 | R-015 |
| `POST/PUT/DELETE /api/Service…` | Yes | 403 | 403 | 403 | 403 | R-005, R-016 |
| `GET /api/User/me` | Yes | Yes | Yes | Yes | 403 | R-007, R-015 |
| `GET /api/User`, `GET /api/User/{id}` | Yes | 403 | 403 | 403 | 403 | R-005 |
| `PUT /api/User/{id}/status`, `PUT /api/User/{id}/link` | Yes | 403 | 403 | 403 | 403 | R-005, R-008 |
| `GET /health`, `GET /alive` | anonymous allowed | | | | | – |

**How it is tested:** one data-driven test (`[Theory]`) generates a row per endpoint × identity from a
single table in code that mirrors this matrix. A new endpoint without a row fails the test, so the matrix
cannot silently fall behind the controllers.

## 3. Object-level access (BOLA)

Every **Own** decision is made by one component, `IPatientAccessPolicy` (ARCHITECTURE.md §3). It is tested on
three levels:

- **Rules:** `PatientAccessPolicyTests` (unit) — each role and scope against own, foreign and missing ids.
- **Use:** `ArchitectureRulesTests` (unit) — fails when a controller decides ownership itself instead of asking
  the policy.
- **Behavior:** `BolaTests` (API) — the cases below, end to end.

For every endpoint marked **Own**, test with two linked patients A and B and one unlinked patient:

| Case | Expected |
|---|---|
| A requests A's record | 2xx, body contains only A's data |
| A requests B's record | 403 |
| A requests an ID that does not exist | 403 (same as B's — no ID probing, R-004) |
| A lists appointments / medical records | Only A's items; none of B's IDs appear |
| A books an appointment with `patientId` = B | 403, and no appointment is created |
| A cancels B's appointment | 403, and B's appointment is unchanged |
| Unlinked patient lists | Empty list |
| Unlinked patient requests any ID | 403 |
| Staff requests an ID that does not exist | 404 |

"Unchanged" and "not created" are checked by reading the database or by a follow-up request as staff,
not assumed from the status code.

### Record author and editor (R-010, R-011)

Use two doctors D1 and D2, each linked to a doctor profile, and one unlinked doctor account:

| Case | Expected |
|---|---|
| D1 creates a record and sends `doctorId` = D2 in the body | 201, record `doctorId` = D1 |
| D1 attaches a record to D2's appointment for the same patient | 400, no record created |
| Unlinked doctor creates or edits a record | 403, nothing created or changed |
| D2 edits D1's record | 200; `lastEditedByDoctorId` = D2; a new revision with editor D2 and the changed fields |
| Edit that changes nothing | 200; no new revision; no audit entry |
| History after one edit | 2 revisions: revision 1 = original content by the author, revision 2 = new content by the editor |
| Patient or Admin reads history | 403 |

## 4. Token validation (R-006)

Each case must return **401** on a protected endpoint:

- No `Authorization` header; `Bearer` with an empty value; a non-JWT string
- Expired token (beyond the 30 s clock skew)
- Wrong issuer; wrong audience
- Signed with a different key; signature removed; `alg: none`
- Token that is not yet valid (`nbf` in the future)

## 5. Deactivated accounts (R-007)

A user deactivated through `PUT /api/User/{id}/status` gets **403 "Account deactivated"** on every
`/api/*` endpoint on their next request, including `GET /api/User/me`, with a token that is otherwise valid.

## 6. Data exposure (R-017)

- Error responses for 400, 404, 409 and 500 contain no stack trace, SQL, connection string or token.
- A patient's responses never contain another patient's ID, name or medical content.
- Logs written during a security test contain no access tokens or passwords.

## 7. Over-posting (R-016)

For endpoints that bind an entity directly (`POST/PUT /api/Service`), send extra properties
(`id`, `patients`, `doctorServices`) and assert that only the documented fields change.

## 8. Secrets (R-012)

gitleaks runs on every pull request. A finding blocks the merge. A leaked secret is rotated, not just
deleted — git history keeps it.

## 9. Handling a security finding

1. A failing security test, or an agent finding, is recorded as **unrated** using the finding template in
   [QUALITY_GATES.md](QUALITY_GATES.md#6-templates).
2. `qe-analyst` may propose an Impact and Likelihood; the **test owner** sets the rating.
3. The fix and the test that proves it land in the same pull request.
4. The test is never changed to match the insecure behavior.
