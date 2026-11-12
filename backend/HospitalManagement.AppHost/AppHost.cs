// Aspire AppHost: the development-time topology of the Hospital Management platform.
//
//   dotnet run --project backend/HospitalManagement.AppHost
//
// Starts PostgreSQL and Keycloak as containers, runs the API as a project, wires
// configuration between them and opens the Aspire dashboard (logs, traces, metrics).

// WithoutHttpsCertificate is still marked experimental in Aspire 13.
#pragma warning disable ASPIRECERTIFICATES001

const string RealmName = "HospitalRealm";

var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL server with the application database. The data volume keeps data between runs.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("hospitalmanagement-aspire-postgres");
var hospitalDb = postgres.AddDatabase("hospitaldb");

// Keycloak on a fixed port so the token issuer (http://localhost:8080/realms/HospitalRealm)
// matches the redirect URIs in the realm file. The realm is the same one docker-compose imports.
var keycloak = builder.AddKeycloak("keycloak", 8080)
    // Plain HTTP, like docker-compose: keeps the issuer and redirect URIs identical in both setups.
    .WithoutHttpsCertificate()
    .WithRealmImport("../../keycloak/realms");

builder.AddProject<Projects.api>("hospital-api")
    // Injects ConnectionStrings__hospitaldb.
    .WithReference(hospitalDb)
    .WaitFor(hospitalDb)
    // Injects services__keycloak__http__0 for service discovery.
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithEnvironment("Keycloak__Authority",
        ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/{RealmName}"))
    .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
    .WithEnvironment("Database__MigrateOnStartup", "true")
    .WithEnvironment("Database__SeedOnStartup", "true")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
