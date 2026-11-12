## Hospital Management System

The Hospital Management System is a project developed for managing hospital operations efficiently. It consists of a backend API built with ASP.NET Core EF 8, following the principles of Clean Architecture, a web frontend developed using React TypeScript, and a desktop frontend built with Windows Forms .NET.

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

#### Frontend (Desktop)

- Windows Forms .NET

## Getting Started

#### Setting up the Backend:

The backend is an ASP.NET Core API with PostgreSQL and Keycloak. See [ARCHITECTURE.md](ARCHITECTURE.md) for how it fits together and [docs/API_ENDPOINTS.md](docs/API_ENDPOINTS.md) for every endpoint.

**Option 1: Docker (everything in containers).** Requires Docker Desktop.

1. From the repository root run `docker compose up --build`.
2. Open Swagger at `http://localhost:5117/swagger` and click **Authorize** to sign in.
3. Keycloak admin console: `http://localhost:8080` (user `admin`, password `admin_dev_password`).

Development sign-ins: `admin.user` / `Admin123!`, `doctor.user` / `Doctor123!`, `nurse.user` / `Nurse123!`, `patient.user` / `Patient123!`.
To change passwords or the API port, copy `.env.example` to `.env`.

**Option 2: .NET Aspire (API from source, with dashboard).** Requires Docker Desktop and the .NET 10 SDK.

1. Run `dotnet run --project backend/HospitalManagement.AppHost`.
2. Open the dashboard link printed in the console; the API is at `http://localhost:5117/swagger`.

Both options use port 8080 for Keycloak, so run one at a time. Migrations and sample data are applied automatically on startup.

#### Setting up the Frontend (Web):

Open the frontend-web directory. `cd frontend-web`
Install dependencies:
`npm install`
Run the development server:
`npm run dev`
Or using docker:
`docker compose up`

#### Running the Desktop App:

Open the AppDesktop directory.
Run the application in Visual Studio.

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
