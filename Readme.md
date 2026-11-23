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


## Quality and testing

Every change is tested against the risks it could cause, and a person makes every decision. AI agents help to
find risks, design tests and review changes, but they only advise; the automated checks in CI never call an AI.

![How a change is tested: ten steps from input to monitoring](docs/images/testing-flow-overview.png)

In short: a change starts as a requirement or a finding (1). It is analysed for risks (2), and the test owner
rates them in the risk register (3). Tests are designed at the lowest level that can prove each risk (4), written
together with the code (5) and reviewed (6). Every pull request must pass the automated gates (7) before the code
owner merges it (8); a risk counts as covered only once a passing test proves it (9). Nightly runs keep watching
(10), and any failure starts the loop again at step 2.

Details: [docs/testing/README.md](docs/testing/README.md).
