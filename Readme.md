## Hospital Management System

The Hospital Management System is a project developed for managing hospital operations efficiently. It consists of a backend API built with ASP.NET Core EF 8, following the principles of Clean Architecture, and a web frontend developed using React TypeScript that signs in through Keycloak.

### Features

- CRUD operations for doctors
- CRUD operations for patients
- CRUD operations for services
- Relationship between doctors and services (many-to-many)
- Relationship between services and patients (one-to-many)

### Technologies Used

#### Backend (APIs)

- ASP.NET Core with Clean Architecture
- Entity Framework Core 8

#### Frontend (Web)

- React with TypeScript
- Axios for API requests
- Redux Toolkit for state management
- Ant Design (Antd) for UI components

## Getting Started

#### Setting up the Backend:

The backend is an ASP.NET Core API with PostgreSQL and Keycloak. See [ARCHITECTURE.md](ARCHITECTURE.md) for how it fits together and [docs/API_ENDPOINTS.md](docs/API_ENDPOINTS.md) for every endpoint.

**Option 1: Docker (everything in containers).** Requires Docker Desktop.

1. Copy `.env.example` to `.env` and set `POSTGRES_PASSWORD`, `KEYCLOAK_ADMIN_PASSWORD` and `DEV_USERS_PASSWORD`. No password is stored in the repository, and `.env` is git-ignored.
2. From the repository root run `docker compose up --build`. This also loads `docker-compose.override.yml`,
   which adds sample data and Swagger for local development. `docker compose -f docker-compose.yml up --build`
   runs without them.
3. Open Swagger at `http://localhost:5117/swagger` and click **Authorize** to sign in.
4. Keycloak admin console: `http://localhost:8080` (user and password from `.env`).

Sample sign-ins: `admin.user`, `doctor.user`, `nurse.user`, `patient.user`, all with the password you set in `DEV_USERS_PASSWORD`.

**Option 2: .NET Aspire (API from source, with dashboard).** Requires Docker Desktop and the .NET 10 SDK.

1. Set the sample users' password once: `dotnet user-secrets set "Parameters:dev-users-password" "<your password>" --project backend/HospitalManagement.AppHost`.
2. Run `dotnet run --project backend/HospitalManagement.AppHost`.
3. Open the dashboard link printed in the console; the API is at `http://localhost:5117/swagger`.

Both options use port 8080 for Keycloak, so run one at a time. Migrations and sample data are applied automatically on startup.

#### Setting up the Frontend (Web):

Open the frontend-web directory. `cd frontend-web`
Optional: copy `.envExample` to `.env` to change the API or Keycloak address (`.env` is git-ignored; without
it the defaults match docker-compose: API on 5117, Keycloak on 8080). Then sign in at `http://localhost:3000`
with one of the sample users.
Install dependencies:
`npm install`
Run the development server:
`npm run dev`
Or using docker:
`docker compose up`

## Contributing

    If you find any bugs or would like to optimize the code, feel free to contribute. Pull requests are welcome!


## Project Screenshots
![Screenshot 2024-07-18 123447](https://github.com/user-attachments/assets/d3dce57d-29a5-4774-87b8-383db5565f5b)
<img width="561" alt="Services_managment" src="https://github.com/user-attachments/assets/efd2b663-b4a6-4042-9683-25c358e8b590">
<img width="563" alt="patient_managment" src="https://github.com/user-attachments/assets/4c114898-6d85-408a-aed9-e6906ab7a04d">
<img width="872" alt="HospitalManagement_swagger" src="https://github.com/user-attachments/assets/736e5268-7094-4550-8176-f45b96feac70">
<img width="569" alt="edit_doctor" src="https://github.com/user-attachments/assets/198b0399-ace2-4c54-bc31-b270798395f7">
<img width="615" alt="doctors_managment" src="https://github.com/user-attachments/assets/dbb4ff38-ad75-44f4-99ac-c4b95c1caf89">
<img width="614" alt="doctor_details" src="https://github.com/user-attachments/assets/9b85f401-79bf-47a3-a107-8c23e3e015cd">
