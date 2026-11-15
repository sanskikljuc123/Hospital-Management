namespace HospitalManagement.E2E.Support;

/// <summary>
/// Where the stack runs and how to sign in. Everything comes from the environment, so the same tests run locally
/// (docker compose + the frontend dev server) and in CI. Defaults match docker-compose.
/// </summary>
public static class E2EConfig
{
    public static string FrontendUrl => Env("E2E_FRONTEND_URL") ?? "http://localhost:3000";
    public static string ApiUrl => Env("E2E_API_URL") ?? "http://localhost:5117";
    public static string KeycloakUrl => Env("E2E_KEYCLOAK_URL") ?? "http://localhost:8080";
    public const string Realm = "HospitalRealm";

    /// <summary>Password of the four sample users (DEV_USERS_PASSWORD of the stack).</summary>
    public static string UsersPassword => Env("E2E_USERS_PASSWORD") ?? Env("DEV_USERS_PASSWORD")
        ?? throw new InvalidOperationException(
            "Set E2E_USERS_PASSWORD (or DEV_USERS_PASSWORD) to the sample users' password of the running stack.");

    /// <summary>Keycloak admin, used only to create and delete a temporary user without a hospital role.</summary>
    public static string KeycloakAdmin => Env("E2E_KEYCLOAK_ADMIN") ?? Env("KEYCLOAK_ADMIN") ?? "admin";
    public static string KeycloakAdminPassword => Env("E2E_KEYCLOAK_ADMIN_PASSWORD") ?? Env("KEYCLOAK_ADMIN_PASSWORD")
        ?? throw new InvalidOperationException("Set E2E_KEYCLOAK_ADMIN_PASSWORD (or KEYCLOAK_ADMIN_PASSWORD).");

    /// <summary>Headed browser for watching a run locally: E2E_HEADED=1.</summary>
    public static bool Headed => Env("E2E_HEADED") == "1";

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
