# API Endpoint Inventory

Base URL: `http://localhost:5117` (docker-compose and Aspire). Interactive version: `/swagger`.

## Conventions

- **Authentication.** Every `/api/*` endpoint requires `Authorization: Bearer <access token>` issued by
  Keycloak realm `HospitalRealm` with audience `hospital-api`. Only `/health`, `/alive` and Swagger are anonymous.
- **Roles.** `Admin`, `Doctor`, `Nurse`, `Patient` (Keycloak realm roles). "Any" below means any authenticated user.
  "Staff" means Admin, Doctor or Nurse.
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
| GET | `/api/Doctor` | List doctors | Any | – | `Doctor[]` |
| GET | `/api/Doctor/{id}` | Get one doctor | Any | – | `Doctor` |
| POST | `/api/Doctor` | Create a doctor and assign services | Admin | `DoctorCreationDto` | `DoctorCreationDto` (with `id`) |
| PUT | `/api/Doctor/{id}` | Update a doctor and replace their services | Admin | `DoctorCreationDto` | `DoctorCreationDto` |
| DELETE | `/api/Doctor/{id}` | Delete a doctor (409 if they have appointments or records) | Admin | – | the deleted `id` |

```jsonc
// DoctorCreationDto
{ "id": 1, "firstName": "John", "lastName": "Doe", "birthday": "1980-01-01",
  "address": "123 Main St", "departmentId": 1, "serviceIds": [1, 2] }

// Doctor
{ "id": 1, "firstName": "John", "lastName": "Doe", "birthday": "1980-01-01T00:00:00Z",
  "address": "123 Main St", "departmentId": 1, "userId": null, "doctorServices": null }
```

## Patients — `/api/Patient`

| Method | Path | Purpose | Roles | Input | Output |
|---|---|---|---|---|---|
| GET | `/api/Patient` | List patients | Staff | – | `Patient[]` |
| GET | `/api/Patient/{id}` | Get one patient | Staff; Patient (own record only) | – | `Patient` |
| POST | `/api/Patient` | Register a patient | Staff | `PatientCreationDto` | `Patient` |
| PUT | `/api/Patient/{id}` | Update a patient | Staff | `PatientCreationDto` | `{ "patientDto": PatientCreationDto, "id": number }` |
| DELETE | `/api/Patient/{id}` | Delete a patient (409 if they have appointments or records) | Admin | – | confirmation text |

```jsonc
// PatientCreationDto
{ "userName": "patient1", "phone": 12345678, "email": "patient1@hospital.local", "serviceId": 1 }

// Patient
{ "id": 1, "userName": "patient1", "phone": 12345678, "email": "patient1@hospital.local",
  "serviceId": 1, "service": null, "userId": null }
```

## Services — `/api/Service`

| Method | Path | Purpose | Roles | Input | Output |
|---|---|---|---|---|---|
| GET | `/api/Service` | List medical services | Any | – | `Service[]` |
| GET | `/api/Service/{id}` | Get one service | Any | – | `Service` |
| POST | `/api/Service` | Create a service | Admin | `Service` | `Service` |
| PUT | `/api/Service/{id}` | Update a service (the route id wins over the body id) | Admin | `Service` | `Service` |
| DELETE | `/api/Service/{id}` | Delete a service | Admin | – | confirmation text |

```jsonc
// Service
{ "id": 1, "name": "Cardiac consultation", "description": "...", "departmentId": 1,
  "doctorServices": null, "patients": null }
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
| PUT | `/api/Appointment/{id}` | Reschedule, reassign or change status | Staff | `AppointmentUpdateDto` | `AppointmentDto` |
| POST | `/api/Appointment/{id}/cancel` | Cancel (409 if already Completed or NoShow) | Staff; Patient (own only) | – | `AppointmentDto` |
| DELETE | `/api/Appointment/{id}` | Hard delete | Admin | – | 204 |

Create, update, cancel and delete are audited. Create and update also notify the patient through `INotificationService`.

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
| POST | `/api/MedicalRecord` | Create a record | Doctor | `MedicalRecordCreateDto` | 201 + `MedicalRecordDto` |
| PUT | `/api/MedicalRecord/{id}` | Update diagnosis, treatment, notes | Doctor | `MedicalRecordUpdateDto` | `MedicalRecordDto` |

Create and update are audited. The audit entry lists which fields changed, never their content.
There is no delete endpoint: medical records are retained.

```jsonc
// MedicalRecordCreateDto (appointmentId is optional and must belong to the patient)
{ "patientId": 1, "doctorId": 1, "appointmentId": 1, "diagnosis": "Hypertension",
  "treatment": "Lisinopril 10mg", "notes": "Review in 3 months" }

// MedicalRecordUpdateDto
{ "diagnosis": "Hypertension", "treatment": "Lisinopril 20mg", "notes": "Dose increased" }

// MedicalRecordDto
{ "id": 1, "patientId": 1, "patientName": "patient1", "doctorId": 1, "doctorName": "John Doe",
  "appointmentId": 1, "diagnosis": "Hypertension", "treatment": "Lisinopril 10mg",
  "notes": "Review in 3 months", "createdAt": "2026-11-12T12:45:44Z", "updatedAt": null }
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
  "ipAddress": "172.18.0.1", "details": "{\"PatientId\":1,\"ChangedFields\":[\"Treatment\"]}",
  "success": true }
```

Actions: `User.Login`, `User.Provisioned`, `User.Activated`, `User.Deactivated`, `User.Linked`,
`Appointment.Created`, `Appointment.Updated`, `Appointment.Deleted`, `MedicalRecord.Created`, `MedicalRecord.Updated`.

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
| Appointments: cancel | ✔ | ✔ | ✔ | own |
| Appointments: delete | ✔ | – | – | – |
| Medical records: read | – | ✔ | ✔ | own |
| Medical records: write | – | ✔ | – | – |
| Users: manage | ✔ | – | – | – |
| Own profile (`/api/User/me`) | ✔ | ✔ | ✔ | ✔ |
| Audit log | ✔ | – | – | – |
