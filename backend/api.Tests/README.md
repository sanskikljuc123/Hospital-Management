# API tests

API-level security tests for the backend. They run the real API in-process against a real PostgreSQL started in Docker, with the real migrations and real token validation.

## Run

Docker must be running.

```bash
dotnet test backend/api.Tests -c Release -f net8.0
```

On a machine that has only the .NET 10 runtime:

```bash
dotnet test backend/api.Tests -c Release -f net10.0
```

`-c Release` also avoids file locks when the API is running locally from a Debug build.

## What is covered

| File | Covers | Gate |
|---|---|---|
| `Security/AuthenticationTests.cs` | Missing, malformed, expired, wrongly signed, unsigned, wrong-issuer, wrong-audience and not-yet-valid tokens; altered role claim; deactivated account; anonymous health endpoints | G-SEC-01 |
| `Security/RoleByEndpointMatrix.cs` | The role by endpoint matrix as data, for all 36 endpoints of the eight controllers | G-SEC-01 |
| `Security/AuthorizationMatrixTests.cs` | One test per matrix cell; refused writes leave state unchanged; a completeness check over every endpoint the API exposes | G-SEC-01 |
| `Security/BolaTests.cs` | Object-level access to patient records, using two Patient identities each linked to their own record | G-SEC-02 |
| `Security/AppointmentBolaTests.cs` | Object-level access to appointments: read, list and its filters, book, cancel, reschedule, delete | G-SEC-02 |
| `Security/MedicalRecordBolaTests.cs` | Object-level access to medical records; admins have no access to clinical content; only doctors write | G-SEC-02 |

Test names and `Case` traits use the case IDs in `docs/qe-framework/SECURITY_TESTING.md` (for example `BOLA-02`).

## Known failing tests

Four tests fail because the API does not behave as the documentation and the security standard require. They are left failing on purpose; do not skip or loosen them. Fix the API, or change the documented rule and then the test.

| Finding | Failing tests | Observed | Expected |
|---|---|---|---|
| F-01 Record enumeration | `AppointmentBolaTests.Patient_cannot_tell_a_missing_appointment_from_another_patients_appointment`, `AppointmentBolaTests.Patient_cannot_probe_for_appointments_through_cancel`, `MedicalRecordBolaTests.Patient_cannot_tell_a_missing_record_from_another_patients_record` | A Patient gets 404 for an id that does not exist and 403 for one that belongs to someone else, so they can learn which appointment and medical record ids exist | The same response for both (BOLA-08), as `GET /api/Patient/{id}` already does |
| F-02 Undocumented access for an identity without roles | `AuthorizationMatrixTests.Matrix_cell_has_the_documented_outcome(GET /api/Appointment, NoRoles)` | 200 with an empty list | 403: the endpoint is documented for Staff and Patient only |

## What is not covered yet

- Tokens come from a test issuer, not from Keycloak. Validation settings are the API's own, but the realm export (roles, audience mapper, token lifetimes) is not exercised. `docs/qe-framework/TEST_DATA_AND_ENVIRONMENTS.md` section 8 requires a smaller suite against a real Keycloak instance; it does not exist yet.
- Doctor-owned objects: the documentation states no rule limiting a doctor to their own patients or appointments, so none is tested.
- Mass assignment (AUTHZ-07), information leakage in responses and logs (LEAK cases), and input validation.
- No unit tests, no business-rule tests (for example double booking), no mutation testing.

## Adding an endpoint

1. Add a row to `RoleByEndpointMatrix.Rows` with the roles documented as allowed in `docs/API_ENDPOINTS.md`. Take the expected outcome from the documentation, not from the controller attributes.
2. If the route takes an object identifier, add object-level tests or record why they do not apply.
3. The completeness test fails until step 1 is done.

## Rules

- Each test creates the data it needs. Do not rely on seed data or on another test.
- Only synthetic data and the identities in `Infrastructure/TestIdentities.cs`.
- Do not disable or stub token validation.
- Before trusting a new test, break the behavior once and see the test fail.
