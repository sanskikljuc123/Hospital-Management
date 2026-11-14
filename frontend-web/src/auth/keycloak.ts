import Keycloak from "keycloak-js";

/** Hospital roles the API accepts (Keycloak realm roles). */
export const HOSPITAL_ROLES = ["Admin", "Doctor", "Nurse", "Patient"] as const;
export type HospitalRole = (typeof HOSPITAL_ROLES)[number];

// Public client with Authorization Code + PKCE; no secret lives in the browser.
const keycloak = new Keycloak({
  url: import.meta.env.VITE_KEYCLOAK_URL ?? "http://localhost:8080",
  realm: import.meta.env.VITE_KEYCLOAK_REALM ?? "HospitalRealm",
  clientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID ?? "hospital-web",
});

let initPromise: Promise<boolean> | undefined;

/**
 * Signs the user in (redirecting to Keycloak if needed). Safe to call more than once:
 * React StrictMode mounts twice in development, but keycloak-js may only be initialized once.
 */
export const initKeycloak = (): Promise<boolean> => {
  initPromise ??= keycloak.init({
    onLoad: "login-required",
    pkceMethod: "S256",
    checkLoginIframe: false,
  });
  return initPromise;
};

/** Returns a token valid for at least 30 more seconds, refreshing it if needed. */
export const getAccessToken = async (): Promise<string | undefined> => {
  try {
    await keycloak.updateToken(30);
  } catch {
    // The session ended (refresh token expired or revoked): sign in again.
    await keycloak.login();
  }
  return keycloak.token;
};

export const getHospitalRoles = (): HospitalRole[] => {
  const roles = keycloak.tokenParsed?.realm_access?.roles ?? [];
  return HOSPITAL_ROLES.filter((role) => roles.includes(role));
};

export const getDisplayName = (): string => {
  const token = keycloak.tokenParsed as { name?: string; preferred_username?: string } | undefined;
  return token?.name || token?.preferred_username || "";
};

export const logout = () => keycloak.logout({ redirectUri: window.location.origin });

export default keycloak;
