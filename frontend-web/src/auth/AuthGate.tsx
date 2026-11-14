import { ReactNode, useEffect, useState } from "react";
import { Button, Result, Spin } from "antd";
import { getHospitalRoles, initKeycloak, logout } from "./keycloak";

type State = "loading" | "ready" | "no-role" | "error";

/**
 * Renders the app only for a signed-in user with a hospital role.
 * Not signed in → redirect to the Keycloak login page.
 */
const AuthGate = ({ children }: { children: ReactNode }) => {
  const [state, setState] = useState<State>("loading");

  useEffect(() => {
    initKeycloak()
      .then((authenticated) => {
        if (!authenticated) return; // login-required redirects to Keycloak
        setState(getHospitalRoles().length > 0 ? "ready" : "no-role");
      })
      .catch(() => setState("error"));
  }, []);

  if (state === "loading")
    return (
      <div style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100vh" }}>
        <Spin size='large' />
      </div>
    );

  if (state === "no-role")
    return (
      <Result
        status='403'
        title='No access'
        subTitle='Your account has no hospital role. Ask an administrator to assign one.'
        extra={<Button onClick={logout}>Sign out</Button>}
      />
    );

  if (state === "error")
    return (
      <Result
        status='error'
        title='Sign-in is unavailable'
        subTitle='The identity provider could not be reached. Try again later.'
        extra={<Button onClick={() => window.location.reload()}>Try again</Button>}
      />
    );

  return <>{children}</>;
};

export default AuthGate;
