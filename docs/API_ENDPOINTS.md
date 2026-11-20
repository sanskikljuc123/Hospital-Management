# API Endpoint Inventory

Base URL: `http://localhost:5117` (docker-compose and Aspire). Interactive version: `/swagger`.

## Conventions

- **Authentication.** Every `/api/*` endpoint requires `Authorization: Bearer <access token>` issued by
  Keycloak realm `HospitalRealm` with audience `hospital-api`. Only `/health`, `/alive` and Swagger are anonymous.
- **Roles.** `Admin`, `Doctor`, `Nurse`, `Patient` (Keycloak realm roles). Every `/api/*` endpoint also
  requires **one of these roles**: a valid token without a hospital role gets 403 everywhere, including
  `/api/User/me`. "Any" below means any user with a hospital role. "Staff" means Admin, Doctor or Nurse.
- **Doctor profile.** Writing medical records needs a Doctor account linked to a doctor record
  (`PUT /api/User/{id}/link` with `doctorId`). An unlinked doctor gets 403. The author and editor of a record
  are always taken from that link, never from the request.
- **Own data.** A user with only the `Patient` role is limited to the patient record an admin linked to their
  account (`PUT /api/User/{id}/link`). Until linked, they see empty lists and get 403 on individual records.
  A patient gets 403 for any patient, appointment or medical record id that is not theirs, whether or not it
  exists, so ids cannot be probed. Staff get 404 for an id that does not exist.
- **Common responses.**

  | Status | Meaning |
  |---|---|
  | 400 | Validation failed (problem details) or a business rule was violated (plain text message) |
  | 401 | Missing, expired or invalid token |
  | 403 | Authenticated but the role or ownership check failed, or the account is deactivated |
  | 404 | Record not found (plain text message) |
  | 409 | Conflict: double booking, duplicate name, or the record is still referenced by other data |

- **Formats.** JSON, camelCase. Dates are ISO 8601 and stored in UTC. Enums are strings.

## Health

| Method | Path | Purpose | Auth | Output |
|---|---|---|---|---|
| GET | `/health` | Readiness: process and database reachable | Anonymous | `Healthy` / `Unhealthy` (200 / 503) |
| GET | `/alive` | Liveness: process responds | Anonymous | `Healthy` (200) |

## Doctors — `/api/Doctor`

| Method | Path | Purpose | Roles | Input | Output |
|---|---|---|---|---|---|
| GET | `/api/Doctor` | List doctors | Any | – | `DoctorDto[]` |
| GET | `/api/Doctor/{id}` | Get one doctor | Any | – | `DoctorDto` |
| POST | `/api/Doctor` | Create a doctor and assign services (400 for an unknown department or service) | Admin | `DoctorCreationDto` | 201 + `DoctorDto` |
| PUT | `/api/Doctor/{id}` | Update a doctor and replace their services | Admin | `DoctorCreationDto` | `DoctorDto` |
| DELETE | `/api/Doctor/{id}` | Delete a doctor (409 if they have appointments or records) | Admin | – | 204 |

Create, update and delete are audited (`Doctor.*`).

```jsonc
// DoctorCreationDto (id is ignored; firstName and lastName are required)
{ "firstName": "John", "lastName": "Doe", "birthday": "1980-01-01",
  "address": "123 Main St", "departmentId": 1, "serviceIds": [1, 2] }

// DoctorDto
{ "id": 1, "firstName": "John", "lastName": "Doe", "birthday": "1980-01-01T00:00:00Z",
  "address": "123 Main St", "departmentId": 1, "serviceIds": [1, 2] }
```

## Patients — `/api/Patient`

| Method | Path | Purpose | Roles | Input | Output |
|---|---|---|---|---|---|
| GET | `/api/Patient` | List patients | Staff | – | `PatientDto[]` |
| GET | `/api/Patient/{id}` | Get one patient | Staff; Patient (own record only) | – | `PatientDto` |
| POST | `/api/Patient` | Register a patient (400 for an unknown service) | Staff | `PatientCreationDto` | 201 + `PatientDto` |
| PUT | `/api/Patient/{id}` | Update a patient | Staff | `PatientCreationDto` | `PatientDto` |
| DELETE | `/api/Patient/{id}` | Delete a patient (409 if they have appointments or records) | Admin | – | 204 |

Create, update and delete are audited (`Patient.*`) with ids and changed field names only, never personal data.

```jsonc
// PatientCreationDto (userName and serviceId are required)
{ "userName": "patient1", "phone": 12345678, "email": "patient1@hospital.local", "serviceId": 1 }

// PatientDto
{ "id": 1, "userName": "patient1", "phone": 12345678, "email": "patient1@hospital.local",
  "serviceId": 1, "serviceName": "Cardiac consultation" }
```

## Services — `/api/Service`

| Method | Path | Purpose | Roles | Input | Output |
|---|---|---|---|---|---|
| GET | `/api/Service` | List medical services | Any | – | `ServiceDto[]` |
| GET | `/api/Service/{id}` | Get one service | Any | – | `ServiceDto` |
| POST | `/api/Service` | Create a service (400 for an unknown department) | Admin | `ServiceDto` | 201 + `ServiceDto` |
| PUT | `/api/Service/{id}` | Update a service (the route id wins over the body id) | Admin | `ServiceDto` | `ServiceDto` |
| DELETE | `/api/Service/{id}` | Delete a service; **409 while patients or doctors still use it** (it never deletes them) | Admin | – | 204 |

Only `name`, `description` and `departmentId` can be set; any other field in the body is ignored.
Create, update and delete are audited (`Service.*`).

```jsonc
// ServiceDto (id is ignored on input; name is required)
{ "id": 1, "name": "Cardiac consultation", "description": "...", "departmentId": 1 }
```

## Departments — `/api/Department`

| Method | Path | Purpose | Roles | Input | Output |
|---|---|---|---|---|---|
| GET | `/api/Department` | List departments | Any | – | `DepartmentDto[]` |
| GET | `/api/Department/{id}` | Get one department | Any | – | `DepartmentDto` |
| POST | `/api/Department` | Create a department (409 on duplicate name) | Admin | `DepartmentDto` | 201 + `DepartmentDto` |
| PUT | `/api/Department/{id}` | Update a department | Admin | `DepartmentDto` | `DepartmentDto` |
| DELETE | `/api/Department/{id}` | Delete a department (doctors and services are unassigned) | Admin | – | 204 |

```jsonc
// DepartmentDto (id is ignored on input)
{ "id": 1, "name": "Cardiology", "description": "Heart and vascular care" }
```

## Appointments — `/api/Appointment`

| Method | Path | Purpose | Roles | Input | Output |
|---|---|---|---|---|---|
| GET | `/api/Appointment` | List appointments | Staff (all); Patient (own only) | Query: `patientId`, `doctorId`, `from`, `to` (all optional; `patientId` is forced for patients) | `AppointmentDto[]` |
| GET | `/api/Appointment/{id}` | Get one appointment | Staff; Patient (own only) | – | `AppointmentDto` |
| POST | `/api/Appointment` | Book an appointment. Must be in the future; 409 if the doctor is busy in that slot | Staff (any patient); Patient (self only) | `AppointmentCreateDto` | 201 + `AppointmentDto` |
| PUT | `/api/Appointment/{id}` | Reschedule, reassign or change status, following the lifecycle below | Staff (`Completed`: Doctor only) | `AppointmentUpdateDto` | `AppointmentDto` |
| POST | `/api/Appointment/{id}/cancel` | Cancel; only a Scheduled or Confirmed appointment that has not started (409 otherwise) | Staff; Patient (own only) | – | `AppointmentDto` |
| POST | `/api/Appointment/{id}/correct-status` | Correct a final status set by mistake (see *Status corrections*) | Admin | `AppointmentStatusCorrectionDto` | `AppointmentDto` |
| DELETE | `/api/Appointment/{id}` | Hard delete | Admin | – | 204 |

Create, update, cancel and delete are audited. Create and update also notify the patient through `INotificationService`.

### Appointment lifecycle

```
 book ──▶ Scheduled ⇄ Confirmed ──┬──▶ Cancelled   (before the start; patient for own, or staff)
                                  ├──▶ Completed   (doctor, once it has started)
                                  └──▶ NoShow      (staff, once it has started)
```

| Rule | Response when broken |
|---|---|
| `Completed`, `NoShow` and `Cancelled` are final. Only `notes` can still change; any other field or status change is refused. | 409 |
| `Scheduled` ⇄ `Confirmed` at any time. | – |
| `Cancelled` only before `scheduledAt` (both through `PUT` and `/cancel`). | 409 |
| `Completed` only by a Doctor and only once `scheduledAt` has passed. | 403 (not a doctor), 409 (not started) |
| `NoShow` only once `scheduledAt` has passed, and never when a medical record is linked to the appointment (the visit is documented). | 409 |
| Changing `doctorId`, `serviceId`, `scheduledAt` or `durationMinutes` (moving) is allowed only while `Scheduled` or `Confirmed`, only **before** the current `scheduledAt` (a started visit is closed with `Completed` or `NoShow`, not moved), only into the future, and must not be combined with a final status. | 400 (new time in the past), 409 (already started, with final status, or doctor busy) |
| A doctor never has two overlapping appointments that are not cancelled, also when two bookings or moves arrive at the same moment: the database decides, and every request but one gets the same 409 as a busy slot. A rare 409 "another change … was saved at the same moment" means the same thing: reload and try again. | 409 |
| A moved appointment always goes back to `Scheduled` and has to be confirmed again. | – |

### Status corrections

A final status set by mistake is not changed with `PUT`. An admin corrects it with
`POST /api/Appointment/{id}/correct-status` and a reason (10–500 characters, 400 otherwise):

| From | To | Condition |
|---|---|---|
| `NoShow` | `Completed` | – (e.g. the patient arrived late and was seen) |
| `Completed` | `NoShow` | No medical record is linked to the appointment (e.g. the wrong appointment was marked as completed). A documented visit took place and stays `Completed` |
| `Cancelled` | `Scheduled` | The appointment has not started and the doctor is still free in that slot |

Any other correction, or a correction of an appointment that is not final, returns 409. Each correction is
audited as `Appointment.StatusCorrected` with the old status, the new status and the reason, and the
patient is notified.

```jsonc
// AppointmentStatusCorrectionDto
{ "status": "Completed", "reason": "Patient arrived 10 minutes late, examination was done" }
```

```jsonc
// AppointmentCreateDto
{ "patientId": 1, "doctorId": 1, "serviceId": 1, "scheduledAt": "2027-03-01T09:00:00Z",
  "durationMinutes": 30, "reason": "Checkup" }

// AppointmentUpdateDto
{ "doctorId": 1, "serviceId": 1, "scheduledAt": "2027-03-01T10:00:00Z", "durationMinutes": 45,
  "status": "Confirmed", "reason": "Checkup", "notes": "Bring previous results" }

// AppointmentDto — status: Scheduled | Confirmed | Completed | Cancelled | NoShow
{ "id": 1, "patientId": 1, "patientName": "patient1", "doctorId": 1, "doctorName": "John Doe",
  "serviceId": 1, "serviceName": "Service1", "scheduledAt": "2027-03-01T09:00:00Z",
  "durationMinutes": 30, "status": "Scheduled", "reason": "Checkup", "notes": null,
  "createdAt": "2026-11-12T12:45:43Z", "updatedAt": null }
```

## Medical records — `/api/MedicalRecord`

Admins have no access to clinical content.

| Method | Path | Purpose | Roles | Input | Output |
|---|---|---|---|---|---|
| GET | `/api/MedicalRecord` | List records | Doctor, Nurse (all); Patient (own only) | Query: `patientId` (optional; forced for patients) | `MedicalRecordDto[]` |
| GET | `/api/MedicalRecord/{id}` | Get one record | Doctor, Nurse; Patient (own only) | – | `MedicalRecordDto` |
| POST | `/api/MedicalRecord` | Create a record. The author is the caller's linked doctor | Doctor (linked) | `MedicalRecordCreateDto` | 201 + `MedicalRecordDto` |
| PUT | `/api/MedicalRecord/{id}` | Update diagnosis, treatment, notes. Any doctor may edit any record; every change becomes a new revision | Doctor (linked) | `MedicalRecordUpdateDto` | `MedicalRecordDto` |
| GET | `/api/MedicalRecord/{id}/history` | Every version of the record, oldest first, with who changed what and when | Doctor, Nurse | – | `MedicalRecordRevisionDto[]` |

- **Author.** The record's `doctorId` is the doctor linked to the caller's account; a `doctorId` sent in the
  body is ignored. `appointmentId` is optional and must be an appointment of the same patient **and** the
  same doctor (400 otherwise, checked first). The appointment must also be a visit that can have taken
  place: it has started (`scheduledAt` ≤ now) and is `Scheduled`, `Confirmed` or `Completed` — 409 for a
  future, `Cancelled` or `NoShow` appointment, nothing written. An unlinked doctor gets 403.
- **History.** Revision 1 is the record as created. Each edit that changes something stores the full new
  content, the changed fields, the editing doctor, the user name and the time, in the same transaction as
  the edit. An edit that changes nothing creates no revision. Revisions are never changed or deleted.
- **Audit.** Create and update are audited. The audit entry lists the revision number and which fields
  changed, never their content.
- There is no delete endpoint: medical records are retained.

```jsonc
// MedicalRecordCreateDto (no doctorId: the author comes from the caller's account)
{ "patientId": 1, "appointmentId": 1, "diagnosis": "Hypertension",
  "treatment": "Lisinopril 10mg", "notes": "Review in 3 months" }

// MedicalRecordUpdateDto
{ "diagnosis": "Hypertension", "treatment": "Lisinopril 20mg", "notes": "Dose increased" }

// MedicalRecordDto — revision is the current version number (1 = never edited)
{ "id": 1, "patientId": 1, "patientName": "patient1", "doctorId": 1, "doctorName": "John Doe",
  "appointmentId": 1, "diagnosis": "Hypertension", "treatment": "Lisinopril 20mg",
  "notes": "Dose increased", "createdAt": "2026-11-12T12:45:44Z", "updatedAt": "2026-11-14T09:10:00Z",
  "lastEditedByDoctorId": 2, "lastEditedByDoctorName": "Jane Smith", "revision": 2 }

// MedicalRecordRevisionDto
{ "revision": 2, "diagnosis": "Hypertension", "treatment": "Lisinopril 20mg", "notes": "Dose increased",
  "changedFields": ["Treatment", "Notes"], "editedByDoctorId": 2, "editedByDoctorName": "Jane Smith",
  "editedByUserName": "jane.smith", "editedAt": "2026-11-14T09:10:00Z" }
```

## Users — `/api/User`

Local profiles of Keycloak users. A profile is created automatically the first time a user calls the API.
Passwords and role assignments are managed in the Keycloak admin console, not here.

| Method | Path | Purpose | Roles | Input | Output |
|---|---|---|---|---|---|
| GET | `/api/User/me` | The caller's own profile | Any | – | `UserDto` |
| GET | `/api/User` | List users | Admin | – | `UserDto[]` |
| GET | `/api/User/{id}` | Get one user | Admin | – | `UserDto` |
| PUT | `/api/User/{id}/status` | Activate or deactivate. Deactivated users get 403 on every request. You cannot deactivate yourself | Admin | `{ "isActive": false }` | `UserDto` |
| PUT | `/api/User/{id}/link` | Link the account to a doctor and/or patient record; `null` clears a link. 409 if the record belongs to another user | Admin | `{ "doctorId": null, "patientId": 1 }` | `UserDto` |

Status changes and links are audited.

```jsonc
// UserDto
{ "id": 1, "keycloakId": "695843e9-...", "userName": "patient.user", "email": "patient@hospital.local",
  "firstName": "Pat", "lastName": "Patient", "role": "Patient", "isActive": true,
  "createdAt": "2026-11-12T12:45:19Z", "lastLoginAt": "2026-11-12T12:45:43Z",
  "doctorId": null, "patientId": 1 }
```

## Audit log — `/api/AuditLog`

| Method | Path | Purpose | Roles | Input | Output |
|---|---|---|---|---|---|
| GET | `/api/AuditLog` | Read the audit trail, newest first | Admin | Query: `action`, `userId` (Keycloak subject), `entityType`, `entityId`, `from`, `to`, `page` (default 1), `pageSize` (default 50, max 200) | `AuditLogDto[]` |

```jsonc
// AuditLogDto — details is a JSON string
{ "id": 12, "timestamp": "2026-11-12T12:45:45Z", "action": "MedicalRecord.Updated",
  "entityType": "MedicalRecord", "entityId": "1", "userId": "f3a1...", "userName": "doctor.user",
  "ipAddress": "172.18.0.1",
  "details": "{\"PatientId\":1,\"Revision\":2,\"ChangedFields\":[\"Treatment\"],\"EditedByDoctorId\":1}",
  "success": true }
```

Actions: `User.Login`, `User.Provisioned`, `User.Activated`, `User.Deactivated`, `User.Linked`,
`Appointment.Created`, `Appointment.Updated`, `Appointment.StatusCorrected`, `Appointment.Deleted`,
`MedicalRecord.Created`, `MedicalRecord.Updated`, `Patient.Created`, `Patient.Updated`, `Patient.Deleted`,
`Doctor.Created`, `Doctor.Updated`, `Doctor.Deleted`, `Service.Created`, `Service.Updated`, `Service.Deleted`,
`Department.Created`, `Department.Updated`, `Department.Deleted`.

## Role matrix

| Area | Admin | Doctor | Nurse | Patient |
|---|---|---|---|---|
| Doctors, services, departments: read | ✔ | ✔ | ✔ | ✔ |
| Doctors, services, departments: write | ✔ | – | – | – |
| Patients: read | ✔ | ✔ | ✔ | own |
| Patients: create, update | ✔ | ✔ | ✔ | – |
| Patients: delete | ✔ | – | – | – |
| Appointments: read | ✔ | ✔ | ✔ | own |
| Appointments: book | ✔ | ✔ | ✔ | own |
| Appointments: update | ✔ | ✔ | ✔ | – |
| Appointments: mark Completed | – | ✔ | – | – |
| Appointments: cancel | ✔ | ✔ | ✔ | own |
| Appointments: correct a final status | ✔ | – | – | – |
| Appointments: delete | ✔ | – | – | – |
| Medical records: read | – | ✔ | ✔ | own |
| Medical records: write | – | ✔ (linked) | – | – |
| Medical records: history | – | ✔ | ✔ | – |
| Users: manage | ✔ | – | – | – |
| Own profile (`/api/User/me`) | ✔ | ✔ | ✔ | ✔ |
| Audit log | ✔ | – | – | – |

A user with a valid token but none of these four roles gets 403 on every `/api/*` endpoint.
